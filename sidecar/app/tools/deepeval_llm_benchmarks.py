from __future__ import annotations

import importlib
import inspect
import os
import subprocess
import sys
import uuid
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

import httpx

from app.runner.status import base_status, read_status, write_status
from app.schemas import (
    SidecarJudgeModelConfig,
    SidecarLlmBenchmarkRequest,
    SidecarLlmBenchmarkResponse,
    SidecarLlmBenchmarkStartResponse,
    SidecarTaskScore,
)

# Standardized deepeval LLM benchmarks (BIG-Bench Hard, HellaSwag, MMLU, DROP,
# TruthfulQA, HumanEval, GSM8K). This is a separate, opt-in capability from
# the per-tutor benchmark scorers in `deepeval_benchmark.py` /
# `deepeval_metrics.py`: those score outputs the backend already generated,
# while this module invokes a target model directly against deepeval's
# research-backed public datasets.
#
# Execution model (plan standard-benchmark-hardening WS1, decisions D-R1..D-R3):
# - POST /internal/tools/llm-benchmark validates the request, then spawns ONE
#   runner subprocess (`python -m app.runner`) and returns 202 with a handle.
# - A single runner slot is enforced via the latest run's status file; a
#   second concurrent request is rejected with 409 Sidecar.BenchmarkBusy (D-R2).
# - The subprocess executes the synchronous deepeval evaluate() call off the
#   uvicorn event loop, so /health stays responsive during long runs (C2).
# - The backend polls GET /internal/benchmarks/{run_id}/status (D-R3).
#
# Cost and safety notes:
# - Every run reaches a real model (Ollama or OpenAI) and, depending on
#   `tasks`/`n_shots`, can issue many requests. Callers should pass a small
#   `tasks` subset to keep a first run low-cost; omitting `tasks` runs
#   deepeval's full default task set for that benchmark.
# - `human_eval` executes model-generated Python code locally to score
#   pass@k. This requires `allow_code_execution=True` and should only be run
#   in a disposable/sandboxed environment, never against an untrusted model
#   in a shared process.
# - No credentials are read from this repository or its environment.
#   `judge_model.api_key` carries the stored runtime-engine key sent by the
#   backend over the local Unix socket (WS5 decision, 2026-09-19). The key
#   transits the local socket and the local request file only. It never
#   reaches the network and never appears in logs; the runner deletes the
#   request file after reading it.

_BENCHMARK_MODULE = "deepeval.benchmarks"
_TASK_ENUM_NAMES = {
    "big_bench_hard": "BigBenchHardTask",
    "hellaswag": "HellaSwagTask",
    "mmlu": "MMLUTask",
    "drop": "DROPTask",
    "truthful_qa": "TruthfulQATask",
    "human_eval": "HumanEvalTask",
    "gsm8k": None,  # GSM8K has no task subdivisions.
}
_BENCHMARK_CLASS_NAMES = {
    "big_bench_hard": "BigBenchHard",
    "hellaswag": "HellaSwag",
    "mmlu": "MMLU",
    "drop": "DROP",
    "truthful_qa": "TruthfulQA",
    "human_eval": "HumanEval",
    "gsm8k": "GSM8K",
}

# Per-suite few-shot ceiling (decision D-R4). deepeval's BigBenchHard asserts
# n_shots <= 3; the other shot-capable suites accept up to 5. Suites without
# shot support report 0.
_SUITE_MAX_SHOTS = {
    "big_bench_hard": 3,
    "mmlu": 5,
    "hellaswag": 5,
    "drop": 5,
    "gsm8k": 5,
    "truthful_qa": 0,
    "human_eval": 0,
}

# Shared volume directory for run artifacts (status + request files). The
# socket volume is mounted at /var/run/bitcraft in both containers.
_RUNS_DIR = Path(os.getenv("BENCHMARK_RUNS_DIR", "/var/run/bitcraft/runs"))

# Path of the most recently started run's status file. The single runner
# slot (decision D-R2) is derived from this file's terminal state.
_last_status_path: str | None = None


class BenchmarkBusyError(Exception):
    """Raised when a run is requested while the single runner slot is held."""


class BenchmarkValidationError(Exception):
    """Raised when a request fails validation before a subprocess is spawned."""


