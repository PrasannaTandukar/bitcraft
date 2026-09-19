from __future__ import annotations

import importlib
import inspect
import os
from importlib.metadata import PackageNotFoundError, version

import httpx

from app.schemas import (
    SidecarJudgeModelConfig,
    SidecarLlmBenchmarkRequest,
    SidecarLlmBenchmarkResponse,
)

# Standardized deepeval LLM benchmarks (BIG-Bench Hard, HellaSwag, MMLU, DROP,
# TruthfulQA, HumanEval, GSM8K). This is a separate, opt-in capability from
# the per-tutor benchmark scorers in `deepeval_benchmark.py` /
# `deepeval_metrics.py`: those score outputs the backend already generated,
# while this module invokes a target model directly against deepeval's
# research-backed public datasets.
#
# Cost and safety notes:
# - Every call here reaches a real model (Ollama or OpenAI) and,
#   depending on `tasks`/`n_shots`, can issue many requests. Callers should
#   pass a small `tasks` subset to keep a first run low-cost; omitting
#   `tasks` runs deepeval's full default task set for that benchmark.
# - `human_eval` executes model-generated Python code locally to score
#   pass@k. This requires `allow_code_execution=True` and should only be run
#   in a disposable/sandboxed environment, never against an untrusted model
#   in a shared process.
# - No credentials are read from this repository or its environment.
#   `judge_model.api_key` carries the stored runtime-engine key sent by the
#   backend over the local Unix socket (WS5 decision, 2026-09-19). The key
#   transits the local socket only. It never reaches the network and never
#   appears in logs.

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
                completions = [call(prompt) for _ in range(n)]
                return completions, 0.0

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
    if not requested:
        return None
    task_enum = _import_task_enum(benchmark_key)
    if task_enum is None:
        return None
    resolved = []
    for name in requested:
        member = getattr(task_enum, name.upper(), None)
        if member is not None:
            resolved.append(member)
    return resolved or None


async def run_llm_benchmark(
    request: SidecarLlmBenchmarkRequest,
) -> SidecarLlmBenchmarkResponse:
    engine_version = _resolve_engine_version()
    key = request.benchmark_key.strip().lower()
    class_name = _BENCHMARK_CLASS_NAMES.get(key)
    if class_name is None:
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"Unsupported benchmark_key '{key}'. Supported: {sorted(_BENCHMARK_CLASS_NAMES)}.",
        )

    if key == "human_eval" and not request.allow_code_execution:
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=(
                "HumanEval executes model-generated Python code to score pass@k. "
                "Set allow_code_execution=true only in a disposable/sandboxed environment."
            ),
        )

    # Consolidation plan OD-2: only ollama and openai are active dialects.
    # Reject any other provider before dataset loading so the failure is
    # attributed to the provider, not to a downstream import error.
    judge_provider = request.judge_model.provider.strip().lower()
    if judge_provider not in ("ollama", "openai"):
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"Unsupported judge provider: {request.judge_model.provider}. Supported: ollama, openai.",
        )

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

        benchmark.evaluate(**evaluate_kwargs)
        overall_score = float(getattr(benchmark, "overall_score", 0.0) or 0.0)
        task_scores = getattr(benchmark, "task_scores", None)
        task_count = (
            int(len(task_scores))
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
    except Exception as error:  # noqa: BLE001 - never crash the sidecar process on a benchmark failure.
        return SidecarLlmBenchmarkResponse(
            benchmark_key=key,
            overall_score=None,
            task_count=0,
            engine_version=engine_version,
            notes="",
            error=f"Benchmark run failed: {str(error)[:500]}",
        )


def _resolve_engine_version() -> str:
    try:
        return version("deepeval")
    except PackageNotFoundError:
        return os.getenv("DEEPEVAL_VERSION", "not-installed")
