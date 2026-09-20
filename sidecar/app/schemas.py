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
    """Identifies the model to benchmark. `api_key` carries the stored
    runtime-engine key sent by the backend over the local Unix socket. The
    key transits the local socket only. It never reaches the network and
    never appears in logs."""

    provider: str  # "ollama" | "openai" (Anthropic removed, consolidation plan OD-2)
    model_name: str
    base_url: str | None = None
    api_key: str | None = None


class SidecarLlmBenchmarkRequest(BaseModel):
    benchmark_key: str
    judge_model: SidecarJudgeModelConfig
    tasks: list[str] | None = None
    n_shots: int = Field(default=0, ge=0, le=5)
    # Caps the number of problems evaluated. 0 means "no cap" and runs the
    # full default dataset for the benchmark. For task-based benchmarks
    # (MMLU, HellaSwag, BBH, DROP, TruthfulQA) deepeval applies the cap per
    # task, not across all tasks combined; for single-dataset benchmarks
    # (GSM8K) it takes the first N problems sequentially.
    n_problems: int = Field(default=10, ge=0)
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


class SidecarTaskScore(BaseModel):
    """Per-task score reported by a task-based benchmark (decision D-R7)."""

    task: str
    score: float


class SidecarLlmBenchmarkStartResponse(BaseModel):
    """Handle returned when a runner subprocess is spawned (decisions D-R1/D-R3).

    The backend polls GET /internal/benchmarks/{run_id}/status until the
    status document reaches `completed` or `failed`.
    """

    run_id: str
    status_path: str
