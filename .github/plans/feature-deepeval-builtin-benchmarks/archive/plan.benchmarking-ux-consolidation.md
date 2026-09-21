---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: planner
editor: planner
ai_gen: true
status: done
css: 5
dis: 5
cil: medium
---
# Sub-Plan: Benchmarking UX Consolidation — Remove the Parallel Standard Wizard Path

Origin: Planner, 2026-09-21, from a human-reported UX regression found during the
`bitcraft-review` pass over WS1-WS7. Supersedes the standard-category branch of
`plan.wizard-flow-revert.md` and the WS6 wizard scope in
`plan.standard-benchmark-hardening.md`.

## Goal

Restore one coherent Benchmarking workflow. The wizard keeps the original shape:
**select revision (which carries its model) → select benchmark (all options inside)**.
Standard built-in benchmarks become a benchmark *type* inside that flow, not a
parallel flow with its own model picker. Remove the hallucinated model-selection
stage, delete the code it dragged in, and show launcher progress in the existing
benchmark run history with an "In Progress {indicator}" label.

## Verified Current State (evidence, 2026-09-21)

- **The parallel path is real.** `BenchmarkRunWizard.jsx` holds a second, standard-only
  target stage: `standardModelId` state (line 34), `interactionModels` state (38-40),
  an admin-only `listInteractionModelsForAdmin` fetch (93-102), a step-1 validation
  branch `if (!standardModelId) return "Select an interaction model to evaluate."`
  (182-183), and a dedicated model `Select` (282-295). This replaces revision
  selection for the standard category. It is not in the original design.
- **The original flow still exists** for `rag` and `arduino`: step 1 selects
  tutor + revision, step 2 selects the benchmark. `isUsableRevision` already
  guarantees a revision carries `interactionModelId`, `promptVersionId`, and
  `latestSnapshotId` (`benchmarkingUtils.js:150-156`).
- **The revision already carries the model.** `TutorRevisionResponse` exposes
  `InteractionModelId` (`TutorRevisions.cs:14`). The wizard already reads
  `selectedRevision?.interactionModelId` when launching RAG runs
  (`BenchmarkRunWizard.jsx:246`). No new model picker is needed.
- **Backend is model-scoped by design.** `CreateStandardBenchmarkRunCommand` takes
  `InteractionModelId` (`StandardBenchmarkFeatures.cs:37`) and
  `StandardBenchmarkRun.InteractionModelId` is the only target FK
  (`StandardBenchmarkRun.cs:14`). The backend needs no change to accept a
  revision-derived model id.
- **Progress indicator gap.** `runStatusLabel` (`benchmarkingUtils.js:158-162`) maps
  only `Generating`/`Evaluating` stages and returns bare status otherwise. The
  history `Status` column renders `runStatusLabel(run)`
  (`BenchmarkRunHistory.jsx:60`). There is no "In Progress {indicator}" form and no
  completed/total or percentage display for standard runs.
- **Standard runs are invisible to the shared history.** `StandardBenchmarkHistory.jsx`
  is a separate component with its own table and drawer. The human wants launcher
  progress reflected in the *existing* benchmark run history.
- **Bloat to remove after revert:** `standardModelId`, `interactionModels`,
  `interactionModelsLoading`, `interactionModelsError`, `selectedStandardModel`,
  the `listInteractionModelsForAdmin` import and fetch, the admin-only model
  `Select` block, and the step-1 standard validation branch. Also reassess
  `listInteractionModelsForAdmin` in `benchmarkingApi.js` — if no caller remains,
  delete it.

## Non-Goals

- No change to the backend standard-benchmark pipeline (runner subprocess,
  start/poll, `TaskScoresJson`, progress endpoint). Those are validated and stay.
- No change to `StandardBenchmarkRun` being model-scoped. The revision supplies the
  model id; the domain model does not gain a revision FK.
- No new EF migration.
- No change to tutor-scoped RAG or Arduino flows beyond sharing the progress label.
- No Git writes. Commit/push/PR stay human-owned.

## Assumptions

- A standard run launched from a revision uses that revision's
  `interactionModelId`. The revision's prompt/system configuration is *not* sent to
  the standard benchmark, because deepeval built-in suites supply their own prompts.
  The revision is the selection vehicle for the model, not a prompt source.
- `isUsableRevision` remains the gate for both categories, so a standard run can
  only launch from a revision with an enabled model and engine.
- The existing history component is the single place users watch runs.

