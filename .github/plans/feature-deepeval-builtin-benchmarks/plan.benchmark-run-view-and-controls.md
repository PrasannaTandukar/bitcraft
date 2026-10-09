---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: planner
editor: planner
ai_gen: true
status: done
css: 7
dis: 7
cil: high
---
# Sub-Plan: Benchmark Run View, Controls, and Honest Progress

Origin: Planner, 2026-09-21. Human-reported defects from live use, third
iteration. The human explicitly required that requirements be pinned before
implementation: "Let's ensure we get the requirements down right before we even
waste time again — it's third iteration and we're not even close to quality yet."

## Goal

Give standard benchmark runs a dedicated drill-down view with real insight into
results, add run controls (cancel, delete, requeue, queue reorder), make launch
explicitly confirmed, reset the wizard after launch, and make progress counts
honest so a run never reports `57/1` or freezes on "Finalizing".

## Non-Goals

- No pause or resume. The human chose cancel-only.
- No changes to tutor-scoped RAG benchmark runs. The human scoped this to
  standard runs only. `BenchmarkRunDetails.jsx` stays as-is.
- No custom-benchmark authoring UI (decision D-2 stands).
- No changes to the sidecar's single-runner-slot rule (decision D-R2).
- No log-shipping infrastructure. Logs are read from the runner's existing
  output, not a new pipeline.

## Verified Current State (evidence, 2026-09-21)

All findings below were confirmed by reading the installed deepeval 4.2.3
package and the current working tree. None are inferred.

### Bug A — Status shows `In Progress 57/1`

Root cause is a computation error, not a display error.

1. `len(list(MMLUTask)) == 57`. Verified by importing the enum from
   `.conda/Lib/site-packages/deepeval/benchmarks/mmlu/task.py`.
2. The wizard sends `tasks: null` when no task is selected
   (`BenchmarkRunWizard.jsx:204`: `selectedTasks.length ? selectedTasks : null`).
   deepeval then runs all 57 tasks (`mmlu.py:26`: `list(MMLUTask) if tasks is
   None else tasks`).
3. `_estimate_total_tasks` (`deepeval_llm_benchmarks.py:521`) computes
   `task_count = len(resolved_tasks) if resolved_tasks else 1`. With
   `resolved_tasks` null, `task_count` is **1**, not 57.
4. With `nProblems: 1` the estimate is `1 * 1 = 1`.
5. `predictions` ends with 57 rows (1 problem x 57 tasks), so
   `_count_completed_tasks` returns 57.

Result: `completed=57`, `total=1`. The total is derived from the request
instead of from the benchmark's actual resolved task list.

Fix direction: resolve the effective task list the same way deepeval does, and
derive the total from `benchmark.tasks` after construction. See WS1.

### Bug B — "Finalizing" appears to hang

Two separate causes.

1. **No progress during the blocking call.** `evaluate()` is one synchronous
   call and deepeval 4.2.3 exposes no per-problem callback. The status file
   stays at `running/evaluating` with `completed_tasks: null` for the whole
   run. On 57 MMLU tasks that is hours of no movement.
2. **`finalizing` is written twice with different meanings.** The runner writes
   `stage_writer("finalizing", ...)` while still `running`
   (`deepeval_llm_benchmarks.py:452`), then writes the terminal status with
   `base_status("completed", "finalizing")` (`runner/__main__.py:103`). So
   `finalizing` is both an in-progress stage and the terminal stage name.
3. The backend polls every 30 s (`PollIntervalSeconds: 30`), so even a settled
   run lags by up to 30 s.

Fix direction: emit a status write per prediction by wrapping the judge model's
`generate()`. See WS1 step 4.

### Bug C — No launch confirmation, no wizard reset

`launch()` in `BenchmarkRunWizard.jsx:191` POSTs immediately. There is no
confirmation step and no Cancel. After a successful launch the function returns
without resetting `step`, `selectedTasks`, `nShots`, or `nProblems`
(`BenchmarkRunWizard.jsx:206-209` returns early after `onCreated`).

### Bug D — No run controls

