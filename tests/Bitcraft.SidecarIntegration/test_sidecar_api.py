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

app = importlib.import_module("app.main").app


client = TestClient(app)


def test_health_when_called_returns_ok() -> None:
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


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


def test_execute_tool_with_deepeval_benchmark_returns_summary_and_case_metrics() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 2,
                "guidance_path": "docs/benchmarking-templates.md",
                "metrics_json": "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85},{\"key\":\"answer_relevancy\",\"enabled\":true,\"threshold\":0.75},{\"key\":\"hallucination\",\"enabled\":true,\"threshold\":0.85}]",
                "cases": [
                    {
                        "case_id": "case-1",
                        "case_index": 0,
                        "input_text": "What is the capital of France?",
                        "actual_output": "The capital of France is Paris.",
                        "expected_output": "Paris",
                        "context_text": "France is a country in Europe. Paris is the capital and largest city of France.",
                        "metadata_json": "{\"difficulty\":\"easy\"}",
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
    assert json.loads(payload["summary"]["summary_json"])["template_key"] == "single-turn-llm-eval-v1"


def test_execute_tool_with_malformed_metric_threshold_returns_safe_response() -> None:
    response = client.post(
        "/internal/tools/execute",
        json={
            "tool_key": "deepeval.benchmark",
            "payload": {
                "template_key": "single-turn-llm-eval-v1",
                "consistency_runs": 1,
                "metrics_json": "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":\"bad\"}]",
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
                "metrics_json": "[{\"key\":\"faithfulness\",\"enabled\":\"false\",\"threshold\":0.85},{\"key\":\"answer_relevancy\",\"enabled\":true,\"threshold\":0.75}]",
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
