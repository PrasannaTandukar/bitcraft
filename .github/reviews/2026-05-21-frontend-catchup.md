---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: secretary
editor: secretary
ai_gen: true
---
# Frontend Catch-Up Report — 2026-05-21

> **Audience**: Web Dev and Human reviewer who needs to catch up before starting frontend work.
> **Scope**: Backend changes from D25 steps 1–8, current + outstanding frontend decisions, and design requirements still to be set.

---

## Part 1 — Backend Changes & Feature-Freeze Progress

### What was shipped (D25 steps 1–8)

All eight steps landed on branch `feature-freeze/05-tutor-document-mn`. Below is the developer-facing summary. The full rationale is in [plan.decisions.md](../plans/feature-freeze-2026-05-21/plan.decisions.md) and [docs/change-notes.md](../../docs/change-notes.md).

| Step | What landed | Key contracts / files |
|------|-------------|----------------------|
| **Step 1** — Identity & Roles vocabulary | `AppRoles`, `AppPolicies` centralized; coarse roles `Admin / Editor / Auditor`; policy constants in `Bitcraft.Application.Common.Security` | `AppRoles.cs`, `AppPolicies.cs` |
| **Step 2** — Resource Membership | `TutorMember` table replaces the old `Tutors.OwnerId` column. A Tutor now has zero or more members with `ResourceRole` (Owner, Editor, Viewer). All handlers re-authorization replaced from role checks → membership reads. | `TutorMember.cs`, `ITutorMembershipReader.cs` |
| **Step 3** — RuntimeEngine entity | `RuntimeEngine` is a first-class admin-managed entity (`Id`, `Name`, `ProviderType`, `Endpoint`, `ApiKeySecretRef`, `IsEnabled`). Model rows no longer carry provider/endpoint columns. | `RuntimeEngine.cs`, `GET/POST /api/runtime-engines` |
| **Step 4** — Model TPH hierarchy | `Model` is EF Core TPH: leaf types `InteractionModel` / `EmbeddingModel` (plus reserved `JudgeModel` / `RerankerModel`). `EmbeddingModel` carries `EmbeddingDimensions`. Model catalog page must filter by type. | `Model.cs`, `InteractionModel.cs`, `EmbeddingModel.cs` |
| **Step 5** — Tutor-Document M:N | `TutorDocument` and `TutorDocumentFolder` link tables. A tutor can attach individual documents **or** a folder of documents. Documents are admin-scoped source-of-truth artifacts; knowledge is the runtime retrieval layer. | `TutorDocument.cs`, `TutorDocumentFolder.cs`, `DocumentFolder.cs` |
| **Step 6** — Dynamic-dimension Knowledge | `KnowledgeChunk.Embedding` is now untyped `vector` with a per-`EmbeddingModelId` dimension check. The HNSW index is gone (sequential scan until re-enabled). Vectorization requires an explicit `EmbeddingModelId`. | `KnowledgeChunk.cs`, `VectorizeDocumentCommand` |
| **Step 7** — TutorRevision aggregate | Core of the freeze. A `TutorRevision` is a versioned snapshot of a tutor's configuration. State machine: **Draft → Published → Active → Archived**. Activation captures a snapshot, generates an `ActivationSlug` (slugified title, max 80 chars), and archives the previously active revision. | `TutorRevision.cs`, `TutorRevisionFeatures.cs` |
| **Step 8** — BenchmarkRun FK realignment | `BenchmarkRun.TutorRevisionId` (nullable FK). Links a benchmark run to the exact revision that was active when the run was triggered. | `BenchmarkRun.cs` |

**Also shipped (local-setup revision):**
- `GET /api/health` (anonymous) → `{ status: "ok", version }` — used by Docker HEALTHCHECK.
- `BITCRAFT_RUNTIME_LOCAL_ONLY` build flag (defined only for `Configuration == 'Local'`). Only affects the local-dev anonymous controller variant.
- `Taskfile.yml`, `run.ps1`, `run.sh` launchers and `docs/local-setup.md`.

### Current API surface for frontend consumption

All management endpoints require JWT. Use the role policies below — see [docs/authorization-roles.md](../../docs/authorization-roles.md) for the full matrix.

