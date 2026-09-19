from __future__ import annotations

import importlib
import json
import sys
from pathlib import Path

from fastapi.testclient import TestClient

REPO_ROOT = Path(__file__).resolve().parents[2]
SIDECAR_ROOT = REPO_ROOT / "sidecar"

if str(SIDECAR_ROOT) not in sys.path:
    sys.path.insert(0, str(SIDECAR_ROOT))

main_module = importlib.import_module("app.main")
app = main_module.app


client = TestClient(app)


def test_health_when_called_returns_ok() -> None:
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


# ---------------------------------------------------------------------------
# Config checks: verify tool registration reflects current product direction
# (deterministic scoring only; LLM-as-judge deferred) without requiring
# deepeval, network access, or a live model. These act as a cheap guardrail
# against silently re-enabling/removing tools during unrelated refactors.
# ---------------------------------------------------------------------------


def test_registry_registers_deterministic_benchmark_tool() -> None:
    assert main_module.registry.get("deepeval.benchmark") is not None


def test_registry_does_not_register_llm_judge_tool() -> None:
    # LLM-as-judge (deepeval_metrics.py) is intentionally deferred for this
    # iteration per product direction: deterministic scoring only.
    assert main_module.registry.get("deepeval.llm_judge_benchmark") is None


def test_execute_tool_with_unregistered_llm_judge_key_returns_not_found() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.llm_judge_benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 1,
                "metrics_json": "[]",
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "",
                        "actual_output": "Standalone answer.",
                    }
                ],
            },
        },
    )

    assert response.status_code == 404
    assert response.json()["code"] == "Sidecar.ToolNotFound"


def test_execute_tool_with_unknown_tool_returns_not_found() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "unknown.tool",
            "payload": {
                "template_key": "template-key",
                "consistency_runs": 1,
                "metrics_json": "[]",
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "",
                        "actual_output": "Standalone answer.",
                    }
                ],
            },
        },
    )

    assert response.status_code == 404
    assert response.json() == {
        "code": "Sidecar.ToolNotFound",
        "description": "Tool 'unknown.tool' is not registered.",
    }


def test_execute_tool_with_deepeval_benchmark_returns_summary_and_case_metrics() -> (
    None
):
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 2,
                "guidance_path": "docs/benchmarking-templates.md",
                "metrics_json": '[{"key":"faithfulness","enabled":true,"threshold":0.85},{"key":"answer_relevancy","enabled":true,"threshold":0.75},{"key":"hallucination","enabled":true,"threshold":0.85}]',
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "What is the capital of France?",
                        "actual_output": "The capital of France is Paris.",
                        "expected_output": "Paris",
                        "context_text": "France is a country in Europe. Paris is the capital and largest city of France.",
                        "metadata_json": '{"difficulty":"easy"}',
                    }
                ],
            },
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["engine_name"] == "deepeval-deterministic"
    assert payload["engine_version"]
    assert payload["summary"]["average_score"] >= 0.0
    assert payload["summary"]["pass_rate"] >= 0.0
    assert payload["summary"]["total_latency_ms"] >= 0.0

    run_case = payload["cases"][0]
    assert run_case["case_id"] == "case-1"
    metrics = json.loads(run_case["metrics_json"])
    assert any(metric["key"] == "faithfulness" for metric in metrics)
    assert any(metric["key"] == "answer_relevancy" for metric in metrics)
    assert any(metric["key"] == "hallucination" for metric in metrics)
    performance = json.loads(run_case["performance_json"])
    assert performance["timing_breakdown_ms"]["metric_verification_ms"] >= 0.0
    summary = json.loads(payload["summary"]["summary_json"])
    assert summary["quality_gate_threshold"] == 0.8167
    assert "timing_breakdown_ms" in summary
    raw_result = json.loads(payload["raw_result_json"])
    assert (
        raw_result["summary"]["timing_breakdown_ms"]["result_packaging_ms"]
        == summary["timing_breakdown_ms"]["result_packaging_ms"]
    )
    assert (
        json.loads(payload["summary"]["summary_json"])["template_key"]
        == "single-turn-llm-eval-v1"
    )