## Options

### Option A (recommended) — Standard as a benchmark type inside the existing flow

Step 1 always selects tutor + revision. Step 2 offers RAG / Standard / Arduino as
benchmark types; Standard shows suite cards plus shots, problem cap, and tasks.
Launch sends `selectedRevision.interactionModelId`. Delete the model-picker stage
and all its state.

- Pros: one flow, matches the original design, removes ~7 state variables and an
  admin-only fetch, no backend change, no migration.
- Cons: standard runs can only launch from a usable revision (acceptable — the
  revision is the model carrier).

### Option B — Keep both paths, hide the model picker

- Pros: smaller diff.
- Cons: leaves the parallel design and dead state in place. Rejected: the human
  explicitly asked to remove the parallel design and clean up bloat.

**Recommendation: Option A.**

## Work Streams

### WS1 — Remove the parallel standard target stage (frontend)

Phase 2 skill: `bitcraft-frontend-feature`.

Each step names the file, the symbol, the change, and the check. Run the check
before the next step.

1. In `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx`, delete the four
   state declarations `standardModelId`, `interactionModels`,
   `interactionModelsLoading`, and `interactionModelsError`.
   Check: `npm run lint` in `tutor-studio/` reports no new error.
2. Delete the derived constant `selectedStandardModel`.
   Check: `npm run lint` reports no new error.
3. Delete the admin-only `useEffect` block that calls
   `listInteractionModelsForAdmin`.
   Check: `npm run lint` reports no new error.
4. Remove `listInteractionModelsForAdmin` from the `benchmarkingApi.js` import
   list on line 7.
   Check: `npm run lint` reports no new error.
5. In `selectBenchmarkCategory`, replace the `targetMissing` ternary with
   `const targetMissing = !tutorId || !revisionId;`.
   Check: the function contains no reference to `standardModelId`.
6. In `selectBenchmarkCategory`, set the validation message to
   "Tutor benchmarks evaluate a tutor revision. Select a tutor and revision to continue."
   for every category.
   Check: the function contains no reference to `standardModelId`.
7. In `validateStep`, change the first branch condition from
   `stepNumber === 1 && benchmarkType !== "standard"` to `stepNumber === 1`.
   Check: `npm run lint` reports no new error.
8. In `validateStep`, delete the `stepNumber === 1 && benchmarkType === "standard"`
   branch and its `standardModelId` message.
   Check: grep `standardModelId` in `tutor-studio/` returns 0 matches.
9. In `launch`, change the standard payload field to
   `interactionModelId: selectedRevision?.interactionModelId ?? null`.
   Check: the standard payload keeps `benchmarkKey`, `tasks`, `nShots`, and `nProblems`.
10. In the step-1 render block, delete the `benchmarkType === "standard"` ternary
    branch so the tutor and revision card renders for every category.
    Check: `npm run build` in `tutor-studio/` exits 0.
11. In the step-3 review block, set the standard `Model` item value to
    `selectedRevision?.providerModelName || selectedRevision?.interactionModelName || "Configured model"`.
    Check: `npm run build` exits 0.
12. In `tutor-studio/src/benchmarking/benchmarkingApi.js`, delete the
    `listInteractionModelsForAdmin` export.
    Check: grep `listInteractionModelsForAdmin` in `tutor-studio/` returns 0 matches.

### WS2 — Progress indicator in the existing history

Phase 2 skill: `bitcraft-frontend-feature`.

**Verified data constraint (2026-09-21).** The indicator order in D-3 is
`completed/total` → percentage → stage. Only tutor runs can reach the first two
levels. `TutorBenchmarkRunSummaryResponse` carries `CompletedCaseCount`,
`TotalCaseCount`, and `ProgressPercent`. `StandardBenchmarkRunResponse` carries
only `ProgressStage` and `TaskCount`; `StandardBenchmarkRun` has no
`ProgressPercent` and no completed/total fields. The sidecar writes coarse stage
names only (`loading-dataset`, `evaluating`, `finalizing`) because deepeval
exposes no per-problem callback (decision D-R8). So a standard run always falls
through to the stage level. Do not fabricate counts or percentages for standard
runs.

1. In `tutor-studio/src/benchmarking/benchmarkingUtils.js`, add the pure helper
   `progressIndicator(run)`.
   Check: the file exports `progressIndicator`.
2. In `progressIndicator`, return `null` when `run?.status` is not `Running` and
   not `Queued`.
   Check: the function returns `null` for `Completed` and `Failed`.
