---
created_at: 2026-08-13
edited_at: 2026-08-13
creator: planner
editor: planner
ai_gen: true
---

> **Steering change created**: AI Ops inbox item added to make branching strategy and Jira ticket verification standard steps in all future plans. See `.github/inboxes/ai-ops/inbox.md`.

# Frontend Navigation Rehaul — Plan

## Goal

Restructure the Tutor Studio navigation from a flat single-row navbar into a two-layer grouped menu with semantically grouped sections, renamed pages, a user-profile dropdown, and clarified naming for the admin model/engine surfaces.

## Verified Current State

### Navigation (`App.jsx`)

- Single horizontal `Navbar` with a flat list of `Nav.Link` items.
- Links: Dashboard, Settings, AI Tutor, Benchmarking, Files, Tutors, Role Management, Model Catalog, Interaction models, Runtime engines.
- Admin-only links are appended to the same list with no visual grouping.
- Settings is a top-level peer of work-area links.
- User identity is a plain email + Logout button in the navbar corner; no profile surface.

### Pages

| Current name | Route | Current file |
|---|---|---|
| Dashboard | `/` | `Dashboard.jsx` |
| Settings | `/settings` | `Settings.jsx` (stub — `//TODO implement this page`) |
| AI Tutor | `/ai-tutor` | `aiTutor/AiTutorPage.jsx` |
| Benchmarking | `/benchmarking` | `BenchmarkingPage.jsx` + `benchmarking/` |
| Files | `/files` | `FilesPage.jsx` + `documents/` |
| Tutors | `/tutors` | `TutorsPage.jsx` + `tutors/` |
| Role Management | `/role-management` | `Users.jsx` |
| Model Catalog | `/model-catalog` | `EmbeddingModels.jsx` |
| Interaction models | `/interaction-model` | `InteractionModels.jsx` |
| Runtime engines | `/runtime-engines` | `RuntimeEngines.jsx` |

### Naming Ambiguity (confirmed against Domain)

The Domain has `Modeling/` with `Model` (abstract), `EmbeddingModel`, `InteractionModel`, and `RuntimeEngine` with `RuntimeEngineProviderType` (Ollama / OpenAI / Anthropic). The frontend surfaces these as "Model Catalog", "Interaction models", and "Runtime engines" — which is ambiguous because:

- "Model Catalog" only shows EmbeddingModels, not the full model concept.
- "Interaction models" is the chat/completion model role but the label doesn't say so.
- "Runtime engines" is the provider/endpoint layer, not a model.
- There is no distinction in the UI between **Model** (the named model configuration) and **Provider** (the engine/endpoint that serves it).

### Architecture Constraints (from `tutor-studio/AGENTS.md`)

- React 19, Vite 8, plain JS/JSX — TypeScript deferred post-PoC.
- Redux Toolkit for client state only (auth, nav flags). Server data must NOT enter Redux.
- TanStack Query is the planned server-state layer (not yet installed; do not import until Human confirms).
- react-bootstrap 2.10 + Bootstrap 5.3 retained.
- `src/api/client.js` is the single API client; no hardcoded URLs.
- Page components live under `src/` as `<Area>Page.jsx`.
- Lint errors are deferred to the final quality pass; do not introduce new ones.

## Options And Recommendation

### Option A — Top-nav with dropdown groups (recommended)

Keep the top navbar but replace the flat link list with grouped dropdowns:

- **Dashboard** — standalone link.
- **Chat** — standalone link (renamed from AI Tutor).
- **Documents ▾** — dropdown with "Browse" (current Files list) and "Upload" (current upload dialog, surfaced as a distinct view).
- **Tutors ▾** — dropdown with "Tutors" (list), "Benchmarking", and "Tutor Setup" (placeholder for future config/setup).
- **Admin ▾** — dropdown (admin-only) with "Role Management", "Embedding Models", "AI Models", "AI Providers".
- **User ▾** — dropdown with "Profile", "Settings", "Logout".

**Why recommended:** Minimal layout change (stays top-nav), uses Bootstrap's existing `Dropdown` component, preserves the current shell, and is the smallest structural change that achieves the two-layer grouping.

### Option B — Sidebar navigation

Replace the top navbar with a persistent left sidebar containing grouped sections.

**Tradeoff:** Larger visual and CSS change, more files touched, higher DIS. Better long-term scalability but disproportionate effort for a PoC-stage app. Defer until post-PoC redesign.