| Resource | Reads | Mutations | Notes |
|----------|-------|-----------|-------|
| `/api/auth/register`, `/api/auth/login` | Anonymous | Anonymous | Returns JWT with role claims. |
| `/api/runtime-engines` | `AdminOnly` | `AdminOnly` | Engine list for model-connection pickers. |
| `/api/models` | `AuditorOrAbove` | `AdminOnly` | TPH types: `InteractionModel`, `EmbeddingModel`. |
| `/api/tutors` | `AuditorOrAbove` | `EditorOrAdmin` | Includes membership, prompts, knowledge links, tools, deployment. |
| `/api/tutors/{tutorId}/revisions` | `AuditorOrAbove` | `EditorOrAdmin` | Full state-machine lifecycle. |
| `/api/documents` | `AuditorOrAbove` | `EditorOrAdmin` | Upload, update, vectorize (with `EmbeddingModelId`). |
| `/api/document-folders` | `EditorOrAdmin` | `EditorOrAdmin` | Admin-only grouping. |
| `/api/knowledge` | `AuditorOrAbove` | — | Tutor-scoped search. |
| `/api/health` | Anonymous | — | Docker health-gate only; no UI use. |
| `/api/benchmark-runs` | `AuditorOrAbove` | `EditorOrAdmin` | Now links to `TutorRevisionId`. |

### What is NOT yet implemented

- `GrantTutorMembership` / `RevokeTutorMembership` / `ChangeTutorMemberRole` endpoints — skeleton read-path exists; write path is an open net-dev item.
- Concrete `IPolicyEvaluator` implementation (WS6) — skeleton interface only; policy catalog not yet implemented.
- `SaveChanges` audit interceptor (D10) — not yet started.
- `Document.Visibility` first ABAC resource attribute (D1.a) — not yet started.

---

## Part 2 — Current Frontend Decisions

These decisions are **locked** (not open for re-debate unless explicitly re-opened by a Human).

| Decision | ID | What was decided |
|----------|----|-----------------|
| Language | **D18** | Migrate Tutor Studio to TypeScript **now**, in this iteration. |
| Migration order | **D18.a** | **Settings → ModelCatalog → Tutors → Files → Benchmarking**. Shared modules (`store/`, `data.js`, `App.jsx`) after at least 3 pages convert. Enable `strict: true` + `noUncheckedIndexedAccess` at the **end**. |
| Server-state cache | **D14** | Adopt **TanStack Query v5** (`@tanstack/react-query`). Replace `useEffect` + Redux fetch slices with query/mutation hooks. |
| Forms | **D15** | Adopt **React Hook Form v7 + Zod** (`react-hook-form`, `zod`, `@hookform/resolvers`). Zod schemas double as TS types and validation contracts. |
| Data tables | **D16** | Adopt **TanStack Table v8 headless** (`@tanstack/react-table`). Renders using existing react-bootstrap `<Table>`. |
| Design system | **D20** | Keep **react-bootstrap 5.3** for the freeze. Mantine/Radix re-evaluation deferred post-freeze. |
| OpenAPI client | **D27** | Codegen deferred until backend contracts are stable post-PoC. Hand-written TS clients typed from Zod schemas in the meantime. |
| Document folder UX | **D17 / D4b** | `DocumentFolder` is **admin-only** grouping. Runtime never sees folders — only `Knowledge` chunks. Folder UI is for Editor/Admin organization, not end-user retrieval. |
| Testing | **D19** | **Vitest + @testing-library/react** scaffolded now, even if only smoke tests for a single page. |
| Accessibility | — | `eslint-plugin-jsx-a11y` in flat config. `@axe-core/react` mounted in dev only. |

**State split rules (locked once §6 of plan.frontend.md is approved):**

| Kind | Lives in |
|------|----------|
| Server data (lists, details) | TanStack Query cache |
| Mutations | TanStack Query mutations + targeted invalidation |
| Cross-page client state (auth token, theme, nav) | Redux Toolkit slices |
| Local component state (modals, in-progress form drafts) | `useState` / `useReducer` |

**Current package.json baseline:**
React 19, Vite 8, Bootstrap 5.3, react-bootstrap 2.10, react-router-dom (latest), redux + @reduxjs/toolkit, react-redux, @svar-ui/react-filemanager 2.5, ESLint flat config. No TypeScript, no forms library, no table primitive, no test runner.

---

## Part 3 — Outstanding Frontend Decisions

These are **open** and require Human input or design approval before Web Dev can implement them.

### O4 — Library set & `@svar-ui/react-filemanager` keep-or-replace

