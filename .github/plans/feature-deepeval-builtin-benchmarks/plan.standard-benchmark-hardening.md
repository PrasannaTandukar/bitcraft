---
created_at: 2026-09-20
edited_at: 2026-09-20
creator: planner
editor: planner
ai_gen: true
---
# Sub-Plan: Standard Benchmark Execution Hardening — Runner Process, Start/Poll, Full-Branch Findings

Origin: Planner, 2026-09-20. Converts the combined `bitcraft-review` + `bitcraft-security-review`
findings over the full branch diff (C1–C3, H4–H20, plus security notes) into fixed work streams.
All human decisions were captured 2026-09-20 via the VS Code question prompt and are FINAL
(see "Human Decisions Captured"). Execution order: WS1 → WS2 → WS3 → WS4 → WS5 → WS6 → WS7.

## Goal

Make standard benchmark runs actually work end to end and make the whole Benchmarking feature
coherent and final on this branch: a dedicated per-run runner process keeps the sidecar event
loop free, a start/poll contract removes the 6-hour HTTP timeout, per-task scores and progress
become real data, the .NET pipeline gets strong typing and Microsoft-Learn-grounded hygiene,
and the frontend consumes all of it with polling and task selection.

## Verified Current State (evidence, 2026-09-20)

- **C1 (blocker):** `sidecar/requirements.txt` lacks `datasets` and `pandas`. Empirically proven
  in `.conda` (deepeval 4.2.3): `MMLU(n_shots=0, n_problems_per_task=2)` raises
  `ModuleNotFoundError: No module named 'pandas'`. Every enabled suite fails at construction.
- **C2 (blocker):** `sidecar/app/tools/deepeval_llm_benchmarks.py:176` — `run_llm_benchmark` is
  `async def` but calls synchronous `benchmark.evaluate(...)` (line 259). No `a_evaluate` exists
  on any deepeval 4.2.3 benchmark class (verified). The call blocks the uvicorn event loop for
  up to `StandardTimeoutSeconds = 21_600` (6 h); `/health` stops answering; the compose
  healthcheck fails while a run is in progress.
- **C3 (privacy):** `DEEPEVAL_TELEMETRY_OPT_OUT` is never set. deepeval 4.2.3 posts telemetry to
  `https://us.i.posthog.com` on every evaluate. `sidecar/Dockerfile` sets `DEEPEVAL_DISABLE_DOTENV`
  only. Contradicts `docs/transparency-and-privacy.md` "No First-Party Analytics By Default".
- **H4:** API accepts `n_shots` 0–5 (`StandardBenchmarkFeatures.cs:52`); deepeval's BigBenchHard
  asserts `n_shots <= 3` → runtime AssertionError surfaced as generic "Benchmark run failed".
- **H5:** `_SidecarJudgeLLM.build().generate_samples` returns `(completions, 0.0)` tuple;
  deepeval expects `list[str]` → HumanEval TypeError (latent; suite `Enabled: false`).
- **H6:** `_resolve_tasks` (deepeval_llm_benchmarks.py:160-172) silently drops unresolvable task
  names and returns `None` → full default task set runs (cost surprise).
- **H7/H8:** `DeepEvalBenchmarkEngine.CreateClient` builds `new HttpClient(...)` per call on both
  paths; `HttpClient.Timeout` set to 6 h for standard runs. MS Learn: reuse HttpClient instances;
  default timeout 100 s; with a per-request CancellationToken only the shorter of the two
  timeouts applies.
- **H9:** `StandardBenchmarkRunRepository` read queries use `.Include(...)` with no
  `AsNoTracking()`. MS Learn: no-tracking queries for read-only data.
- **H10:** `services.Configure<DeepEvalBenchmarkOptions>` (DependencyInjection.cs:142) with no
  validation. MS options pattern supports `ValidateDataAnnotations().ValidateOnStart()`.
- **H11/H12:** `StandardBenchmarkRunWorker.ExecuteAsync` awaits `RecoverPersistedRunsAsync`
  synchronously before the queue loop (startup block); `BackgroundService` default 30 s
  graceful-shutdown timeout vs 6 h runs.
- **H14:** No `GET /api/standard-benchmark-runs/{runId}/progress` (tutor runs have
  `{runId}/progress` at `TutorBenchmarkRunsController.cs:47`).