### Option C — Mega-menu / bottom nav

Not appropriate for this app's content density and desktop-first PoC context.

**Recommendation: Option A.**

## CSS, DIS, CIL

| Metric | Score | Rationale |
|---|---|---|
| CSS | 4 | Contained UI behavior change; no data, no auth logic, no backend contracts. |
| DIS | 5 | Touches the app shell (`App.jsx`, `App.css`), multiple page renames/routes, and introduces a new Profile route. Multiple files but one bounded context (frontend navigation). |
| CIL | Medium | CSS ≤ 6, DIS ≤ 6. Routes `cil-medium.yaml`: Web Dev implements, Lead Dev reviews. |

## Impacted Files

### New files

- `src/ProfilePage.jsx` — user profile view (initially read-only: display email, roles, account info).
- `src/Profile.css` — profile page styles.

### Modified files

- `src/App.jsx` — navigation structure, route definitions, page renames.
- `src/App.css` — navbar styling for dropdown groups and user dropdown.
- `src/Settings.jsx` — remains a stub; route moves under user dropdown.
- `src/FilesPage.jsx` — rename references from "Files" to "Documents" in UI text; prepare for Browse/Upload split (initially both render the same page, Upload opens the existing dialog).
- `src/documents/FilesPage.css` — rename to `DocumentsPage.css` (or keep and alias).
- `src/documents/documentApi.js` — no changes, but "Files" labels in comments/strings updated.

### Renamed / moved files (route-level, not filesystem renames yet)

| Current | New route | New label |
|---|---|---|
| `/ai-tutor` | `/chat` | Chat |
| `/files` | `/documents` | Documents |
| `/model-catalog` | `/admin/embedding-models` | Embedding Models |
| `/interaction-model` | `/admin/ai-models` | AI Models |
| `/runtime-engines` | `/admin/ai-providers` | AI Providers |
| `/role-management` | `/admin/roles` | Role Management |

File names (`EmbeddingModels.jsx`, `InteractionModels.jsx`, `RuntimeEngines.jsx`) are **not** renamed in this iteration — only the routes and nav labels change. File renames are a follow-up.

### Naming clarification (frontend labels only)

| Current label | New label | Maps to Domain |
|---|---|---|
| Model Catalog | Embedding Models | `EmbeddingModel` |
| Interaction models | AI Models | `InteractionModel` |
| Runtime engines | AI Providers | `RuntimeEngine` |

This aligns the UI with the Domain distinction: **Model** = named model configuration (EmbeddingModel / InteractionModel), **Provider** = the engine/endpoint that serves it (RuntimeEngine with ProviderType).

## Delegation Plan

1. **Web Dev** implements the navigation restructure:
   - Replace flat `Nav.Link` list with Bootstrap `Dropdown` groups.
   - Add user-profile dropdown (Profile / Settings / Logout).
   - Rename routes and labels per the table above.
   - Create `ProfilePage.jsx` (read-only profile view).
   - Add `/documents` route with Browse/Upload sub-navigation (both render `FilesPage` initially; Upload opens the existing `DocumentUploadDialog`).
   - Add `/admin/*` nested routes under the Admin dropdown.
   - Keep `RoleRoute` / `AdminRoute` guards; move admin routes under `/admin/` prefix.

2. **Lead Dev** reviews the navigation shell change and route restructure.

3. **Sec Ops** is **not** required for this iteration (no auth logic changes, no new permissions), but the plan notes that the admin route prefix change (`/admin/*`) should be verified against any backend route assumptions — there are none, since the backend routes are `/api/admin/*` and the frontend routes are client-side only.

## Validation Plan

1. `npm run lint` in `tutor-studio/` — no new lint errors introduced.
2. `npm run build` — clean build.
3. Manual browser validation:
   - All nav links navigate correctly.
   - Admin dropdown is hidden for non-admin users.
   - User dropdown shows Profile / Settings / Logout.
   - Documents Browse and Upload both work (Upload opens the existing dialog).
   - Responsive behavior on mobile (collapsed navbar).
   - No text overflow in dropdown labels.
4. Verify no hardcoded URLs were introduced.
5. Verify `RoleRoute` / `AdminRoute` guards still protect the correct pages.

## Branching Strategy

This change follows the locked branching model (HD-IT2-3, 2026-05-21):

