---
created_at: 2026-10-29
edited_at: 2026-10-29
creator: planner
editor: planner
ai_gen: true
---
# Plan: Benchmarking Fixes and PoC Polish — Built-ins + Arduino

> Origin: Planner, 2026-10-29. Consolidated from plan v1.0–v1.3
> (feature-benchmark-run-queue). Human review: pending approval of HD-4,
> HD-8, HD-9. Delivery branch: `bugfix/benchmark-fixes`.

## Goal

Fully functional benchmarking for the proof of concept: every built-in suite
and the arduino custom benchmark run end to end with reliable status, complete
results, live views, and an Admin sidecar status and recovery surface. No
parallelism.

## Non-Goals

- No benchmark parallelism of any kind (human decision 2026-10-29).
- No per-run pause/continue/restart run commands; existing requeue covers
  reruns, and a global queue pause covers scheduling control.
- No runtime editing of sidecar configuration through the API.
- No changes to tutor-scoped RAG benchmark runs or Editor workflows.
- No custom-benchmark authoring UI (decision D-2 stands).
- No SSE/WebSocket transport; polling stays.
- No HumanEval enablement.

## Verified Defects (all reproduced or code-verified, 2026-10-09 → 2026-10-29)

| # | Defect | Evidence |
|---|--------|----------|
| G1 | Stale slot never recovers; a dead runner leaves the queue blocked forever | `last_status_path.txt` still points at the 2026-09-21 `running` status; `_slot_held()` trusts the file |
| G8 | 409 `SidecarBusy` permanently fails a queued run within seconds | `ProcessQueuedStandardBenchmarkRunHandler` maps the 409 straight to `MarkFailedAsync`; DB shows 3 runs failed this way |
| G9 | Never-started failed runs have no log surface | No `EngineRunId` → log handler returns null → controller 404 `LogUnavailable` → empty log panel |
| G10 | Runner crashes writing the terminal status: stdlib `json.dump` cannot serialize Pydantic `SidecarTaskScore` | Live traceback 2026-10-29 after `Overall MMLU Accuracy: 0.9`: `TypeError ... SidecarTaskScore is not JSON serializable` at `runner/__main__.py:100` → `status.py:48`. Status stays `running`; every successful task-based run re-poisons the slot |
| G15 | Arduino sub-scores are silently dropped | `BitcraftCustomBenchmark.evaluate` sets `task_scores` as a list of `(name, score)` tuples; `_normalize_task_scores` handles only DataFrame/dict shapes and returns None for tuples. The four sub-scores (locked decision D-1) never reach the UI |
| G2 | Missing queue actions: archive | Only cancel/delete/requeue/priority exist |
| G3 | No queue management view | Controls are scattered: priority only on the run page |
| G4 | No live log refresh | `StandardBenchmarkRunLog` is manual-refresh only |
| G6 | No queue position display | History shows status only |
| G11 | No Admin surface for the sidecar; recovery requires `docker exec` | No backend endpoint reads sidecar `/health` or slot state |
| G14 | Backend never reads sidecar `/health` | Engine status is inferred from call failures only |
| G12 | Suite enablement is code-level | `StandardBenchmarkSuiteRegistry.cs` compiled flags |
| G13 | No engine configuration surfaced via API/UI | Settings live in appsettings/compose only |

G12/G13 are recorded and deferred; they do not block the PoC.

## Role Requirements (Editor vs Admin, verified against controllers)

- Editor flow is complete: templates (`EditorOrAdmin`), tutor RAG runs
  (`EditorOrAdmin`), history/detail views (`AuditorOrAbove`). Editor needs
  only the reliability fixes so shared history is not polluted by stuck
  standard runs.
- Standard runs stay Admin-only (launch and all actions).
- Queue and sidecar status are readable by Auditor+; every action and the
  recovery control are Admin-only.
- Admin gains: archive, global queue pause, sidecar status, clear-slot.

## Work Streams

### WS1 — Runner reliability (sidecar, then backend)