- **H15:** `StandardBenchmarkRun.TaskScoresJson` (Domain line 44, jsonb column, mapped in
  `StandardBenchmarkRunConfiguration.cs:33`, rendered in `StandardBenchmarkHistory.jsx:121-123`)
  is never written by `ProcessQueuedStandardBenchmarkRunHandler`. Grounded: the sidecar already
  reads `benchmark.task_scores` (deepeval_llm_benchmarks.py:264) but discards it; the observability
  research doc lists `TaskScoresJson` as a queryable JSONB signal. Human decision: populate it.
- **H16:** Standard path payload is an anonymous object with manual `JsonElement` parsing
  (`DeepEvalBenchmarkEngine.cs:113-149`); tutor path uses source-gen
  `DeepEvalBenchmarkJsonContext`. Inconsistent, weakly typed.
- **H17:** `docker-compose.prod.yml` has no `benchmark-sidecar` service.
- **H18:** `src/Bitcraft.Api/appsettings.json` has no `Benchmarking:DeepEval` section.
- **H19:** `README.md` API table (lines 151-153) and `docs/architecture.md` §3.5/§5 omit the
  standard-benchmark endpoints; `docs/authorization-roles.md` has no benchmark rows at all.
- **H20:** Frontend: `tasks: null` hardcoded in `launch` (`BenchmarkRunWizard.jsx:246`);
  `SupportsTasks` capability unused; `StandardBenchmarkHistory` has manual refresh only;
  wizard `selectBenchmarkCategory` bounces back to stage 1 when the target is missing.
- **Security (verified):** WS5 api-key transport is sound (UDS-only, preserve-on-null,
  endpoint-match prefill guard). `RuntimeEngine.ApiKey` is raw at rest, capped at 200 chars
  (`RuntimeEngineConfiguration.cs:30-32`). NIST guidance could not be fetched (CSRC JS-rendered,
  xlsx binary, pages.nist.gov unreachable) — NIST mapping remains OPEN, do not fabricate.
- Validation baseline: `dotnet build` 0/0; unit 406/406.

## Non-Goals

- No secret store or encryption-at-rest change (locked: raw key at rest).
- No HumanEval enablement (locked decision; only the `generate_samples` shape is fixed).
- No Tier-2 LLM-judge tool registration, no FallbackPolicy change, no `main.jsx` lint fix
  (human-deferred earlier).
- No NIST control mapping until sources are retrievable.
- No Git writes; commit/push/PR stay human-owned.

## Assumptions

- The sidecar transport (local UDS) is trusted-internal; the api_key payload over UDS is the
  resolved WS5 decision and does not change.
- deepeval stays on 4.2.x (`>=4.2,<5.0` pin); all signature claims verified against 4.2.3.
- Single backend instance per environment (in-memory queue + recovery is the accepted model).
- The runner writes only to `/var/run/bitcraft/runs/` (shared socket volume) — no new volumes.

## Work Streams

### WS1 — Sidecar runner process (C1, C2, C3, H5, H6)

**Design (human-selected: per-run subprocess, single parallel process, 409 on busy).**

New module `sidecar/app/runner/__init__.py` + `sidecar/app/runner/__main__.py`:

- CLI: `python -m app.runner --request-file <path> --status-file <path>`. Runs one benchmark
  synchronously (the current `run_llm_benchmark` body, made synchronous) and exits.
- Writes `<status-file>` JSON atomically (write temp + rename) at every state change:
  `{"state": "running"|"completed"|"failed", "stage": str, "overall_score": float|null,
  "task_scores": [{"task": str, "score": float}]|null, "task_count": int, "engine_version": str,
  "notes": str, "error": str|null, "updated_utc": str}`. Stage granularity: `loading-dataset`,
  `evaluating`, `finalizing` (deepeval 4.2.3 exposes no per-problem callback; stages are
  honest, not fabricated percentages).
- `run_llm_benchmark` becomes a thin dispatcher: validates the request (provider, suite,
  HumanEval guard), checks the single runner slot (module-level lock; 409
  `{"code": "Sidecar.BenchmarkBusy", "description": "..."}` when held), spawns
  `subprocess.Popen([sys.executable, "-m", "app.runner", ...])` with `start_new_session=True`,
  and returns `202 {"run_id": <uuid>, "status_path": "runs/<uuid>.json"}` immediately.
