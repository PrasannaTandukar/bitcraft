---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: planner
editor: planner
ai_gen: true
---
# Bitcraft Plans

Branch-scoped task plans. One folder per active branch. Archives are out of normal MCP context unless explicitly requested.

## Structure

```text
.github/plans/
  README.md
  archive/              ← completed plan folders (whole-branch)
  templates/
    plan.template.yaml  ← lean per-task config; human-modifiable
    plan.template.md    ← optional planning narrative
    context.template.md ← short scope/file-refs document
  <branch-slug>/        ← one folder per active branch
    archive/            ← completed sub-plan files inside this branch
    plan.yaml           ← REQUIRED: lean config + metadata
    plan.md             ← OPTIONAL: planning narrative
    context.md          ← OPTIONAL: live scope and file refs
```

## Rules

- One active branch folder = one active task line. Multiple active branches are allowed but discouraged.
- `plan.yaml` is the source of truth for agent/model selection, CSS/DIS/CIL, and status.
- Workflow entrypoints are skills under `.github/skills/` (prompt files were retired 2026-09-17); they are not stored here.
- When a sub-task file is complete, move it to the branch-local `archive/`.
- When the whole branch is complete, move the whole branch folder to `.github/plans/archive/`.
- The MCP server exposes only active (non-archived) plans via `list_active_plans()` and `read_plan(branch_slug)`. Use `include_archived=True` only when explicitly searching history.

## Human-Modifiable Fields

`plan.yaml` is designed for direct human editing. Agents must respect the human-set values for `agents`, `model_family`, `cil`, and `status` unless the human asks otherwise.