Phase 2 skill: `bitcraft-python-sidecar`, then `bitcraft-backend-feature`.
CIL: high.

Current state: a successful task-based run can crash at its terminal write
(G10) and re-poison the slot (G1); a busy run fails permanently (G8); arduino
sub-scores never surface (G15).

Steps (execute in order; validate each before the next):

1. File `sidecar/app/runner/status.py`. Add a module-level function
   `_json_default(value)` that returns `value.model_dump(mode="json")` for
   pydantic `BaseModel` instances and raises `TypeError` otherwise. Pass
   `default=_json_default` to `json.dump` in `write_status`.
   Validation: `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py`
   passes.
2. File `sidecar/app/runner/__main__.py`. In the completed-status write,
   serialize task scores: replace `"task_scores": result.task_scores` with
   `[t.model_dump() for t in result.task_scores] if result.task_scores else None`.
   Validation: `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py`
   passes.
3. File `sidecar/app/runner/__main__.py`. Wrap the terminal status write in
   try/except. On exception, write a minimal failed status whose `error` field
   carries the exception text, and return 1. A terminal-write crash must never
   leave the status `running`.
   Validation: unit test forcing the completed write to raise ends with a
   `failed` status.
4. File `sidecar/app/tools/deepeval_llm_benchmarks.py`. Extend
   `_normalize_task_scores` to accept a list of `(name, score)` tuples
   (arduino's shape) in addition to DataFrame and dict shapes.
   Validation: unit test with tuple input returns four `SidecarTaskScore`
   records for a stub arduino-like metric list.
5. File `sidecar/app/main.py`. Add a FastAPI startup handler that sweeps the
   runs directory: any status file in a non-terminal state
   (`running`, `starting`) whose `updated_utc` is older than
   `BENCHMARK_STALE_AFTER_SECONDS` (default 300) is rewritten as
   `failed` with a `stale-runner` note, and the slot marker
   `last_status_path.txt` is deleted when it points at a swept file. This
   heals the leaked slot on every sidecar restart without manual volume
   surgery.
   Validation: pytest with a planted stale status file asserts the slot is
   free and the file reads `failed`.
6. Add sidecar regression tests for G10 and the sweep in
   `tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` (keep this suite
   deepeval-free; use stub objects and the real `write_status`).
   Validation: `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py`
   passes, including the two new tests.
7. File `sidecar/tests/test_custom_benchmarks.py`. Add a module-level
   `pytest.importorskip("deepeval")` so the suite skips cleanly where deepeval
   is not installed instead of failing collection.
   Validation: `python -m pytest sidecar/tests/test_custom_benchmarks.py`
   reports skipped, not error.
8. File
   `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`,
   `ProcessQueuedStandardBenchmarkRunHandler`. When the start call fails with
   `StandardBenchmark.SidecarBusy`, do not mark the run failed. Leave the run
   `Queued`, write a progress note (`LastProgressUtc = now`,
   `ProgressStage = "waiting-for-engine"`), and return success so the worker
   picks it up again on the next wake-up. Fail with
   `StandardBenchmark.QueueTimeout` only when `QueuedUtc` is older than
   `StandardTimeoutSeconds`.
   Validation: `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`
   passes with new tests for busy-retry and queue-timeout.
9. Files `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`
   (`GetStandardBenchmarkRunLogHandler`) and
   `src/Bitcraft.Api/Controllers/Benchmarking/StandardBenchmarkRunsController.cs`
   (`GetLog`). When `EngineRunId` is empty and the run has a failure,
   synthesize a diagnostic log block from `FailureCode`, `FailureDescription`,
   `QueuedUtc`, and `StartedUtc`, and return it as the log content. The
   controller returns 200 with that content; 404 stays only for unknown run
   ids.
   Validation: unit test — a failed run without `EngineRunId` returns a log
   containing the failure code; `dotnet build` 0 errors.

Acceptance: a completed task-based run always writes a terminal status; the
slot heals on restart; busy runs wait instead of failing; failed runs always
have a readable log; arduino sub-scores reach `task_scores`.

### WS2 — Admin sidecar status and recovery (sidecar + backend + API)

Phase 2 skill: `bitcraft-python-sidecar`, `bitcraft-backend-feature`,
`bitcraft-api-endpoint`. CIL: high. Pending HD-9 approval.

Steps:

1. File `sidecar/app/main.py`. Add `GET /internal/benchmarks/slot` returning
   `{"slot_held": bool, "run_id": str|null, "engine_version": str}` derived
   from `_slot_held()` and the persisted slot marker. Add
   `DELETE /internal/benchmarks/slot` that deletes the slot marker and returns
   the post-clear state; a free slot is a safe no-op.
   Validation: pytest hits both endpoints through `TestClient`.
2. File `src/Bitcraft.Application/Common/Interfaces/IBenchmarkEngine.cs`. Add
   `GetSidecarStatusAsync(CancellationToken)` returning
   `Result<SidecarEngineStatus>` with reachability, transport, slot state, and
   engine version; add `ClearSidecarSlotAsync(CancellationToken)` returning
   `Result<bool>`.
3. File `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs`.
   Implement both: call `/health` and `/internal/benchmarks/slot`; map any
   transport failure to `Sidecar.Unreachable` with a safe description (never
   include exception text that could carry socket paths).
4. File `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`.
   Add `GetBenchmarkEngineStatusQuery` and `ClearBenchmarkEngineSlotCommand`
   handlers. Status is readable by Auditor+; clear-slot is Admin-only.
5. File `src/Bitcraft.Api/Controllers/Benchmarking/StandardBenchmarkRunsController.cs`.
   Add `GET api/standard-benchmark-runs/engine` (`AuditorOrAbove`) and
   `POST api/standard-benchmark-runs/engine/clear-slot` (`AdminOnly`) with
   full `ProducesResponseType` metadata and contract records in
   `src/Bitcraft.Api/Contracts/Tutoring/TutorBenchmarkRuns.cs`.
   Validation: `dotnet build` 0 errors; api-integration tests pass including
   new policy tests for both endpoints.

Acceptance: an Admin can see sidecar reachability, slot state, and engine
version in-product and clear a poisoned slot with one click; Auditor+ can
read status.

### WS3 — Queue completion: archive + global pause (backend + API + frontend)

Phase 2 skill: `bitcraft-backend-feature`, `bitcraft-api-endpoint`,
`bitcraft-frontend-feature`. CIL: high. Pending HD-4 and HD-8 approval.

Steps:

1. File `src/Bitcraft.Domain/Benchmarking/StandardBenchmarkRun.cs`. Add
   `IsArchived` (bool, default false) and `ArchivedUtc` (DateTime?). Create
   EF migration `StandardBenchmarkRunArchive`.
   Validation: `dotnet build` 0 errors.
2. File `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`.
   Add `ArchiveStandardBenchmarkRunCommand` and
   `UnarchiveStandardBenchmarkRunCommand` (Admin-only; archive allowed for
   any non-Running run; running runs must be cancelled first). List queries
   filter `IsArchived == false` by default; add an `includeArchived` query
   parameter to `ListStandardBenchmarkRunsQuery`.
   Validation: unit tests cover archive/unarchive transitions and list
   filtering.
3. Global queue pause (HD-8): add `BenchmarkQueueOptions` with a single
   `IsPaused` flag persisted through an Application-level store keyed
   `BenchmarkQueueState` (Guid entity + `IsPaused` + `UpdatedUtc`; one row,
   seeded on first use). Worker checks the flag before dequeuing; a running
   run finishes. Add `SetBenchmarkQueuePausedCommand(bool)` (Admin-only) and
   a read in the engine status response.
   Validation: unit tests cover paused dequeue behavior and the command.
4. File `src/Bitcraft.Api/Controllers/Benchmarking/StandardBenchmarkRunsController.cs`.
   Add `POST {runId:guid}/archive`, `POST {runId:guid}/unarchive`
   (AdminOnly), `PUT queue/paused` with a `SetBenchmarkQueuePausedRequest`
   body (AdminOnly), and `includeArchived` on the list action.
   Validation: `dotnet build` 0 errors; policy tests pass.
5. Frontend: create `tutor-studio/src/benchmarking/BenchmarkQueuePanel.jsx`
   as a new `queue` tab on `BenchmarkingPage.jsx` (visible to
   Editor/Admin via `canManage`, read-only status for Auditor+). Shows
   Queued/Running rows with position, suite, model, priority, stage; row
   actions cancel/delete/requeue/priority/archive; global pause/resume button
   and slot-status banner (from WS2 engine endpoint, clear-slot Admin-only);
   polls every 5 s while any row is active.
   Validation: `npm run build` exits 0; `npm run lint` reports only the
   pre-existing `main.jsx` error.
6. Frontend: `StandardBenchmarkRunPage.jsx` gains Archive/Unarchive for
   admins; `BenchmarkRunHistory.jsx` gains an "Archived" visibility toggle
   wired to `includeArchived`.
   Validation: `npm run build` exits 0.
7. API layer additions in `tutor-studio/src/benchmarking/benchmarkingApi.js`:
   archive, unarchive, setQueuePaused, getEngineStatus, clearEngineSlot.
   Validation: `npm run build` exits 0.

Acceptance: an Admin can archive finished runs, pause/resume the queue, and
manage everything from one queue view; the default history stays clean.

### WS4 — Views polish and docs (frontend + docs)

Phase 2 skill: `bitcraft-frontend-feature`, `bitcraft-doc-maintenance`.
CIL: low.

Steps:

1. File `tutor-studio/src/benchmarking/StandardBenchmarkRunLog.jsx`. Add an
   auto-refresh toggle (default off, 3 s interval) that stays active while
   the parent run is `Running` and stops on terminal states.
   Validation: `npm run build` exits 0.
2. File `tutor-studio/src/benchmarking/StandardBenchmarkRunPage.jsx`. Add an
   "updated Ns ago" stamp next to the progress bar using `LastProgressUtc`.
   Validation: `npm run build` exits 0.
3. Docs: update `docs/benchmarking-templates.md` (or add
   `docs/benchmark-run-queue.md`) with: role/action matrix, queue pause
   semantics, sidecar status + clear-slot, archive behavior, the G10 fix
   note, and the interim manual unblock command for deployments predating
   this fix.
   Validation: markdown lint clean; frontmatter updated per repo rules.
4. Full validation pass and record results in this plan's Status section:
   `dotnet build bitcraft.slnx`; all four `dotnet test` suites;
   `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py`;
   `python -m pytest sidecar/tests`; `npm run build`; `npm run lint` in
   `tutor-studio/`.
   Validation: all green (sidecar/tests may skip deepeval-gated files).

Acceptance: views are polished; documentation matches delivered behavior;
validation results recorded.

## Status

### WS4 — DONE (2026-10-29)

Steps 1-4 complete. Log auto-refresh toggle (3 s, active-run gated),
progress "updated Ns ago" stamp driven by the poll cycle (react-compiler
compliant), `docs/benchmark-run-queue.md` created with role/action matrix,
pause semantics, sidecar surface, archive behavior, G10 note, and the
interim manual unblock command. Plan docs committed with the slice.

### Final Validation (2026-10-29, HEAD 444a5ae)

- `dotnet build bitcraft.slnx` — Build succeeded, 0 errors.
- `dotnet test` UnitTests — 586 passed, 0 failed (baseline 571; +15 new).
- `dotnet test` ApiIntegrationTests — 97 passed, 6 skipped, 0 failed
  (skips are environment-gated, pre-existing; +4 new policy tests net).
- `dotnet test` InfrastructureIntegrationTests — 37 passed, 0 failed.
- `pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` — 27
  passed, 3 failed: the 3 failures reproduce identically on the pre-branch
  base (verified: 20 passed / 3 failed on 8cae69f); they require `deepeval`
  installed and pass in the container. All 7 new tests pass.
- `pytest sidecar/tests` — 23 passed, 1 skipped (importorskip guard,
  formerly a collection error).
- `npm run build` in `tutor-studio/` — exit 0.
- `npm run lint` in `tutor-studio/` — exit 0, no problems.

### WS3 — DONE (2026-10-29)

Backend steps 1-4: `IsArchived`/`ArchivedUtc` fields with migration
`StandardBenchmarkRunArchive`, `BenchmarkQueueState` singleton row with
runtime `GetOrCreateAsync` seeding (no HasData; BaseModel's private Id
setter blocks fixed-key seed rows), archive/unarchive commands
(non-Running rows only), `SetBenchmarkQueuePausedCommand`, paused-aware
worker, `includeArchived` list parameter, controller endpoints
(archive/unarchive/queue-paused + read), contract records and mapping.
Frontend steps 5-7: `BenchmarkQueuePanel.jsx` (queue tab with position,
per-row actions, inline priority, pause button, engine status card with
clear-slot), run-page archive action, history archived-visibility toggle,
API client functions. One JSX bug (missing attribute-closing brace) was
caught by the build and fixed; duplicate `size` prop removed.

### WS2 — DONE (2026-10-29)

Steps 1-5 complete: sidecar `GET/DELETE /internal/benchmarks/slot`
endpoints with hermetic tests; `IBenchmarkEngine.GetSidecarStatusAsync` +
`ClearSidecarSlotAsync`; `DeepEvalBenchmarkEngine` implementation with
`Sidecar.Unreachable` mapping (TutoringMapping extended to 502 for
`Benchmark.SidecarException`/`Sidecar.Unreachable`); Application
`GetBenchmarkEngineStatusQuery` (Auditor+) and
`ClearBenchmarkEngineSlotCommand` (Admin); API `GET engine` (Auditor+) and
`POST engine/clear-slot` (Admin) with Swagger metadata and contracts;
fake engine updated; unit + policy integration tests added.

### WS1 — DONE (2026-10-29)

Executed by Lead Dev under `bitcraft-develop`. Steps 1-9 complete.

Validation results:

- `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` —
  25 passed, 3 pre-existing environment-gated failures (need `deepeval`
  installed; identical failures reproduce on clean HEAD before the changes:
  baseline 20 passed / 3 failed). The 5 new tests (G10 write serialization,
  G15 tuple normalization, G10 end-to-end runner write, G1 stale sweep, G1
  fresh-status preservation) all pass.
- `python -m pytest sidecar/tests` — 23 passed, 1 skipped
  (`test_custom_benchmarks.py` now skips cleanly via `importorskip("deepeval")`
  instead of failing collection).
- `dotnet build bitcraft.slnx` — 0 errors.
- `dotnet test tests/Bitcraft.UnitTests` — 575 passed, 0 failed (baseline 571
  + 4 new: busy-retry stays queued, busy-past-budget QueueTimeout, synthesized
  G9 log, completed-without-engine-id returns null).

Drift and notes:

1. Plan step 6 said to add the regression tests to the SidecarIntegration
   suite "keeping it deepeval-free" — done as specified; no drift.
2. `write_status` gained a `default=` handler (step 1) AND the runner
   serializes task_scores via `model_dump()` (step 2) AND the terminal write
   is wrapped with a failed-status fallback (step 3): three layers per plan.
3. Step 5 sweep uses `BENCHMARK_STALE_AFTER_SECONDS` (default 300) and only
   sweeps statuses older than the threshold, so a fast sidecar restart during
   a live run does not reap it. The FastAPI `on_event("startup")` handler is
   deprecated in newer FastAPI but matches the pinned requirements; flagged
   for a later lifespan migration, not a blocker.
4. WS1 steps 8-9 (backend) required no new repository methods; the busy-retry
   reuses the worker's existing wake-up loop (`MarkFailedAsync` is only
   bypassed for `StandardBenchmark.SidecarBusy`).

