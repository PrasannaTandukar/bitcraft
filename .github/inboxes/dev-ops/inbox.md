---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Dev Ops Inbox

- Date: 2026-05-19
- Status: active
- Source: Human (D-3)
- Target: dev-ops
- Priority: medium
- Task: Adopt the branch convention captured in `.github/governance/BRANCH_CONVENTION.md`: plan folder slug is derived from the working Git branch; `dev` / `main` / `test` are protected (agents HALT and notify the human). Confirm CI branch filters and merge policy are compatible, and document any deviations.
- Context: Supersedes the original "propose a branch convention" item. Agents do not perform Git writes. Coordinate with `.github/plans/` folder naming and the existing `batch-2-agentic-stack/` placeholder.
- Acceptance/Cleanup: CI and merge policy compatibility confirmed (or follow-up items raised); remove this item.

- Date: 2026-05-19
- Status: active
- Source: Planner
- Target: dev-ops
- Priority: low
- Task: Confirm the `.github/plans/` folder design is compatible with the adopted branch convention. Flag any naming collisions in active folders.
- Context: Branch convention now lives in `.github/governance/BRANCH_CONVENTION.md`.
- Acceptance/Cleanup: Confirm or suggest adjustments; remove this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: dev-ops
- Priority: medium
- Task: Schedule the first Integrity Audit after the branch convention direction is approved.
- Context: Use `.github/audits/templates/audit.template.md` and create remediation inbox items rather than broad report sprawl.
- Acceptance/Cleanup: Audit is completed or converted into concrete remediation tasks; remove this item afterward.