3. In `progressIndicator`, return `` `${completed}/${total}` `` when
   `run.completedCaseCount` and `run.totalCaseCount` are both finite numbers and
   `run.totalCaseCount` is greater than 0.
   Check: `progressIndicator({ status: "Running", completedCaseCount: 3, totalCaseCount: 10 })` returns `"3/10"`.
4. In `progressIndicator`, return `formatProgressPercent(run.progressPercent / 100)`
   when `run.progressPercent` is a finite number greater than 0.
   Check: `progressIndicator({ status: "Running", progressPercent: 42 })` returns `"42%"`.
5. In `progressIndicator`, return `run.progressStage` when it is a non-empty string.
   Check: `progressIndicator({ status: "Running", progressStage: "evaluating" })` returns `"evaluating"`.
6. In `progressIndicator`, return `null` when no level applies.
   Check: `progressIndicator({ status: "Running" })` returns `null`.
7. In `benchmarkingUtils.js`, extend `runStatusLabel(run)` so an active run with a
   non-null indicator returns `` `In Progress · ${indicator}` ``.
   Check: `runStatusLabel({ status: "Running", progressStage: "evaluating" })` returns `"In Progress · evaluating"`.
8. In `runStatusLabel`, keep the existing `Generating` and `Evaluating` mappings
   ahead of the new branch so tutor-run stage names stay unchanged.
   Check: `runStatusLabel({ status: "Running", progressStage: "Generating" })` returns `"Generating"`.
9. In `runStatusLabel`, return the bare status for terminal runs.
   Check: `runStatusLabel({ status: "Completed" })` returns `"Completed"`.
10. In `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx`, keep the Status
    column call as `runStatusLabel(run)` and pass the result to the existing
    `StatusTag`.
    Check: `npm run build` in `tutor-studio/` exits 0.
11. Do not add a new component. `StatusTag` already maps the substring
    "in progress" to the `processing` color.
    Check: grep for a new status component file in `tutor-studio/src/` returns 0 matches.

### WS3 — Surface standard runs in the shared history

Phase 2 skill: `bitcraft-frontend-feature`.

**Decision D-2 is resolved: shared list, no separate tab.** One history view
serves both run kinds. Reuse the existing table, the existing `StatusTag`, and
the existing standard drawer. Do not create a new history component.

**Derived rule (planner, under D-2).** Standard runs are model-scoped and carry
no tutor id. The tutor filter therefore gates tutor rows only; standard rows
always appear. Do not add a revision fetch to infer a tutor for a standard run.
Flag this rule to the human if it is wrong.

1. In `tutor-studio/src/benchmarking/benchmarkingUtils.js`, add the pure helper
   `toHistoryRow(run, kind)`.
   Check: the file exports `toHistoryRow`.
2. In `toHistoryRow`, return an object with the keys `id`, `kind`, `status`,
   `statusLabel`, `model`, `target`, `score`, `count`, `queuedUtc`,
   `completedUtc`, `failureDescription`, and `raw`.
   Check: the returned object has 12 keys.
3. In `toHistoryRow`, set `statusLabel` to `runStatusLabel(run)`.
   Check: `toHistoryRow({ status: "Running", progressStage: "evaluating" }, "standard").statusLabel` equals `"In Progress · evaluating"`.
4. In `toHistoryRow`, set `model` to `run.modelAlias` for kind `tutor` and to
   `run.interactionModelName` for kind `standard`.
   Check: both branches return a string or `undefined`.
5. In `toHistoryRow`, set `target` to `run.benchmarkTemplateName` for kind `tutor`
   and to `run.benchmarkKey` for kind `standard`.
   Check: both branches return a string or `undefined`.
6. In `toHistoryRow`, set `score` to `run.averageScore` for kind `tutor` and to
   `run.overallScore` for kind `standard`.
   Check: both branches return a number or `null`.
7. In `toHistoryRow`, set `count` to `run.totalCaseCount` for kind `tutor` and to
   `run.taskCount` for kind `standard`.
   Check: both branches return a number or `undefined`.
8. In `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx`, import
   `listStandardBenchmarkRuns` and `toHistoryRow`.
   Check: `npm run lint` in `tutor-studio/` reports no new error.
9. Add the state field `standardRuns` and load it with
   `listStandardBenchmarkRuns(null, signal)` in the existing effect.
   Check: the effect sets both `runs` and `standardRuns`.
