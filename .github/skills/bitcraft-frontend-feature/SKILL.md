---
created_at: 2026-05-19
edited_at: 2026-09-21
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-frontend-feature
description: "Implement Bitcraft Tutor Studio frontend changes. Use for React/Vite UI, routing, state, styling, API integration, accessibility, responsive behavior, and frontend validation."
argument-hint: "[frontend feature, bug, page, component, or integration task]"
---

# Bitcraft Frontend Feature

Use this skill for work in `tutor-studio/`.

## Procedure

1. Requirement check first: restate the requested behavior in one sentence. Verify every state field, fetch, and component you intend to add is named by the request or the plan. If it is not, do not add it.
2. Read `.github/instructions/frontend.instructions.md` and inspect existing Tutor Studio patterns near the target component or page.
3. Reuse before creating: search for an existing component, hook, API helper, or state slice that already carries the value. Extend it instead of adding a parallel one.
4. Identify the smallest UI or state slice that controls the requested behavior.
5. Preserve existing design language unless the user asks for a redesign.
6. Coordinate contract assumptions carefully before changing backend API calls — Lead Dev owns both sides and must keep them aligned.
7. Make minimal, responsive, accessible changes.
8. Run the narrowest available frontend lint, build, or browser validation.
9. Emit the next step: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: <changed files>. Original request: <one-sentence restatement from step 1>."

## Output

- Frontend behavior changed.
- Files changed.
- Validation run.
- Backend/API assumptions.
- Remaining UX or test risk.