def test_execute_tool_with_malformed_metric_threshold_returns_safe_response() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 1,
                "metrics_json": '[{"key":"faithfulness","enabled":true,"threshold":"bad"}]',
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "What is the capital of France?",
                        "actual_output": "The capital of France is Paris.",
                        "expected_output": "Paris",
                        "context_text": "Paris is the capital of France.",
                        "metadata_json": None,
                    }
                ],
            },
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["engine_name"] == "deepeval-deterministic"
    metrics = json.loads(payload["cases"][0]["metrics_json"])
    assert metrics[0]["key"] == "answer_relevancy"
    assert metrics[0]["passed"] is False


def test_execute_tool_with_non_boolean_enabled_does_not_enable_metric() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 1,
                "metrics_json": '[{"key":"faithfulness","enabled":"false","threshold":0.85},{"key":"answer_relevancy","enabled":true,"threshold":0.75}]',
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "Explain Paris.",
                        "actual_output": "Paris is the capital of France.",
                        "expected_output": None,
                        "context_text": None,
                        "metadata_json": None,
                    }
                ],
            },
        },
    )

    assert response.status_code == 200
    metrics = json.loads(response.json()["cases"][0]["metrics_json"])
    assert [metric["key"] for metric in metrics] == ["answer_relevancy"]


def test_execute_tool_with_empty_cases_returns_validation_error() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 1,
                "metrics_json": "[]",
                "cases": [],
            },
        },
    )

    assert response.status_code == 422


# ---------------------------------------------------------------------------
# /internal/tools/llm-benchmark: standardized built-in benchmarks (MMLU,
# HellaSwag, etc.). These cases exercise guard-rail paths only (unsupported
# key, missing opt-in for code execution) so they run without deepeval
# installed and without reaching a real model, per the safety notes in
# app/tools/deepeval_llm_benchmarks.py.
# ---------------------------------------------------------------------------


def _judge_model_payload() -> dict:
    return {
        "provider": "ollama",
        "model_name": "llama3.1",
        "base_url": "http://localhost:11434",
    }


def test_llm_benchmark_with_unsupported_key_returns_structured_error() -> None:
    response = client.post(
        "/internal/tools/llm-benchmark",
        json={
            "benchmark_key": "not-a-real-benchmark",
            "judge_model": _judge_model_payload(),
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["overall_score"] is None
    assert payload["task_count"] == 0
    assert "Unsupported benchmark_key" in payload["error"]


def test_llm_benchmark_human_eval_without_opt_in_returns_safety_error() -> None:
    response = client.post(
        "/internal/tools/llm-benchmark",
        json={
            "benchmark_key": "human_eval",
            "judge_model": _judge_model_payload(),
            "allow_code_execution": False,
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["overall_score"] is None
    assert "allow_code_execution" in payload["error"]


def test_llm_benchmark_with_missing_judge_model_returns_validation_error() -> None:
    response = client.post(
        "/internal/tools/llm-benchmark",
        json={"benchmark_key": "mmlu"},
    )

    assert response.status_code == 422


def test_llm_benchmark_n_shots_out_of_range_returns_validation_error() -> None:
    response = client.post(
        "/internal/tools/llm-benchmark",
        json={
            "benchmark_key": "mmlu",
            "judge_model": _judge_model_payload(),
            "n_shots": 6,
        },
    )

    assert response.status_code == 422


def test_llm_benchmark_negative_n_problems_returns_validation_error() -> None:
    response = client.post(
        "/internal/tools/llm-benchmark",
        json={
            "benchmark_key": "mmlu",
            "judge_model": _judge_model_payload(),
            "n_problems": -1,
        },
    )

    assert response.status_code == 422


def test_llm_benchmark_n_problems_defaults_and_schema_accepts_zero() -> None:
    # n_problems defaults to 10 and accepts 0 (= run the full dataset).
    # The request is validated without reaching a real model: an offline
    # endpoint yields a structured error, which proves the schema accepted
    # both the default and the explicit 0.
    for n_problems in (None, 0):
        body = {
            "benchmark_key": "gsm8k",
            "judge_model": _judge_model_payload(),
        }
        if n_problems is not None:
            body["n_problems"] = n_problems
        response = client.post("/internal/tools/llm-benchmark", json=body)

        assert response.status_code == 200
        payload = response.json()
        assert payload["overall_score"] is None
        assert payload["error"] is not None
