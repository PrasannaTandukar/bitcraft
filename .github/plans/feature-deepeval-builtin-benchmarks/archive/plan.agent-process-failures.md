---
created_at: 2026-09-20
edited_at: 2026-09-20
creator: planner
editor: planner
ai_gen: true
---
# Sub-Plan: Agent Process Failure Analysis and AI Ops Action Plan

> Origin: Planner, 2026-09-20. Commissioned by the human after the Benchmarking wizard
> fix cycle produced a regression (Revision selection removed from the initial view) and
> a tool-loop failure. This plan analyzes what went wrong and hands concrete actions to
> AI Ops. It is a process plan, not a code plan.

## Goal

Identify the agent-side mistakes that caused the faulty development results on
2026-09-20, and give AI Ops a concrete, checkable action plan so the same failure modes
do not recur.

## Verified Current State (evidence)

All events below are from this session's working-tree history and chat record.

1. **Scope escalation on a one-line bug (root cause of the regression).**
   The reported bug was: selecting a benchmark category on step 2 reset the wizard to
   step 1. Root cause: `selectBenchmarkCategory` called `setStep(1)`. The minimal fix was
   deleting that one line. Instead, the agent restructured the wizard from 4 steps to
   5 steps (category-first), moving Tutor+Revision selection off the initial view. The
   human saw this as a regression: "the wizard now lacks our Revision selection."
   Evidence: `plan.benchmark-wizard-fixes.md` B1 specified the reorder; the human's
   approval said "I approve changes" but the plan itself over-scoped B1 relative to the
   reported symptom.

2. **Review validated the restructure instead of the requirement.**
   The `bitcraft-review` pass checked the diff against architecture, lint, and build —
   all green — but never checked the change against the user's actual complaint. A
   structurally clean change can still be the wrong change. The review record shows
   findings about fetch gating and stale state, none about "does the wizard still show
   Revision on the first screen."

3. **Tool-loop failure (wasted turn budget).**
   After the revert, the agent tried to verify whether a `metricName(metric.key)` fix
   survived by grepping a diff file via the terminal. The command returned empty output
   repeatedly; the agent re-ran the same command ~40 times instead of switching to a
   direct file read after the first empty result. No state was corrupted, but a large
   amount of turn budget was burned and the user had to intervene twice.

4. **Validation gap that let the regression ship.**
   Frontend validation was lint + build only. Both pass for any syntactically valid
   restructure. There is no UI-level check (manual walk checklist, screenshot, or test)
   that the wizard's first screen still contains Tutor and Revision selectors. The
   validation plan in `plan.benchmark-wizard-fixes.md` listed a manual UI walk as a
   human step, but the agent marked B1 "DONE" before any such walk occurred.

5. **Plan drift between symptom and work item.**
   The sub-plan's B1 said "reorder wizard so category is step 1" — a design decision —
   while the human's report was "selecting a benchmark tab option moves me back to
   initial view — broken." The plan converted a bug report into a redesign without
   flagging the scope change as a human decision.

## Root Causes (aggregated)

- RC1: Bug reports were converted into design changes without an explicit
  minimal-fix-first gate.
- RC2: Review checked code quality, not requirement fidelity.
- RC3: No cheap UI-state assertion existed to catch "first screen lost Revision."
- RC4: On repeated empty tool output, the agent did not switch strategy after the first
  or second failure.
- RC5: "DONE" markers were written into plan files before human-visible validation.

## Non-Goals

- No blame assignment beyond process facts; no model-routing changes in this plan.
- No new CI infrastructure (Vale remains the only enforcement surface per
  `DEVELOPER_GUIDE.md`).
- No changes to `.github` skill files in this plan (AI Ops owns any such edits; they are
  listed as actions, not executed here).

## AI Ops Action Plan

Each action is small, checkable, and owned by AI Ops. None require human decisions
beyond normal review.

### A1 — Add a minimal-fix-first rule to planning and frontend skills (High)
- In `.github/skills/bitcraft-planning/SKILL.md`: add a constraint — "For a reported bug,
  the plan must state the minimal fix and the chosen fix. If the chosen fix exceeds the
  minimal fix, mark the excess as a separate work item requiring human approval."
- In `.github/skills/bitcraft-frontend-feature/SKILL.md`: add to Procedure — "Identify
  the smallest UI or state slice" already exists; add "Do not restructure navigation,
  step order, or layout to fix a state or handler bug unless the human asked for a
  redesign."
- Acceptance: both files updated; STE-compliant wording; frontmatter `edited_at`
  updated; no other files touched.

### A2 — Add a requirement-fidelity gate to the review skill (High)
- In `.github/skills/bitcraft-review/SKILL.md`: add a checklist item — "The change
  satisfies the original request, not only quality gates. Restate the user's complaint
  and confirm the diff addresses exactly that."
- Acceptance: checklist item added; existing items unchanged.

### A3 — Add a UI-walk checklist artifact for wizard-style flows (Medium)
- Create `.github/plans/templates/ui-walk.template.md` (or add to the existing
  templates folder per `.github/plans/README.md`) with a short checklist: list the
  surfaces that must remain visible per step, and require the implementer to tick them
  before marking a work item DONE.
- Acceptance: template exists, follows frontmatter rules, referenced from
  `.github/plans/README.md` if that file lists templates.

### A4 — Add a tool-loop guard to agent guidance (Medium)
- In `.github/copilot-instructions.md` (or the agent definition files AI Ops owns): add
  a rule — "If the same tool call returns empty or an error twice, stop retrying the
  same form. Switch to a different tool or read the target directly."
- Acceptance: rule added in STE style; no other instruction drift.

### A5 — Define a DONE gate (Medium)
- Add to the planning skill output rules: "A work item may be marked DONE only after
  its validation plan's agent-runnable steps pass. Human-only validation steps must be
  listed as pending, and the item marked 'implemented, pending human validation' —
  never DONE."
- Acceptance: rule present in the skill; retroactively consistent with how
  `plan.benchmark-wizard-fixes.md` should have been marked.

### A6 — Record the incident (Low)
- Append a short incident note to `docs/change-notes.md` (or the branch plan's
  decisions file) summarizing: regression introduced by over-scoped fix, caught by
  human, reverted same day; tool-loop failure burned budget. One paragraph, factual.
- Acceptance: note present; no prose inflation.

## Sequencing

A1 and A2 first (they prevent the two most expensive failure modes), then A3–A5, then
A6. All are AI Ops-owned; none touch production code.

## CSS, DIS, CIL

- CSS: 2 — process/documentation only, no runtime behavior.
- DIS: 2 — `.github` skill and instruction files only.
- CIL: **Low** (CSS ≤ 2 and DIS ≤ 2). Protocol `.github/config/cil-low.yaml`:
  implementing-agent self-check plus focused validation. AI Ops self-validates
  frontmatter, naming, and markdown diagnostics per the standard validation rule for
  `.github` changes.

## Validation Plan

- Frontmatter validity and STE wording check on every touched `.github` file.
- Grep for the new rules to confirm placement in the intended files.
- No build/test runs required (no code change).

## Open Human Decisions

- Whether A4's tool-loop rule goes into `copilot-instructions.md` (always-on) or only
  into agent definition files (scoped). Recommendation: `copilot-instructions.md`,
  because the failure occurred in the default Lead Dev path.
- Whether A6's incident note goes in `docs/change-notes.md` (public-facing) or the
  branch plan decisions file (internal). Recommendation: branch plan decisions file;
  `docs/` is human-facing product documentation.