- New endpoints in `app/main.py`:
  - `POST /internal/tools/llm-benchmark` → 202 + handle (contract change, see WS2) or 409.
  - `GET /internal/benchmarks/{run_id}/status` → current status-file content; 404 unknown id.
- Runner slot is released when the status file reaches `completed`/`failed` (dispatcher polls
  the file; a stale `running` file older than `--max-runtime` is treated as dead → `failed`).
- `_resolve_tasks` hard-fails on unknown names: returns the list of unresolvable names in the
  error (`"Unknown tasks for <key>: [a, b]"`), no silent full-dataset fallback (H6).
- `generate_samples` returns `completions` only, not `(completions, 0.0)` (H5).
- `requirements.txt`: add `datasets>=2.20,<3.0` and `pandas>=2.2,<3.0` (C1).
- `sidecar/Dockerfile`: add `ENV DEEPEVAL_TELEMETRY_OPT_OUT=1` (C3); same env in
  `docker-compose.yml` benchmark-sidecar environment for explicitness.
- Runner inherits `DEEVAL_TELEMETRY_OPT_OUT` via the environment (subprocess default).

**Acceptance criteria**

- AC1: With `datasets`+`pandas` installed, `MMLU(n_shots=0, n_problems_per_task=2)` constructs
  and evaluates without `ModuleNotFoundError` (conda smoke test).
- AC2: While a runner subprocess sleeps (fake benchmark), `GET /health` answers < 1 s and a
  second `POST /internal/tools/llm-benchmark` returns 409 with code `Sidecar.BenchmarkBusy`.
- AC3: Status file transitions `running → completed` with `overall_score` and non-empty
  `task_scores` for a 2-problem MMLU run; `GET /internal/benchmarks/{id}/status` returns it.
- AC4: A request with task name `"not_a_task"` fails with the unknown name listed; it does not
  run the full dataset.
- AC5: No outbound request to `us.i.posthog.com` during an evaluate (opt-out env set; verified
  by env presence + deepeval source check).
- AC6: `generate_samples` returns a plain list (source inspection + pytest).

### WS2 — .NET engine start/poll + strong typing (H7, H8, H16)

- `DeepEvalBenchmarkContracts.cs`: add source-gen records for the standard path —
  `StandardBenchmarkStartRequest` (snake_case pins), `StandardBenchmarkStartResponse(run_id)`,
  `StandardBenchmarkStatusResponse` (all runner status fields). Extend
  `DeepEvalBenchmarkJsonContext`.
- `IBenchmarkEngine`: replace `EvaluateStandardLlmBenchmarkAsync` with
  `StartStandardLlmBenchmarkAsync(request, ct) -> Result<StandardBenchmarkHandle>` and
  `GetStandardLlmBenchmarkStatusAsync(handle, ct) -> Result<StandardBenchmarkStatus>`.
- `DeepEvalBenchmarkEngine`:
  - One cached UDS `HttpClient` (lazy, disposed by the engine's `IAsyncDisposable` or a
    `sealed` holder); HTTP fallback keeps `IHttpClientFactory` client. No per-call
    construction (H7).
  - Start call: short timeout (existing `TimeoutSeconds`, 120 s default). Poll call: short
    timeout; poll loop lives in the Application worker, not the engine (H8 — no 6 h
    `HttpClient.Timeout` anywhere; per-request `CancellationToken` governs total duration).
  - 409 from sidecar maps to `Error("StandardBenchmark.SidecarBusy", ...)`.
- `StandardLlmBenchmarkRequest`/`StandardLlmBenchmarkResult` records move to typed contracts;
  `ToSidecarProvider` magic strings become constants shared with the payload records.

**Acceptance criteria**

- AC7: No `new HttpClient(` outside engine construction; `dotnet build` 0 warnings.
- AC8: Unit test: engine start returns handle; poll returns typed status; 409 maps to
  `StandardBenchmark.SidecarBusy`.
- AC9: No `JsonElement` manual parsing in the standard path (grep clean).

### WS3 — Application worker poll loop + progress + task scores (H11, H12, H14, H15)

- `ProcessQueuedStandardBenchmarkRunHandler`: start via engine, then poll
  `GetStandardLlmBenchmarkStatusAsync` every `PollIntervalSeconds` (new option, default 30)
  until terminal state or `StandardTimeoutSeconds` budget exceeded (→ `MarkFailedAsync` with
  `StandardBenchmark.Timeout`). Each poll updates the run row (throttled to poll cadence).
