from __future__ import annotations

from pydantic import BaseModel, Field


class SidecarCaseRequest(BaseModel):
    case_id: str
    case_index: int
    input_text: str
    actual_output: str
    expected_output: str | None = None
    context_text: str | None = None
    metadata_json: str | None = None


class SidecarRequest(BaseModel):
    template_key: str
    consistency_runs: int = Field(default=1, ge=1, le=10)
    guidance_path: str | None = None
    metrics_json: str | None = None
    cases: list[SidecarCaseRequest] = Field(min_length=1, max_length=200)


class SidecarEnvelope(BaseModel):
    tool_key: str
    payload: SidecarRequest


class SidecarCaseResult(BaseModel):
    case_id: str
    case_index: int
    passed: bool
    score: float
    reason: str
    metrics_json: str
    performance_json: str
    resource_json: str


class SidecarSummary(BaseModel):
    average_score: float
    pass_rate: float
    total_latency_ms: float
    total_tokens: float | None = None
    tokens_per_second: float | None = None
    estimated_co2_grams: float | None = None
    summary_json: str
    system_metrics_json: str | None = None


class SidecarResponse(BaseModel):
    engine_name: str
    engine_version: str
    summary: SidecarSummary
    cases: list[SidecarCaseResult]
    raw_result_json: str | None = None


class SidecarError(BaseModel):
    code: str
    description: str


class SidecarJudgeModelConfig(BaseModel):
    """Identifies the model to benchmark. No credentials are embedded here:
    `api_key_env` names an environment variable that must already be set in
    the sidecar process; the key value itself is never part of the request."""

    provider: str  # "ollama" | "openai" | "anthropic"
    model_name: str
    base_url: str | None = None
    api_key_env: str | None = None


class SidecarLlmBenchmarkRequest(BaseModel):
    benchmark_key: str
    judge_model: SidecarJudgeModelConfig
    tasks: list[str] | None = None
    n_shots: int = Field(default=0, ge=0, le=5)
    # HumanEval executes model-generated Python code to check pass/fail.
    # This must be explicitly opted into and should never be enabled against
    # an untrusted model or outside a sandboxed execution context.
    allow_code_execution: bool = False


class SidecarLlmBenchmarkResponse(BaseModel):
    benchmark_key: str
    overall_score: float | None
    task_count: int
    engine_version: str
    notes: str
    error: str | None = None