`StandardBenchmarkRunsController.cs` exposes only `GET suites`, `GET`,
`GET {id}`, `GET {id}/progress`, `POST`. There is no delete, cancel, requeue,
or reorder. `BenchmarkRunStatus` has no `Cancelled` member
(`BenchmarkRun.cs:72-78`: Queued, Running, Completed, Failed).

### Gap E — No dedicated run view

Standard run detail renders only in a `Drawer`
(`StandardBenchmarkHistory.jsx:16`, `StandardBenchmarkRunDrawer`). It shows
status, stage, model, suite, score, counts, shots, engine, timestamps, notes,
failure, and a task-score table. There is no route, no per-problem results, and
no log view.

### Constraint F — Queue is a plain FIFO channel

`InMemoryStandardBenchmarkRunQueue` is a bounded `Channel<Guid>` with
`SingleReader = true`. It cannot reorder or remove entries. Reordering requires
either a priority-aware queue implementation or a persisted priority column read
at dequeue time. See WS5.

### Constraint G — Cancel cannot reach inside `evaluate()`

`start_benchmark_run` spawns the runner with `subprocess.Popen(...,
start_new_session=True)` and discards stdout/stderr to `DEVNULL`
(`deepeval_llm_benchmarks.py:337-352`). The pid is not retained. Cancelling
means: mark the run Cancelled in the DB, stop the backend polling it, and
terminate the runner process group. It cannot stop `evaluate()` mid-problem.
See WS4.

## Human Decisions (resolved 2026-09-21 via question prompt)

- **D-1 — RESOLVED: standard runs only.** The dedicated view covers standard
  benchmark runs. Tutor RAG runs keep the existing `BenchmarkRunDetails` drawer.
- **D-2 — RESOLVED: drill-down from the Evaluations tab.** Clicking a row opens
  `/benchmarking/runs/:runId`. The list stays the entry point.
- **D-3 — RESOLVED: cancel only, no pause.** No `Paused` status, no resume.
- **D-4 — RESOLVED: per-problem real counts.** Wrap the judge model's
  `generate()` to write status per prediction. This is the only way to get
  movement during the blocking `evaluate()` call.
- **D-5 — RESOLVED: all four controls.** Cancel queued/running, delete a run
  record, requeue a failed/cancelled run, and reorder/prioritise the queue.
- **D-6 — RESOLVED (human free-text): add a run log.** Keep or obtain a
  reference log, and surface it in a scrollable text view, filtered to
  effective info and errors.

## Derived Rules

- **DR-1 (from Bug A):** The total task count must come from the benchmark
  object after construction, never from the request. When `tasks` is null the
  effective count is the suite's full task-enum length, matching deepeval.
- **DR-2 (from Bug B):** `finalizing` must mean exactly one thing. Rename the
  in-progress stage to `scoring` and keep `finalizing` as the terminal stage
  only.
- **DR-3 (from Bug B):** Progress writes must be throttled. Writing the status
  file on every prediction would hammer the shared volume on a 1319-problem
  GSM8K run. Write at most once per second, and always on the last prediction.
- **DR-4 (from D-3):** Cancel is terminal and one-way. A cancelled run never
  resumes; the human requeues it instead.
- **DR-5 (from Constraint F):** Queue reordering is implemented as a priority
  integer on the run row, read at dequeue time. The channel is not reordered
  in place. Priority 0 is normal; lower numbers run first.
- **DR-6 (from D-6):** The runner's stdout/stderr must be captured to a file
  beside the status file instead of `DEVNULL`, so the log view has a source.
  The log must be size-capped and must never contain the API key.

## Impacted Files

