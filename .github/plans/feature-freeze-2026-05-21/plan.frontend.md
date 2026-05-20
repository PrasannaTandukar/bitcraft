---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# Plan Addendum: Tutor Studio Frontend Iteration (v3)

> Origin: Lead Dev, 2026-05-20. Companion to [plan.md](plan.md) and [plan.erm.md](plan.erm.md). The user explicitly carved frontend work out as a separate iteration because it is "impactful". This document captures the baseline, the candidate libraries, the recommendation, and the gates that block frontend implementation. **No frontend code lands until the recommendations in §6 are accepted by the human reviewer.**
>
> **🔒 LOCKED: TypeScript-now (D18/D18.a).** Tutor Studio migrates to TypeScript in this iteration, in the order **Settings → ModelCatalog → Tutors → Files → Benchmarking**, with `strict: true` + `noUncheckedIndexedAccess` enabled at the end of migration. This is **not** subject to Open Decision O4. See §4.6 and [plan.decisions.md D18 / D18.a](plan.decisions.md#d18--frontend-libraries-d14d20).
>
> **v3 amendments (Planner, 2026-05-20)** — captured for traceability. The body of this document has been brought into alignment with the v3 deltas (Lead Dev, 2026-05-20). Locked decisions D14–D20 from [plan.decisions.md](plan.decisions.md):
>
> - **D18 Language — Migrate Tutor Studio to TypeScript NOW.** This supersedes
>   the v2 "stay on JavaScript for the freeze" framing. OpenAPI-generated
>   client adoption follows once backend contracts are stable enough not to
>   regenerate-break the frontend each iteration; otherwise hand-written TS
>   clients are used.
> - **D17 / D4b Document/Folder UI** — Folder UI is **administrative** only.
>   The runtime never sees folders; only `Knowledge` is consumed at chat time.
>   The §4.4 "Document and folder UX" framing is updated to reflect this:
>   folder UI is for Editor/Admin organization of attached documents, not for
>   end-user retrieval scoping. End-user file uploads at chat time are out of
>   PoC scope.
> - **Cohesion-with-backend hard constraint** — the frontend stack does not
>   diverge from the backend contract shape. Selections fill gaps in the
>   existing stack only; existing libraries (react-bootstrap 5.3,
>   `@svar-ui/react-filemanager`) are retained unless an architectural
>   decision in [plan.decisions.md](plan.decisions.md) forces replacement.
> - **D14 TanStack Query v5**, **D15 React Hook Form + Zod**, **D16 TanStack
>   Table v8 headless**, **D19 Vitest + Testing Library smoke**, **D20
>   react-bootstrap 5.3** — all locked.
>
> v3 body rewrite (re-ordered §4 and refreshed §6) is tracked as Open Decision
> **O4** in [plan.md](plan.md#open-human-decisions-v3).

## 1. Goal

Define the Tutor Studio architecture (state, forms, data tables, file/folder UI, data-fetching, accessibility, design system) that supports the full backend feature set in [plan.erm.md](plan.erm.md): multi-owner Tutors, admin-grouped document folders, polymorphic model registry with capability filtering, dynamic-dimension embeddings, draft/published/active TutorRevisions, activation, and benchmarking shell. The frontend must scale past the PoC freeze without a rewrite **and remains cohesive with backend contracts (no divergent shapes)**.

## 2. Baseline

Captured from [tutor-studio/package.json](../../../tutor-studio/package.json) on 2026-05-20:

- React 19.x, Vite 8.x.
- Redux Toolkit 2.x, react-redux 9.x.
- react-bootstrap 2.10 + bootstrap 5.3 (current design system).
- react-router-dom (latest 6/7 line).
- @svar-ui/react-filemanager 2.5 (current file manager surface).
- ESLint flat config.
- **No TypeScript yet** (JavaScript + JSX only). **Target state per D18/D18.a: full TypeScript migration in this iteration.**
- No server-state cache, no schema-driven forms, no headless table primitive, no test runner wired.

## 3. Concern Map

| Concern | Today | Gap |
|---|---|---|
| Routing | react-router-dom | Adequate; needs nested routes for Tutor → Revision → Test panel. |
| Client-only UI state | Redux Toolkit | Adequate; should narrow to true client state (modals, filters, theme). |
| Server state (cache, retry, invalidation) | Manual `fetch` + Redux slices | Major gap. No staleness control, no background refresh, no cache keys per resource. |
| Forms (Tutor, Model wizard, Revision editor) | Hand-rolled controlled inputs | Major gap. No validation primitive, no shared error contract. |
| Data tables (Users, Models, Documents, Runs) | Hand-rolled `<table>` | Sorting/filter/pagination missing; UX inconsistent. |
| File + virtual folder UI | @svar-ui/react-filemanager | Works for files; does not model the **virtual folder** concept (a doc can live in many folders; a tutor can attach a folder). UI must distinguish "physical upload" from "virtual collection membership". |
| Design system | react-bootstrap 2.10 | Acceptable for POC. Limits theming and density; flagged as post-freeze re-evaluation. |
| Accessibility | Implicit | No baseline checks. Adopt axe-core dev rule. |
| Type safety | None (JS) | Major gap relative to backend's typed contracts. TypeScript migration considered. |
| Testing | None wired | Vitest + Testing Library recommended (post-freeze acceptable, but baseline scaffolding now). |

## 4. Candidate Libraries (Researched)

### 4.1 Server-state cache

**TanStack Query v5** ([overview](https://tanstack.com/query/latest/docs/framework/react/overview), [important defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults))

- Pros: caching keyed by `queryKey`, automatic refetch on stale, request dedup, mutation lifecycle with built-in invalidation, devtools, mature React 19 support, no opinions on transport (works with `fetch`/axios).
- Cons: another mental model alongside Redux; team must agree on "Redux for client state, Query for server state" split; SSR not relevant for our SPA.
- Verdict: **Adopt.** Highest leverage change. Eliminates large swaths of bespoke `useEffect` + dispatch + loading-flag code.

Alternatives considered: **SWR** (lighter, simpler, smaller ecosystem; weaker mutation story). **RTK Query** (already in Redux Toolkit; reduces stack count by 1 but is markedly less ergonomic for our list/detail/mutation patterns and is a weaker fit for the revision-heavy UI). Recommendation stands on TanStack Query.

### 4.2 Forms

**React Hook Form v7 + Zod via `@hookform/resolvers`** ([get-started](https://react-hook-form.com/get-started), [zod resolver](https://react-hook-form.com/docs/useform#resolver))

- Pros: uncontrolled-first (fast); minimal re-renders; co-located validation schema; resolver pattern lets us share Zod schemas with hypothetical TypeScript backend contracts; well-known team standard.
- Cons: learning curve for devs unfamiliar with refs; Zod adds a dependency.
- Verdict: **Adopt.** Required for the Tutor Revision editor and the Model wizard, both of which have multi-step validation.

Alternatives considered: **Formik** (heavier, slower with large forms); **TanStack Form** (newer, ecosystem still maturing); **Native FormData** (insufficient for nested objects in Revisions).

### 4.3 Data tables

**TanStack Table v8** ([introduction](https://tanstack.com/table/latest/docs/introduction))

- Pros: headless (we control markup, so it stays react-bootstrap-themed); sorting/filtering/pagination/column visibility out of the box; trivial to compose; React 19 compatible.
- Cons: not "drop-in"; we author the rendering layer.
- Verdict: **Adopt.** Pairs naturally with react-bootstrap `<Table>`.

Alternatives considered: **AG Grid Community** (component-based, fast, but visually distinctive and license restrictions on Enterprise features); **Material React Table** (would pull MUI). Headless wins for design consistency.

### 4.4 Document and folder UX (administrative)

Per D4b, `DocumentFolder` is an **administrative grouping** owned by Editor/Admin scope for organizing attached documents. The Tutor runtime never sees folders; only `Knowledge` is consumed at chat time. End-user file uploads at chat time are **out of PoC scope** and will be modeled as a separate runtime concept, not as folders.

**Keep @svar-ui/react-filemanager** for the physical file upload/list surface only, *if and only if* it can cleanly represent admin-side selection (multi-select of documents into a folder; many-to-many membership; no implicit single-parent semantics). If that admin-only UX cannot be represented cleanly, replace it with a TanStack Table-driven selection surface (the existing tables choice from §4.3). Re-evaluation is owned by Web Dev during frontend planning (D17).

Two administrative surfaces:

- **Documents** page — upload, list, edit metadata, view text. Tabular (TanStack Table). Visibility column (`Internal` default; reserved `Public`/`Restricted`) exposed read-only for the freeze.
- **Folders** page (admin-only) — create/rename folders, add/remove documents (multi-select), attach folder to tutors. Many-to-many membership is the explicit model; the UI must not imply a single-parent filesystem.

Document detail panel shows: "Member of folders: A, B" + "Attached to tutors: T1, T2 (3 via folder A)". A folder attached to a tutor materializes its document set into the next activation Snapshot (D23); the UI surfaces a read-only "Resolved documents" preview at attach time so editors see what runtime will see.

### 4.5 Design system

**Keep react-bootstrap for the freeze.** Re-evaluate Mantine or Radix-UI + Tailwind post-freeze. Migration is too disruptive inside the two-day window.

### 4.6 Language

**Migrate Tutor Studio to TypeScript NOW (D18).** This supersedes the earlier "stay on JavaScript for the freeze" framing. The Tutor Revision editor alone has 6+ inter-dependent fields and TS catches drift cheaply; doing the migration after the frontend grows is strictly more expensive.

Migration sequencing (locked):

1. Add `tsconfig.json` with `allowJs: true` and `strict: false` initially; configure Vite for `.ts`/`.tsx` resolution; wire ESLint with `@typescript-eslint`.
2. Convert pages in this order, one PR per page: **Settings → ModelCatalog → Tutors → Files → Benchmarking**. Order reflects increasing surface complexity; earlier pages establish the typing patterns reused later.
3. Convert shared modules (`store/`, `data.js`, `App.jsx`, `main.jsx`) once at least three pages are on TS so the patterns are stable.
4. Enable `strict: true` (and `noUncheckedIndexedAccess`) at the **end** of migration; no `any` in production code from that point.

OpenAPI-generated client adoption follows once backend contracts are stable enough not to regenerate-break the frontend each iteration (tracked as Open Decision **O3** in [plan.md](plan.md#open-human-decisions-v3)). Until then, hand-written TypeScript clients live alongside the converted pages and are typed from the same Zod schemas used by React Hook Form (§4.2).

### 4.7 Testing

**Vitest + @testing-library/react** scaffold in this iteration, even if only smoke tests for a single page are added before freeze. This is foundation, not feature.

### 4.8 Accessibility and a11y dev guardrails

- `eslint-plugin-jsx-a11y` in flat config.
- `@axe-core/react` mounted in dev only.
- Tab order and focus-trap checks on modals (Revision activate confirm, irreversible actions).

## 5. State Split Rules (Locked Once Approved)

| Kind | Lives in | Examples |
|---|---|---|
| Server data | TanStack Query cache | Tutors list, models, documents, folders, revisions, knowledge chunks, benchmark runs. |
| Mutations | TanStack Query mutations + targeted invalidation | Create revision, activate revision, attach folder, vectorize. |
| Cross-page client state | Redux Toolkit slices | Auth token (until rotated to httpOnly cookie), theme, navigation panels, persistent filters. |
| Local component state | `useState` / `useReducer` | Modal open flags, form-in-progress drafts (before submit). |

## 6. Recommendation Block (Pending Human Approval)

1. Add: `@tanstack/react-query` (D14), `react-hook-form` + `zod` + `@hookform/resolvers` (D15), `@tanstack/react-table` (D16).
2. Add (dev): `typescript`, `@typescript-eslint/*`, `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `eslint-plugin-jsx-a11y`, `@axe-core/react` (D19).
3. **Migrate to TypeScript NOW (D18)** per the sequencing in §4.6.
4. Keep: React, Vite, Redux Toolkit (narrowed scope), react-redux, **react-bootstrap 5.3 (D20)**, react-router-dom, `@svar-ui/react-filemanager` (retain unless §4.4 admin-only re-evaluation forces replacement).
5. **Cohesion-with-backend hard constraint**: existing libraries are retained unless an architectural decision in [plan.decisions.md](plan.decisions.md) forces replacement, and frontend types do not diverge from backend contract shapes (hand-written TS now; OpenAPI codegen per O3 once contracts stabilize).
6. Defer: Mantine/Radix evaluation, AG Grid.
7. New page: **Folders** (admin-only) for `DocumentFolder` management; rework **Documents** to be table-driven; rework **Tutors** to support Revision tabs (Draft / History / Test).
8. Adopt the state-split rules in §5.

## 7. Acceptance Criteria For The Frontend Iteration (When It Starts)

- Documents page: TanStack Table, multi-select, upload via existing file manager component contained within an "Upload" modal, metadata edit drawer, knows about Folders membership read-only.
- Folders page (admin-only): create/rename folder, drag-or-bulk-add documents, attach folder to tutor(s); read-only smart-filter banner stating future availability.
- Models page: TanStack Table grouped by `ModelType`; create wizard via React Hook Form + Zod with capability-driven steps (Interaction vs Embedding); embedding-dim field required only for `EmbeddingModel`.
- Tutors page: list with member badge ("Owner of 3, Editor of 5"); detail page tabs (Settings, Revisions, Knowledge, Activation, Test); Revisions tab supports Draft editing, Publish, Activate, View History.
- Revision editor: single React Hook Form instance with Zod schema, model pickers filtered by capability, validation surfaces server errors per field.
- TanStack Query devtools enabled in dev only.
- Vitest test runs in CI (one smoke test minimum).
- `npm run lint` clean.

## 8. Risks

- Adding five libraries in a freeze window is a meaningful surface. Mitigation: each library is opt-in per page; existing pages keep working until rewritten.
- The admin-only Folders concept (many-to-many membership; not a filesystem) is unfamiliar to editors used to filesystem folders. Mitigation: in-page help text + read-only "Resolved documents" preview when attaching a folder to a tutor.
- Auth token in Redux is incorrect long-term (XSS exposure). Mitigation: keep as-is for freeze (status quo), schedule httpOnly cookie rotation post-freeze.

## 9. Gates Before This Iteration Starts

- [ ] Human approves the §6 library set and the admin-only Folders UX (`@svar-ui/react-filemanager` keep-or-replace) — this is the remaining Open Decision **O4** in [plan.md](plan.md#open-human-decisions-v3).
- [x] [plan.erm.md](plan.erm.md) accepted with the 8-step migration sequence (D25).
- [x] TypeScript-now adoption (D18) and migration order (D18.a) locked.
- [x] OpenAPI client adoption timing decided (D27 — deferred until post-PoC contract stability).
- [ ] WS3/WS4/WS5/WS6 backend contracts in [plan.md](plan.md) are stabilized through their D25-ordered migration steps (no rolling renames during frontend buildout).

Until those open gates pass, agents do not modify files under `tutor-studio/`.
