---
created_at: 2026-09-20
edited_at: 2026-09-20
creator: planner
editor: planner
ai_gen: true
---
# Sub-Plan: Benchmarking Wizard & Pipeline Fixes (human review findings)

> Origin: Planner, 2026-09-20. Raised during human review of the finalized
> `feature/deepeval-builtin-benchmarks` branch. All findings below are grounded in
> current source on this branch (verified by reading the files, not from memory).

## Goal

Make the Benchmarking "New run" wizard functional end-to-end and remove the pipeline
defects that block or mislead a standard (model-scoped) benchmark run.

## Verified Current State (evidence)

- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` — `selectBenchmarkCategory`
  calls `setStep(1)`. The wizard starts at `step = 1` ("Target & Revision"), but the
  category selector (RAG / Standard / Arduino) is rendered on `step === 2`. Selecting a
  category therefore resets the user to step 1. This is the reported bug: "selecting any
  benchmark tab option moves me back to initial view."
- The wizard's default `benchmarkType` is `"rag"`. Step 1 renders the tutor+revision form
  for RAG/Arduino and the interaction-model form only when `benchmarkType === "standard"`.
  Because category is chosen on step 2 (after target), a Standard run cannot cleanly reach
  its model-selection state — the user picks a tutor first, then switches category and is
  bounced to step 1 showing the RAG tutor form. The selected standard model is not visible.
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx:90` renders `<Space size={4} ...>`
  but the antd import on line 2 (`Alert, Button, Empty, Flex, Form, Select, Table,
  Typography`) does not include `Space`. This is a runtime `ReferenceError` whenever a run
  row renders its score cell. ESLint does not flag it in the current config, so it passes
  lint and build.
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs`
  `CreateClient()` sets `client.Timeout = TimeSpan.FromSeconds(sidecarOptions.TimeoutSeconds)`
  for both the tutor and standard paths. `DeepEvalBenchmarkOptions.TimeoutSeconds` defaults
  to 120 and `appsettings.json` does not override `Benchmarking:DeepEval:TimeoutSeconds`.
  A full-dataset standard run (MMLU/GSM8K, thousands of model calls) exceeds 120s, so the
  `HttpClient` aborts and the run is marked `Failed` even though the sidecar may still be
  working. Full-dataset standard runs are effectively non-functional over the current
  transport. Capped runs (default `n_problems = 10`) may fit, but this is not guaranteed.
- `tutor-studio/src/BenchmarkingPage.jsx` shows the "New run" tab to `Editor` or `Admin`.
  `StandardBenchmarkRunsController.Create` is `[Authorize(Policy = AdminOnly)]` and
  `InteractionModelsController` (used by `listInteractionModelsForAdmin`) is `AdminOnly`.
  An Editor who selects the Standard category gets a 403 on the model list and cannot
  launch. The wizard surfaces this only at launch.
- Standard runs expose only `Status` (no `/progress` endpoint, no `ProgressPercent`), unlike
  tutor runs. A long standard run shows no intermediate feedback in the results drawer.

## Non-Goals

- No change to the standard-run data model, migration, or sidecar benchmark logic.
- No new progress/telemetry infrastructure for standard runs (noted as a follow-up option,
  not in scope here).
- No Git writes; commit/push stays human-owned.
- No change to the locked Model Evaluation vs Tutor Benchmarking separation.

## Assumptions

- The wizard reorder is a frontend-only concern; the API contract for both run types is
  unchanged.
- Raising the standard-path sidecar timeout is safe because the sidecar call is
  trusted-internal (local UDS) and the run is already async/queued.

## Work Items

### B1 — Reorder wizard: category first, target second (the reported bug) [DONE 2026-09-20]
- Make benchmark category selection step 1.
- Make target selection step 2 and render it from the chosen category:
  - RAG / Arduino → tutor + revision form (current step-1 RAG content).
  - Standard → interaction-model form (current step-1 standard content).
- Remove `setStep(1)` from `selectBenchmarkCategory`; changing category on step 1 should not
  reset anything, and moving to step 2 should validate the category is chosen.
- Update `STEP_LABELS`, `validateStep`, `goNext`, and the Back-button logic to match the new
  order (Standard still skips case editing: step 2 → review).
- Owner: lead-dev. Frontend only.

### B2 — Fix missing `Space` import in run history (runtime crash) [DONE 2026-09-20]
- Add `Space` to the antd import in `BenchmarkRunHistory.jsx`.
- Owner: lead-dev. One-line fix.

### B3 — Decouple the standard-run sidecar timeout (bottleneck) [DONE 2026-09-20]
- Give the standard LLM benchmark path a long (or unbounded) client timeout, separate from
  the short per-case tutor timeout. Options (lead-dev picks one, flag in completion note):
  - A dedicated `HttpClient`/handler for `EvaluateStandardLlmBenchmarkAsync` with a large
    timeout (e.g. hours) or `Timeout.InfiniteTimeSpan`, or
  - A separate options value `Benchmarking:DeepEval:StandardTimeoutSeconds` with a high
    default, wired in `DependencyInjection.cs`.
- Keep the tutor path on the existing 120s timeout.
- Owner: lead-dev. Infrastructure only; no contract change.

### B4 — Gate the Standard category by role (misleading funnel) [DONE 2026-09-20]
- In the wizard, either hide the Standard category for non-Admin users or show a clear
  "Administrators only" notice and disable launch, so an Editor is not funneled into a path
  that 403s.
- Owner: lead-dev. Frontend only.

## Impacted Files

- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` (B1, B4)
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx` (B2)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs` (B3)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkOptions.cs` (B3, if options route chosen)
- `src/Bitcraft.Infrastructure/DependencyInjection.cs` (B3, if options route chosen)
- `src/Bitcraft.Api/appsettings.json` (B3, optional override)

## CSS, DIS, CIL

- CSS: 4 — contained UI behavior + one Infrastructure timeout value; no auth, data, or
  contract change.
- DIS: 5 — one frontend view path + one Infrastructure service (no Domain/Api contract
  change).
- CIL: **Medium** (CSS ≤ 6 and DIS ≤ 6). Protocol `.github/config/cil-medium.yaml`.

## Validation Plan

- `npm run lint` and `npm run build` in `tutor-studio/` — clean (B1, B2, B4).
- Manual UI walk: New run → pick each category on step 1 → correct target form on step 2 →
  launch path reachable for RAG, Standard (Admin), Arduino. Confirm no step reset on
  category change. Confirm run-history table renders score cells without error (B2).
- `dotnet build bitcraft.slnx` — 0 errors (B3).
- Unit tests: existing benchmark suites still pass; add/adjust a test if B3 changes options
  wiring.
- Human: confirm a capped standard run completes; full-dataset run no longer aborts at 120s
  (B3) — local environment.

## Delegation

- lead-dev: B1, B2, B3, B4 (all implementation).
- planner: this plan + inbox items.
- No Sec Ops review required (no credential, compose, or auth-policy change). B4 is UI
  gating only; the backend AdminOnly policy is unchanged.

## Execution Record (2026-09-20, lead-dev)

- B1: wizard restructured to 5 steps — 1 Category, 2 Target (tutor+revision for RAG/Arduino,
  interaction model for Standard), 3 Suite & Configuration, 4 Test Cases, 5 Review & Launch.
  `selectBenchmarkCategory` no longer resets the step. Standard still skips case editing
  (step 3 → 5 forward, 5 → 3 back). `STEP_LABELS`, `validateStep`, `goNext`, launch
  validation, and Back-button logic updated to the new numbering.
- B2: `Space` added to the antd import in `BenchmarkRunHistory.jsx`.
- B3: options-value route chosen — new `DeepEvalBenchmarkOptions.StandardTimeoutSeconds`
  (default 21600s = 6h, overridable via `Benchmarking:DeepEval:StandardTimeoutSeconds`).
  `DeepEvalBenchmarkEngine.CreateClient(int? timeoutSeconds)` takes an explicit budget;
  `EvaluateStandardLlmBenchmarkAsync` passes the standard budget, the tutor path keeps the
  120s default. No appsettings change needed (default is sufficient).
- B4: Standard category card is disabled for non-Admin users with an "Admin only" tag and
  reduced opacity; `validateStep(1)` also blocks Continue for non-Admin on Standard.
  Backend AdminOnly policies unchanged.

Validation run (2026-09-20):
- `npx eslint` on both changed frontend files — exit 0.
- `npm run build` in tutor-studio — exit 0 (pre-existing chunk/dynamic-import warnings only).
- `dotnet build Bitcraft.Infrastructure.csproj` — 0 warnings, 0 errors.
- `dotnet test Bitcraft.UnitTests --filter FullyQualifiedName~Benchmark` — 87/87 passed.

Not run: full unit suite, API/infra integration suites, sidecar pytest (no sidecar change),
manual UI walk and live standard-run check (human, local environment).

## Review Record (2026-09-20, lead-dev, bitcraft-review)

Scope: working-tree diff of B1-B4. CSS 4 / DIS 5 / CIL Medium confirmed. No Sec Ops or
AI Ops routing required (no auth policy, credential, compose, or agentic-stack change).

Findings and disposition:
- M (fixed in review): `listInteractionModelsForAdmin` fired on mount for every role.
  Editors received a guaranteed 403 and an error alert on the Standard target step even
  though they could never launch. Fix: fetch only when `isAdmin`.
- M (fixed in review): stale `benchmarkType === "standard"` state (URL restore, in-session
  role change) let a non-admin render and attempt the Standard path. Fix: derived
  `category` value (`!isAdmin && standard -> rag`) used by all validation, navigation,
  rendering, and launch logic; raw state only drives the step-1 radio group.
- L (fixed in review): step-3 card titles kept old "2." numbering after the reorder.
  Renumbered to plain titles.
- L (fixed in review, pre-existing): `metricName(metric)` passed an object instead of
  `metric.key` in the step-5 RAG metrics row. Fixed to `metricName(metric.key)`.
- Note (accepted): `interactionModelsLoading` stays true for non-admins; harmless because
  the Standard target step is unreachable for them (derived category + disabled radio).
- Note (accepted, pre-existing): wizard state is not URL-synced except tutor/revision via
  `onSelectionChange`; category/suite selections reset on tab switch. Out of scope here.

Re-validation after review fixes: eslint exit 0 (both files), npm build exit 0.