- Domain `StandardBenchmarkRun`: add `ProgressStage` (string, 100) + `LastProgressUtc`
  (nullable) — mirrors `BenchmarkRun.SetProgress` semantics; EF migration (additive columns +
  `TaskScoresJson` population starts; no column drops).
- On completion: `TaskScoresJson = JsonSerializer.Serialize(status.TaskScores)` (H15 — human
  decision: populate; sidecar already reads `benchmark.task_scores`).
- `StandardBenchmarkRunWorker`: `RecoverPersistedRunsAsync` fires recovery items into the
  channel instead of awaiting inline (H12); `UseDefaultShutdown` behavior documented — worker
  honors `stoppingToken` between polls, so graceful shutdown is bounded by one poll interval,
  not 6 h (H11).
- New `GetStandardBenchmarkRunProgressQuery` + `GET /api/standard-benchmark-runs/{runId}/progress`
  returning `StandardBenchmarkRunProgressResponse(Id, Status, ProgressStage, OverallScore,
  TaskCount, QueuedUtc, StartedUtc, CompletedUtc, LastProgressUtc, FailureCode,
  FailureDescription)` — AuditorOrAbove, mirrors the tutor progress endpoint shape (H14).
- Repository: `AsNoTracking()` on `GetByIdAsync` used by progress/list reads (H9); the Process
  handler's tracked fetch stays tracked (it updates the row).

**Acceptance criteria**

- AC10: Unit test — poll loop completes a run, stores `OverallScore`, `TaskScoresJson`
  (non-null when sidecar reports task scores), `ProgressStage` transitions recorded.
- AC11: Unit test — sidecar `failed` status marks the run failed with the sidecar error text.
- AC12: Unit test — poll exceeding `StandardTimeoutSeconds` marks the run failed with
  `StandardBenchmark.Timeout`.
- AC13: Integration test — `GET .../progress` returns 200 for Auditor, 403 for plain user.
- AC14: Startup with a persisted `Running` run does not block queue processing (recovery
  enqueued, not awaited inline).

### WS4 — Suite registry bounds + options validation (H4, H10, H18)

- `SuiteInfo` gains `MaxShots` (BBH: 3; others: 5; suites without shot support: 0). Create
  handler validates `NShots <= suite.MaxShots` → `StandardBenchmark.NShotsOutOfRange` with
  suite-specific message (H4). Contract `StandardBenchmarkSuiteResponse` gains `maxShots`;
  wizard `InputNumber max` binds to it.
- `DeepEvalBenchmarkOptions` gains `[Required]`, `[Range(1, 86400)]` on timeouts,
  `PollIntervalSeconds` `[Range(5, 600)]`; DI: `.ValidateDataAnnotations().ValidateOnStart()`
  (H10).
- `appsettings.json`: add `Benchmarking:DeepEval` section with documented defaults (H18).

**Acceptance criteria**

- AC15: Unit test — BBH with `nShots: 4` rejected before queueing; MMLU with 5 accepted.
- AC16: Invalid config (empty Transport) fails host startup with a clear options error.

### WS5 — Compose + deployment (H17, C3 env)

- `docker-compose.prod.yml`: add `benchmark-sidecar` service (same build, env incl.
  `DEEPEVAL_TELEMETRY_OPT_OUT: "1"`, `benchmark_dataset_cache` + `benchmark_sidecar_socket`
  volumes, healthcheck) and wire backend `depends_on` + `Benchmarking__DeepEval__*` env +
  socket volume. Routes through Sec Ops review (compose is Sec Ops surface).
- `docker-compose.yml`: add `DEEPEVAL_TELEMETRY_OPT_OUT: "1"` to the sidecar env block.

**Acceptance criteria**

- AC17: `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` resolves with
  the sidecar present and healthy (pre-existing prod-overlay base-image caveat documented).

### WS6 — Frontend (H20)

**Status (2026-09-21):** The wizard scope of WS6 (stage-1 target forms, category
bounce-back, standard model selection) is superseded by
`plan.benchmarking-ux-consolidation.md`. Step 1 is revision-only for every category.
The polling and task-selection items remain as implemented.

