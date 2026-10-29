---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Branch Convention

Captured from human decision on 2026-05-19. This rule replaces the open "propose a branch convention" item (then owned by the retired Dev Ops agent; now Sec Ops).

## Rule

- The active plan branch slug is **derived directly from the working Git branch name**.
- The plan folder under `.github/plans/<branch-slug>/` uses that same slug.
- Agents do **not** create new Git branches.

## Protected Working Branches

If the working branch is one of `dev`, `main`, or `test`:

- The agent must **not** create or assume a plan folder named after the working branch.
- The agent must HALT and notify the human that a working branch is unsuitable for direct plan-tracked changes.
- The human decides whether to create a feature branch (manual Git operation) or accept the change as a small in-place edit not tracked by a plan folder.

## Slug Normalization

When the working branch name is acceptable, the slug used for the plan folder is the branch name lowercased with `/` replaced by `-`. No other transformation. Example: working branch `feature/tutor-snapshots` becomes `feature-tutor-snapshots`.

## Notes

- This rule is read by agents during planning. See the `bitcraft-planning` skill and `.github/config/cil-*.yaml`.
- Sec Ops owns merge policy guidance, release tagging, and CI branch filters — those are out of scope for this document.
