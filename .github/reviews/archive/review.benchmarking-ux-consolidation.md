---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: lead-dev
editor: lead-dev
ai_gen: true
branch: feature/deepeval-builtin-benchmarks
plan: plan.benchmarking-ux-consolidation.md
css: 5
dis: 5
cil: medium
verdict: accepted-with-findings
superseded_by: plan.benchmarking-observability-and-custom-suites.md
---
# Review: Benchmarking UX Consolidation (WS1-WS4)

Scope: `tutor-studio/src/BenchmarkingPage.jsx`, `BenchmarkRunHistory.jsx`,
`BenchmarkRunWizard.jsx`, `StandardBenchmarkHistory.jsx`, `benchmarkingApi.js`,
`benchmarkingUtils.js`.

Original request: restore one coherent benchmarking workflow — select revision
(carries the model) → select benchmark — with standard runs as a benchmark type
inside the shared history.

**Disposition: findings captured, not fixed in place.** The human directed that
these feed a larger development plan instead of in-flight remediation.

## Stage A — Drift and Staleness

The diff is 152 insertions / 275 deletions across 6 files. Net −123 lines. No new
files. No backend files touched. No contract change. No migration.

**No scope drift found.** Every change traces to WS1-WS4. The one behavior change
beyond a literal reading of the request is the removal of the `standard` tab
(WS3 steps 18-19), which D-2 explicitly authorized ("shared list, no separate
tab").

**Staleness checks:**

- `standardModelId`, `interactionModels` (wizard), `selectedStandardModel`,
  `listInteractionModelsForAdmin` — 0 matches in `tutor-studio/src/`. Clean.
- `isAdmin` in `BenchmarkRunWizard.jsx` — used only by the standard category card
  gate (lines 312-316). Retained intentionally.
- `Descriptions` and `Select` imports in the wizard — still referenced. Clean.
- `StandardBenchmarkHistory.jsx` — reduced to the drawer; both the named and
  default export are consumed by `BenchmarkRunHistory.jsx:7`. Clean.

## Stage B — Findings

Ordered by severity. Line numbers are post-change.

### F1 (Medium) — Refresh button is disabled when no tutor is selected

`BenchmarkRunHistory.jsx:191`

```jsx
<Button icon={<ReloadOutlined />} onClick={refresh} disabled={!selectedTutorId} loading={loading}>Refresh history</Button>
```

The table now always loads standard runs, and the empty-state text at line 203
tells the user "Standard runs appear without a tutor filter." The Refresh control
stays disabled until a tutor is chosen, so a user viewing only standard runs
cannot refresh them. The gate predates WS3 and was correct when the table was
tutor-scoped.

Fix: drop `disabled={!selectedTutorId}`.

### F2 (Medium) — Standard runs are fetched unconditionally, so a role without the Auditor role gets a 403 on page load

`BenchmarkRunHistory.jsx:25`

`listStandardBenchmarkRuns` is called on every mount with no role gate. The
endpoint is `[Authorize(Policy = AuditorOrAbove)]`
(`StandardBenchmarkRunsController.cs:17`), and `AuditorOrAbove` requires
`Auditor`, `Editor`, or `Admin` (`DependencyInjection.cs:110`). A newly
registered user has no roles (`IdentityService.cs:43` reads roles after create;
registration assigns none), so the request 403s and the error banner renders.

The route guard is `allowedRoles={["Auditor", "Editor", "Admin"]}`
(`App.jsx:40`), so a roleless user should not reach the page at all. The risk is
latent, not live: it fires only if the route guard and the API policy ever
diverge. This is the same class of bug as the admin-only model fetch that WS1
removed — an unconditional fetch against a role-gated endpoint.

Fix: gate the standard fetch on the user's roles, or confirm and document that
the route guard is the single source of truth.

### F3 (Low) — Stale comment describes a category-dependent stage-1 form that no longer exists

`BenchmarkRunWizard.jsx:126-129`

```js
// The stage-1 target form depends on the category. If the new category needs
// a target the user has not chosen, return to stage 1 so the matching target
// form is completed before benchmark configuration. Stage-2 selections are
// preserved and the user returns here after fixing the target.
```

After WS1 the stage-1 form is identical for every category. The comment is now
wrong and should be reduced to one line, or deleted.

### F4 (Low) — `Boolean(...)` wrapper is redundant

`BenchmarkRunHistory.jsx:61`

```js
const standardOpen = Boolean(selectedRow?.kind === "standard");
```

`===` already yields a boolean. `Boolean()` adds nothing.

### F5 (Low) — `toHistoryRow` returns `undefined` for absent fields instead of a stable empty value

`benchmarkingUtils.js` — `toHistoryRow`

`model`, `target`, `score`, and `count` read optional response fields with `?.`,
so they can be `undefined`. Every consumer already handles this with `|| "—"` or
`?? "—"`, so there is no defect. Noted for consistency only; no change required
unless the larger plan touches this helper.

### F6 (Info) — No frontend test infrastructure exists

`tutor-studio/` has no test runner (`package.json` scripts are dev/build/lint/
preview only; no `*.test.js` or `*.spec.js` files). `progressIndicator` and
`toHistoryRow` are pure functions and are the natural first candidates if the
larger plan adds frontend unit tests. Backend validation covered the change
indirectly: 410 unit tests and 39 API integration tests pass.

## Checklist results

| Item | Result |
| --- | --- |
| Clean Architecture dependency direction | N/A — frontend only, no backend files touched |
| Domain entities exposed through contracts | N/A — no contract change |
| Strong typing | N/A — JS; no `any`, no object bags introduced |
| Naming conventions | Pass — `progressIndicator`, `toHistoryRow`, `StandardBenchmarkRunDrawer` |
| `Result<T>` for expected failures | N/A — frontend |
| Authorization and ownership checks intact | See F2 — latent divergence risk, no live bypass |
| Comments minimal and in STE | See F3 — one stale comment |
| Optimality first | Pass — net −123 lines; drawer reused, no new component |
| Test coverage for edge cases | See F6 — no frontend test infrastructure |
| `.github` frontmatter validity | Pass — plan frontmatter updated, `status: done` |

## Validation performed

- `npm run lint` — only the pre-existing `main.jsx` error. PASS.
- `npm run build` — exit 0. PASS.
- `dotnet build bitcraft.slnx` — 0 errors, 0 warnings. PASS.
- `dotnet test tests/Bitcraft.UnitTests` — 410 passed, 0 failed. PASS.
- `dotnet test tests/Bitcraft.ApiIntegrationTests` — 39 passed, 0 failed. PASS.

**Validation gap:** the manual UI walk (plan validation step 6) was not run. It
needs a human in the loop: select a revision, choose a standard suite, confirm no
model picker appears, launch, and confirm the history row shows
`In Progress · {indicator}` and settles to Completed.

## Addendum — Human-reported defects (2026-09-21)

Captured after the first live standard benchmark run. These are production
defects, not style findings. All are carried into
`plan.benchmarking-observability-and-custom-suites.md`.

### F7 (Critical) — Every standard run reports 0%

`deepeval_llm_benchmarks.py:409` reads
`float(getattr(benchmark, "overall_score", 0.0) or 0.0)`. DeepEval 4.2.3's base
result model is `DeepEvalBaseBenchmarkResult` with field **`overall_accuracy`**
(`.conda/Lib/site-packages/deepeval/benchmarks/base_benchmark.py:9-11`). If the
concrete benchmark exposes `overall_accuracy`, `getattr` silently falls back to
`0.0`. Confidence: High on the mechanism, Medium on the concrete attribute —
must be verified at runtime before fixing.

### F8 (High) — A stalled run blocks the whole queue

`StandardBenchmarkRunWorker.ExecuteAsync` awaits each run to completion before
reading the next (`StandardBenchmarkRunWorker.cs:22-26`), and the queue is a
bounded channel with `SingleReader = true`. One run stuck in `Running` starves
every later run, which matches the report that the new run stayed `Queued`.
There is no timeout-based reclamation: `ListRecoverableRunIdsAsync` requeues
`Queued` and `Running` rows at startup
(`StandardBenchmarkRunRepository.cs:43-48`), but the handler then hits the
sidecar's single slot.

### F9 (High) — Runner slot state is process-local

`_slot_held()` derives the slot from `_last_status_path`, a module global
(`deepeval_llm_benchmarks.py:322-336`). A sidecar restart loses the slot while
the DB row stays `Running`. Note `finalizing` is the runner's *terminal* stage
(`runner/__main__.py:103`), so seeing it means the runner finished writing but
the backend did not settle — pointing at a backend restart or a lost poll.

### F10 (Medium) — `task_scores` shape mismatch

Official docs state `task_scores` is a **pandas DataFrame**
(https://deepeval.com/docs/benchmarks-introduction). `_normalize_task_scores`
expects a dict-like `.items()` (`deepeval_llm_benchmarks.py:449-470`). If the
installed version returns a DataFrame, per-task scores silently normalize to
`None`.

### F11 (Medium) — No progress data exists to display

`StandardBenchmarkRun` has no `ProgressPercent`, `CompletedTasks`, or
`TotalTasks` (`StandardBenchmarkRun.cs:35`). The sidecar emits coarse stage names
only (D-R8). The "no progress" report is a data gap, not a rendering bug.

### F12 (Medium) — Resolved task list is never shown before launch

The wizard sends `tasks: selectedTasks.length ? selectedTasks : null`. The user
cannot see which tasks will run when the selection is empty (full default set).

## Follow-up inbox items

- lead-dev (Medium): F1 — remove `disabled={!selectedTutorId}` from the Refresh
  button in `BenchmarkRunHistory.jsx:191`.
- sec-ops (Medium): F2 — confirm whether the route guard in `App.jsx:40` is the
  intended single source of truth for standard-run visibility, or gate the fetch
  on roles.
- lead-dev (Low): F3 — delete or shorten the stale stage-1 comment in
  `BenchmarkRunWizard.jsx:126-129`.
- lead-dev (Low): F4 — drop the redundant `Boolean()` at
  `BenchmarkRunHistory.jsx:61`.
- Human (Medium): manual UI walk of the consolidated flow (validation step 6).
- Human (Low): confirm the WS3 derived rule — standard rows appear regardless of
  the tutor filter, because standard runs carry no tutor id.
