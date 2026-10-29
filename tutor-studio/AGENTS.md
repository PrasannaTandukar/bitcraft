---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: human
editor: secretary
ai_gen: false
---
# AGENTS.md - Tutor Studio Frontend

This nested file gives coding agents focused guidance when working inside `tutor-studio/`. The repository-root `AGENTS.md` and `.github/copilot-instructions.md` still apply; this file adds frontend-specific rules.

> **Last updated 2026-05-21** by Secretary after Human Decision session (D29–D41 locked). Changes: TypeScript deferred post-PoC; Redux Toolkit clarified as client-state-only; planned library set documented; no-hardcoded-URLs enforced; dev/prod config separation required.

## Stack (PoC)

- **React 19**, **Vite 8**, **plain JavaScript/JSX** — TypeScript is deferred to post-PoC (D29). Architecture and API client structure must be designed to support future TS adoption without a rewrite.
- **Redux Toolkit 2.x + react-redux** — narrowed to **client-only state** (auth token, user, roles, theme, nav). Server data must NOT enter Redux.
- **react-bootstrap 2.10 + Bootstrap 5.3** — retained for PoC. No design-system migration (D20).
- **react-router-dom** — current version.
- **ESLint** configured via `eslint.config.js`.
- Nginx is used only for production container image (`Dockerfile.prod`, `nginx.conf`).

## Planned Libraries (Not Yet Installed — Human installs before development begins)

The following library set is locked by decisions D14–D16, D19, D28:

| Library | Version | Decision | Purpose |
|---|---|---|---|
| `@tanstack/react-query` | v5 | D14 | Server state / async data fetching |
| `react-hook-form` | v7 | D15 | Forms |
| `zod` + `@hookform/resolvers` | latest | D15 | Schema validation |
| `@tanstack/react-table` | v8 | D16 | Headless data tables |
| `vitest` + `@testing-library/react` | latest | D19 | Unit + component tests |

**Do not import or reference these libraries in code until Human confirms they are installed (`npm ci` passes).**

## Commands

- Install: `npm ci` (preferred; use `npm install` only when adding new packages).
- Dev server: `npm run dev`.
- Lint: `npm run lint` (run via `conda run -p .\.conda npm --prefix .\tutor-studio run lint` on Windows with conda).
- Build: `npm run build`.

Run the narrowest relevant validation first.

## API Client — No Hardcoded URLs (D40)

- **All API calls go through `src/api/client.js`** (or `client.ts` post-PoC).
- The base URL is read from `import.meta.env.VITE_API_BASE_URL`.
- `VITE_API_BASE_URL` defaults to `http://localhost:8000` via `.env.development` (git-ignored).
- `.env.development.example` is tracked. `.env.development` is not.
- **No component file may contain a hardcoded `http://` URL.** Any violation should be flagged and fixed before the PR is opened.

## Configuration Separation (D41)

- `.env.development` — git-ignored, local dev overrides.
- `.env.production` — git-ignored, production values set by deployment environment.
- `.env.development.example` and `.env.production.example` — tracked, no secrets.
- Production security config (CSP, HSTS in `nginx.conf`) must be present in production paths and not silently omitted.

## Conventions

- Page-level components live directly under `src/` and are named `<Area>Page.jsx` or `<Area>.jsx` for non-page screens (`TutorsPage.jsx`, `FilesPage.jsx`, `Benchmarking.jsx`).
- **Server state** (API data, lists, detail objects) must use **TanStack Query** once installed. Do not put server data into Redux.
- **Client state** (auth session, theme, nav flags) stays in `src/store/` (Redux Toolkit).
- API integration is centralized in `src/api/client.js`; do not hardcode URLs in components (D40).
- Keep components small. Extract a child component when a render branch grows beyond one screen.
- Follow the Human Web Dev's established code style and approach. Minimize disruptive changes; do not introduce workarounds that break the existing visual style or UX patterns (D37).

## Authorization / Routing

- `AdminRoute` guards Admin-only pages: Settings, RuntimeEngines, Users, Model Catalog.
- `EditorRoute` admits **both Editor and Admin** roles (D35). Use `roles.includes('Editor') || roles.includes('Admin')` — not `Editor`-only.
- `EditorOrAdmin`-scoped backend APIs: Documents, DocumentFolders, Tutors, Revisions, Benchmarking.
- Any inline role check that duplicates the same pattern must include: `// TODO post-PoC: extract role-check hook when framework auth context is installed.`

## Feature Parity Requirement (D36)

Frontend must surface **all backend functionality** introduced in D25 steps 1–8. These surfaces are **not optional polish**:
- `TutorRevision` CRUD workflow (Draft → Published → Active → Archived)
- `TutorDocument` M:N attach/detach (replaces old `KnowledgeSourceLink` pattern)
- `DocumentFolder` flat-list CRUD as a component within Files/Admin page (D37, D31)
- Document move (`PATCH /api/documents/{id}/folder`)
- Embedding model selector on vectorize action (replaces free-text provider/model fields)
- `TutorMember` membership management (grant/revoke/change role)
- Activation slug display (read-only, derived from Tutor title — no manual override, D38)
- `BenchmarkRun.TutorRevisionId` revision column in benchmark list (D39)

## Constraints

- **Do not introduce TypeScript** — deferred to post-PoC (D29). Plan and structure code to be TypeScript-ready.
- **Do not add new global state libraries** — Redux Toolkit is the client-state store. Server state goes to TanStack Query once installed.
- Do not store secrets, tokens, production endpoints, or hardcoded URLs in source files.
- Do not modify root configuration files (`.gitignore`, root Docker Compose) without explicit human approval.
- **Lint errors are deferred** to the final iteration's quality pass (D34). Do not block feature PRs on pre-existing lint errors; do not introduce new ones.

## Backend Contract

The frontend talks to `src/Bitcraft.Api`. Contracts live in `src/Bitcraft.Api/Contracts/`. Never expose Domain entities through API contracts. If the backend exposes a Domain entity, raise the issue before changing the frontend.

Key contract notes:
- `GET /api/documents` returns `DocumentSummaryResponse` (no `RawText`).
- `GET /api/documents/{id}` returns `DocumentDetailResponse` (with `RawText`).
- `TutorRevision` contracts: `src/Bitcraft.Api/Contracts/Tutoring/TutorRevisions.cs`.
- `DocumentFolder` contracts: `src/Bitcraft.Api/Contracts/Documents/`.
- Model catalog endpoints are at `/api/models` (TPH polymorphic — `InteractionModel`, `EmbeddingModel`).