- `benchmarkingApi.js`: add `getStandardBenchmarkRunProgress(runId, signal)`.
- `StandardBenchmarkHistory.jsx`: poll `listStandardBenchmarkRuns` every 10 s while any run is
  `Queued`/`Running` (interval cleanup on unmount; drawer shows `progressStage` and
  `taskScores` as a task/score table when present).
- `BenchmarkRunWizard.jsx`:
  - Task selection: when `selectedStandardSuite.supportsTasks`, render a multi-Select fed by a
    static task-name map per suite (mirrors `_TASK_ENUM_NAMES`; single source comment pointing
    at the sidecar enum). `launch` sends the selected list (or `null`).
  - `nShots` InputNumber `max` = `selectedStandardSuite.maxShots ?? 5`.
  - Remove the stage-1 bounce-back: category switch keeps the step; validation message shows
    inline on the target card instead (the target forms are both on stage 1 already).
- UX flow validation with the human in the loop (per R8 deliverable 4) after implementation,
  before review sign-off.

**Acceptance criteria**

- AC18: `npm run lint` 0 errors; `npm run build` exit 0.
- AC19: Manual (human in loop): launch MMLU with 2 problems → history shows Running with
  stage, transitions to Completed, drawer shows per-task scores.
- AC20: Wizard: BBH card enforces max shots 3; task multi-select appears only for
  task-supporting suites; no bounce-back on category switch.

### WS7 — Docs (H19)

- `README.md` API table: add `GET/POST /api/standard-benchmark-runs`, `GET .../{runId}`,
  `GET .../{runId}/progress`, `GET .../suites` rows.
- `docs/architecture.md` §3.5: add `StandardBenchmarkRun` to the class diagram + one invariant
  line (model-scoped, sidecar runner subprocess, start/poll contract).
- `docs/authorization-roles.md`: add the four endpoints with Admin/Auditor roles.
- `docs/transparency-and-privacy.md`: note `DEEPEVAL_TELEMETRY_OPT_OUT=1` blocks deepeval
  PostHog telemetry (C3 closure).

**Acceptance criteria**

- AC21: All four docs mention the standard-benchmark surface consistently (cross-check grep).

## CSS / DIS / CIL

- **CSS 7** — touches the benchmark execution path, worker lifecycle, a new subprocess
  execution surface in the sidecar, and an EF migration (additive).
- **DIS 7** — spans sidecar + Infrastructure + Application + Api + frontend + compose + docs.
- **CIL High** (unchanged from `plan.yaml`). Protocol: `.github/config/cil-high.yaml` —
  Lead Dev implements, Sec Ops review mandatory (sidecar subprocess execution, compose prod
  overlay, api-key adjacency), human approval before merge.

## Impacted Files

- Sidecar: `sidecar/app/runner/__init__.py` (new), `sidecar/app/runner/__main__.py` (new),
  `sidecar/app/tools/deepeval_llm_benchmarks.py`, `sidecar/app/main.py`, `sidecar/app/schemas.py`,
  `sidecar/requirements.txt`, `sidecar/Dockerfile`.
- Backend: `DeepEvalBenchmarkEngine.cs`, `DeepEvalBenchmarkContracts.cs`,
  `DeepEvalBenchmarkOptions.cs`, `IBenchmarkEngine` (Application/Common/Interfaces),
  `StandardBenchmarkFeatures.cs`, `StandardBenchmarkSuiteRegistry.cs`,
  `StandardBenchmarkRunWorker.cs`, `StandardBenchmarkRunRepository.cs`,
  `StandardBenchmarkRun.cs` (Domain), `StandardBenchmarkRunConfiguration.cs`, new EF migration,
  `DependencyInjection.cs`, `StandardBenchmarkRunsController.cs`, `TutorBenchmarkRuns.cs`
  (contracts), `TutoringMapping.cs`, `appsettings.json`.
- Compose: `docker-compose.yml`, `docker-compose.prod.yml`.
- Frontend: `benchmarkingApi.js`, `StandardBenchmarkHistory.jsx`, `BenchmarkRunWizard.jsx`,
  `benchmarkingUtils.js` (task-name map).
- Docs: `README.md`, `docs/architecture.md`, `docs/authorization-roles.md`,
  `docs/transparency-and-privacy.md`.
- Tests: `StandardBenchmarkFeaturesTests.cs`, engine tests, new progress integration test,
  sidecar pytest (busy 409, status lifecycle, unknown-task failure, generate_samples shape).