## CSS, DIS, CIL

- CSS: 7 — queue orchestration, persistence changes, admin control surface;
  no credential or destructive-data surface beyond existing delete.
- DIS: 6 — sidecar + Application + Infrastructure + Api + frontend in one
  feature path.
- CIL: high — `.github/config/cil-high.yaml`: Lead Dev + mandatory Sec Ops
  review; human approval before merge.

## Open Human Decisions

| # | Decision | Proposal | Blocks |
|---|----------|----------|--------|
| HD-4 | Archive semantics | `IsArchived` flag; hidden from default lists; never auto-deleted; delete still allowed | WS3 |
| HD-8 | Queue pause shape | One global Admin toggle on the worker dequeue loop; running runs finish | WS3 |
| HD-9 | Sidecar control surface | Status endpoint (Auditor+ read, Admin action) + clear-slot; no runtime config editing | WS2 |

## Impacted Files

- Sidecar: `app/runner/status.py`, `app/runner/__main__.py`,
  `app/tools/deepeval_llm_benchmarks.py`, `app/main.py`,
  `tests/test_custom_benchmarks.py` (import guard),
  `tests/test_deepeval_llm_benchmarks.py` (new tests),
  `tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` (new tests).
- Backend: `StandardBenchmarkRun.cs`, `StandardBenchmarkFeatures.cs`,
  `IBenchmarkEngine.cs`, `DeepEvalBenchmarkEngine.cs`,
  `IStandardBenchmarkRunRepository.cs` + repository (archived filter),
  `StandardBenchmarkRunWorker.cs` (pause check),
  `StandardBenchmarkRunsController.cs`,
  contracts in `src/Bitcraft.Api/Contracts/Tutoring/`,
  `TutoringMapping.cs`, new `BenchmarkQueueState` entity + migration
  `StandardBenchmarkRunArchive`.
