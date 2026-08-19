from __future__ import annotations

import json
import os
import platform
import time
from importlib.metadata import PackageNotFoundError, version

from app.schemas import SidecarCaseResult, SidecarRequest, SidecarResponse, SidecarSummary
from app.tools.base import SidecarTool

# Real deepeval LLM-as-a-judge scoring. This is an additive, opt-in sibling of
# `deepeval.benchmark` (the deterministic token/claim-overlap scorer). It is
# registered under a *different* tool key ("deepeval.llm_judge_benchmark") so
# the deterministic engine's behavior, tests, and docs remain unchanged.
#
# Running this tool requires a judge model to be reachable from the sidecar
# process. deepeval resolves the judge model from its own global CLI
# configuration (`deepeval set-openai` / `set-ollama` / `set-anthropic`) or
# from provider environment variables such as OPENAI_API_KEY. No credentials
# are read, stored, or forwarded by Bitcraft; this process only asks deepeval
# to run, and deepeval reads whatever is already configured in its own
# environment. If no judge model is configured, every metric fails closed
# with a clear reason instead of raising, so a single misconfigured judge
# cannot take down a benchmark run.
#
# Kept to the same four metric keys as the deterministic scorer
# (faithfulness, answer_relevancy, hallucination, correctness) so the
# Application-layer template validation list and existing contracts do not
# need to change for this iteration.
_SUPPORTED_METRIC_KEYS = {"faithfulness", "answer_relevancy", "hallucination", "correctness"}
_DEFAULT_METRICS = [
    {"key": "faithfulness", "enabled": True, "threshold": 0.85},
    {"key": "answer_relevancy", "enabled": True, "threshold": 0.75},
    {"key": "hallucination", "enabled": True, "threshold": 0.85},
]


