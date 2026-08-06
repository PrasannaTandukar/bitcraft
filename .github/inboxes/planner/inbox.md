---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: planner
ai_gen: true
---
# Planner Inbox

- Date: 2026-05-21
- Status: complete (plan delivered to plan.md v3.6 2026-05-21)
- Source: Secretary (Human Decision session 2026-05-21)
- Target: planner
- Priority: high (blocking development start)
- Task: Produce a pre-final 3-iteration PoC development plan for Human review and confirmation before automated final-planning and development begins. The plan must cover all remaining work to deliver a fully functional and testable PoC. Human has locked all decisions D29–D41; see `plan.decisions.md` for the full register.

  **Constraints:**
  - 2–3 iterations maximum (D30). Target 3 iterations.
  - TypeScript deferred post-PoC (D29). All frontend work is plain JavaScript/JSX.
  - Human reviews the pre-final plan and confirms before final-planning + development begins (D30).
  - Human must be given the list of all planned libraries/frameworks that need to be installed so they can run `npm install` before development starts (D28 + Additional 4).
  - Lint/quality fixes deferred to Iteration 3's quality pass (D34).
  - All backend functionality from D25 steps 1–8 must be surfaced in the frontend (D36).

  **Iteration shape (recommended — Planner adjusts):**
  - **Iteration 1 — Foundation & Preflight**: API client centralization (D40), EditorRoute fix (D35), DocumentResponse split (D33), register form drift, vite.config.js process fix, CSP in nginx.conf (D32), dev/prod env file structure (D41), appsettings.Development.json for host-shell EF migrations (HD-11), Net Dev M-2 IsPermit() + IDOR guard + health endpoint gate.
  - **Iteration 2 — Feature Parity**: All missing frontend surfaces for D25 backend (TutorRevision CRUD workflow, TutorDocument M:N attach/detach, DocumentFolder flat-list component in Files page, embedding model selector, TutorMember management, activation slug display, BenchmarkRun revision linkage, Models page aligned to TPH contracts, ABAC concrete evaluator + policy catalog, grant/revoke membership endpoints).
  - **Iteration 3 — Integration, Quality & Freeze**: Benchmarking shell (real data), end-to-end smoke path validation, lint green gate, Dashboard stale route fix, change-notes.md and architecture.md updates, Sec Ops SaveChanges interceptor review, dev-ops compose smoke tests.

  **Key decisions affecting plan shape:**
  - D29 (no TS), D30 (2-3 iter), D31 (EditorOrAdmin + flat folders), D32 (localStorage accepted), D33 (doc DTO split), D34 (lint deferred), D35 (EditorRoute), D36 (feature parity), D37 (folders as component), D38 (slug from title), D39 (M:N cohesion), D40 (no hardcoded URLs), D41 (dev/prod separation).
  - All prior D0a–D28 decisions remain in effect.

  **Output format:**
  - `plan.yaml` revision under `.github/plans/feature-freeze-2026-05-21/` (update version, add new locks, reflect D29 superseding D18).
  - `plan.md` updated open decisions table (all are now resolved; add v3.6 resolution note).
  - `plan.frontend.md` updated to remove TS-now content and reflect D29/D30.
  - Concise iteration table in `plan.md` covering the 3 iterations with acceptance criteria, owner, and CIL.
  - Library install list for Human (npm packages to install before Iteration 1 dev starts).
  - Per-agent inbox items created for each iteration's work (Web Dev, NET Dev, Dev Ops, Sec Ops).

- Context: All Human Decisions locked in plan.decisions.md D29–D41. Inboxes have been updated by Secretary with Iteration 1 preflight items. Reviews in `.github/reviews/2026-05-21-frontend-catchup.md` and `.github/reviews/2026-05-21-frontend-framework-selection.md` contain the detailed frontend state.
- Acceptance/Cleanup: Pre-final plan delivered; Human has confirmed or iterated; Secretary presents final decision overview before development begins. Remove this item after Human confirms the plan.


- Status: parked
- Source: AI Ops
- Target: planner
- Priority: medium
- Task: Plan the first metrics capture implementation after the human chooses capture location and thresholds.
- Context: Use `.github/governance/METRICS.md`; avoid adding automation until the capture surface is approved.
- Acceptance/Cleanup: Plan identifies files, schema, validation, and delegation; remove or replace this item with implementation inbox items.
