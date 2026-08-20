from __future__ import annotations

from fastapi import FastAPI
from fastapi.responses import JSONResponse, ORJSONResponse

from app.schemas import SidecarEnvelope, SidecarLlmBenchmarkRequest, SidecarLlmBenchmarkResponse, SidecarResponse
from app.tool_registry import ToolRegistry
from app.tools.deepeval_benchmark import DeepEvalBenchmarkTool
from app.tools.deepeval_llm_benchmarks import run_llm_benchmark

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


@app.post("/internal/tools/llm-benchmark", response_model=SidecarLlmBenchmarkResponse)
async def execute_llm_benchmark(request: SidecarLlmBenchmarkRequest) -> SidecarLlmBenchmarkResponse:
    """Opt-in standardized LLM benchmarks (BIG-Bench Hard, HellaSwag, MMLU,
    DROP, TruthfulQA, HumanEval, GSM8K). See deepeval_llm_benchmarks.py for
    cost and safety notes before enabling this against a live model."""
    return await run_llm_benchmark(request)

