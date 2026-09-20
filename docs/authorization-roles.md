---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: human
editor: lead-dev
ai_gen: false
---
# Authorization Roles

Authorization uses three roles only: `Admin`, `Editor`, and `Auditor`.

## Roles

- `Admin`: manages system-level configuration, model catalog entries, role assignments, and admin-only health checks.
- `Editor`: creates and updates tutors, source documents, prompts, knowledge links, tools, deployment profiles, model connections, and snapshots.
- `Auditor`: reads owned tutors/documents and performs tutor-scoped knowledge searches.

## Policies

- `AdminOnly`: requires `Admin`.
- `EditorOrAdmin`: requires `Editor` or `Admin`.
- `AuditorOrAbove`: requires `Auditor`, `Editor`, or `Admin`.

Policy and role names are centralized in `Bitcraft.Application.Common.Security` so controllers and handlers do not repeat raw role strings.

## Endpoint Matrix

| Area | Policy | Notes |
|---|---|---|
| Auth register/login | Anonymous | Tokens include current user roles. |
| Admin role management | `AdminOnly` | Assigns/revokes existing seeded roles. |
| Admin model catalog | `AdminOnly` | Includes model health checks. |
| Documents read/list | `AuditorOrAbove` | Application handlers still enforce document ownership. |
| Documents create/update/delete/vectorize | `EditorOrAdmin` | Application handlers still enforce ownership. |
| Knowledge search | `AuditorOrAbove` | Search remains tutor-scoped and owner-checked. |
| Tutors read/list | `AuditorOrAbove` | Application handlers still enforce tutor ownership. |
| Tutor create/update and sub-resources | `EditorOrAdmin` | Includes prompts, tools, model connections, knowledge links, deployment profiles, and snapshots. |
| Tutor revisions read/list | `AuditorOrAbove` | Scoped to a specific tutor `{tutorId}`. |
| Tutor revision mutations (create, publish, activate, archive, discard) | `EditorOrAdmin` | Includes all state-machine transitions. |
| Document folders (admin grouping) | `EditorOrAdmin` | Administrative organization only; folders are never visible at tutor runtime. |
| Standardized benchmark suites and runs (read) | `AuditorOrAbove` | `GET /api/standard-benchmark-runs`, `.../suites`, `.../{runId}`, `.../{runId}/progress`. |
| Standardized benchmark run launch | `AdminOnly` | `POST /api/standard-benchmark-runs` queues a model-scoped DeepEval run. |
| Health check | Anonymous | `GET /api/health` returns `{ status, version }`. |


## Seeding

The API seeds `Admin`, `Editor`, and `Auditor` roles at startup through ASP.NET Core Identity. Seeding is idempotent and does not create users or auto-promote the first registered user. A bootstrap admin account is provisioned only when both `BITCRAFT_ADMIN_EMAIL` and `BITCRAFT_ADMIN_PASSWORD` environment variables are set; otherwise the seeder logs a warning and skips that step.

## Manual Validation

1. Apply migrations and run the API.
2. Register or log in to get a JWT.
3. Assign a role to the target user through an Admin-authenticated request.
4. Log in again so the new JWT includes role claims.
5. Verify unauthorized requests return `401`, wrong-role requests return `403`, and correct-role requests succeed.

## Security Notes

- Role claims are issued into JWTs at login/register time. Changing a user's roles requires a new login/token to observe the updated claims.
- Ownership checks remain in Application handlers and are not replaced by role policies.
- No startup path auto-assigns `Admin`; a human/admin flow must grant roles deliberately.
- Vectorization accepts the `Ollama` embedding provider only. Additional embedding providers need a provider dispatcher behind the existing Application port.
