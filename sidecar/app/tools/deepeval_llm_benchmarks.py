from __future__ import annotations

import importlib
import inspect
import logging
import os
import signal
import subprocess
import sys
import time
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

logger = logging.getLogger(__name__)

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

# WS5: Bitcraft custom benchmarks shipped with the sidecar image (decision D-2).
# These are not part of `deepeval.benchmarks`, so they resolve through their own
# module path. Read-only: no user authoring.
_CUSTOM_BENCHMARK_PATHS = {
    "arduino": ("app.tools.custom_benchmarks.arduino_benchmark", "ArduinoBenchmark"),
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
    "arduino": 0,
}

# Shared volume directory for run artifacts (status + request files). The
# socket volume is mounted at /var/run/bitcraft in both containers.
_RUNS_DIR = Path(os.getenv("BENCHMARK_RUNS_DIR", "/var/run/bitcraft/runs"))

# Path of the most recently started run's status file. The single runner
# slot (decision D-R2) is derived from this file's terminal state.
# WS3: the path is also persisted to disk so the slot survives a sidecar
# restart. Without this, a restart loses the slot while the DB row stays
# Running, and the next run is accepted against a live runner subprocess.
_last_status_path: str | None = None
_SLOT_FILE = _RUNS_DIR / "last_status_path.txt"

# Live runner subprocess pids keyed by run id, so a cancel can terminate the
# process group (Constraint G). The pid is dropped when the run is cancelled.
_RUNNER_PIDS: dict[str, int] = {}

# Maximum runner log size. The log is truncated in place when it exceeds this.
_LOG_MAX_BYTES = 2 * 1024 * 1024


def _persist_last_status_path(path: str) -> None:
    """Write the slot marker to disk so a restart can recover it."""
    try:
        _RUNS_DIR.mkdir(parents=True, exist_ok=True)
        _SLOT_FILE.write_text(path, encoding="utf-8")
    except OSError:
        # A read-only or missing volume must not block a run start.
        pass