10. Build `rows` as the merged array of `runs.map((run) => toHistoryRow(run, "tutor"))`
    and `standardRuns.map((run) => toHistoryRow(run, "standard"))`.
    Check: `rows` contains one entry per source run.
11. Sort `rows` by `completedUtc || queuedUtc` descending.
    Check: the newest run is first.
12. Point the `Table` `dataSource` at `rows` and keep `rowKey="id"`.
    Check: `npm run build` in `tutor-studio/` exits 0.
13. Add a `Type` column that renders `Standard` for kind `standard` and `Tutor`
    for kind `tutor`.
    Check: the table shows the Type column.
14. Add a poll effect that refreshes both lists every 10 seconds while any row
    has status `Queued` or `Running`.
    Check: the poll stops when no row is active.
15. In `tutor-studio/src/benchmarking/StandardBenchmarkHistory.jsx`, export the
    existing `Drawer` block as the named component `StandardBenchmarkRunDrawer`
    accepting `detail`, `open`, and `onClose`.
    Check: the file exports `StandardBenchmarkRunDrawer`.
16. Delete the list state, the two poll effects, the `columns` array, and the
    `Table` from `StandardBenchmarkHistory.jsx`.
    Check: `npm run lint` reports no new error.
17. In `BenchmarkRunHistory.jsx`, render `StandardBenchmarkRunDrawer` when the
    selected row has kind `standard`, and keep `BenchmarkRunDetails` for kind
    `tutor`.
    Check: `npm run build` exits 0.
18. In `tutor-studio/src/BenchmarkingPage.jsx`, delete the `standard` tab item and
    the `StandardBenchmarkHistory` import.
    Check: grep `StandardBenchmarkHistory` in `tutor-studio/src/` matches only `StandardBenchmarkHistory.jsx`.
19. In `BenchmarkingPage.jsx`, remove `"standard"` from the `validTab` list and
    from `updateHistoryLocation` so a launched standard run opens the `history`
    tab.
    Check: `npm run build` exits 0.
20. In `BenchmarkRunWizard.jsx`, change the standard launch callback to
    `onCreated(tutorId, created.id)`.
    Check: grep `"standard"` as a third `onCreated` argument returns 0 matches.

### WS4 — Cleanup and coherence pass

Phase 2 skill: `bitcraft-frontend-feature`.

1. Grep `tutor-studio/src/` for `standardModelId`, `interactionModels`,
   `selectedStandardModel`, and `listInteractionModelsForAdmin`.
   Check: each grep returns 0 matches.
2. Grep `tutor-studio/src/` for `isAdmin` in `BenchmarkRunWizard.jsx`.
   Check: `isAdmin` is used only by the standard category card gate.
3. Remove unused imports from every file touched in WS1-WS3.
   Check: `npm run lint` in `tutor-studio/` reports only the pre-existing `main.jsx` error.
4. Grep `tutor-studio/src/` for `Descriptions` and `Select` in
   `BenchmarkRunWizard.jsx`.
   Check: each import is still referenced in the file.
5. Confirm no new component, hook, or API helper was added in WS1-WS3.
   Check: `git status --short tutor-studio/` lists only modified files, no new files.
6. In `plan.wizard-flow-revert.md`, add a Status line stating the standard
   model-picker scope is superseded by
   `plan.benchmarking-ux-consolidation.md`.
   Check: the file contains the word "superseded".
7. In `plan.standard-benchmark-hardening.md`, mark the WS6 wizard scope
   superseded by `plan.benchmarking-ux-consolidation.md`.
   Check: the file contains the word "superseded".

## CSS / DIS / CIL

- **CSS 5** — frontend workflow and status rendering; no auth, persistence, or
  deployment change. Raised to 6 if WS3 touches the backend list contract.
- **DIS 5** — spans `tutor-studio/src/benchmarking/` plus one shared util; backend
  untouched under Option A.
- **CIL Medium** — CSS <= 6 and DIS <= 6. Protocol: `.github/config/cil-medium.yaml`.
  Lead Dev implements and self-reviews; Sec Ops not required unless WS3 changes a
  contract.

## Impacted Files

- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` (WS1)
- `tutor-studio/src/benchmarking/benchmarkingApi.js` (WS1)
- `tutor-studio/src/benchmarking/benchmarkingUtils.js` (WS2, WS3)
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx` (WS2, WS3)
- `tutor-studio/src/benchmarking/StandardBenchmarkHistory.jsx` (WS3, reduced to a drawer)
- `tutor-studio/src/BenchmarkingPage.jsx` (WS3)
- Plan files: `plan.wizard-flow-revert.md`, `plan.standard-benchmark-hardening.md` (WS4)