**Backend**
- `src/Bitcraft.Domain/Benchmarking/BenchmarkRun.cs` (add `Cancelled` to `BenchmarkRunStatus`)
- `src/Bitcraft.Domain/Benchmarking/StandardBenchmarkRun.cs` (add `Priority`, `RunLogPath`, `CancelledUtc`)
- `src/Bitcraft.Application/Common/Interfaces/IStandardBenchmarkRunQueue.cs` (priority-aware enqueue)
- `src/Bitcraft.Application/Common/Interfaces/IStandardBenchmarkRunRepository.cs` (delete, list queued ordered)
- `src/Bitcraft.Application/Common/Models/BenchmarkingModels.cs` (status gains `Cancelled`, log fields)
- `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs` (cancel, delete, requeue, reorder handlers)
- `src/Bitcraft.Infrastructure/Persistence/Repositories/Benchmarking/StandardBenchmarkRunRepository.cs`
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/InMemoryStandardBenchmarkRunQueue.cs` (DR-5)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/StandardBenchmarkRunWorker.cs` (skip cancelled, priority order)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs` (log fetch, cancel signal)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkContracts.cs`
- `src/Bitcraft.Api/Contracts/Tutoring/TutorBenchmarkRuns.cs`
- `src/Bitcraft.Api/Mapping/TutoringMapping.cs`
- `src/Bitcraft.Api/Controllers/Benchmarking/StandardBenchmarkRunsController.cs` (new endpoints)
- `src/Bitcraft.Infrastructure/Persistence/Migrations/` (new migration)

**Sidecar**
- `sidecar/app/tools/deepeval_llm_benchmarks.py` (Bug A, Bug B, DR-3, DR-6)
- `sidecar/app/runner/__main__.py` (stage rename, log capture)
- `sidecar/app/runner/status.py` (log fields)
- `sidecar/app/schemas.py`
- `sidecar/app/main.py` (cancel endpoint)

**Frontend**
- `tutor-studio/src/App.jsx` (new route)
- `tutor-studio/src/benchmarking/StandardBenchmarkRunPage.jsx` (new, D-1/D-2)
- `tutor-studio/src/benchmarking/StandardBenchmarkRunLog.jsx` (new, D-6)
- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` (Bug C)
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx` (drill-down link)
- `tutor-studio/src/benchmarking/benchmarkingApi.js`
- `tutor-studio/src/benchmarking/benchmarkingUtils.js`

**Tests**
- `tests/Bitcraft.UnitTests/Application/Benchmarking/StandardBenchmarkFeaturesTests.cs`
- `sidecar/tests/test_deepeval_llm_benchmarks.py`

## Work Steps

### WS1 — Honest progress counts (sidecar)

Phase 2 skill: `bitcraft-python-sidecar`.

1. In `sidecar/app/tools/deepeval_llm_benchmarks.py`, add a helper
   `_effective_task_count(benchmark, resolved_tasks)` that returns
   `len(benchmark.tasks)` when the benchmark exposes `tasks`, else
   `len(resolved_tasks)` when tasks were requested, else 1.
   Check: `python -c` importing the module reports no error.
2. In `execute_benchmark`, compute `total_tasks` from
   `_effective_task_count` after `benchmark_cls(**kwargs)` and before
   `evaluate()`. Multiply by `n_problems_forwarded` when a cap was forwarded.
   Check: a unit test with a stub benchmark exposing 3 tasks and
   `n_problems=2` yields `total_tasks == 6`.
3. Delete the `len(resolved_tasks) if resolved_tasks else 1` fallback from
   `_estimate_total_tasks`. The estimate now takes the resolved count only.
   Check: grep for `else 1` in that function returns 0 matches.
4. Wrap the judge model so each prediction writes progress. In
   `_SidecarJudgeLLM.build()`, accept an optional `on_prediction` callback and
   invoke it after every `generate()` and `generate_samples()` call.
   Check: a stub run with 3 predictions invokes the callback 3 times.
5. In `execute_benchmark`, pass an `on_prediction` callback that increments a
   counter and calls `stage_writer("evaluating", {"completed_tasks": n,
   "total_tasks": total})`. Throttle writes to at most one per second and
   always write the final count (DR-3).
   Check: a stub run writes `completed_tasks` values that increase and end at
   the total.
6. Rename the in-progress stage `finalizing` to `scoring` (DR-2). Keep the
   terminal stage `finalizing` in `runner/__main__.py` unchanged.
   Check: grep for `"finalizing"` in `deepeval_llm_benchmarks.py` returns
   0 matches; the runner still writes it once.
7. Add sidecar pytest coverage for Bug A: a stub benchmark with 3 tasks and
   `tasks=None` must report `total_tasks == 3`, not 1.
   Check: `pytest` in `sidecar/` passes.