def max_shots_for(benchmark_key: str) -> int:
    return _SUITE_MAX_SHOTS.get(benchmark_key, 5)


class _SidecarJudgeLLM:
    """Minimal `DeepEvalBaseLLM` implementation that calls a target model over
    HTTP. Built lazily (imports deepeval only when a benchmark actually runs)
    so the rest of the sidecar keeps working without a judge configured."""

    def __init__(self, config: SidecarJudgeModelConfig):
        from deepeval.models.base_model import (
            DeepEvalBaseLLM,
        )  # local import, see class docstring

        self._config = config
        self._base = DeepEvalBaseLLM

    def build(self):
        base = self._base
        config = self._config
        call = self._call

        class _Model(base):  # type: ignore[misc, valid-type]
            def load_model(self):
                return config.model_name

            def get_model_name(self) -> str:
                return f"{config.provider}:{config.model_name}"

            def generate(self, prompt: str) -> str:
                return call(prompt)

            async def a_generate(self, prompt: str) -> str:
                return call(prompt)

            def generate_samples(self, prompt: str, n: int, temperature: float):
                # Best-effort HumanEval support: request `n` independent
                # completions of the same prompt for the pass@k metric.
                # deepeval expects a plain list of strings (decision D-R6).
                return [call(prompt) for _ in range(n)]

        return _Model()

    def _call(self, prompt: str) -> str:
        provider = self._config.provider.lower()
        if provider == "ollama":
            base_url = self._config.base_url or "http://localhost:11434"
            response = httpx.post(
                f"{base_url}/api/generate",
                json={
                    "model": self._config.model_name,
                    "prompt": prompt,
                    "stream": False,
                },
                timeout=120.0,
            )
            response.raise_for_status()
            return response.json().get("response", "")

        # Active provider dialects: Ollama (local) and OpenAI (hosted API).
        # The backend rejects other engine types before a run is queued.
        if provider == "openai":
            # WS5: the key value arrives in the request payload over the
            # local Unix socket. It is used for the Authorization header and
            # never logged.
            api_key = self._config.api_key or None
            headers = {"Authorization": f"Bearer {api_key}"} if api_key else {}
            base_url = self._config.base_url or "https://api.openai.com/v1"
            if not api_key and "api.openai.com" in base_url:
                raise RuntimeError(
                    "OpenAI hosted models require `api_key` in the judge model payload."
                )
            response = httpx.post(
                f"{base_url}/chat/completions",
                headers=headers,
                json={
                    "model": self._config.model_name,
                    "messages": [{"role": "user", "content": prompt}],
                },
                timeout=120.0,
            )
            response.raise_for_status()
            return response.json()["choices"][0]["message"]["content"]

        # Anthropic was removed from the active dialect set (consolidation plan, OD-2):
        # the backend only ever sends ollama/openai.
        raise ValueError(f"Unsupported judge provider: {self._config.provider}")


def _import_task_enum(benchmark_key: str):
    task_enum_name = _TASK_ENUM_NAMES.get(benchmark_key)
    if not task_enum_name:
        return None
    candidates = [
        _BENCHMARK_MODULE,
        f"deepeval.benchmarks.{benchmark_key}.task",
        f"deepeval.benchmarks.tasks",
    ]
    for module_path in candidates:
        try:
            module = importlib.import_module(module_path)
            return getattr(module, task_enum_name)
        except (ImportError, AttributeError):
            continue
    return None


def _resolve_tasks(benchmark_key: str, requested: list[str] | None):
    """Resolve requested task names against the deepeval task enum.

    Unknown names hard-fail with the offending names listed (decision D-R5);
    no silent fallback to the full default task set.
    """
    if not requested:
        return None
    task_enum = _import_task_enum(benchmark_key)
    if task_enum is None:
        raise BenchmarkValidationError(
            f"Benchmark '{benchmark_key}' does not support task filtering."
        )
    resolved = []
    unknown = []
    for name in requested:
        member = getattr(task_enum, name.upper(), None)
        if member is not None:
            resolved.append(member)
        else:
            unknown.append(name)
    if unknown:
        raise BenchmarkValidationError(
            f"Unknown tasks for '{benchmark_key}': {unknown}. "
            f"Valid task names: {[member.name for member in task_enum]}."
        )
    return resolved or None


