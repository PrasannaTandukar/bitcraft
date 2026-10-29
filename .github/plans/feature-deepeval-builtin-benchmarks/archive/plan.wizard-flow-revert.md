---
created_at: 2026-09-20
edited_at: 2026-09-20
creator: planner
editor: planner
ai_gen: true
---
# Sub-Plan: Wizard Flow Revert — Tutor Selection, Benchmark Selection, Review and Launch

> Origin: Planner, 2026-09-20. Human direction after the B1 restructure regressed the
> wizard: "plan change to revert Wizard flow: Tutor selection, Benchmark selection,
> Review and Launch."

**Status (2026-09-21):** The standard-category model-picker scope in this sub-plan is
superseded by `plan.benchmarking-ux-consolidation.md`. Step 1 is revision-only for every
category; the standalone interaction-model picker is removed.

## Goal

Restore the Benchmarking "New run" wizard to the human-specified three-stage flow:

1. **Tutor selection** (tutor + revision)
2. **Benchmark selection** (category + suite/template configuration)
3. **Review and Launch**

## Verified Current State (evidence, working tree 2026-09-20)

- The working tree already contains a partial revert of the 5-step restructure:
  `STEP_LABELS` is back to the original 4 labels ("Target & Revision", "Benchmark Suite
  & Configuration", "Test Cases", "Review & Launch"); `selectBenchmarkCategory` no
  longer calls `setStep(1)`; step rendering is back to `step === 1..4` with the original
  content mapping; `goNext` jumps 2→4 for standard; Back maps 4→2 for standard.
  Validation: eslint exit 0, `npm run build` exit 0.
- Retained from the fix cycle (intentional, do not lose):
  - `BenchmarkRunHistory.jsx`: `Space` added to the antd import (runtime crash fix).
  - `BenchmarkRunWizard.jsx`: admin-gated `listInteractionModelsForAdmin` fetch
    (`if (!isAdmin) return undefined;` inside the effect, `[isAdmin]` dependency).
  - `BenchmarkRunWizard.jsx`: Standard category card disabled for non-admins with
    "Admin only" tag; `onClick={() => isAdmin && selectBenchmarkCategory("standard")}`.
  - `BenchmarkRunWizard.jsx:531`: `metricName(metric.key)` fix in the review step.
  - Backend B3: `StandardTimeoutSeconds` (6h default) on the standard sidecar path.
- Remaining delta vs the human's requested flow: the current wizard still has **4**
  stages (Test Cases is its own step). The human asked for **3** stages. The Test Cases
  content must fold into the flow without becoming a separate top-level step, or the
  human must confirm 4 stages are acceptable.

## Options

### Option A — Three stages, Test Cases folded into Benchmark Selection (recommended)
- Stage 1 "Tutor Selection": tutor + revision (current step-1 RAG/Arduino content).
- Stage 2 "Benchmark Selection": category cards + template/suite configuration + test
  cases and execution settings, all on one stage. Standard runs show category + suite
  options (no case editing, unchanged behavior).
- Stage 3 "Review and Launch": unchanged content.
- Implementation: keep the existing step state machine but render stage 2 as a stacked
  `Space` containing the current step-2 and step-3 content; `goNext`/Back map 2↔3
  directly; `validateStep` validates the combined stage (template + cases + limits).
- Risk: a long stage-2 page for RAG runs (category + template + N cases). Mitigation:
  the case editors already render as cards; anchor-scroll or a sub-heading keeps it
  navigable. No state model change.

### Option B — Keep 4 stages, relabel only
- Keep the current 4-step machine; rename labels to the human's three names plus
  "Test Cases". Zero structural risk, but does not match the requested 3-stage flow.

### Recommendation
Option A. It matches the human's stated flow exactly, the state machine change is
small (merge two render blocks, adjust three functions), and validation is the same
lint/build plus a manual walk.

## Non-Goals

- No backend changes; no contract changes; no changes to the standard-run pipeline.
- No redesign of individual cards or styling beyond what merging steps requires.
- No change to the admin gating, the `Space` import fix, the timeout fix, or the
  `metricName` fix — all stay.
- No Git writes.

## Assumptions

- "Benchmark selection" includes choosing the category (RAG / Standard / Arduino) and,
  for RAG, the template; for Standard, the suite and n_shots/n_problems.
- Test cases remain part of the flow for RAG/Arduino; Standard keeps skipping them.

## Impacted Files

- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` — only file changed.

## CSS, DIS, CIL

- CSS: 3 — contained UI behavior in one view.
- DIS: 3 — one frontend view, no contract or backend touch.
- CIL: **Low** (CSS ≤ 2? No — CSS 3 → not Low by the table). CSS 3 / DIS 3 → **Medium**
  per `.github/config/cil-medium.yaml` (CSS ≤ 6 and DIS ≤ 6, and not both ≤ 2).
  Review route: Lead Dev self-review plus focused validation; no specialist required.

## Validation Plan

- `npx eslint` on the file — exit 0.
- `npm run build` in `tutor-studio/` — exit 0.
- Manual UI walk (human or agent with browser tools): stage 1 shows Tutor + Revision;
  stage 2 shows category + template/suite + test cases for RAG, category + suite for
  Standard; stage 3 shows the review summary; Launch works for RAG; Standard still
  skips case editing; Back/Continue never lands on a blank step.
- Confirm the retained fixes still present: admin-gated fetch, disabled Standard card
  for non-admins, `Space` import, `metricName(metric.key)`.

## Delegation

- lead-dev: implement Option A, run validation, update the execution record.
- planner: this plan; no inbox item needed beyond the existing lead-dev entry, which
  should be updated to point at this sub-plan.

## Open Human Decisions

- Confirm Option A (3 stages, cases folded into stage 2) vs Option B (keep 4 stages,
  relabel). Recommendation: Option A.
- If Option A: confirm whether stage 2 should scroll (single long page) or use an
  in-stage sub-anchor for test cases. Recommendation: single page; revisit only if the
  walk feels unwieldy.

## Execution Record (2026-09-20, lead-dev)

Human approved implementation ("follow the plan and fix the issues"); Option A taken
per the plan recommendation.

- Wizard is now 3 stages: 1 Tutor Selection (tutor + revision, or interaction model for
  Standard), 2 Benchmark Selection (category cards + template/suite configuration +
  test cases and execution settings for RAG/Arduino; Standard shows category + suite
  options only, no case editing), 3 Review and Launch.
- `STEP_LABELS` = ["Tutor Selection", "Benchmark Selection", "Review & Launch"].
- `validateStep(2)` now validates the combined stage: template (RAG) or suite
  (Standard), then case/limit rules for non-standard. `goNext` is a plain
  `Math.min(3, current + 1)` — the standard 2→4 jump is gone because cases no longer
  have their own step. Back is a plain `Math.max(1, current - 1)`. Launch validates
  steps 1–3.
- The old step-3 "Test cases and execution" block was folded into stage 2 inside the
  existing `Space` stack, gated on `benchmarkType !== "standard"` (Standard never
  renders case editing, matching prior behavior).
- Retained fixes verified present after the change: admin-gated
  `listInteractionModelsForAdmin` fetch (`if (!isAdmin) return undefined;`, `[isAdmin]`
  dependency), Standard category card disabled for non-admins with "Admin only" tag,
  `metricName(metric.key)` in the review step, `Space` import in
  `BenchmarkRunHistory.jsx`, backend `StandardTimeoutSeconds` (untouched this round).
- No backend or contract changes.

Validation run (2026-09-20):
- `npx eslint src/benchmarking/BenchmarkRunWizard.jsx` — exit 0.
- `npm run build` in tutor-studio — exit 0.
- Grep verification of structure: 3 STEP_LABELS, `step === 1|2|3` render blocks only,
  `Math.min(3, ...)`/`Math.max(1, ...)` navigation, retained fixes present.

Pending human validation: manual UI walk (stage 1 shows Tutor + Revision; stage 2
shows category + template + cases for RAG and category + suite for Standard; stage 3
review; Launch works; Back/Continue never lands on a blank step). Per the DONE gate,
this item is "implemented, pending human validation" — not DONE.

## Follow-up Fix (2026-09-20, lead-dev) — category change after stage 1

Human-reported error: "Select an interaction model to evaluate" on Launch.

Root cause: stage 1 renders the target form from the category, but the category is
chosen on stage 2. A user who passes stage 1 under RAG (tutor+revision) and then
switches to Standard on stage 2 never sees the interaction-model form, so Launch fails
full validation with no way to fix it from stage 3.

Fix: `selectBenchmarkCategory` now checks whether the new category's target is already
chosen. If not (Standard without a model, or RAG/Arduino without tutor+revision), it
sets a clear validation message and returns to stage 1, which renders the matching
target form. Stage-2 selections are preserved. If the target is already valid for the
new category (RAG↔Arduino), the user stays on stage 2 — no bounce.

Validation: eslint exit 0, npm build exit 0. Pending human walk: switch category on
stage 2 in both directions and confirm the bounce-back message and target form.