### WS2 — Runner log capture (sidecar)

Phase 2 skill: `bitcraft-python-sidecar`.

1. In `sidecar/app/tools/deepeval_llm_benchmarks.py`, change the `Popen` call
   to redirect stdout and stderr to `<run_id>.log` beside the status file
   instead of `DEVNULL` (DR-6).
   Check: a started run creates a `.log` file.
2. Retain the child pid in a module-level dict keyed by run id so a cancel can
   terminate it later (Constraint G).
   Check: the dict holds the pid after a start.
3. Cap the log file at 2 MB. When the cap is exceeded, truncate the oldest
   half. Do not use a rotating handler that needs a long-lived process.
   Check: a unit test writing 3 MB of lines leaves a file under 2 MB.
4. Scrub the judge API key from every log line before writing.
   Check: a unit test with the key in the output records `[REDACTED]`.
5. Add `log_path` to the status document in `sidecar/app/runner/status.py`.
   Check: the status file contains `log_path`.
6. Add a pytest asserting the log file is created and the key is scrubbed.
   Check: `pytest` in `sidecar/` passes.

### WS3 — Cancel endpoint (sidecar + backend)

Phase 2 skill: `bitcraft-python-sidecar`, then `bitcraft-backend-feature`.

1. In `sidecar/app/main.py`, add `POST /internal/benchmarks/{run_id}/cancel`
   that terminates the runner process group for that run id and writes a
   `cancelled` terminal state to the status file.
   Check: calling it on a running run sets the state to `cancelled`.
2. In `DeepEvalBenchmarkEngine.cs`, add
   `CancelStandardLlmBenchmarkAsync(StandardBenchmarkHandle, CancellationToken)`
   returning `Result<bool>`.
   Check: `dotnet build` reports 0 errors.
3. In `StandardBenchmarkFeatures.cs`, add
   `CancelStandardBenchmarkRunCommand(Guid RunId)` and its handler. The handler
   loads the run, rejects terminal runs with
   `StandardBenchmark.NotCancellable`, sets `Status = Cancelled`, sets
   `CancelledUtc`, and calls the engine cancel when a handle exists.
   Check: `dotnet build` reports 0 errors.
4. Add `Cancelled` to `BenchmarkRunStatus` in `BenchmarkRun.cs` with value 4.
   Check: `dotnet build` reports 0 errors.
5. Add unit tests: cancel a queued run succeeds; cancel a completed run fails
   with `StandardBenchmark.NotCancellable`.
   Check: `dotnet test tests/Bitcraft.UnitTests` passes.

### WS4 — Delete, requeue, and reorder (backend)

Phase 2 skill: `bitcraft-backend-feature`.

1. Add `DeleteStandardBenchmarkRunCommand(Guid RunId)` and its handler. The
   handler requires the Admin role and hard-deletes the row (repository
   default: no soft delete).
   Check: `dotnet build` reports 0 errors.
2. Add `RequeueStandardBenchmarkRunCommand(Guid RunId)` and its handler. The
   handler clones the run's parameters into a new `Queued` run and enqueues it.
   It must not mutate the original row.
   Check: `dotnet build` reports 0 errors.
3. Add `Priority` (int, default 0) to `StandardBenchmarkRun` and create the EF
   migration `StandardBenchmarkRunControls`.
   Check: the migration file exists and `dotnet build` reports 0 errors.
4. Add `SetStandardBenchmarkRunPriorityCommand(Guid RunId, int Priority)` and
   its handler. Lower values run first (DR-5).
   Check: `dotnet build` reports 0 errors.
5. In `StandardBenchmarkRunWorker.cs`, dequeue by reading queued runs ordered
   by `Priority` then `QueuedUtc`, and skip any run whose status is
   `Cancelled` before processing it.
   Check: `dotnet build` reports 0 errors.
6. Add unit tests for delete, requeue, priority ordering, and skipping a
   cancelled run.
   Check: `dotnet test tests/Bitcraft.UnitTests` passes.

### WS5 — API surface (backend)

Phase 2 skill: `bitcraft-api-endpoint`.

