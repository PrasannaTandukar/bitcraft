---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: planner
editor: planner
ai_gen: true
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

- `BenchmarkRunWizard.jsx`: delete `standardModelId`, `interactionModels`,
  `interactionModelsLoading`, `interactionModelsError`, `selectedStandardModel`,
  the `listInteractionModelsForAdmin` import and its `useEffect`, the admin-only
  model `Select` card, and the step-1 `benchmarkType === "standard"` validation
  branch. Step 1 becomes revision-only for every category.
- `selectBenchmarkCategory`: drop the `standardModelId` branch from the
  `targetMissing` check so no category switch bounces to a model picker.
- `launch`: for `standard`, send
  `interactionModelId: selectedRevision?.interactionModelId ?? null` and keep
  `benchmarkKey`, `tasks`, `nShots`, `nProblems`.
- Review step: show the revision and its model instead of the standalone model row.
- `benchmarkingApi.js`: remove `listInteractionModelsForAdmin` if no caller remains.

### WS2 — Progress indicator in the existing history

- `benchmarkingUtils.js`: extend `runStatusLabel` (or add a sibling used by the
  history) to render `In Progress {indicator}` for active runs. Indicator is
  `completed/total` when totals are known, otherwise the percentage, otherwise the
  stage name. Keep the function pure and reusable by both run shapes.
- `BenchmarkRunHistory.jsx`: use the new label in the Status column so launcher
  progress is visible without opening a drawer.
- Reuse the existing `StatusTag` and formatting helpers. Do not add a new
  component when the existing one covers it.

### WS3 — Surface standard runs in the shared history

- Decide and implement how standard runs appear alongside tutor runs in
  `BenchmarkRunHistory.jsx`: either the backend list endpoint unions both shapes
  behind one response record, or the history component merges the standard list
  into its rows. Prefer the option that adds no new class: a shared row shape
  built in `benchmarkingUtils.js` from both responses.
- Keep `StandardBenchmarkHistory.jsx` only if it still owns something unique after
  the merge; otherwise delete it and fold its drawer into the shared detail view.

### WS4 — Cleanup and coherence pass

- Grep for orphaned references after WS1-WS3: unused imports, unused state, dead
  props, unused API helpers.
- Confirm no unused classes or models remain. Reuse existing records rather than
  introducing parallel ones.
- Update `plan.wizard-flow-revert.md` and the WS6 section of
  `plan.standard-benchmark-hardening.md` to mark the standard model-picker scope
  superseded.

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
- `tutor-studio/src/benchmarking/benchmarkingApi.js` (WS1, conditional)
- `tutor-studio/src/benchmarking/benchmarkingUtils.js` (WS2, WS3)
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx` (WS2, WS3)
- `tutor-studio/src/benchmarking/StandardBenchmarkHistory.jsx` (WS3, maybe deleted)
- Possibly `tutor-studio/src/benchmarking/BenchmarkRunDetails.jsx` (WS3)
- Plan files: `plan.wizard-flow-revert.md`, `plan.standard-benchmark-hardening.md`

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
- **Standard runs need a usable revision.** A model with no usable revision can no
  longer be benchmarked directly. Accepted: the revision is the model carrier per
  the original design. Flag to the human if direct model benchmarking is wanted.
- **WS3 merge shape** could tempt a new response class. Mitigation: build the shared
  row in `benchmarkingUtils.js` from existing responses; add a class only if reuse
  is impossible.

## Open Human Decisions

- **D-1:** Confirm standard runs launch only from a usable revision (Option A), or
  keep a direct model path for models with no revision. Recommendation: Option A.
- **D-2:** For WS3, confirm standard runs appear in the shared
  `BenchmarkRunHistory` list, or stay in a separate tab. Recommendation: shared
  list with a type indicator.
- **D-3:** Confirm the progress indicator format: `completed/total` when known,
  else percentage, else stage. Recommendation: that order.

## Open Items for AI Ops

- **A1:** The standard model-picker stage was implemented without a requirement
  check and survived review. Propose a guard so agentic work validates against the
  stated requirement before implementation (for example, a requirement-fidelity
  step in `bitcraft-backend-feature` / `bitcraft-frontend-feature`).
- **A2:** Assess whether the "bloat" pattern (new state, new fetch, new component
  per variant) needs an explicit reuse-first rule in the coding standards.