- **Branch source**: `dev`, **not** `main`.
- **Branch name**: `feature/ui-navbar-improvement` — short-lived feature branch (already created and checked out).
- **Branch naming convention**: The repository's Bitbucket/Jira-compatible branch convention is still being finalized by Dev Ops (tracked in the Developer Guide as "the next operational task"). Once approved, branch names will include Jira issue keys. This plan uses the descriptive slug `feature/ui-navbar-improvement` as the interim convention.
- **Workflow**: Human fetches `origin/dev`, creates the branch locally, and opens the PR back to `dev`. Agents do not perform Git writes.
- **`main` is off-limits to all agents, always** — production readiness is a separate human-only process.

## Jira Work Ticket Verification

**No Jira work ticket has been verified for this change.** The repository references Jira/Bitbucket integration as a planned capability (Secretary's description includes "Jira/Bitbucket status"; the Developer Guide notes the branch convention should include Jira issue keys), but no active Jira integration is configured in the repository.

**Before implementation begins, the Human should:**

1. Create a Jira work ticket for this change (suggested summary: "Restructure Tutor Studio navigation into grouped two-layer menu with renamed pages and clarified admin naming").
2. Link the ticket to the branch name once the Jira-key branch convention is approved.
3. Use the ticket to track progress, acceptance criteria, and the open decisions listed below.

This is an administrative step that enables early traceability and external progress tracking. The plan proceeds without a ticket, but implementation should wait for Human confirmation that a ticket exists or is not needed.

## Inbox Conflict Resolution (Secretary review, 2026-08-13)

Secretary reviewed all agent inboxes before implementation. Three conflicts were identified and resolved:

### 1. Document Folders UI on FilesPage — RESOLVED (folded into this branch)

**Conflict:** Web Dev has two pending high-priority items that both target `FilesPage.jsx`: the folder tree component and the Document Folders UI + ingestion fix. The nav rehaul renames Files→Documents and introduces a Documents sub-menu on that same surface. Building the folders component on the old `FilesPage` naming/structure would be immediately stale.

**Resolution:** Fold the folders/move/model-selector work into this branch so the component lands on the renamed Documents surface in one pass. This is lower rework risk than deferring and rebasing. The Web Dev inbox items for "Tutor Studio UI for the new backend surface delivered in PR #4-8" and "Document Folders UI + Document Ingestion Fix" are now scoped to this branch.

### 2. ESLint debt fixes — RESOLVED (applied alongside)

**Conflict:** `EmbeddingModels.jsx`, `InteractionModels.jsx`, `RuntimeEngines.jsx` have pending ESLint fixes (derived-state-via-effect and mount-fetch setState patterns). These are the admin pages being re-parented under `/admin/`. Both tasks edit the same files in the same window.

**Resolution:** Apply the ESLint fixes on this branch alongside the route restructure. No functional conflict; one agent owns the final file state.

### 3. Activation slug surface — RESOLVED (verified, no action needed)

**Conflict:** Tutor detail view deep-link resolution. If any deep links encode the old `/ai-tutor` or tutor-detail route shape, the nav rehaul's route renames could break them.

**Resolution:** Verified that activation-slug deep links use the tutor ID/slug contract rather than hardcoded route paths. No action needed; the route renames do not affect slug-based deep links.

## Open Human Decisions

1. **Documents sub-navigation** — should "Upload" be a separate route (`/documents/upload`) or a tab/panel within `/documents`? The plan assumes a separate route that renders the same page with the upload dialog pre-opened, but a tabbed approach is also valid.
2. **Profile page scope** — the plan scopes Profile to read-only display (email, roles). Should it include password change, email change, or theme settings in this iteration?
3. **Settings location** — the plan moves Settings under the user dropdown. Should it remain a top-level link for discoverability?
4. **Admin route prefix** — the plan prefixes admin routes with `/admin/`. This is a client-side convention only (backend routes are `/api/admin/*`), but it changes bookmarked URLs. Confirm this is acceptable.
5. **File renames** — the plan defers renaming `EmbeddingModels.jsx` → `EmbeddingModelsPage.jsx`, `InteractionModels.jsx` → `AiModelsPage.jsx`, etc. to a follow-up. Confirm or accelerate.
6. **Tutors sub-menu** — the plan includes "Tutor Setup" as a placeholder. Should this be included now (as a stub) or deferred?
7. **Jira ticket** — confirm whether a Jira work ticket should be created for this change before implementation, and provide the ticket key once the branch convention is finalized.

---

## Requirements

### Functional Requirements

1. **Two-layer navigation**: The top-level menu presents grouped sections (dropdowns) and standalone links. A second layer appears as a dropdown beneath the group label.
2. **Dashboard** — standalone top-level link, main landing view.
3. **Chat** — standalone top-level link, renamed from "AI Tutor".
4. **Documents** — grouped menu with two sub-items:
   - **Browse** — file list view with CRUD operations (list, preview, delete, vectorize, folder assignment).
   - **Upload** — file upload view (the existing upload dialog, surfaced as a distinct entry point).
   - Documents supports a single-layer folder system (flat folders, files flat inside folders).
5. **Tutors** — grouped menu with sub-items:
   - **Tutors** — tutor list and detail pages.
   - **Benchmarking** — benchmark run history, templates, and run wizard.
   - **Tutor Setup** — placeholder for future tutor configuration/setup surfaces.
6. **Admin** — grouped menu (admin-only visibility) with sub-items:
   - **Role Management** — user role assignment.
   - **Embedding Models** — embedding model catalog (renamed from "Model Catalog").
   - **AI Models** — interaction/completion model catalog (renamed from "Interaction models").
   - **AI Providers** — runtime engine/provider management (renamed from "Runtime engines").
7. **User profile dropdown** — replaces the plain email + Logout button. Contains:
   - **Profile** — user profile view (read-only: email, roles).
   - **Settings** — moved from top-level nav.
   - **Logout** — existing logout action.
8. **Naming clarity**: UI labels distinguish **Model** (named model configuration: EmbeddingModel / InteractionModel) from **Provider** (the engine/endpoint that serves it: RuntimeEngine).

### Non-Functional Requirements

1. **Responsive**: Navigation collapses correctly on mobile; dropdowns are accessible in the collapsed state.
2. **Accessible**: Dropdowns use keyboard navigation; active link states are visible; ARIA labels are present.
3. **No new packages**: The restructure uses only react-bootstrap's existing `Dropdown` component and react-router-dom. No new dependencies.
4. **No backend contract changes**: All route changes are client-side. Backend API routes (`/api/admin/*`, `/api/documents/*`, etc.) are unchanged.
5. **TypeScript-ready**: Code structure supports future TS migration (no dynamic property access patterns that would break).
6. **No hardcoded URLs**: All API calls continue through `src/api/client.js`.
7. **Lint discipline**: No new lint errors introduced (per D34, pre-existing errors are deferred).

---

## Best Practices And Code Standards

### Modularity

- **Navigation as a component**: Extract the navigation shell into `src/Navigation.jsx` (or `src/Navbar.jsx`) rather than keeping it inline in `App.jsx`. This isolates the nav structure from route definitions and makes it testable.
- **Route configuration as data**: Define routes in a `routes.js` config array (path, element, guard, label, parent group) rather than hardcoding `<Route>` elements. This makes adding/removing pages a data change, not a render-tree change.
- **Guard composition**: `RoleRoute` and `AdminRoute` are already composable. The plan preserves this pattern; no new guard types are introduced.
- **Page component discipline**: Each page remains a single exported default component under `src/`. Child components stay in their feature folders (`documents/`, `tutors/`, `benchmarking/`, `aiTutor/`).

### Code Practices

- **React functional components and hooks** — consistent with the existing codebase.
- **Bootstrap Dropdown component** — use `Dropdown`, `Dropdown.Toggle`, `Dropdown.Menu` from react-bootstrap for the grouped menus. This is the idiomatic Bootstrap 5 pattern and requires no custom CSS for dropdown behavior.
- **NavLink active state** — preserve `NavLink` for active-state styling; dropdown sub-items use `NavLink` so the parent dropdown can reflect the active child.
- **Role-based visibility** — reuse the existing `roles` selector pattern (`roles?.includes("Admin")`, `roles?.some(...)`). Do not duplicate role-check logic; extract to a shared `useRoleCheck` hook only post-PoC (per the existing TODO convention).
- **CSS containment** — nav-specific styles stay in `App.css` (or a new `Navigation.css` if extracted). Page styles remain in their feature folders. No global style leakage.
- **No server state in Redux** — the Profile page displays data already in Redux (email, roles from `authSlice`). No new API calls for the initial Profile view.
- **Defensive rendering** — dropdowns and links render safely when `roles` is undefined (use optional chaining, consistent with the existing `AppContent` pattern).

### Naming Conventions

- **UI labels match Domain intent**: "Embedding Models" → `EmbeddingModel`, "AI Models" → `InteractionModel`, "AI Providers" → `RuntimeEngine`. This resolves the current ambiguity where "Model Catalog" meant only embedding models and "Runtime engines" obscured the provider concept.
- **Route naming**: `/documents` (not `/files`), `/chat` (not `/ai-tutor`), `/admin/*` prefix for admin surfaces. These are client-side conventions that align with the Domain vocabulary.
- **File naming**: Page files keep `<Area>Page.jsx` convention. The plan defers renaming `EmbeddingModels.jsx` → `EmbeddingModelsPage.jsx` etc. to a follow-up to keep this iteration focused.

---

## Gap Analysis — Current State vs. Target

### Navigation Structure

| Aspect | Current | Target | Gap |
|---|---|---|---|
| Menu shape | Flat single-row list | Two-layer grouped dropdowns | Entire nav structure must be replaced |
| Grouping | None | Documents, Tutors, Admin groups | Three new dropdown groups |
| User area | Email + Logout button | Profile dropdown (Profile / Settings / Logout) | New dropdown, new Profile page |
| Admin separation | Mixed into main list | Isolated Admin dropdown | Visual and structural separation |

### Page Names And Routes

| Page | Current route | Target route | Change |
|---|---|---|---|
| Dashboard | `/` | `/` | None |
| AI Tutor | `/ai-tutor` | `/chat` | Route + label rename |
| Files | `/files` | `/documents` | Route + label rename |
| Benchmarking | `/benchmarking` | `/tutors/benchmarking` (under Tutors group) | Route re-parenting |
| Tutors | `/tutors` | `/tutors` | Label stays; gains sub-menu |
| Role Management | `/role-management` | `/admin/roles` | Route re-parenting + rename |
| Model Catalog | `/model-catalog` | `/admin/embedding-models` | Route re-parenting + rename |
| Interaction models | `/interaction-model` | `/admin/ai-models` | Route re-parenting + rename |
| Runtime engines | `/runtime-engines` | `/admin/ai-providers` | Route re-parenting + rename |
| Settings | `/settings` | `/settings` (under user dropdown) | Route stays; nav location changes |
| Profile | (none) | `/profile` | New page, new route |

### Documents Feature

| Aspect | Current | Target | Gap |
|---|---|---|---|
| Entry point | Single "Files" link | "Documents" group with Browse + Upload | Two sub-items needed |
| Upload surface | Dialog opened from list | Distinct Upload entry point | Upload needs a route or tab |
| Folder system | Flat folders exist (`DocumentFolder`) | Single-layer folders, files flat inside | Already supported by backend; UI needs folder navigation |
| Knowledge separation | "Files" conflates documents and knowledge | "Documents" is the document surface; Knowledge is a separate concept | Naming resolves the conflation |

### Naming Clarity

| Concept | Current UI label | Domain type | Target UI label | Gap |
|---|---|---|---|---|
| Embedding model config | "Model Catalog" | `EmbeddingModel` | "Embedding Models" | Ambiguous; implies all models |
| Chat/completion model | "Interaction models" | `InteractionModel` | "AI Models" | "Interaction" is not user-facing vocabulary |
| Provider/endpoint | "Runtime engines" | `RuntimeEngine` | "AI Providers" | "Engine" obscures the provider/endpoint role |
| Provider type | "Ollama / OpenAI / Anthropic" | `RuntimeEngineProviderType` | (same) | Already clear in the engine form |

### Authorization

| Aspect | Current | Target | Gap |
|---|---|---|---|
| Admin route guards | `AdminRoute` on individual routes | `AdminRoute` on `/admin/*` routes | Guard pattern stays; route paths change |
| Editor/Auditor routes | `RoleRoute` with role arrays | Same | No change |
| User dropdown visibility | Always shown when authenticated | Same | No change |

---

## Step-By-Step Action Plan

### Phase 1 — Foundation (Web Dev)

1. **Extract navigation into `src/Navigation.jsx`**
   - Move the `Navbar` JSX from `App.jsx` into a `Navigation` component.
   - Accept `roles`, `user`, `onLogout`, `navigate` as props.
   - `App.jsx` renders `<Navigation />` and `<Routes />` separately.

2. **Define route configuration in `src/routes.js`**
   - Create a `routes` array: `{ path, element, guard, label, group, roles }`.
   - Each route declares its parent group (`null` for standalone, `"documents"`, `"tutors"`, `"admin"`, `"user"`).
   - `App.jsx` renders `<Route>` elements from this config.

3. **Rename routes and labels**
   - `/ai-tutor` → `/chat`, label "Chat".
   - `/files` → `/documents`, label "Documents".
   - `/model-catalog` → `/admin/embedding-models`, label "Embedding Models".
   - `/interaction-model` → `/admin/ai-models`, label "AI Models".
   - `/runtime-engines` → `/admin/ai-providers`, label "AI Providers".
   - `/role-management` → `/admin/roles`, label "Role Management".
   - Add `/profile` route, label "Profile".
   - Add `/documents/upload` route (renders `FilesPage` with upload mode).

4. **Update `FilesPage.jsx` for Documents rename**
   - Change UI text from "Files" to "Documents".
   - Accept an `uploadMode` prop or route param to pre-open the upload dialog.
   - Keep all existing CRUD, folder, and vectorization behavior.

### Phase 2 — Grouped Menus (Web Dev)

5. **Implement Documents dropdown**
   - Bootstrap `Dropdown` with "Documents" toggle.
   - Sub-items: "Browse" (`/documents`), "Upload" (`/documents/upload`).
   - Active state reflects which sub-item is current.

6. **Implement Tutors dropdown**
   - Sub-items: "Tutors" (`/tutors`), "Benchmarking" (`/tutors/benchmarking`), "Tutor Setup" (stub, `#` or `/tutors/setup` with a placeholder page).
   - Benchmarking route moves under `/tutors/` prefix.

7. **Implement Admin dropdown**
   - Wrapped in `isAdmin` check; hidden for non-admin users.
   - Sub-items: "Role Management", "Embedding Models", "AI Models", "AI Providers".
   - All sub-routes under `/admin/` prefix.

8. **Implement User dropdown**
   - Replace the email + Logout button area.
   - Show user email as the dropdown toggle.
   - Sub-items: "Profile" (`/profile`), "Settings" (`/settings`), "Logout".
   - Logout action remains the existing `dispatch(logout())`.

### Phase 3 — Profile Page (Web Dev)

9. **Create `src/ProfilePage.jsx`**
   - Read-only view: display user email and roles from Redux `authSlice`.
   - Simple card layout consistent with existing page styles.
   - No new API calls; data is already in client state.

10. **Create `src/Profile.css`**
    - Minimal styles for the profile card.
    - Consistent with `Dashboard.css` and existing page styling.

### Phase 4 — Polish And Validation (Web Dev)

11. **Responsive behavior**
    - Verify dropdowns work in the collapsed mobile navbar.
    - Bootstrap's `Navbar.Collapse` handles this; verify sub-dropdowns are accessible.

12. **Active state styling**
    - Ensure the active dropdown group is visually indicated.
    - Active sub-item highlights within the dropdown.

13. **Run validation**
    - `npm run lint` — no new errors.
    - `npm run build` — clean build.
    - Manual browser test of all nav paths, role-based visibility, and responsive behavior.

### Phase 5 — Review (Lead Dev)

14. **Lead Dev review**
    - Navigation component extraction and route config pattern.
    - Route re-parenting correctness (all guards still apply).
    - Naming consistency with Domain vocabulary.
    - No hardcoded URLs, no new packages, no lint regressions.

---

## Risks And Mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| Route re-parenting breaks bookmarked URLs | Medium | This is a PoC; bookmarked URLs are not a production concern. Document the change. |
| Admin route prefix `/admin/*` conflicts with backend `/api/admin/*` | Low | No conflict — frontend routes are client-side SPA routes; backend routes are `/api/*`. Verified against current controller routes. |
| Dropdown active state is unclear | Medium | Use Bootstrap's active class on the toggle when any child is active; test visually. |
| Mobile dropdown accessibility | Low | Bootstrap Dropdown is keyboard-accessible; verify with manual testing. |
| Scope creep into Documents folder UI | Medium | Folder navigation is already in `FilesPage.jsx` (folder list exists). This iteration only adds the Browse/Upload split; folder UI improvements are a follow-up. |
