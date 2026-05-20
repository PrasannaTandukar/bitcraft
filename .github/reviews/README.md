---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Reviews

Design-decision iteration log for the agentic stack. Each iteration captures: trigger, research checks, decisions, best-practice deviations, and follow-up work.

## Structure

```text
.github/reviews/
  README.md
  archive/                              ← superseded iterations
  templates/
    iteration.template.md
  FRAMEWORK_REVIEW.md                   ← active design log (current iteration on top)
```

## Rules

- Each iteration is a top-level section in `FRAMEWORK_REVIEW.md` or a separate file under `archive/` once superseded.
- Do not duplicate audit reports here; audits live in `.github/audits/`.
- Do not duplicate plans here; plans live in `.github/plans/`.
- Reviews capture WHY a structural decision was made; plans capture WHAT will be done.
