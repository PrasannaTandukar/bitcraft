---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Typewriter Inbox

- Date: 2026-05-21
- Status: active
- Source: Secretary
- Target: typewriter
- Priority: medium
- Task: Update `docs/architecture.md` §3 Bounded Contexts and §4 (if present) to reflect the D25 implementation that landed: TutorRevision aggregate (state machine, ActivationSlug, snapshot capture), TutorDocument / TutorDocumentFolder M:N tables (admin-only DocumentFolder grouping), BenchmarkRun.TutorRevisionId FK, and the local-setup additions (health endpoint, anonymous `/api/health` trust zone, Taskfile launchers).
- Context: `docs/architecture.md` was last edited 2026-05-20 (pre-implementation). Reference: `src/Bitcraft.Domain/Tutoring/TutorRevision.cs`, `src/Bitcraft.Api/Controllers/Tutoring/TutorRevisionsController.cs`, `src/Bitcraft.Api/Controllers/HealthController.cs`. The `docs/architecture.md` frontmatter has `ai_gen: true` so Typewriter may edit it directly.
- Acceptance/Cleanup: §3 Tutoring context reflects TutorRevision + TutorDocument; System Overview §2 or a new §6 notes the `/api/health` anonymous endpoint; close this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: typewriter
- Priority: low
- Task: Update human-facing docs after branch convention and metrics capture decisions are approved.
- Context: Keep operational details in `.github/governance`; keep developer overview in `docs/`.
- Acceptance/Cleanup: Docs reflect approved behavior and validation; remove this item afterward.