1. In `StandardBenchmarkRunsController.cs`, add
   `POST {runId:guid}/cancel` with `AdminOnly`.
   Check: `dotnet build` reports 0 errors.
2. Add `DELETE {runId:guid}` with `AdminOnly`.
   Check: `dotnet build` reports 0 errors.
3. Add `POST {runId:guid}/requeue` with `AdminOnly`.
   Check: `dotnet build` reports 0 errors.
4. Add `PUT {runId:guid}/priority` with `AdminOnly` and a
   `SetStandardBenchmarkRunPriorityRequest` record.
   Check: `dotnet build` reports 0 errors.
5. Add `GET {runId:guid}/log` returning the filtered, size-capped log text.
   Check: `dotnet build` reports 0 errors.
6. Extend `StandardBenchmarkRunResponse` with `Priority`, `CancelledUtc`, and
   `HasLog`. Map them in `TutoringMapping.cs`.
   Check: `dotnet build` reports 0 errors.
7. Add `[ProducesResponseType]` for 400, 403, and 404 on every new action.
   Check: `dotnet build` reports 0 errors.

### WS6 — Launch confirmation and wizard reset (frontend)

Phase 2 skill: `bitcraft-frontend-feature`.

1. In `BenchmarkRunWizard.jsx`, add a `launchConfirmOpen` state and render an
   Ant `Modal` with a summary of the run (suite, model, tasks, shots, problem
   cap) and two actions: `Launch` and `Cancel`.
   Check: `npm run build` exits 0.
2. Change `launch()` to open the modal instead of POSTing. Move the POST into a
   `confirmLaunch()` invoked by the modal's primary action.
   Check: `npm run build` exits 0.
3. After a successful launch, reset `step` to 1, clear `selectedTasks`, reset
   `nShots` to 0 and `nProblems` to 10, and close the modal before calling
   `onCreated`.
   Check: `npm run build` exits 0.
4. Keep `setSubmitting(false)` in a `finally` so a failed launch leaves the
   button usable.
   Check: `npm run lint` reports only the pre-existing `main.jsx` error.

### WS7 — Dedicated run view (frontend)

Phase 2 skill: `bitcraft-frontend-feature`.

1. Create `tutor-studio/src/benchmarking/StandardBenchmarkRunPage.jsx` showing
   header (suite, model, status, timestamps), a live progress bar with
   `completed/total`, the overall score, the per-task score table, and the
   failure block when present.
   Check: `npm run build` exits 0.
2. Add the route `benchmarking/runs/:runId` in `App.jsx` wrapped in `RoleRoute`
   with `allowedRoles={["Auditor", "Editor", "Admin"]}`.
   Check: `npm run build` exits 0.
3. Poll `getStandardBenchmarkRunProgress` every 5 s while the run is active and
   refetch the full run once it settles.
   Check: `npm run build` exits 0.
4. Render the four controls with role gating: Cancel (Admin, active runs only),
   Delete (Admin), Requeue (Admin, failed or cancelled only), Priority (Admin,
   queued only).
   Check: `npm run build` exits 0.
5. In `BenchmarkRunHistory.jsx`, change the row action from opening the drawer
   to navigating to `/benchmarking/runs/{id}` for standard rows. Tutor rows keep
   the existing drawer.
   Check: `npm run build` exits 0.
6. Delete `StandardBenchmarkRunDrawer` from `StandardBenchmarkHistory.jsx` once
   no caller remains.
   Check: grep for `StandardBenchmarkRunDrawer` in `tutor-studio/src/` returns
   0 matches.

### WS8 — Run log view (frontend)

Phase 2 skill: `bitcraft-frontend-feature`.

1. Create `tutor-studio/src/benchmarking/StandardBenchmarkRunLog.jsx` rendering
   the log in a scrollable `Typography.Text` block with a fixed-height
   container and a level filter (All, Errors only).
   Check: `npm run build` exits 0.
2. Filter to effective information: drop tqdm progress bars and blank lines,
   and keep error and warning lines.
   Check: `npm run build` exits 0.
3. Add a Refresh control and do not poll the log automatically.
   Check: `npm run lint` reports only the pre-existing `main.jsx` error.