- Frontend: `BenchmarkingPage.jsx`, `BenchmarkQueuePanel.jsx` (new),
  `StandardBenchmarkRunPage.jsx`, `StandardBenchmarkRunLog.jsx`,
  `BenchmarkRunHistory.jsx`, `benchmarkingApi.js`, `benchmarkingUtils.js`.
- Docs: `docs/benchmarking-templates.md` or `docs/benchmark-run-queue.md`.

## Validation Plan

- `dotnet build bitcraft.slnx` → 0 errors.
- `dotnet test` on UnitTests, ApiIntegrationTests, InfrastructureIntegrationTests → green.
- `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` → green.
- `python -m pytest sidecar/tests` → green (deepeval-gated files may skip).
- `npm run build` and `npm run lint` in `tutor-studio/` → green (only the
  pre-existing `main.jsx` fast-refresh error).
- Manual QA (human): run one task-based suite (small MMLU subset) end to end;
  verify completed status, sub-scores, live log; run arduino end to end;
  verify four sub-scores; pause queue, archive a run, clear slot via UI.

## Delegation Plan

| Work Stream | Primary | Supporting | Phase 2 skill |
|---|---|---|---|
| WS1 | lead-dev | sec-ops | `bitcraft-python-sidecar`, `bitcraft-backend-feature` |
| WS2 | lead-dev | sec-ops | `bitcraft-python-sidecar`, `bitcraft-backend-feature`, `bitcraft-api-endpoint` |
| WS3 | lead-dev | sec-ops | `bitcraft-backend-feature`, `bitcraft-api-endpoint`, `bitcraft-frontend-feature` |
| WS4 | lead-dev | secretary | `bitcraft-frontend-feature`, `bitcraft-doc-maintenance` |

## Handover Note for the Developing Agent

- Read `context.md` in this folder first. It lists verified facts; do not
  re-derive them.
- Execute WS1 first. WS1 steps 1-7 are sidecar-only and independent of the
  C# work; steps 8-9 are backend.
- The current live sidecar volume still holds the poisoned slot file. After
  WS1 step 5 merges and the image is rebuilt, the startup sweep clears it on
  first container start. Until then the interim manual command in context.md
  applies.
- HALT and ask the human if HD-4, HD-8, or HD-9 is still unapproved when you
  reach WS2/WS3.
- Keep agent-facing text in Simplified Technical English.
