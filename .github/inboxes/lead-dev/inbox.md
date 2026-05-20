created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: secretary
ai_gen: true
# Lead Dev Inbox

- Date: 2026-05-21
- Status: active
- Source: Secretary
- Target: lead-dev
- Priority: high
- Task: Next batch must resolve the host-side EF migration failure `No such host is known` by reconciling local-host credentials/hostname versus container-only defaults, then validate every Compose entrypoint. The current tracked app config points `DefaultConnection` at `Host=db;Port=5432` while local docs use `Host=localhost;Port=5433`; confirm the intended host-shell path for `dotnet ef database update`, ensure the correct credentials source is used without introducing tracked secrets, and make the host-run and Compose-run paths both explicit.
- Context: Observed on 2026-05-21 after the D25 steps 4-8 batch. `src/Bitcraft.Api/appsettings.json` currently uses `Host=db;Port=5432;Database=bitcraft;Username=bitcraft_user;Password=bitcraft_password`, which works inside Compose but not from a host shell. `docker-compose.yml` uses the same `Host=db` connection string for `migrator` and `backend` while exposing Postgres on `5433` to the host. Validate all variants: `docker-compose.yml`, `docker-compose.dev.yml`, `docker-compose.prod.yml`, `docker-compose.ai.yml`, `docker-compose.ai.gpu.yml`, `docker-compose.ai.jetson.yml`.
- Acceptance/Cleanup: Lead Dev routes the implementation to Dev Ops/NET Dev as needed; every compose file resolves with `docker compose ... config`, host-run EF migrations are documented and succeed against the intended local target, and credentials/hostname handling is consistent between docs, app config, and compose overrides. Remove this item once the batch lands.

- Date: 2026-05-19
- Status: active
- Source: Planner
- Target: lead-dev
- Priority: high
- Task: Coordinate Agentic Stack Batch 2 implementation. Delegate WS1–WS3 and WS6–WS7 to AI Ops, WS5 to Researcher, WS8 to Sec Ops. Confirm FastMCP direction with human (Open Decision 1). Run final review after all work streams complete; route to Sec Ops for security findings, AI Ops for agentic-stack changes. CIL: Medium.
- Context: See `.github/plans/batch-2-agentic-stack/plan.md` for full scope and delegation table.
- Acceptance/Cleanup: All WS items complete, validation passes, human handover produced; remove this item.