No backend file is touched. No contract changes. No migration.

## Delegation Plan

- WS1-WS4: Lead Dev (frontend), using `bitcraft-frontend-feature`.
- AI Ops: review the process failure that let a hallucinated parallel flow land
  without a requirement check, and propose a guard (see Open Items).
- Secretary: update `docs/change-notes.md` after implementation.
- Review: `bitcraft-review` (Lead Dev) after WS4.

## Validation Plan

1. `npm run lint` in `tutor-studio/` — expect only the pre-existing `main.jsx` error.
2. `npm run build` in `tutor-studio/` — exit 0.
3. `dotnet build bitcraft.slnx` — 0 errors (confirms no backend drift).
4. `dotnet test tests/Bitcraft.UnitTests` — all pass.
5. `dotnet test tests/Bitcraft.ApiIntegrationTests` — all pass.
6. Manual UI walk (human in loop): select a revision, choose a standard suite,
   confirm no model picker appears, launch, and confirm the history row shows
   "In Progress {indicator}" and settles to Completed.
7. Report validation performed and anything intentionally not run.

## Risks

- **Removing the admin-only model fetch** may strand `listInteractionModelsForAdmin`.
  Mitigation: grep for callers before deleting; delete only when orphaned.
  Verified 2026-09-21: the only caller is `BenchmarkRunWizard.jsx`, so the helper
  is safe to delete in WS1 step 12.
- **Standard runs need a usable revision.** A model with no usable revision can no
  longer be benchmarked directly. Accepted under D-1: the revision is the model
  carrier per the original design.
- **WS3 merge shape** could tempt a new response class. Mitigation: build the shared
  row in `benchmarkingUtils.js` from existing responses; add a class only if reuse
  is impossible.
- **Standard runs have no tutor id.** The tutor filter cannot scope them.
  Mitigation: standard rows always appear in the shared list. Flagged to the human
  in WS3.
- **Standard runs report no counts or percentages.** The sidecar emits coarse stage
  names only (decision D-R8). Mitigation: the indicator falls through to the stage
  level. Do not fabricate progress numbers.
- **Authorization differs between the two list endpoints.** Tutor runs use
  `AuditorOrAbove`; standard runs use `AuditorOrAbove` for reads and `AdminOnly`
  for create. Mitigation: the merged history shows standard rows to any
  `AuditorOrAbove` user, which matches the existing standard tab behavior. No
  policy change.

## Human Decisions (resolved 2026-09-21)

- **D-1 — RESOLVED: Yes.** Standard runs launch only from a usable revision.
  Option A is adopted. A model with no usable revision cannot be benchmarked
  directly. The revision is the model carrier.
- **D-2 — RESOLVED: Shared list.** Standard runs appear in the shared
  `BenchmarkRunHistory` list with a type indicator. No separate tab. Reuse
  existing views to keep the UI minimal and reduce UX overhead.
- **D-3 — RESOLVED: Yes.** Indicator order is `completed/total` → percentage →
  stage.

## Acceptance Criteria

1. Step 1 of the wizard shows the tutor and revision card for every category.
2. No interaction-model picker renders anywhere in the wizard.
3. A standard launch sends the `interactionModelId` of the selected revision.
4. `isUsableRevision` gates every category, including standard.
5. The shared history lists tutor runs and standard runs in one table.
6. Each history row shows a Type value of `Tutor` or `Standard`.
7. An active run shows `In Progress · {indicator}` in the Status column.
8. A standard run shows its stage as the indicator, because the sidecar reports
   no counts or percentages.
9. The history refreshes while any run is queued or running, and stops after.
10. Opening a standard row opens the existing standard drawer.
11. The Benchmarking page has no `standard` tab.
12. `npm run lint` reports only the pre-existing `main.jsx` error.
13. `npm run build` exits 0.
14. `dotnet build bitcraft.slnx` reports 0 errors.

## Open Items for AI Ops

- **A1 — RESOLVED 2026-09-21.** The requirement-fidelity guard is in place.
  `bitcraft-frontend-feature` step 1 requires a requirement check before
  implementation. `bitcraft-backend-feature` carries the same step.
