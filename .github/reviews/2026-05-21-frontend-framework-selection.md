---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: secretary
editor: secretary
ai_gen: true
---
# Frontend Framework Selection — Approach Analysis

> **Audience**: Human reviewer + Web Dev.
> **Purpose**: Lay out concrete approach options for completing the Tutor Studio frontend stack. Two strategy envelopes are analysed: **Additive only** (keep everything, only add) and **Replace/Remove** (can swap or remove libraries). Locked decisions (D14–D20, D18, D18.a) are not re-debated — they are treated as constraints.

---

## Context & Constraints

**Already locked (not open for debate):**
- React 19 + Vite 8 stays.
- TypeScript migration: **now**, order Settings → ModelCatalog → Tutors → Files → Benchmarking.
- TanStack Query v5 (server state), React Hook Form + Zod (forms), TanStack Table v8 (data tables) — **locked pending Human approval of O4**.
- Vitest + Testing Library scaffolded now.
- OpenAPI codegen deferred (D27); hand-written TS clients from Zod schemas until post-PoC.

**Current stack baseline:**
```
react@19, react-dom@19, vite@8
bootstrap@5.3, react-bootstrap@2.10
react-router-dom@latest (v6/v7)
redux@5, @reduxjs/toolkit@2, react-redux@9
@svar-ui/react-filemanager@2.5
eslint flat config
(no TypeScript, no test runner, no forms lib, no table lib, no server-state cache)
```

---

## Envelope A — Additive Only

**Rule**: Zero existing dependencies are removed or replaced. Every new capability is layered on top of what exists.

### A1 — Minimum viable addition (locked D14/D15/D16 set)

Add only what is required to close the largest gaps. This is the baseline that the locked decisions already describe.

**Add:**
| Package | Why | Learner note |
|---------|-----|-------------|
| `@tanstack/react-query` | Server-state cache: auto-refreshes stale data, deduplicates parallel requests, gives each resource a cache key. Eliminates most `useEffect(() => fetch(...))` patterns. | Think of it as a smart memoized fetch with built-in loading/error states. |
| `react-hook-form` | Form state without re-rendering every keystroke. Handles complex multi-field forms (Revision editor) cheaply. | The form registers inputs by `ref` instead of `state`, so React never re-renders until submit or explicit validation. |
| `zod` + `@hookform/resolvers` | Schema-driven validation. The same Zod schema describes the TypeScript type **and** the runtime validation rule. | Zod: `z.string().min(1)` means "this field is a required string, and the TS type is `string`." One definition, two uses. |
| `@tanstack/react-table` | Headless table primitive: sorting, filtering, pagination. Renders with react-bootstrap `<Table>`. | "Headless" means no default CSS; you control the markup. Sorting logic is handled; the table still looks like your design system. |
| `typescript` (dev) + `@typescript-eslint` | Enables the TypeScript migration. `allowJs: true` means existing `.jsx` files still work during transition. | TypeScript catches mismatches between what the backend sends and what the frontend expects at compile time, not at runtime. |
| `vitest` + `@testing-library/react` (dev) | Test runner compatible with Vite. No separate Jest config needed. | Tests run in the same Vite pipeline as the build — no config duplication. |
| `eslint-plugin-jsx-a11y` (dev) | Accessibility lint rules in the editor, not a runtime check. | Flags issues like missing `aria-label` on icon buttons before they reach users. |
| `@axe-core/react` (dev-only) | Runs accessibility audits in the browser console during development. | Only active when `process.env.NODE_ENV === 'development'`. Zero production overhead. |

**Keep as-is:**
- `redux + @reduxjs/toolkit` — narrowed scope to auth token, theme, and persistent nav state.
- `react-bootstrap 5.3` — design system stays.
- `react-router-dom` — routing stays.
- `@svar-ui/react-filemanager` — physical upload surface only (see O4 open decision).

**What you get:** Closes all four major gaps (server state, forms, tables, TypeScript) with the minimum set. Clean migration path. No churn to existing pages during transition.

**What you don't get:** No design system upgrade, no file manager replacement for the many-to-many Folders UX. `@svar-ui` limitations around non-filesystem folder semantics remain.

**Risk:** Adding five production libraries at once is a meaningful scope. Mitigation: each library is opt-in per page; existing pages continue to work until rewritten.

---

### A2 — Additive + lightweight UX polish

Same as A1, plus small additions for the known UX gaps. Does NOT replace anything.

**Additional adds (on top of A1):**
| Package | Why | Tradeoff |
|---------|-----|----------|
| `react-hot-toast` or `react-toastify` | Lightweight toast notifications. Currently Tutor Studio has no feedback system for async mutations (activate revision, vectorize, etc.). | Small bundle (~5 KB); opinionated styling — needs CSS override to match Bootstrap theme. |
| `date-fns` | Human-friendly date formatting for `CapturedUtc`, `CreatedUtc` columns. | Smaller than `moment`; tree-shakes well. Alternatively use `Intl.DateTimeFormat` natively with no package. |