**Status**: Open (plan.frontend.md §9 gate).
**Decision needed**: Human approves the full §6 recommendation from `plan.frontend.md`:
1. Add `@tanstack/react-query`, `react-hook-form`, `zod`, `@hookform/resolvers`, `@tanstack/react-table`.
2. Add dev deps: `typescript`, `@typescript-eslint/*`, `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `eslint-plugin-jsx-a11y`, `@axe-core/react`.
3. Keep `@svar-ui/react-filemanager` for physical file upload surface **only** — OR replace with a TanStack Table-driven selection surface if the admin many-to-many folder UX cannot be cleanly represented.

> **Context for Human**: `@svar-ui/react-filemanager` was designed for single-parent filesystem UX. Our `DocumentFolder` is many-to-many (a document can belong to multiple folders; a tutor can attach a whole folder). If the component cannot show multi-parent membership cleanly, we replace it with a custom TanStack Table view with checkboxes. This is a UX cohesion question, not a feature question.

**Unblocks**: Web Dev opening the first TypeScript-migration PR.

---

### OA — TutorRevision UI design (view + interaction requirements)

**Status**: Pending Human design input.
**What needs a decision**:
- Visual layout for the **Revisions tab** on the Tutor detail page (list view vs. timeline vs. accordion? which fields are visible at a glance?).
- Draft editing flow: single full-form page? or a stepper/wizard with sections (Prompt, Model selection, Interaction params, Deployment)?
- Activate flow: confirm dialog with slug preview? slug is auto-generated from title — should users be able to override it?
- After activation: show the captured snapshot inline? navigate to a separate Snapshot view?

**Reference**: Contracts in [src/Bitcraft.Api/Contracts/Tutoring/TutorRevisions.cs](../../src/Bitcraft.Api/Contracts/Tutoring/TutorRevisions.cs). Fields: `TutorRevisionId`, `Status`, `ActivationSlug`, `PreviousActivationSlug`, `Title`, `SystemPrompt`, `InteractionModelId`, `QueryEmbeddingModelId`, `InteractionParamsJson`, `DeploymentTarget`, `DeploymentConfigJson`, `CapturedUtc`, `CapturedById`.

**Unblocks**: Web Dev starting the Revisions UI slice (web-dev inbox step 9 item).

---

### OB — DocumentFolders UI design (admin-only)

**Status**: Pending Human design input.
**What needs a decision**:
- Should folders appear in a sidebar tree (traditional explorer feel) or a flat-list page?
- Multi-select to add documents to a folder — drag-and-drop or checkbox + "Add to Folder" button?
- When viewing a Tutor's attached folders: show a "Resolved documents preview" (the effective document set on next activation) — is this a modal or an inline panel?
- Can editors create/rename folders? or is that Admin-only?

**Reference**: D12 — no `DocumentFolderMember` permission boundary. D4/D4b — folder is admin-only organization; runtime only sees Knowledge.

**Unblocks**: Web Dev starting the Folders page (web-dev inbox step 9 item).

---

### OC — Activation slug display & deep-linking strategy

**Status**: Pending Human design decision.
**What needs a decision**:
- When a Tutor is activated, the `ActivationSlug` becomes the stable identifier for the runtime endpoint. Should the UI show the slug prominently (e.g., as a copyable URL fragment)?
- The previous slug (`PreviousActivationSlug`) is kept for one activation window for redirect. Does the UI need to surface this (e.g., a "Slug changed — old slug is still valid for now" notice)?
- Should editors be able to manually set the slug (or only auto-generate from title)?

**Reference**: D22, D22.a, D22.b in [plan.decisions.md](../plans/feature-freeze-2026-05-21/plan.decisions.md).

**Unblocks**: The activation confirmation flow in Web Dev's revision UI.

---

### OD — Benchmark run UI linking to revision

**Status**: Low priority / can defer.
**What needs a decision**:
- Should the Benchmarking page show which revision was active when each run was triggered?
- Is a "Run with current active revision" button needed, or do users trigger runs through the Revision detail view?

**Reference**: `BenchmarkRun.TutorRevisionId` is nullable — existing runs without a revision link remain valid.

---

## Part 4 — How Human Can Help Right Now

1. **Approve O4** (library set): Say yes/no to each of the five libraries in the add list, and decide on `@svar-ui/react-filemanager` keep-or-replace. This unblocks Web Dev starting the TypeScript migration immediately.
2. **Sketch or describe the Revisions tab layout** (OA): Even a rough description ("list with status badge, click to open a drawer editor") gives Web Dev enough to start. Full wireframes not required.
3. **Describe the Folders page interaction** (OB): Multi-select mechanism preference — drag-drop or checkbox?
4. **Decide on slug display** (OC): Should the activation slug be a copyable field in the Tutor detail header?

Items OD can wait until after the primary revision UI ships.