def _validate_request(request: SidecarLlmBenchmarkRequest) -> str:
    """Validate everything that must fail before a subprocess is spawned.

    Returns the normalized benchmark key. Raises BenchmarkValidationError.
    """
    key = request.benchmark_key.strip().lower()
    if key not in _BENCHMARK_CLASS_NAMES:
        raise BenchmarkValidationError(
            f"Unsupported benchmark_key '{key}'. Supported: {sorted(_BENCHMARK_CLASS_NAMES)}."
        )

    if key == "human_eval" and not request.allow_code_execution:
        raise BenchmarkValidationError(
            "HumanEval executes model-generated Python code to score pass@k. "
            "Set allow_code_execution=true only in a disposable/sandboxed environment."
        )

    # Consolidation plan OD-2: only ollama and openai are active dialects.
    # Reject any other provider before dataset loading so the failure is
    # attributed to the provider, not to a downstream import error.
    judge_provider = request.judge_model.provider.strip().lower()
    if judge_provider not in ("ollama", "openai"):
        raise BenchmarkValidationError(
            f"Unsupported judge provider: {request.judge_model.provider}. Supported: ollama, openai."
        )

    max_shots = max_shots_for(key)
    if request.n_shots > max_shots:
        raise BenchmarkValidationError(
            f"Benchmark '{key}' supports at most {max_shots} few-shot examples; got {request.n_shots}."
        )

    return key


def start_benchmark_run(
    request: SidecarLlmBenchmarkRequest,
) -> SidecarLlmBenchmarkStartResponse:
    """Validate the request and spawn one runner subprocess (D-R1/D-R2/D-R3).

    Returns a handle the backend polls. Raises BenchmarkBusyError when the
    single runner slot is held and BenchmarkValidationError on invalid input.
    """
    global _last_status_path
    # Validate first (D-R1): an invalid request must report 400, never 409.
    # A busy slot must not mask a validation error.
    key = _validate_request(request)

    if _slot_held():
        raise BenchmarkBusyError(
            "A standardized benchmark run is already in progress. Wait for it to finish."
        )

    run_id = uuid.uuid4()
    _RUNS_DIR.mkdir(parents=True, exist_ok=True)
    status_path = _RUNS_DIR / f"{run_id}.json"
    request_path = _RUNS_DIR / f"{run_id}.request.json"

    write_status(status_path, {**base_status("running", "starting")})
    request_path.write_text(request.model_dump_json(), encoding="utf-8")

    subprocess.Popen(
        [
            sys.executable,
            "-m",
            "app.runner",
            "--request-file",
            str(request_path),
            "--status-file",
            str(status_path),
        ],
        cwd=str(Path(__file__).resolve().parents[2]),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        stdin=subprocess.DEVNULL,
        start_new_session=True,
    )

    _last_status_path = str(status_path)
    return SidecarLlmBenchmarkStartResponse(
        run_id=str(run_id), status_path=str(status_path)
    )


def _slot_held() -> bool:
    """True while the most recently started run has not reached a terminal state.

    The single runner slot (decision D-R2) is derived from the latest run's
    status file: held while it exists and its state is not completed/failed.
    A missing or unreadable file is treated as free.
    """
    if _last_status_path is None:
        return False
    status = read_status(Path(_last_status_path))
    if status is None:
        return False
    return status.get("state") not in ("completed", "failed")


def get_benchmark_run_status(run_id: str) -> dict | None:
    """Return the parsed status document for a started run, or None."""
    if not run_id or "/" in run_id or ".." in run_id:
        return None
    status_path = _RUNS_DIR / f"{run_id}.json"
    return read_status(status_path)


