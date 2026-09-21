---
created_at: 2026-09-17
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-planning
description: "Create a Bitcraft technical plan: goal, non-goals, assumptions, impacted files, CSS/DIS/CIL classification, risks, validation strategy, and delegation route. Use after overview or research, or whenever the user asks for a plan before implementation."
argument-hint: "[task, feature, bug, architecture question, or research summary]"
---

# Bitcraft Planning

Use this skill to convert goals and evidence into an actionable technical plan. Runs inside the Planner agent. The `bitcraft-plan` entry skill wraps this skill with the Phase 1 decision tree; use `bitcraft-plan` as the workflow entrypoint.

## Procedure

1. Read `.github/governance/DEVELOPER_GUIDE.md`, `.github/governance/METRICS.md`, and the Planner inbox.
2. Verify current state by reading referenced files, inbox items, and relevant source files. Do not plan from stale memory.
3. Define goal, non-goals, assumptions, impacted files, risks, and validation.
4. Classify CSS, DIS, and CIL using the `bitcraft-change-risk` skill.
5. Persist the plan under `.github/plans/<branch-slug>/` per `.github/plans/README.md`: `plan.yaml` is the source of truth for status, agents, model family, and CIL. Derive the branch slug from the working Git branch (lowercase, `/` → `-`). HALT on `dev`, `main`, or `test`.
6. Route development work to Lead Dev, integrity work to Sec Ops, agentic stack work to AI Ops, documentation to Secretary.
7. Write concise inbox items for delegated future work when useful.

## Constraints

- Do not implement production code during planning.
- Do not decide branch policy, metrics thresholds, or release policy without human input.

## Output

- Goal and verified current state.
- Options and recommendation.
- CSS, DIS, and CIL.
- Impacted files.
- Delegation plan.
- Validation plan.
- Open human decisions.
- Next step: "Run the `bitcraft-develop` skill for `.github/plans/<branch-slug>/plan.<slug>.md`. Execute work steps in order. Validate each step before the next."
