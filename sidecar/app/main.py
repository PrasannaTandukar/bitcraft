from __future__ import annotations

from fastapi import FastAPI
from fastapi.responses import JSONResponse, ORJSONResponse

from app.schemas import (
    SidecarEnvelope,
    SidecarLlmBenchmarkRequest,
    SidecarLlmBenchmarkResponse,
    SidecarLlmBenchmarkStartResponse,
    SidecarResponse,
)
from app.tool_registry import ToolRegistry
from app.tools.deepeval_benchmark import DeepEvalBenchmarkTool
from app.tools.deepeval_llm_benchmarks import (
    BenchmarkBusyError,
    BenchmarkValidationError,
    cancel_benchmark_run,
    get_benchmark_run_status,
    read_run_log,
    start_benchmark_run,
)

app = FastAPI(default_response_class=ORJSONResponse)
registry = ToolRegistry()
registry.register(DeepEvalBenchmarkTool())
# NOTE: LLM-as-judge scoring (deepeval.llm_judge_benchmark / DeepEvalLlmJudgeBenchmarkTool
# in app/tools/deepeval_metrics.py) is intentionally NOT registered for this iteration.
# Product direction: deterministic scoring only (deepeval.benchmark + the standardized
# built-in benchmarks below). Re-register when LLM-as-judge is prioritized again.


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/internal/tools/execute", response_model=SidecarResponse)
async def execute_tool(envelope: SidecarEnvelope) -> SidecarResponse:
    tool = registry.get(envelope.tool_key)
    if tool is None:
        return JSONResponse(
            status_code=404,
            content={
                "code": "Sidecar.ToolNotFound",
                "description": f"Tool '{envelope.tool_key}' is not registered.",
            },
        )

    return await tool.execute(envelope.payload)


@app.post(
    "/internal/tools/llm-benchmark",
    response_model=SidecarLlmBenchmarkStartResponse,
    status_code=202,
)
async def start_llm_benchmark(request: SidecarLlmBenchmarkRequest):
    """Start one standardized LLM benchmark in a dedicated runner subprocess
    (BIG-Bench Hard, HellaSwag, MMLU, DROP, TruthfulQA, HumanEval, GSM8K).

    Returns 202 with a run handle the backend polls at
    /internal/benchmarks/{run_id}/status. Returns 409 when the single runner
    slot is already held (decision D-R2). See deepeval_llm_benchmarks.py for
    cost and safety notes before enabling this against a live model."""
    try:
        return start_benchmark_run(request)
    except BenchmarkBusyError as error:
        return JSONResponse(
            status_code=409,
            content={"code": "Sidecar.BenchmarkBusy", "description": str(error)},
        )
    except BenchmarkValidationError as error:
        return JSONResponse(
            status_code=400,
            content={"code": "Sidecar.BenchmarkInvalid", "description": str(error)},
        )


@app.post("/internal/benchmarks/{run_id}/cancel")
async def cancel_llm_benchmark(run_id: str):
    """Cancel a running benchmark (Constraint G).

    Terminates the runner process group and writes a `cancelled` terminal state.
    Cancel cannot stop `evaluate()` mid-problem, so the process may take up to
    one problem to exit. Returns 404 when no live runner exists for the id.
    """
    if not cancel_benchmark_run(run_id):
        return JSONResponse(
            status_code=404,
            content={
                "code": "Sidecar.BenchmarkRunNotFound",
                "description": f"No live runner found for benchmark run '{run_id}'.",
            },
        )
    return {"run_id": run_id, "state": "cancelled"}


@app.get("/internal/benchmarks/{run_id}/log")
async def get_llm_benchmark_log(run_id: str):
    """Return the scrubbed, capped runner log for a benchmark run.

    The log holds the runner's stdout and stderr. The judge API key is removed
    before the text leaves the sidecar. Returns 404 when no log exists.
    """
    log = read_run_log(run_id)
    if log is None:
        return JSONResponse(
            status_code=404,
            content={
                "code": "Sidecar.BenchmarkLogNotFound",
                "description": f"No log found for benchmark run '{run_id}'.",
            },
        )
    return {"run_id": run_id, "log": log}


@app.get("/internal/benchmarks/{run_id}/status")
async def get_llm_benchmark_status(run_id: str):
    """Return the runner status document for a started benchmark run.

    The document carries state (running/completed/failed), the current stage,
    overall_score, per-task scores, and the error text on failure. Returns
    404 for unknown run ids."""
    status = get_benchmark_run_status(run_id)
    if status is None:
        return JSONResponse(
            status_code=404,
            content={
                "code": "Sidecar.BenchmarkRunNotFound",
                "description": f"No status found for benchmark run '{run_id}'.",
            },
        )
    return status