def execute_benchmark(
    request: SidecarLlmBenchmarkRequest, stage_writer=None
) -> SidecarLlmBenchmarkResponse:
    """Execute one benchmark synchronously. Called only by the runner subprocess.

    `stage_writer` receives coarse stage names (loading-dataset, evaluating,
    finalizing). deepeval 4.2.3 exposes no per-problem callback, so stages are
    honest coarse markers, not fabricated percentages (decision D-R8).
    """
    engine_version = _resolve_engine_version()
    key = request.benchmark_key.strip().lower()
    class_name = _BENCHMARK_CLASS_NAMES[key]

    try:
        module = importlib.import_module(_BENCHMARK_MODULE)
        benchmark_cls = getattr(module, class_name)
    except (ImportError, AttributeError) as error:
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"deepeval.benchmarks.{class_name} is unavailable in the installed deepeval version: {error}",
        )

    try:
        if stage_writer:
            stage_writer("loading-dataset")
        accepted_params = set(inspect.signature(benchmark_cls.__init__).parameters)
        kwargs: dict = {}
        resolved_tasks = _resolve_tasks(key, request.tasks)
        if resolved_tasks and "tasks" in accepted_params:
            kwargs["tasks"] = resolved_tasks
        if "n_shots" in accepted_params:
            kwargs["n_shots"] = request.n_shots
        # n_problems: 0 means "no cap" (full dataset), so only forward a
        # positive value. deepeval names the parameter `n_problems` on
        # single-dataset benchmarks and `n_problems_per_task` on task-based
        # ones; the signature gate picks whichever the installed version
        # accepts so a deepeval upgrade cannot break this call.
        n_problems_forwarded = 0
        if request.n_problems > 0:
            if "n_problems" in accepted_params:
                kwargs["n_problems"] = request.n_problems
                n_problems_forwarded = request.n_problems
            elif "n_problems_per_task" in accepted_params:
                kwargs["n_problems_per_task"] = request.n_problems
                n_problems_forwarded = request.n_problems

        benchmark = benchmark_cls(**kwargs)
        model = _SidecarJudgeLLM(request.judge_model).build()

        evaluate_params = set(inspect.signature(benchmark.evaluate).parameters)
        evaluate_kwargs: dict = {"model": model}
        if key == "human_eval" and "k" in evaluate_params:
            evaluate_kwargs["k"] = min(5, max(1, request.n_shots or 1))

        if stage_writer:
            stage_writer("evaluating")
        benchmark.evaluate(**evaluate_kwargs)
        if stage_writer:
            stage_writer("finalizing")

        overall_score = float(getattr(benchmark, "overall_score", 0.0) or 0.0)
        task_scores_raw = getattr(benchmark, "task_scores", None)
        task_scores = _normalize_task_scores(task_scores_raw)
        task_count = (
            len(task_scores)
            if task_scores is not None
            else (len(resolved_tasks) if resolved_tasks else 1)
        )

        if n_problems_forwarded > 0:
            cap_note = (
                f"Problem cap: {n_problems_forwarded} per task."
                if "n_problems_per_task" in kwargs
                else f"Problem cap: {n_problems_forwarded} problems."
            )
        else:
            cap_note = "Problem cap: none (full default dataset)."
        notes = f"Scored against deepeval's public benchmark dataset using the configured judge model. {cap_note}"

        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=round(overall_score, 4),
            task_count=task_count,
            engine_version=engine_version,
            notes=notes,
            error=None,
        )
    except Exception as error:  # noqa: BLE001 - never crash the runner process on a benchmark failure.
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"Benchmark run failed: {str(error)[:500]}",
        )


def _normalize_task_scores(raw) -> list[SidecarTaskScore] | None:
    """Convert deepeval's task_scores mapping into typed per-task scores.

    deepeval 4.2.3 exposes `task_scores` as a dict-like mapping of task name
    to score on task-based benchmarks; single-dataset benchmarks leave it
    unset. Returns None when the engine reported nothing usable.
    """
    if raw is None:
        return None
    try:
        items = raw.items() if hasattr(raw, "items") else None
    except Exception:  # noqa: BLE001 - defensive: shape varies across deepeval versions.
        items = None
    if items is None:
        return None
    scores: list[SidecarTaskScore] = []
    for task_name, score in items:
        try:
            scores.append(
                SidecarTaskScore(task=str(task_name), score=round(float(score), 4))
            )
        except (TypeError, ValueError):
            continue
    return scores or None


def _resolve_engine_version() -> str:
    try:
        return version("deepeval")
    except PackageNotFoundError:
        return os.getenv("DEEPEVAL_VERSION", "not-installed")
