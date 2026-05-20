---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Web Dev Inbox

- Date: 2026-05-21
- Status: FYI (local-setup-revision-2026-05-21)
- Source: Lead Dev (plan `.github/plans/local-setup-revision-2026-05-21/plan.yaml`)
- Target: web-dev
- Priority: low
- Task: `tutor-studio/vite.config.js` now sets `server.host = '0.0.0.0'` + `server.port = 5173` + `server.strictPort = true`, and `server.watch.usePolling` is driven by `VITE_USE_POLLING` (default `false`, set `true` on WSL2 or some macOS VirtioFS setups). `docker-compose.dev.yml` mounts the host repo into the dev container (`./tutor-studio:/app`) with an anonymous `/app/node_modules` volume so the in-container install is preserved. Please smoke-test HMR on your normal dev host and file a follow-up if hot reload misses changes — that's the `VITE_USE_POLLING` toggle.
- Context: Pure local-dev infra change. No UI behavior changed.
- Acceptance/Cleanup: Close after a quick HMR smoke test (`task up:dev`, edit `tutor-studio/src/App.jsx`, observe browser update).

- Date: 2026-05-21
- Status: pending (D25 step 9 — revision UI slice)
- Source: Lead Dev (plan v3.4 / D25 steps 7-8)
- Target: web-dev
- Priority: high
- Task: Tutor Studio UI for the new `TutorRevision` aggregate. Add a revisions tab on the tutor detail page driven by `GET /api/tutors/{tutorId}/revisions` with: list revisions (newest first, badge by `Status`), create-draft, edit-draft (prompt / interaction model / query embedding model / interaction params JSON / deployment target / deployment config JSON), publish, activate (POST `.../activate`), archive, and discard-draft (DELETE). Surface `ActivationSlug` + `PreviousActivationSlug` on the row. On activate-success refresh `Tutor.ActiveRevisionId` and show the captured snapshot. Existing snapshot/benchmark UIs do **not** need changes for this slice — they continue to work because the new FKs are nullable.
- Context: feature-freeze-2026-05-21 WS9. Contracts in `src/Bitcraft.Api/Contracts/Tutoring/TutorRevisions.cs`. Controller is `TutorRevisionsController` at `/api/tutors/{tutorId}/revisions`. Read endpoints require `AuditorOrAbove`; mutations require `EditorOrAdmin`.
- Acceptance/Cleanup: Folder/document UI for step 5 ships first; revision UI can ship in a separate Web Dev PR. Close after Web Dev opens the follow-up PR.

- Date: 2026-05-21
- Status: pending (D25 step 9)
- Source: Lead Dev (plan v3.4 / D25 step 9)
- Target: web-dev
- Priority: high
- Task: Tutor Studio UI for the new backend surface delivered in PR #4-8. Add (1) a per-user folder tree on `FilesPage` driven by `GET /api/document-folders` with create/rename/move/delete; (2) a "Move" affordance on each Document row calling `PATCH /api/documents/{id}/folder`; (3) an embedding-model selector on the vectorize action that posts `EmbeddingModelId` (sourced from `ModelCatalogPage`'s `EmbeddingModel` list) — the legacy free-text provider/model fields remain only as a debug-only fallback.
- Context: feature-freeze-2026-05-21 WS9. New contracts in `src/Bitcraft.Api/Contracts/Documents/Documents.cs`. Snapshot responses now carry `VersionNumber` — surface it on the snapshot list in `Benchmarking.jsx` and `TutorsPage.jsx`.
- Acceptance/Cleanup: Folders + move + model-selector work end-to-end against dev API; close after Web Dev opens a follow-up PR.

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
