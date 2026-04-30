from __future__ import annotations

import json
import math
import os
import platform
import re
import time
from dataclasses import asdict, dataclass
from importlib.metadata import PackageNotFoundError, version

from app.schemas import SidecarCaseResult, SidecarRequest, SidecarResponse, SidecarSummary
from app.tools.base import SidecarTool


_DEFAULT_METRICS = [
    {"key": "faithfulness", "enabled": True, "threshold": 0.85},
    {"key": "answer_relevancy", "enabled": True, "threshold": 0.75},
    {"key": "hallucination", "enabled": True, "threshold": 0.85},
]
_SUPPORTED_METRIC_KEYS = {"faithfulness", "answer_relevancy", "hallucination", "correctness"}
_DEFAULT_CO2_GRAMS_PER_SECOND = 0.0002


@dataclass
class _MetricConfig:
    key: str
    threshold: float
    enabled: bool = True


@dataclass
class _MetricResult:
    key: str
    score: float
    threshold: float
    passed: bool
    reason: str


class DeepEvalBenchmarkTool(SidecarTool):
    @property
    def key(self) -> str:
        return "deepeval.benchmark"

    async def execute(self, request: SidecarRequest) -> SidecarResponse:
        started = time.perf_counter()
        prep_started = time.perf_counter()
        metric_configs = self._load_metric_configs(request.metrics_json)
        metric_prep_ms = (time.perf_counter() - prep_started) * 1000.0
        case_results: list[SidecarCaseResult] = []

        total_score = 0.0
        total_tokens = 0
        total_claims = 0
        total_supported_claims = 0
        evaluation_elapsed_ms = 0.0

        for case in request.cases:
            case_started = time.perf_counter()
            decomposition_started = time.perf_counter()
            claims = self._decompose_claims(case.actual_output)
            claim_decomposition_ms = (time.perf_counter() - decomposition_started) * 1000.0

            evaluation_started = time.perf_counter()
            metrics, supported_claim_count = self._evaluate_case(
                case.input_text,
                case.actual_output,
                case.expected_output,
                case.context_text,
                metric_configs,
                claims,
            )
            verification_ms = (time.perf_counter() - evaluation_started) * 1000.0
            evaluation_elapsed_ms += verification_ms

            score = sum(metric.score for metric in metrics) / len(metrics)
            passed = all(metric.passed for metric in metrics)
            reason = "; ".join(f"{metric.key}: {metric.reason}" for metric in metrics)
            latency_ms = (time.perf_counter() - case_started) * 1000.0
            estimated_tokens = self._estimate_tokens(case.input_text, case.actual_output)
            total_tokens += estimated_tokens
            total_score += score
            total_claims += len(claims)
            total_supported_claims += supported_claim_count

            performance = {
                "latency_ms": round(latency_ms, 3),
                "estimated_tokens": estimated_tokens,
                "claim_count": len(claims),
                "timing_breakdown_ms": {
                    "claim_decomposition_ms": round(claim_decomposition_ms, 3),
                    "metric_verification_ms": round(verification_ms, 3),
                    "turnaround_ms": round(latency_ms, 3),
                },
            }
            resource = {
                "cpu_count": os.cpu_count(),
                "platform": platform.platform(),
                "python_version": platform.python_version(),
                "judge_mode": "deterministic-claim-benchmark",
            }

            case_results.append(
                SidecarCaseResult(
                    case_id=case.case_id,
                    case_index=case.case_index,
                    passed=passed,
                    score=round(score, 4),
                    reason=reason,
                    metrics_json=json.dumps([asdict(metric) for metric in metrics]),
                    performance_json=json.dumps(performance),
                    resource_json=json.dumps(resource),
                )
            )

        serialization_started = time.perf_counter()
        total_latency_ms = (time.perf_counter() - started) * 1000.0
        case_count = max(len(case_results), 1)
        average_score = total_score / case_count
        pass_rate = sum(1 for case in case_results if case.passed) / case_count
        tokens_per_second = (total_tokens / (total_latency_ms / 1000.0)) if total_latency_ms > 0 else None
        estimated_co2 = (total_latency_ms / 1000.0) * self._resolve_co2_grams_per_second()
        quality_gate_threshold = self._resolve_quality_gate_threshold(metric_configs)
        quality_gate_passed = average_score >= quality_gate_threshold
        active_metric_keys = [metric.key for metric in metric_configs]

        summary_payload = {
            "template_key": request.template_key,
            "consistency_runs": request.consistency_runs,
            "case_count": len(case_results),
            "average_score": round(average_score, 4),
            "pass_rate": round(pass_rate, 4),
            "active_metrics": active_metric_keys,
            "quality_gate_threshold": quality_gate_threshold,
            "quality_gate_passed": quality_gate_passed,
            "claim_support_ratio": round((total_supported_claims / max(total_claims, 1)), 4),
            "timing_breakdown_ms": {
                "metric_preparation_ms": round(metric_prep_ms, 3),
                "evaluation_ms": round(evaluation_elapsed_ms, 3),
                "result_packaging_ms": 0.0,
            },
            "consistency_runs_note": "Consistency runs are collected upstream; the sidecar scores the provided outputs deterministically.",
        }

        system_payload = {
            "engine": "deepeval-deterministic",
            "engine_version": self._resolve_engine_version(),
            "python_version": platform.python_version(),
            "platform": platform.platform(),
            "cpu_count": os.cpu_count(),
            "judge_mode": "deterministic-claim-benchmark",
            "timing_notes": "Network transport latency is measured outside the sidecar; summary timing covers sidecar-local preparation, verification, and packaging.",
        }

        raw_result_json = json.dumps(
            {
                "template_key": request.template_key,
                "guidance_path": request.guidance_path,
                "metrics_json": request.metrics_json,
                "consistency_runs": request.consistency_runs,
                "cases": [case.model_dump() for case in case_results],
                "summary": summary_payload,
            }
        )
        summary_payload["timing_breakdown_ms"]["result_packaging_ms"] = round((time.perf_counter() - serialization_started) * 1000.0, 3)

        return SidecarResponse(
            engine_name="deepeval-deterministic",
            engine_version=self._resolve_engine_version(),
            summary=SidecarSummary(
                average_score=round(average_score, 4),
                pass_rate=round(pass_rate, 4),
                total_latency_ms=round(total_latency_ms, 3),
                total_tokens=float(total_tokens),
                tokens_per_second=round(tokens_per_second, 4) if tokens_per_second is not None else None,
                estimated_co2_grams=round(estimated_co2, 6),
                summary_json=json.dumps(summary_payload),
                system_metrics_json=json.dumps(system_payload),
            ),
            cases=sorted(case_results, key=lambda case: case.case_index),
            raw_result_json=raw_result_json,
        )

    def _evaluate_case(
        self,
        input_text: str,
        actual_output: str,
        expected_output: str | None,
        context_text: str | None,
        metric_configs: list[_MetricConfig],
        claims: list[str],
    ) -> tuple[list[_MetricResult], int]:
        metrics: list[_MetricResult] = []
        supported_by_context = self._count_supported_claims(claims, context_text)
        supported_by_ground_truth = self._count_supported_claims(claims, self._combine_grounding(context_text, expected_output))

        for metric in metric_configs:
            if metric.key == "faithfulness":
                score, reason = self._ratio_metric(
                    supported_by_context,
                    len(claims),
                    "No retrieval context provided, so faithfulness could not be grounded.",
                    "Faithfulness checks supported claims against the provided retrieval context.",
                )
            elif metric.key == "hallucination":
                score, reason = self._ratio_metric(
                    supported_by_ground_truth,
                    len(claims),
                    "No retrieval context or expected output provided, so hallucination could not be grounded.",
                    "Hallucination score is the share of claims grounded by context or golden output.",
                )
            elif metric.key == "answer_relevancy":
                score = self._overlap_score(input_text, actual_output)
                reason = "Answer relevancy compares prompt and response token overlap."
            elif metric.key == "correctness":
                if not expected_output or not expected_output.strip():
                    score = 0.0
                    reason = "No expected output provided, so correctness could not be grounded."
                else:
                    score = self._overlap_score(expected_output, actual_output)
                    reason = "Correctness compares the answer with the golden output."
            else:
                continue

            metrics.append(
                _MetricResult(
                    key=metric.key,
                    score=round(score, 4),
                    threshold=metric.threshold,
                    passed=score >= metric.threshold,
                    reason=reason,
                )
            )

        if not metrics:
            metrics.append(
                _MetricResult(
                    key="answer_relevancy",
                    score=0.0,
                    threshold=0.75,
                    passed=False,
                    reason="No enabled metrics were supplied for the benchmark template.",
                )
            )

        return metrics, supported_by_ground_truth

    @staticmethod
    def _estimate_tokens(input_text: str, actual_output: str) -> int:
        return max(len((input_text + " " + actual_output).split()), 1)

    @staticmethod
    def _decompose_claims(actual_output: str) -> list[str]:
        claims = [segment.strip() for segment in re.split(r"[.!?\n]+", actual_output) if segment.strip()]
        return claims or [actual_output.strip() or ""]

    @staticmethod
    def _normalize_tokens(text: str | None) -> set[str]:
        return set(re.findall(r"[a-z0-9]+", (text or "").lower()))

    def _count_supported_claims(self, claims: list[str], evidence: str | None) -> int:
        if not evidence or not evidence.strip():
            return 0

        evidence_tokens = self._normalize_tokens(evidence)
        supported_claim_count = 0
        for claim in claims:
            claim_tokens = self._normalize_tokens(claim)
            if not claim_tokens:
                continue

            overlap = len(claim_tokens.intersection(evidence_tokens)) / len(claim_tokens)
            if overlap >= 0.5:
                supported_claim_count += 1

        return supported_claim_count

    def _overlap_score(self, reference_text: str | None, candidate_text: str | None) -> float:
        reference_tokens = self._normalize_tokens(reference_text)
        candidate_tokens = self._normalize_tokens(candidate_text)
        if not reference_tokens:
            return 0.0

        return len(reference_tokens.intersection(candidate_tokens)) / len(reference_tokens)

    @staticmethod
    def _combine_grounding(context_text: str | None, expected_output: str | None) -> str:
        return " ".join(part.strip() for part in [context_text or "", expected_output or ""] if part and part.strip())

    @staticmethod
    def _ratio_metric(
        supported_claim_count: int,
        total_claim_count: int,
        missing_grounding_reason: str,
        scored_reason: str,
    ) -> tuple[float, str]:
        if total_claim_count == 0:
            return 0.0, "The answer produced no evaluable claims."

        if supported_claim_count == 0:
            return 0.0, missing_grounding_reason

        return supported_claim_count / total_claim_count, scored_reason

    @staticmethod
    def _load_metric_configs(metrics_json: str | None) -> list[_MetricConfig]:
        payload = _DEFAULT_METRICS
        using_default_payload = True
        if metrics_json and metrics_json.strip():
            try:
                parsed = json.loads(metrics_json)
                if isinstance(parsed, dict):
                    parsed = parsed.get("metrics", [])
                if isinstance(parsed, list) and parsed:
                    payload = parsed
                    using_default_payload = False
            except json.JSONDecodeError:
                payload = _DEFAULT_METRICS

        configs: list[_MetricConfig] = []
        for item in payload:
            if not isinstance(item, dict):
                continue

            key = str(item.get("key", "")).strip()
            if not key or key not in _SUPPORTED_METRIC_KEYS:
                continue

            threshold = item.get("threshold", 0.85 if key != "answer_relevancy" else 0.75)
            threshold_value = DeepEvalBenchmarkTool._parse_threshold(threshold)
            if threshold_value is None:
                continue

            configs.append(
                _MetricConfig(
                    key=key,
                    threshold=threshold_value,
                    enabled=DeepEvalBenchmarkTool._parse_enabled(item.get("enabled", True)),
                )
            )

        enabled_configs = [config for config in configs if config.enabled]
        if enabled_configs:
            return enabled_configs

        if not using_default_payload:
            return []

        return [
            _MetricConfig(key="faithfulness", threshold=0.85),
            _MetricConfig(key="answer_relevancy", threshold=0.75),
            _MetricConfig(key="hallucination", threshold=0.85),
        ]

    @staticmethod
    def _parse_threshold(value: object) -> float | None:
        if isinstance(value, bool):
            return None

        try:
            threshold = float(value)
        except (TypeError, ValueError):
            return None

        if not math.isfinite(threshold) or threshold < 0 or threshold > 1:
            return None

        return threshold

    @staticmethod
    def _parse_enabled(value: object) -> bool:
        return value if isinstance(value, bool) else False

    @staticmethod
    def _resolve_quality_gate_threshold(metric_configs: list[_MetricConfig]) -> float:
        if not metric_configs:
            return 0.85

        return round(sum(metric.threshold for metric in metric_configs) / len(metric_configs), 4)

    @staticmethod
    def _resolve_co2_grams_per_second() -> float:
        try:
            value = float(os.getenv("DEEPEVAL_ESTIMATED_CO2_GRAMS_PER_SECOND", _DEFAULT_CO2_GRAMS_PER_SECOND))
        except (TypeError, ValueError):
            return _DEFAULT_CO2_GRAMS_PER_SECOND

        return value if math.isfinite(value) and value >= 0 else _DEFAULT_CO2_GRAMS_PER_SECOND

    @staticmethod
    def _resolve_engine_version() -> str:
        try:
            return version("deepeval")
        except PackageNotFoundError:
            return os.getenv("DEEPEVAL_VERSION", "not-installed")