### WS9 — Validation and housekeeping

Phase 2 skill: `bitcraft-review`.

1. Run the full validation set listed below.
   Check: every command reports the expected result.
2. Grep for dead code introduced on this branch: unused state, orphaned
   components, superseded helpers.
   Check: each match is removed or justified here.
3. Run the architecture spot check.
   Check: no new violations beyond the 2 pre-existing ones.
4. Record execution notes and any drift in this plan's Status section.
   Check: the Status section lists every deviation.

## Status

Executed 2026-09-21 by Lead Dev. WS1 through WS9 are complete.

Validation results:

- `dotnet build bitcraft.slnx` — 0 errors.
- `dotnet test tests/Bitcraft.UnitTests` — 433 passed, 0 failed.
- `dotnet test tests/Bitcraft.ApiIntegrationTests` — 39 passed, 0 failed.
- `dotnet test tests/Bitcraft.InfrastructureIntegrationTests` — 28 passed, 0 failed.
- `pytest sidecar/tests` with the conda interpreter — 30 passed, 2 skipped
  (pandas and `datasets` are absent from the local env; both skips are
  environment-gated and pre-existing).
- `npm run lint` in `tutor-studio/` — only the pre-existing `main.jsx`
  fast-refresh error.
- `npm run build` in `tutor-studio/` — exit 0.

Not run: validation items 8, 9, and 10 in the Validation Plan. They need a
live sidecar plus a reachable judge model. Raise them as manual QA.

Drift from the plan:

1. WS1 step 6 said to keep the terminal stage `finalizing` in
   `runner/__main__.py`. Execution set it to `None` instead. The stage field
   describes in-flight work, so a completed status has no stage. Keeping
   `finalizing` there is what made the UI freeze on the word. Acceptance
   criterion 7 is therefore superseded: `finalizing` now appears nowhere.
2. WS7 step 6 said to delete `StandardBenchmarkRunDrawer` from
   `StandardBenchmarkHistory.jsx`. The drawer was the file's only export, so
   the whole file was deleted. Grep for `StandardBenchmarkRunDrawer` and
   `StandardBenchmarkHistory` under `tutor-studio/src/` returns 0 matches.
3. WS1 step 4's HALT gate did not trigger. `should_use_batch` in deepeval
   4.2.3 requires both a non-null `batch_size` and a model with
   `batch_generate`. The sidecar passes neither, so batch mode never
   activates and `generate()` is always the instrumented path.
4. WS4 removed the `MaxParallelLanes` constant rather than setting it to 1.
   The priority-ordered worker processes one run per wake-up, so the constant
   had no remaining reader.

Dead code check: grep for `MaxParallelLanes`, `DrainChannelAsync`,
`standardDetail`, `StandardBenchmarkRunDrawer`, and `StandardBenchmarkHistory`
returns 0 matches across `src/`, `sidecar/`, and `tutor-studio/src/`.

Architecture spot check: no `Bitcraft.Domain` or `Bitcraft.Application` file
references `Bitcraft.Infrastructure` or `Bitcraft.Api`. `Bitcraft.Application`
carries only MediatR and three `Microsoft.Extensions.*` abstractions, all on
the allow-list.

Review (Phase 3, 2026-09-21) fixed four findings in place:

1. `StandardBenchmarkRunPage.jsx` passed no argument to the `after` callback, so
   requeue navigated to `/benchmarking/runs/undefined`. `perform` now forwards
   the action result.
2. `App.jsx` allowed Editor on the run route, but the query handler requires
   Auditor or Admin. Editor now cannot reach the page.
3. `cancel_benchmark_run` caught no `AttributeError`, so the Windows fallback
   was unreachable and `os.killpg` raised. `AttributeError` is now caught.
4. The `_cap_log` docstring said "oldest half"; the code keeps the newest half.
   The docstring now matches the code.

Two findings are routed instead of fixed: S1 (secret scrubbing is never active)
and S3 (`_RUNNER_PIDS` leaks) in `.github/inboxes/sec-ops/inbox.md`. S1 needs a
design decision because the runner deletes the request file before the log is
read, so the key is unavailable at scrub time.

