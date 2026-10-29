created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: secretary
ai_gen: true
# Lead Dev Inbox

- Date: 2026-05-21
- Status: routed to Dev Ops (2026-05-21)
- Source: Secretary
- Target: lead-dev → dev-ops
- Priority: high
- Task: EF migration host resolution routed to Dev Ops. Recommended approach (D41): add `appsettings.Development.json` (git-ignored) with a connection string using `Host=localhost;Port=5433` for host-shell `dotnet ef database update` runs. `appsettings.json` remains `Host=db;Port=5432` for Compose-internal use. Both paths made explicit in `docs/local-setup.md`. Dev Ops to implement and document. Remove this item.
- Acceptance/Cleanup: Closed — routed.

- Date: 2026-05-19
- Status: active
- Source: Planner
- Target: lead-dev
- Priority: high
- Task: Coordinate Agentic Stack Batch 2 implementation. Delegate WS1–WS3 and WS6–WS7 to AI Ops, WS5 to Researcher, WS8 to Sec Ops. Confirm FastMCP direction with human (Open Decision 1). Run final review after all work streams complete; route to Sec Ops for security findings, AI Ops for agentic-stack changes. CIL: Medium.
- Context: See `.github/plans/batch-2-agentic-stack/plan.md` for full scope and delegation table.
- Acceptance/Cleanup: All WS items complete, validation passes, human handover produced; remove this item.