## Delegation Plan

- WS1: Lead Dev (sidecar Python) — Sec Ops consults on subprocess execution surface.
- WS2/WS3/WS4: Lead Dev (backend).
- WS5: Lead Dev edits, Sec Ops reviews (compose is Sec Ops surface per O-1 precedent).
- WS6: Lead Dev (frontend) + human UX validation session.
- WS7: Secretary (docs) or Lead Dev inline.
- Review: `bitcraft-review` (Lead Dev) + `bitcraft-security-review` (Sec Ops) before human
  review; NIST mapping stays open until sources are retrievable.

## Validation Plan

1. Conda smoke: runner module executes a 2-problem MMLU end to end; AC1–AC6.
2. `dotnet build bitcraft.slnx` → 0 warnings / 0 errors.
3. `dotnet test tests/Bitcraft.UnitTests` → all pass (AC8, AC10–AC12, AC15).
4. `dotnet test tests/Bitcraft.ApiIntegrationTests` → all pass (AC13).
5. Sidecar pytest suite (conda) → all pass (AC2, AC4, AC6).
6. `docker compose config` for base + prod overlay (AC17).
7. `npm run lint` + `npm run build` in `tutor-studio/` (AC18).
8. Human UX validation session (AC19, AC20) — blocking before review sign-off.
9. Report validation performed and anything intentionally not run.

## Risks

- **Runner subprocess escape surface:** the runner executes deepeval dataset code paths in a
  child process with the same container privileges. Mitigation: HumanEval stays disabled
  (`allow_code_execution` guard unchanged); Sec Ops reviews the spawn call.
- **Status-file trust:** the dispatcher trusts files under `/var/run/bitcraft/runs/`. Mitigation:
  UUID names, no shell interpolation, atomic rename writes.
- **Poll-loop DB churn:** one row update per poll (default 30 s) is negligible; throttled by
  design.
- **Contract break:** `POST /internal/tools/llm-benchmark` changes shape (202 + handle). The
  backend is the only caller (verified by grep); both sides land in the same branch.
- **deepeval upgrade:** `>=4.2,<5.0` pin + existing signature gating keep the runner stable;
  re-verify AC1 on any bump.

## Open Human Decisions

None. All decisions captured 2026-09-20 (below). NIST control mapping remains open as a
research task, not a decision.

## Human Decisions Captured (2026-09-20, VS Code question prompt)

- **D-R1 Runner model:** per-run subprocess (`python -m app.runner`), result via status file in
  the shared socket volume; crash-isolated; no extra compose service.
- **D-R2 Parallelism:** single parallel process — one runner slot; new run requests while busy
  are rejected with 409 (fail fast, clear error).
- **D-R3 Sidecar call pattern:** start + poll. POST starts and returns a handle; .NET polls
  status; removes the 6-hour HTTP timeout risk.
- **D-R4 n_shots bounds:** per-suite `MaxShots` in the registry; API rejects out-of-range
  values before launch (BBH max 3).
- **D-R5 Unknown tasks:** hard fail; list the unknown names; no silent full-dataset fallback.
- **D-R6 HumanEval `generate_samples`:** fix the return shape now (suite stays disabled).
- **D-R7 TaskScoresJson:** populate with real per-task scores (grounded: sidecar already reads
  `benchmark.task_scores`; column + UI drawer exist; observability doc lists it as a JSONB
  signal). Not hallucinated — verified before planning.
- **D-R8 Progress endpoint:** add minimal progress (stage-level; no fabricated percentages).
- **D-R9 Hygiene bundle:** approved in full — HttpClient reuse, per-request timeouts,
  AsNoTracking, options validation, source-gen typing, appsettings section, docs updates.
- **D-R10 Prod sidecar:** add `benchmark-sidecar` to `docker-compose.prod.yml` now (Sec Ops
  review required).
- **D-R11 ApiKey cap:** extend `HasMaxLength(200)` → 512 (small EF migration).
- **D-R12 Frontend scope:** all of it — polling, task selection UI, wizard flow polish. Entire
  Benchmarking concludes on this branch; only custom-benchmark finalization is deferred to a
  second iteration.
- **D-R13 Process:** Plan → Develop (backend + frontend) → Review → Human Review → Commit → PR.
- **D-R14 Decision capture mechanism:** the VS Code question prompt (this mechanism), as used.
