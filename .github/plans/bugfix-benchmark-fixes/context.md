---
created_at: 2026-10-29
edited_at: 2026-10-29
creator: planner
editor: planner
ai_gen: true
---
# Context: bugfix-benchmark-fixes

Verified facts and file references for the developing agent. Do not re-derive
these. Verified 2026-10-09 through 2026-10-29 (sweep, live reproduction, and
grounding against the sidecar skill and canonical test harness).

## Verified Facts

1. G10 live reproduction (2026-10-29): MMLU evaluated to 0.9 overall, then
   the runner crashed at `runner/__main__.py:100` → `status.py:48`:
   `TypeError: Object of type SidecarTaskScore is not JSON serializable`.
   `result.task_scores` is a list of Pydantic `SidecarTaskScore`; stdlib
   `json.dump` in `write_status` has no Pydantic adapter. The status file
   keeps its last complete write (`running`), so the slot re-poisons after
   every successful task-based run. GSM8K escapes (task_scores null).
2. G1 evidence: `/var/run/bitcraft/runs/last_status_path.txt` still pointed
   at the 2026-09-21 `running` status through the 2026-10-29 image rebuild
   (named volume). DB held 3 runs failed `StandardBenchmark.SidecarBusy`;
   the newest (2026-10-29 01:05) failed after the rebuild. The slot was
   manually cleared once that day, which allowed one run — which then hit
   G10.
3. G8 evidence: `ProcessQueuedStandardBenchmarkRunHandler` maps a failed
   `StartStandardLlmBenchmarkAsync` (409 → `StandardBenchmark.SidecarBusy`)
   straight to `MarkFailedAsync`. There is no retry.
4. G9 evidence: a run that fails before start has no `EngineRunId`;
   `GetStandardBenchmarkRunLogHandler` returns Success(null); the controller
   returns 404 `StandardBenchmark.LogUnavailable`; the UI log panel shows
   nothing.
5. G15 (grounding, 2026-10-29): `BitcraftCustomBenchmark.evaluate` sets
   `self.task_scores` as a list of `(name, score)` tuples
   (`base_custom_benchmark.py:132-135` builds `scores_by_task` as tuples).
   `_normalize_task_scores` in `deepeval_llm_benchmarks.py` handles only
   DataFrame (`iterrows`) and dict (`items`) shapes and returns None for a
   tuple list. The arduino benchmark's four weighted sub-scores (locked
   decision D-1: detail detection 0.20, circuit comprehension 0.30, solution
   reasoning 0.30, guidance pedagogy 0.20) never reach the UI. Arduino runs
   complete; only the sub-scores are lost.
6. Arduino path shares the standard runner:
   `_CUSTOM_BENCHMARK_PATHS = {"arduino": ("app.tools.custom_benchmarks.arduino_benchmark", "ArduinoBenchmark")}`
   in `deepeval_llm_benchmarks.py`. `execute_benchmark` handles custom keys
   through the same evaluate/stage/progress flow. Fixing G10 and G15 fixes
   arduino end to end. The arduino feature is delivered separately; these
   fixes must not regress it.
7. Test harness split (grounding): the canonical, deepeval-free suite is
   `tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` (FastAPI
   TestClient; requirements in its own `requirements.txt`; invoked from repo
   root per `bitcraft-python-sidecar` SKILL.md and README).
   `sidecar/tests/test_custom_benchmarks.py` imports deepeval at module top
   and errors on collection without deepeval installed — it needs an
   `importorskip` guard. `sidecar/tests/test_deepeval_llm_benchmarks.py`
   (23 tests) runs without deepeval by design; keep it that way when adding
   G10/sweep tests, or place them in the SidecarIntegration suite.
8. Sidecar skill constraints (grounding, must respect): status files are
   written atomically (temp + rename); key-bearing request files are deleted
   in `finally`; guard path construction against `/` and `..`; fail fast
   400 on invalid requests before busy checks; keys never appear in logs and
   never travel over the network (UDS only); telemetry stays opted out.
9. Worker and queue: `StandardBenchmarkRunWorker` consumes a bounded channel
   as a wake-up signal and picks runs via `ListQueuedOrderedAsync`
   (Priority, then QueuedUtc). One runner slot end to end (sidecar enforces
   409; worker processes one run per wake-up). `ReapStalledRunsAsync` marks
   other Running runs stale after `StalledAfterSeconds` (3600).
10. Options: `Benchmarking:DeepEval` (Transport uds, SocketPath, BaseUrl,
    TimeoutSeconds 120); `Benchmarking:StandardProcessing`
    (PollIntervalSeconds 30, StandardTimeoutSeconds 21600,
    StalledAfterSeconds 3600). Sidecar env: `BENCHMARK_RUNS_DIR` default
    `/var/run/bitcraft/runs`; dataset cache volume `benchmark_dataset_cache`.
11. Role model (verified): history/detail/log = AuditorOrAbove; templates and
    tutor runs = EditorOrAdmin; standard run create/cancel/delete/requeue/
    priority = AdminOnly. Frontend mirrors this (`canManage = Editor|Admin`;
    standard category admin-only in the wizard).
12. Merge coherence verified: PR #64 (provenance) and PR #67 (Jenkins/
    observability) touched no benchmarking files. OD-3 `ApiKey` semantics
    consistent end to end (`model.RuntimeEngine.ApiKey` →
    `StandardLlmBenchmarkRequest.ApiKey` → sidecar judge payload).
13. Validation baseline: build 0 errors; 571 unit tests; `npm run build`
    green; sidecar llm-benchmark tests 23 passed.

## Interim Manual Unblock (until WS1 deploys)

The live sidecar volume still holds the poisoned slot after the G10 crash:

```powershell
docker exec bitcraft-benchmark-sidecar rm /var/run/bitcraft/runs/last_status_path.txt
docker restart bitcraft-benchmark-sidecar
```

WS1 step 5 (startup sweep) makes this unnecessary after it ships and the
image is rebuilt. Until then, this command must be repeated after every
crashed task-based run.

## Key Symbols

- `write_status` / `read_status` / `base_status` — `sidecar/app/runner/status.py`
- Terminal status write — `sidecar/app/runner/__main__.py` (completed branch)
- `_normalize_task_scores` — `sidecar/app/tools/deepeval_llm_benchmarks.py`
- `_slot_held` / `start_benchmark_run` / `_SLOT_FILE` / `_RUNS_DIR` — same file
- `BitcraftCustomBenchmark.evaluate` —
  `sidecar/app/tools/custom_benchmarks/base_custom_benchmark.py`
  (`scores_by_task` tuple list is the G15 source)
- `ProcessQueuedStandardBenchmarkRunHandler` —
  `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`
  (busy → MarkFailedAsync is the G8 line to change)
- `GetStandardBenchmarkRunLogHandler` — same file (G9)
- `StandardBenchmarkRunWorker` —
  `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/StandardBenchmarkRunWorker.cs`
- `DeepEvalBenchmarkEngine` —
  `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs`
  (UDS client; add status/clear-slot calls here)

## Conventions To Respect

- CQRS records immutable; `Result<T>` for expected failures; AdminOnly on
  mutations; AuditorOrAbove on reads; STE for agent-facing text; hard delete
  default; no external packages in Application; contracts in
  `src/Bitcraft.Api/Contracts`; never expose Domain entities through
  contracts.
- Sidecar: atomic status writes; fail-fast 400 before busy; UDS-only keys;
  no telemetry; path-traversal guards on run ids.
- Git writes are human-owned; the developing agent does not branch, commit,
  or push.
