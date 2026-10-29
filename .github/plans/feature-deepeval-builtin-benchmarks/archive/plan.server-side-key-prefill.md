---
created_at: 2026-09-18
edited_at: 2026-09-19
creator: planner
editor: lead-dev
ai_gen: true
---
# Sub-Plan: Server-Side Key Prefill for Read-Only Probes

> Origin: Planner, 2026-09-18. Human review: **complete — OD-1..OD-4 resolved 2026-09-18.** Implementation resumed after human decisions.

## Process Correction

Implementation started before human review of this plan. That violated `cil-high.yaml`
(`development_order: human review of plan.yaml before implementation starts`). A draft exists in
the working tree. Treat the draft as non-authoritative until the Open Human Decisions below are
resolved. Also disclosed: the planner ran one `git stash push` + `git stash pop` during pullback.
That was a Git write outside agent authority. It was reverted immediately and left no state.

## Goal

Read-only probe endpoints resolve the stored runtime-engine API key server-side. Saved engines
test connectivity and list provider models with their persisted credential. The key value never
reaches the client.

## Verified Current State

- One probe endpoint exists: `POST /api/admin/runtime-engines/test`
  (`RuntimeEnginesController.TestConnection`). It serves both call sites below.
- Call site 1: `tutor-studio/src/RuntimeEngines.jsx` "Test connection" drawer button.
- Call site 2: `tutor-studio/src/components/ModelCatalogManager.jsx` model discovery.
- `RuntimeEngineResponse` returns only `hasApiKeySecret` (bool). The key value is never returned.
  Both call sites therefore probe with a null key today. Hosted OpenAI probes fail unless
  `OPENAI_API_KEY` exists in the API process environment. Ollama probes work (no key needed).
- Authorization that exists today (predates the draft): controller
  `[Authorize(Policy = AdminOnly)]` plus handler check `currentUser.IsInRole(AppRoles.Admin)`.
- Frontend navigation for the three affected pages (`/admin/ai-providers`,
  `/admin/embedding-models`, `/admin/ai-models`) is `roles: ["Admin"]` only.
- `useRolePermissions` grants Editors `canWrite` on management surfaces. This hints the permission
  model may intend Editor access to model management. No code grants it today.

## Non-Goals

- Do not change storage semantics. The stored value stays the raw key at rest.
- Do not return the key value to any client.
- Do not add secret-store resolution or encryption.
- Do not change the OpenAI fallback to `ModelProviders:OpenAI:ApiKey` / `OPENAI_API_KEY`.
- Do not expand the provider dropdown beyond the locked decision (Ollama + OpenAI active).

## Draft In Working Tree (non-authoritative, matches Option A)

- `TestRuntimeEngineRequest`: field `ApiKeySecretRef` renamed to `ApiKeyValue`; optional
  `RuntimeEngineId` added.
- `TestRuntimeEngineHandler`: when the client key is blank and `RuntimeEngineId` is set, the
  handler loads the engine and uses the stored key. Client-supplied key still wins. Read is via
  `IRuntimeEngineRepository.GetByIdAsync`. No writes.
- Frontend: both call sites send `apiKeyValue` plus `runtimeEngineId`.
- Unit tests cover prefill semantics (blank preserves lookup, supplied key wins, unknown id falls
  back, Ollama unaffected).

## Options

| Option | Approach | Notes |
| --- | --- | --- |
| A | Prefill by engine id (draft) | Client sends id. Server resolves stored key when the client key is blank. |
| B | Prefill implicit, no id | Server matches endpoint + provider type against stored engines. Fragile. Rejected. |
| C | No prefill; require re-typing the key per probe | Safest. Worst usability. Model discovery cannot use it. |

Recommendation: **Option A**, pending the authorization decision below.

## Open Human Decisions — RESOLVED 2026-09-18

