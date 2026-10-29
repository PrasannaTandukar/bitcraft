---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Create a Bitcraft technical plan with risks, impacted files, validation, Change Impact Level, and delegation route.'
agent: 'Planner'
argument-hint: '[task, feature, bug, architecture question, or research summary]'
---

# Plan

Use this after overview or research, or when the user asks for a technical plan.

1. Read `.github/governance/DEVELOPER_GUIDE.md`, `.github/governance/METRICS.md`, and the Planner inbox.
2. Verify current state by reading referenced files, inbox items, and relevant source files.
3. Define goal, non-goals, assumptions, impacted files, CSS, DIS, CIL, risks, and validation.
4. Route development work through Lead Dev unless the task is expert-only.
5. Write inbox items for delegated future work when useful.

Output: plan, risk classification, delegation, validation, and human decisions needed.