- **A2 — RESOLVED 2026-09-21.** The reuse-first rule is in place.
  `bitcraft-frontend-feature` step 3 requires reuse before creating.
  `bitcraft-develop` adds a no-new-scope constraint.

## Phase 2 Prompt

Run the `bitcraft-develop` skill for
`.github/plans/feature-deepeval-builtin-benchmarks/plan.benchmarking-ux-consolidation.md`.
Execute work steps WS1 through WS4 in order. Validate each step before the next.

## Status (implemented 2026-09-21, Lead Dev)

**WS1 — DONE.** All 12 steps executed. `standardModelId`, `interactionModels`,
`interactionModelsLoading`, `interactionModelsError`, `selectedStandardModel`, the
admin-only fetch effect, the model `Select` card, and the standard validation branch
are removed from `BenchmarkRunWizard.jsx`. Step 1 is revision-only for every
category. Standard launch sends `selectedRevision?.interactionModelId ?? null`.
`listInteractionModelsForAdmin` deleted from `benchmarkingApi.js`.
Validation: `npm run lint` — only the pre-existing `main.jsx` error.

**WS2 — DONE.** `progressIndicator(run)` added to `benchmarkingUtils.js` with the
D-3 order (completed/total → percentage → stage). `runStatusLabel` returns
`In Progress · {indicator}` for active runs; `Generating`/`Evaluating` mappings
stay ahead of the new branch. `BenchmarkRunHistory.jsx` needed no change — it
already renders `runStatusLabel(run)` through `StatusTag`.
Validation: `npm run lint` clean (pre-existing only); `npm run build` exit 0.

**WS3 — DONE.** `toHistoryRow(run, kind)` added to `benchmarkingUtils.js`.
`BenchmarkRunHistory.jsx` loads both lists, merges rows, sorts by completion
descending, adds a Type column, polls every 10 s while any row is active, and
routes standard rows to the drawer. `StandardBenchmarkHistory.jsx` reduced to the
exported `StandardBenchmarkRunDrawer`. `BenchmarkingPage.jsx` loses the `standard`
tab; standard launches open the `history` tab.
Validation: `npm run lint` clean (pre-existing only); `npm run build` exit 0.

**WS4 — DONE.** Grep checks: `standardModelId`, `interactionModels` (wizard),
`selectedStandardModel`, `listInteractionModelsForAdmin` — 0 matches in
`tutor-studio/src/`. `isAdmin` used only by the standard category card gate.
`git status --short tutor-studio/` lists 6 modified files, no new files.
Supersede notes added to `plan.wizard-flow-revert.md` and WS6 of
`plan.standard-benchmark-hardening.md`.

**Full validation (2026-09-21):**
1. `npm run lint` — only the pre-existing `main.jsx` error. PASS.
2. `npm run build` — exit 0. PASS.
3. `dotnet build bitcraft.slnx` — 0 errors, 0 warnings. PASS.
4. `dotnet test tests/Bitcraft.UnitTests` — 410 passed, 0 failed. PASS.
5. `dotnet test tests/Bitcraft.ApiIntegrationTests` — 39 passed, 0 failed. PASS.
6. Manual UI walk — NOT RUN (requires human in loop).

**Drift notes (no scope added):**
- WS2 step 4 text said `formatProgressPercent(run.progressPercent / 100)` but its
  own check expects `"42%"` for `progressPercent: 42`. The backend stores
  `ProgressPercent` as 0-100, so the implementation follows the check:
  `formatProgressPercent(run.progressPercent)`.
- WS3 step 9: a direct `setLoading(true)` in the effect body tripped the
  `react-hooks/set-state-in-effect` lint rule. Removed; loading is already set by
  `selectTutor`, `refresh`, and the initial state.
- The tutor-filter derived rule (standard rows always appear) was implemented as
  planned. Human confirmation still open in the Planner inbox (Low).

**Phase 3 prompt:** Run the `bitcraft-review` skill for branch
`feature/deepeval-builtin-benchmarks`. Scope: `tutor-studio/src/BenchmarkingPage.jsx`,
`tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx`,
`tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx`,
`tutor-studio/src/benchmarking/StandardBenchmarkHistory.jsx`,
`tutor-studio/src/benchmarking/benchmarkingApi.js`,
`tutor-studio/src/benchmarking/benchmarkingUtils.js`. Original request: restore one
coherent benchmarking workflow — select revision (carries the model) → select
benchmark — with standard runs as a benchmark type inside the shared history.