class DeepEvalLlmJudgeBenchmarkTool(SidecarTool):
    """Scores benchmark cases with deepeval's real LLM-as-a-judge metrics."""

    @property
    def key(self) -> str:
        return "deepeval.llm_judge_benchmark"

    async def execute(self, request: SidecarRequest) -> SidecarResponse:
        started = time.perf_counter()
        metric_configs = self._load_metric_configs(request.metrics_json)
        case_results: list[SidecarCaseResult] = []

        total_score = 0.0
        total_tokens = 0

        for case in request.cases:
            case_started = time.perf_counter()
            metric_outcomes = self._evaluate_case(case, metric_configs)
            score = sum(outcome["score"] for outcome in metric_outcomes) / len(metric_outcomes)
            passed = all(outcome["passed"] for outcome in metric_outcomes)
            reason = "; ".join(f"{outcome['key']}: {outcome['reason']}" for outcome in metric_outcomes)
            latency_ms = (time.perf_counter() - case_started) * 1000.0
            estimated_tokens = max(len((case.input_text + " " + case.actual_output).split()), 1)
            total_tokens += estimated_tokens
            total_score += score

            case_results.append(
                SidecarCaseResult(
                    case_id=case.case_id,
                    case_index=case.case_index,
                    passed=passed,
                    score=round(score, 4),
                    reason=reason,
                    metrics_json=json.dumps(metric_outcomes),
                    performance_json=json.dumps({
                        "latency_ms": round(latency_ms, 3),
                        "estimated_tokens": estimated_tokens,
                    }),
                    resource_json=json.dumps({
                        "cpu_count": os.cpu_count(),
                        "platform": platform.platform(),
                        "python_version": platform.python_version(),
                        "judge_mode": "llm-as-a-judge",
                    }),
                )
            )

        total_latency_ms = (time.perf_counter() - started) * 1000.0
        case_count = max(len(case_results), 1)
        average_score = total_score / case_count
        pass_rate = sum(1 for case in case_results if case.passed) / case_count
        tokens_per_second = (total_tokens / (total_latency_ms / 1000.0)) if total_latency_ms > 0 else None
        active_metric_keys = [metric["key"] for metric in metric_configs]

        summary_payload = {
            "template_key": request.template_key,
            "case_count": len(case_results),
            "average_score": round(average_score, 4),
            "pass_rate": round(pass_rate, 4),
            "active_metrics": active_metric_keys,
            "judge_mode": "llm-as-a-judge",
            "judge_config_source": "deepeval global config (CLI `deepeval set-*` or provider env vars)",
        }
        system_payload = {
            "engine": "deepeval-llm-judge",
            "engine_version": self._resolve_engine_version(),
            "python_version": platform.python_version(),
            "platform": platform.platform(),
            "cpu_count": os.cpu_count(),
            "judge_mode": "llm-as-a-judge",
        }

        return SidecarResponse(
            engine_name="deepeval-llm-judge",
            engine_version=self._resolve_engine_version(),
            summary=SidecarSummary(
                average_score=round(average_score, 4),
                pass_rate=round(pass_rate, 4),
                total_latency_ms=round(total_latency_ms, 3),
                total_tokens=float(total_tokens),
                tokens_per_second=round(tokens_per_second, 4) if tokens_per_second is not None else None,
                estimated_co2_grams=None,
                summary_json=json.dumps(summary_payload),
                system_metrics_json=json.dumps(system_payload),
            ),
            cases=sorted(case_results, key=lambda case: case.case_index),
            raw_result_json=json.dumps({"template_key": request.template_key, "summary": summary_payload}),
        )

    def _evaluate_case(self, case, metric_configs: list[dict]) -> list[dict]:
        # Imported lazily so the deterministic tool keeps working even if
        # deepeval's LLM-judge dependencies (e.g. an LLM provider SDK) are
        # unavailable in a given environment.
        from deepeval.metrics import AnswerRelevancyMetric, FaithfulnessMetric, GEval, HallucinationMetric
        from deepeval.test_case import LLMTestCase, LLMTestCaseParams

        outcomes: list[dict] = []
        for metric in metric_configs:
            key = metric["key"]
            threshold = metric["threshold"]
            try:
                if key == "answer_relevancy":
                    test_case = LLMTestCase(input=case.input_text, actual_output=case.actual_output)
                    judge = AnswerRelevancyMetric(threshold=threshold)
                    judge.measure(test_case)
                    outcomes.append(self._to_outcome(key, judge))
                elif key == "faithfulness":
                    if not case.context_text or not case.context_text.strip():
                        outcomes.append(self._skip_outcome(key, threshold, "No retrieval context provided; faithfulness requires retrieval_context."))
                        continue
                    test_case = LLMTestCase(
                        input=case.input_text,
                        actual_output=case.actual_output,
                        retrieval_context=[case.context_text],
                    )
                    judge = FaithfulnessMetric(threshold=threshold)
                    judge.measure(test_case)
                    outcomes.append(self._to_outcome(key, judge))
                elif key == "hallucination":
                    if not case.context_text or not case.context_text.strip():
                        outcomes.append(self._skip_outcome(key, threshold, "No context provided; hallucination requires context."))
                        continue
                    test_case = LLMTestCase(
                        input=case.input_text,
                        actual_output=case.actual_output,
                        context=[case.context_text],
                    )
                    judge = HallucinationMetric(threshold=threshold)
                    judge.measure(test_case)
                    outcomes.append(self._to_outcome(key, judge))
                elif key == "correctness":
                    if not case.expected_output or not case.expected_output.strip():
                        outcomes.append(self._skip_outcome(key, threshold, "No expected output provided; correctness requires a golden answer."))
                        continue
                    test_case = LLMTestCase(
                        input=case.input_text,
                        actual_output=case.actual_output,
                        expected_output=case.expected_output,
                    )
                    judge = GEval(
                        name="Correctness",
                        criteria="Determine whether the actual output is factually correct and semantically equivalent to the expected output.",
                        evaluation_params=[
                            LLMTestCaseParams.INPUT,
                            LLMTestCaseParams.ACTUAL_OUTPUT,
                            LLMTestCaseParams.EXPECTED_OUTPUT,
                        ],
                        threshold=threshold,
                    )
                    judge.measure(test_case)
                    outcomes.append(self._to_outcome(key, judge))
            except Exception as error:  # noqa: BLE001 - a misconfigured/unreachable judge must not crash the run.
                outcomes.append({
                    "key": key,
                    "score": 0.0,
                    "threshold": threshold,
                    "passed": False,
                    "reason": f"LLM judge unavailable or errored: {str(error)[:300]}",
                })

        if not outcomes:
            outcomes.append({
                "key": "answer_relevancy",
                "score": 0.0,
                "threshold": 0.75,
                "passed": False,
                "reason": "No enabled metrics were supplied for the benchmark template.",
            })

        return outcomes

    @staticmethod
    def _to_outcome(key: str, judge) -> dict:
        score = float(judge.score or 0.0)
        return {
            "key": key,
            "score": round(score, 4),
            "threshold": judge.threshold,
            "passed": bool(judge.is_successful()),
            "reason": judge.reason or "No reason returned by the judge.",
        }

    @staticmethod
    def _skip_outcome(key: str, threshold: float, reason: str) -> dict:
        return {"key": key, "score": 0.0, "threshold": threshold, "passed": False, "reason": reason}

    @staticmethod
    def _load_metric_configs(metrics_json: str | None) -> list[dict]:
        payload = _DEFAULT_METRICS
        if metrics_json and metrics_json.strip():
            try:
                parsed = json.loads(metrics_json)
                if isinstance(parsed, dict):
                    parsed = parsed.get("metrics", [])
                if isinstance(parsed, list) and parsed:
                    payload = parsed
            except json.JSONDecodeError:
                payload = _DEFAULT_METRICS

        configs: list[dict] = []
        for item in payload:
            if not isinstance(item, dict):
                continue
            key = str(item.get("key", "")).strip()
            if key not in _SUPPORTED_METRIC_KEYS:
                continue
            if not bool(item.get("enabled", True)):
                continue
            try:
                threshold = float(item.get("threshold", 0.75))
            except (TypeError, ValueError):
                threshold = 0.75
            configs.append({"key": key, "threshold": threshold})

        return configs or [{"key": item["key"], "threshold": item["threshold"]} for item in _DEFAULT_METRICS]

    @staticmethod
    def _resolve_engine_version() -> str:
        try:
            return version("deepeval")
        except PackageNotFoundError:
            return os.getenv("DEEPEVAL_VERSION", "not-installed")