def _load_last_status_path() -> str | None:
    """Read the persisted slot marker, or None when absent or unreadable."""
    try:
        value = _SLOT_FILE.read_text(encoding="utf-8").strip()
        return value or None
    except OSError:
        return None


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

    def build(self, on_prediction=None):
        base = self._base
        config = self._config
        call = self._call

        class _Model(base):  # type: ignore[misc, valid-type]
            def load_model(self):
                return config.model_name

            def get_model_name(self) -> str:
                return f"{config.provider}:{config.model_name}"

            def generate(self, prompt: str) -> str:
                result = call(prompt)
                if on_prediction is not None:
                    on_prediction()
                return result

            async def a_generate(self, prompt: str) -> str:
                result = call(prompt)
                if on_prediction is not None:
                    on_prediction()
                return result

            def generate_samples(self, prompt: str, n: int, temperature: float):
                # Best-effort HumanEval support: request `n` independent
                # completions of the same prompt for the pass@k metric.
                # deepeval expects a plain list of strings (decision D-R6).
                results = [call(prompt) for _ in range(n)]
                if on_prediction is not None:
                    on_prediction()
                return results

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
    supported = sorted(_BENCHMARK_CLASS_NAMES) + sorted(_CUSTOM_BENCHMARK_PATHS)
    if key not in _BENCHMARK_CLASS_NAMES and key not in _CUSTOM_BENCHMARK_PATHS:
        raise BenchmarkValidationError(
            f"Unsupported benchmark_key '{key}'. Supported: {supported}."
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
    log_path = _RUNS_DIR / f"{run_id}.log"

    write_status(
        status_path,
        {**base_status("running", "starting"), "log_path": str(log_path)},
    )
    request_path.write_text(request.model_dump_json(), encoding="utf-8")

    # DR-6: capture runner output to a file instead of DEVNULL so the UI can show
    # a filtered log. The handle stays open only long enough to spawn the child.
    with open(log_path, "w", encoding="utf-8") as log_handle:
        process = subprocess.Popen(
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
            stdout=log_handle,
            stderr=subprocess.STDOUT,
            stdin=subprocess.DEVNULL,
            start_new_session=True,
        )

    # Constraint G: retain the pid so a cancel can terminate the process group.
    _RUNNER_PIDS[str(run_id)] = process.pid

    _last_status_path = str(status_path)
    _persist_last_status_path(_last_status_path)
    return SidecarLlmBenchmarkStartResponse(
        run_id=str(run_id), status_path=str(status_path), log_path=str(log_path)
    )


def _scrub_secrets(text: str, secret: str | None) -> str:
    """Remove the judge API key from a log line (DR-6).

    The key transits the local socket and the request file only. A traceback or
    an echoed request must never leak it into the log the UI renders.
    """
    if not secret or len(secret) < 8:
        return text
    return text.replace(secret, "[REDACTED]")


def _cap_log(log_path: Path, secret: str | None = None) -> None:
    """Truncate the runner log to the newest half when it exceeds the cap (DR-6).

    Reads and rewrites the file in one pass. A run that writes 3 MB ends with a
    file under the 2 MB cap holding the most recent half.
    """
    try:
        if not log_path.exists():
            return
        size = log_path.stat().st_size
        if size <= _LOG_MAX_BYTES:
            return
        raw = log_path.read_text(encoding="utf-8", errors="replace")
        keep_from = len(raw) // 2
        kept = raw[keep_from:]
        marker = "... [log truncated]\n"
        log_path.write_text(marker + _scrub_secrets(kept, secret), encoding="utf-8")
    except OSError:
        # A log that cannot be capped must not fail the run.
        pass


def read_run_log(run_id: str, secret: str | None = None) -> str | None:
    """Return the scrubbed runner log for a run, or None when absent.

    The log is capped before it is returned so a large run cannot flood the API
    response or the UI.
    """
    if not run_id or "/" in run_id or ".." in run_id:
        return None
    log_path = _RUNS_DIR / f"{run_id}.log"
    _cap_log(log_path, secret)
    try:
        return _scrub_secrets(
            log_path.read_text(encoding="utf-8", errors="replace"), secret
        )
    except OSError:
        return None


def cancel_benchmark_run(run_id: str) -> bool:
    """Terminate the runner process group for a run (Constraint G).

    Returns True when a live runner was signalled. Cancel cannot stop
    `evaluate()` mid-problem, so the run may take up to one problem to end.
    """
    if not run_id or "/" in run_id or ".." in run_id:
        return False

    pid = _RUNNER_PIDS.pop(run_id, None)
    if pid is None:
        return False

    # killpg and getpgid are Unix-only. On Windows they raise AttributeError, so
    # fall back to killing the single pid.
    try:
        os.killpg(os.getpgid(pid), signal.SIGTERM)
    except (AttributeError, ProcessLookupError, PermissionError, OSError):
        try:
            os.kill(pid, signal.SIGTERM)
        except OSError:
            return False

    status_path = _RUNS_DIR / f"{run_id}.json"
    try:
        write_status(
            status_path,
            {**base_status("cancelled", None), "notes": "Cancelled by request."},
        )
    except OSError:
        pass
    return True


def _slot_held() -> bool:
    """True while the most recently started run has not reached a terminal state.

    The single runner slot (decision D-R2) is derived from the latest run's
    status file: held while it exists and its state is not completed/failed.
    A missing or unreadable file is treated as free.

    WS3: the in-process global is recovered from the persisted slot file on
    first use, so a sidecar restart does not lose the slot.
    """
    global _last_status_path
    if _last_status_path is None:
        _last_status_path = _load_last_status_path()
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
    scoring). deepeval 4.2.3 exposes no per-problem callback, so stages are
    honest coarse markers, not fabricated percentages (decision D-R8).
    """
    engine_version = _resolve_engine_version()
    key = request.benchmark_key.strip().lower()

    custom = _CUSTOM_BENCHMARK_PATHS.get(key)
    if custom is not None:
        module_name, class_name = custom
    else:
        module_name, class_name = _BENCHMARK_MODULE, _BENCHMARK_CLASS_NAMES[key]

    try:
        module = importlib.import_module(module_name)
        benchmark_cls = getattr(module, class_name)
    except (ImportError, AttributeError) as error:
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"{module_name}.{class_name} is unavailable in the installed deepeval version: {error}",
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

        # DR-1: the total comes from the constructed benchmark, so a run with no
        # task filter reports the suite's real task count (57 for MMLU), not 1.
        total_tasks = _estimate_total_tasks(
            benchmark, resolved_tasks, n_problems_forwarded
        )

        progress = _ProgressTracker(stage_writer, total_tasks)
        model = _SidecarJudgeLLM(request.judge_model).build(on_prediction=progress.tick)

        evaluate_params = set(inspect.signature(benchmark.evaluate).parameters)
        evaluate_kwargs: dict = {"model": model}
        if key == "human_eval" and "k" in evaluate_params:
            evaluate_kwargs["k"] = min(5, max(1, request.n_shots or 1))

        if stage_writer:
            stage_writer("evaluating", {"total_tasks": total_tasks})
        evaluate_result = benchmark.evaluate(**evaluate_kwargs)
        progress.flush()
        completed_tasks = _count_completed_tasks(benchmark, total_tasks)
        # An uncapped run has no pre-evaluation estimate. Reconcile the total with the
        # completed count so a finished run always reports a full progress bar.
        if total_tasks is None:
            total_tasks = completed_tasks
        if stage_writer:
            stage_writer(
                "scoring",
                {"completed_tasks": completed_tasks, "total_tasks": total_tasks},
            )

        # WS1 debug dump: record the concrete result attributes so a future
        # deepeval version drift is visible in the runner log.
        logger.info(
            "Benchmark evaluated. key=%s, result_type=%s, overall_score=%r (%s), "
            "task_scores_type=%s",
            key,
            type(evaluate_result).__name__,
            getattr(benchmark, "overall_score", None),
            type(getattr(benchmark, "overall_score", None)).__name__,
            type(getattr(benchmark, "task_scores", None)).__name__,
        )

        overall_score = _read_overall_score(benchmark, evaluate_result)
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
            task_scores=task_scores,
            completed_tasks=completed_tasks,
            total_tasks=total_tasks,
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


class _ProgressTracker:
    """Counts predictions and writes throttled progress during evaluate() (DR-3).

    deepeval's evaluate() is one blocking call with no per-problem callback, so
    the status file would otherwise sit at a null count for the whole run. This
    writes at most once per second and always flushes the final count, so a
    1319-problem GSM8K run does not hammer the shared volume.
    """

    def __init__(self, stage_writer, total_tasks: int | None, interval: float = 1.0):
        self._stage_writer = stage_writer
        self._total_tasks = total_tasks
        self._interval = interval
        self._count = 0
        self._last_write = 0.0

    @property
    def count(self) -> int:
        return self._count

    def tick(self) -> None:
        self._count += 1
        if self._stage_writer is None:
            return
        now = time.monotonic()
        if now - self._last_write >= self._interval:
            self._write(now)

    def flush(self) -> None:
        if self._stage_writer is None or self._count == 0:
            return
        self._write(time.monotonic())

    def _write(self, now: float) -> None:
        self._last_write = now
        self._stage_writer(
            "evaluating",
            {"completed_tasks": self._count, "total_tasks": self._total_tasks},
        )


def _effective_task_count(benchmark, resolved_tasks) -> int:
    """Task count the benchmark will actually run (DR-1).

    The count must come from the constructed benchmark, never from the request.
    When `tasks` is null deepeval defaults to the suite's full task enum, so a
    request-derived count reports 1 instead of 57 for MMLU.
    """
    tasks = getattr(benchmark, "tasks", None)
    if tasks is not None and hasattr(tasks, "__len__"):
        try:
            return max(1, int(len(tasks)))
        except (TypeError, ValueError):
            pass
    return len(resolved_tasks) if resolved_tasks else 1


def _estimate_total_tasks(
    benchmark, resolved_tasks, n_problems_forwarded: int
) -> int | None:
    """Best-effort total problem count for progress reporting.

    Returns the task count multiplied by the per-task cap when a cap was
    forwarded, else None because the uncapped dataset size is unknown until
    evaluation finishes.
    """
    if n_problems_forwarded <= 0:
        return None
    return _effective_task_count(benchmark, resolved_tasks) * n_problems_forwarded


def _count_completed_tasks(benchmark, total_tasks: int | None) -> int | None:
    """Completed problem count from the benchmark's predictions DataFrame.

    deepeval 4.2.3 stores one row per evaluated problem in `benchmark.predictions`
    (a pandas DataFrame). Falls back to the estimated total when the DataFrame
    is absent, so a finished run always reports a complete progress bar.
    """
    predictions = getattr(benchmark, "predictions", None)
    if predictions is not None and hasattr(predictions, "__len__"):
        try:
            return int(len(predictions))
        except (TypeError, ValueError):
            pass
    return total_tasks


def _read_overall_score(benchmark, evaluate_result) -> float:
    """Read the overall accuracy from a deepeval benchmark result.

    deepeval 4.2.3 sets `benchmark.overall_score` (a 0-1 float) on every
    concrete benchmark and also returns a `DeepEvalBaseBenchmarkResult` whose
    field is `overall_accuracy`. Prefer the instance attribute, fall back to
    the returned result, then to 0.0. Verified against the installed 4.2.3
    (mmlu.py:160, gsm8k.py:88, hellaswag.py:164, drop.py:168, truthful_qa.py:170,
    big_bench_hard.py:193).
    """
    for candidate in (
        getattr(benchmark, "overall_score", None),
        getattr(evaluate_result, "overall_accuracy", None),
        getattr(evaluate_result, "overall_score", None),
    ):
        if candidate is None:
            continue
        try:
            return float(candidate)
        except (TypeError, ValueError):
            continue
    return 0.0


def _normalize_task_scores(raw) -> list[SidecarTaskScore] | None:
    """Convert deepeval's task_scores into typed per-task scores.

    deepeval 4.2.3 exposes `task_scores` as a pandas DataFrame with columns
    `Task` and `Score` on task-based benchmarks; single-dataset benchmarks
    leave it unset. A dict-like mapping is also accepted for forward
    compatibility. Returns None when the engine reported nothing usable.
    """
    if raw is None:
        return None

    pairs: list[tuple[object, object]] | None = None
    if hasattr(raw, "iterrows"):
        # pandas DataFrame: rows of (Task, Score).
        try:
            pairs = [
                (row["Task"], row["Score"])
                for _, row in raw.iterrows()
                if "Task" in raw.columns and "Score" in raw.columns
            ]
        except Exception:  # noqa: BLE001 - defensive: shape varies across deepeval versions.
            pairs = None
    elif hasattr(raw, "items"):
        try:
            pairs = list(raw.items())
        except Exception:  # noqa: BLE001 - defensive: shape varies across deepeval versions.
            pairs = None

    if pairs is None:
        return None

    scores: list[SidecarTaskScore] = []
    for task_name, score in pairs:
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