## Acceptance Criteria

1. Launching a run opens a confirmation modal with Launch and Cancel.
2. Cancelling the modal does not POST and leaves the wizard state intact.
3. Confirming resets the wizard to step 1 with default shots and cap.
4. A 1-problem MMLU run with no task filter reports `1/57`, not `57/1`.
5. A run with a task filter of 2 tasks and 3 problems reports a total of 6.
6. During a run the progress count increases at least once per second.
7. The stage `finalizing` appears nowhere; the in-progress stage is `scoring`
   and a completed status carries no stage.
8. Cancel on a queued run removes it from processing and marks it Cancelled.
9. Cancel on a running run terminates the runner process group.
10. Delete removes the run row.
11. Requeue creates a new Queued run with the same parameters and leaves the
    original row unchanged.
12. Two queued runs with priorities 0 and -1 run in the order -1 then 0.
13. The run view shows progress, score, per-task scores, and failure text.
14. The log view shows a scrollable, filtered log with no API key.
15. No control is visible to a role lacking Admin.

## Risks

- **Per-prediction callbacks add overhead.** Mitigation: throttle writes to 1/s
  (DR-3). The callback is a counter increment plus a throttled file write.
- **Batch mode bypasses `generate()`.** deepeval uses `batch_predict` when
  `should_use_batch` is true, which does not call `generate()` per problem.
  Mitigation: WS1 step 4 must also wrap `batch_predict` if present, or the
  count stalls on batch-capable models. Verify before claiming AC6.
- **Cancel cannot stop `evaluate()` mid-problem.** Mitigation: document that
  cancel is effective at the process level and may take up to one problem to
  take effect. The run is marked Cancelled immediately regardless.
- **Log files accumulate on the shared volume.** Mitigation: cap at 2 MB per
  run and delete the log with the run on delete.
- **Channel reordering is not possible in place.** Mitigation: DR-5 priority
  read at dequeue time.
- **Priority is a new column.** Mitigation: additive, nullable-free with a
  default of 0.

## Validation Plan

1. `dotnet build bitcraft.slnx` — 0 errors.
2. `dotnet test tests/Bitcraft.UnitTests` — all pass.
3. `dotnet test tests/Bitcraft.ApiIntegrationTests` — all pass.
4. `dotnet test tests/Bitcraft.InfrastructureIntegrationTests` — all pass.
5. `pytest` in `sidecar/` with the conda interpreter — all pass.
6. `npm run lint` in `tutor-studio/` — only the pre-existing `main.jsx` error.
7. `npm run build` in `tutor-studio/` — exit 0.
8. Manual: launch a 1-problem MMLU run with no task filter and confirm the
   status reads `1/57` and advances.
9. Manual: cancel a queued run and confirm it never starts.
10. Manual: open a run view and confirm progress, scores, and the log render.
11. Report validation performed and anything intentionally not run.

## Delegation Plan

- WS1, WS2, WS3 (sidecar half): Lead Dev with `bitcraft-python-sidecar`.
- WS3 (backend half), WS4, WS5: Lead Dev with `bitcraft-backend-feature` and
  `bitcraft-api-endpoint`.
- WS6, WS7, WS8: Lead Dev with `bitcraft-frontend-feature`.
- WS9: Lead Dev with `bitcraft-review`.
- Sec Ops: review the new cancel endpoint (process termination), the log
  endpoint (data exposure), and the delete endpoint (destructive).
- AI Ops: no agentic-stack change expected; confirm at WS9.

## Open Human Decisions

None. D-1 through D-6 are resolved. If WS1 step 4 discovers that batch mode
cannot be instrumented, HALT and raise a new decision rather than shipping a
progress count that silently stalls.

## Phase 2 Prompt

Run the `bitcraft-develop` skill for
`.github/plans/feature-deepeval-builtin-benchmarks/plan.benchmark-run-view-and-controls.md`.
Execute work steps WS1 through WS9 in order. Validate each step before the next.
All human decisions D-1 through D-6 are resolved. Do not add scope beyond the
named steps. HALT and raise a new human decision if WS1 step 4 finds that
deepeval batch mode cannot be instrumented for per-problem progress.
