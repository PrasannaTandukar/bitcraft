---
created_at: 2026-05-19
edited_at: 2026-05-20
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Web Dev Inbox

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: web-dev
- Priority: high
- Task: Begin the TypeScript migration of Tutor Studio in the order locked by D18.a: **Settings → ModelCatalog → Tutors → Files → Benchmarking**. Shared modules (API client, store, hooks) migrate only after at least three pages have been converted. Keep `strict: true` and `noUncheckedIndexedAccess` **disabled** until all pages compile, then enable them at the end.
- Context: feature-freeze-2026-05-21 WS9, D18, D18.a, **D27** (OpenAPI client codegen deferred until post-PoC contract stability; hand-written TS clients typed from the same Zod schemas used by React Hook Form until then). Pairs with the proposed `bitcraft-openapi-client-codegen` skill once scaffolded.
- Acceptance/Cleanup: Settings page compiles in TS with current ESLint clean; tracking note added to plan once each subsequent page lands; remove this item once Settings + ModelCatalog are on TS.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: web-dev
- Priority: medium
- Task: Surface the activation slug per D22.a: the active Tutor's slug is resolved through the activation record (not from a `Tutor` column). Update the Tutors page detail view and any deep-link resolution to read from the activation surface and to follow the one-revision redirect when the slug just changed.
- Context: feature-freeze-2026-05-21 WS6/WS7, D22, D22.a. Coordinate with NET Dev on the contract shape returned for the active revision.
- Acceptance/Cleanup: Tutor detail page reads slug from the activation contract; redirect path verified with a manual click-through; remove this item.