**When to choose A2:** If Web Dev wants to ship polished async feedback without waiting for a design system upgrade.

---

## Envelope B — Replace / Remove

**Rule**: Existing dependencies can be removed or replaced if a better-fit alternative exists. Still constrained by locked decisions (React 19, Bootstrap design system for the freeze, etc.).

### B1 — Replace Redux with Zustand (narrowed scope store)

**Replace:** `redux` + `@reduxjs/toolkit` + `react-redux` → **Zustand**

**Why consider it:** Redux Toolkit is well-suited for large normalized state. Our actual remaining Redux scope after TanStack Query takes over server state is small: auth token, theme, maybe a navigation flag. Zustand delivers this in ~30 lines with zero boilerplate. RTK Query (the server-cache option inside Redux Toolkit) is explicitly less ergonomic than TanStack Query for our revision-heavy UI (D14).

**What you gain:** Fewer packages, simpler mental model for newcomers, no action/reducer ceremony for simple flags.

**What you lose:** Redux DevTools are more capable; existing `store/` slices need rewriting; any state-related unit tests need updates.

**How to do it safely:** Keep Redux for the freeze, schedule Zustand migration as a separate post-freeze PR. Do not mix the two.

**Learner note:** Zustand stores are just JavaScript objects with setter functions — no actions, no reducers, no dispatch. A full auth store is: `const useAuth = create(set => ({ token: null, setToken: token => set({ token }) }))`.

---

### B2 — Replace `@svar-ui/react-filemanager` with TanStack Table + custom upload widget

**Replace:** `@svar-ui/react-filemanager` → custom Documents table using TanStack Table (already added in A1) + a `<input type="file">` or `react-dropzone` upload widget.

**Why consider it:** `@svar-ui` is designed for single-parent filesystem UX. Our `DocumentFolder` is a many-to-many virtual collection. Forcing the filemanager component to represent "a document belongs to folders A, B, and C simultaneously" (and "a tutor attaches a folder, not a file") is fighting the component's data model. A TanStack Table view with multi-select checkboxes and an "Add to Folder" action button matches our domain more directly.

**What you gain:** Consistent UI across all list pages (all use TanStack Table). Folder membership becomes a normal multi-select column, not a filesystem metaphor. No dependency on a specialized commercial-adjacent library.

**What you lose:** The rich drag-and-drop upload UX from `@svar-ui` needs to be reimplemented. The file browser visual is replaced by a plain table. Upload preview is no longer built-in.

**Alternative**: Keep `@svar-ui` only inside an "Upload" modal for drag-drop upload UX, and build the folder management surface separately with TanStack Table. This is the hybrid mentioned in plan.frontend.md §4.4 — best of both.

**Learner note:** `react-dropzone` is a minimal drop-zone hook (~15 KB) that handles drag events and file selection, leaving all visual rendering to you.

---

### B3 — Replace react-bootstrap with Mantine (full design system replacement)

**Replace:** `bootstrap` + `react-bootstrap` → **Mantine 7** (with built-in theme, form components, modals, tables).

> ⚠️ **Explicitly deferred post-freeze per D20.** Including here only for completeness. Do not start this during the freeze window.

**Why consider it post-freeze:** Mantine provides a full-featured component library (forms, modals, tables, date pickers, notifications, file dropzone) with a dark-mode ready theme and better TypeScript support than react-bootstrap. Fewer component libraries needed overall.

**What you lose in the transition:** Every existing page needs restyling. All `react-bootstrap` component imports change. Bootstrap CSS utility classes no longer apply. This is a freeze-breaking scope.

**When to decide:** After at least the TypeScript migration is complete and the revision UI is shipped. At that point the codebase is typed, which makes the import-swap safer to do with confidence.

---

## Summary Recommendation

| Approach | Packages changed | Risk | When to use |
|----------|-----------------|------|------------|
| **A1** (locked libraries, add only) | +8 dev/prod adds | Low | Start here. Implements all locked decisions. |
| **A2** (A1 + toast + date-fns) | +2 prod adds | Very low | Add when shipping async mutation UI. |
| **B1** (Redux → Zustand) | −3, +1 | Medium (existing tests) | Post-freeze, separate PR. |
| **B2** (svar-ui → TanStack Table + dropzone) | −1, +1 | Low–Medium | Decide via O4. Hybrid option (keep svar-ui for upload modal only) is lowest risk. |
| **B3** (react-bootstrap → Mantine) | −2, +1 | High | Post-freeze only, after TS migration complete. |

**Immediate recommendation (unblocked by Human approving O4):**
Proceed with **A1 + hybrid B2** (keep `@svar-ui` inside the upload modal; build folder management on TanStack Table). This closes all gaps, matches locked decisions, and avoids the svar-ui many-to-many UX problem without a full replacement.

**For a later iteration:**
B1 (Zustand) and B3 (Mantine) as discrete post-freeze PRs when the TypeScript migration is complete.