| # | Question | Decision |
|---|---|---|
| OD-1 | Probe authorization | Any authenticated user; endpoint-specific permission. Server resolves the stored key and returns probe results without exposing the key. |
| OD-2 | Page navigation scope | (a) The three model/admin pages stay Admin-only. |
| OD-3 | Field naming | Keep project convention: `ApiKeySecretRef` (no rename). **Superseded 2026-09-19** by consolidation OD-3 (`plan.provider-consolidation-cleanup.md`): property renamed to `ApiKey`; the EF column keeps the historical name. |
| OD-4 | Prefill scope | Moot: OD-1 already covers both call sites. |

## Impacted Files

Created: none.

Modified (draft already in tree):
- `src/Bitcraft.Api/Contracts/Admin/RuntimeEngines.cs`
- `src/Bitcraft.Api/Controllers/Admin/RuntimeEnginesController.cs`
- `src/Bitcraft.Application/Features/Admin/RuntimeEngineFeatures.cs`
- `tutor-studio/src/RuntimeEngines.jsx`
- `tutor-studio/src/components/ModelCatalogManager.jsx`
- `tests/Bitcraft.UnitTests/Application/Admin/RuntimeEngineFeaturesTests.cs`
- `tests/Bitcraft.UnitTests/Application/Admin/Fakes/FakeRuntimeEngineRepository.cs`

## CSS, DIS, CIL

- CSS: 7 — authorization and credential boundary. If the probe policy is wrong, a lower-privileged
  user can drive authenticated requests to external providers with a stored key.
- DIS: 5 — Api contract + Application handler + two frontend files + tests.
- CIL: **High** (CSS 7-8 row, credential-adjacent). Protocol: `.github/config/cil-high.yaml`.

## Delegation Plan

| Work Stream | Primary | Supporting | Notes |
|---|---|---|---|
| Human decisions OD-1..OD-4 | Human | Planner | Gate for all code work. |
| Implementation (Option A) | Lead Dev | — | Only after OD-1..OD-4. |
| Security review | Sec Ops | — | Mandatory specialist per cil-high. |
| Change notes | Secretary | — | After validation. |

## Validation Plan

- `dotnet build bitcraft.slnx` — zero errors.
- `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` — all pass, including new
  prefill tests.
- Authorization spot check per OD-1 outcome: policy test for the chosen role.
- Frontend `npm run lint` in `tutor-studio/`.
- Sec Ops review of the credential flow before handover.

## Risks

- Stored-key use on a probe widens credential usability if the role gate is too permissive.
  Mitigation: OD-1 decision plus policy test.
- Ollama sends no key; prefill must stay a no-op there. Covered by unit test.
- Probe config-key fallback: removed per Sec Ops F1 (see review outcome below).

## Sec Ops Review Outcome (2026-09-18)

Verdict: CHANGES REQUIRED → **F1 remediated same day.**

- **F1 (High, fixed):** The OpenAI provider's probe fell back to server-wide
  `ModelProviders:OpenAI:ApiKey` / `OPENAI_API_KEY` and attached it to caller-chosen endpoints.
  With the probe opened to all authenticated users (OD-1), that became a credential-steering
  path. Fix: `ProbeAvailableModelsAsync` now uses only the caller-supplied or endpoint-matched
  stored key. Generation keeps its config fallback (configured endpoints, not caller-chosen).
  Regression tests: `ProbeNeverAttachesServerConfiguredCredential`,
  `ProbeUsesCallerSuppliedKeyOnly`.
- **F2 (accepted risk, recorded):** The probe is a network reachability oracle (IsSuccess,
  LatencyMs, exception messages) available to all authenticated users. Bounded by the global
  10 req/s per-IP rate limiter. Accepted as part of OD-1.
- **F3 (info):** Prefill guard is exact-match after trim, case-insensitive; fails closed on
  trailing-slash or scheme differences. Pinned by `Test_WithEndpointNormalizationVariants_FailClosed`.
- **F4 (follow-up):** Add authorization `FallbackPolicy = RequireAuthenticatedUser` (queued in
  planner inbox).
- **F5 (info):** Raw-key-at-rest unchanged; no new persistence path.
