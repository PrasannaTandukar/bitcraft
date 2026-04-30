from __future__ import annotations

from fastapi import FastAPI
from fastapi.responses import JSONResponse, ORJSONResponse

from app.schemas import SidecarEnvelope, SidecarResponse
from app.tool_registry import ToolRegistry
from app.tools.deepeval_benchmark import DeepEvalBenchmarkTool

app = FastAPI(default_response_class=ORJSONResponse)
registry = ToolRegistry()
registry.register(DeepEvalBenchmarkTool())


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
