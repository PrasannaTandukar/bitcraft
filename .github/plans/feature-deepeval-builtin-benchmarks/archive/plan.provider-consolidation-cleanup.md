---
created_at: 2026-09-19
edited_at: 2026-09-19
creator: planner
editor: lead-dev
ai_gen: true
---
# Sub-Plan: Provider Consolidation and Cleanup (Ollama + OpenAI)

> Origin: Planner, 2026-09-19. Human review: **approved 2026-09-19** — OD-1..OD-6 all per
> recommendations; work continues on `feature/deepeval-builtin-benchmarks`.

## Status (2026-09-19, lead-dev)

- **WS1 done**: `LlmDialect` + `RuntimeEngineProviderTypeDialect` in Domain; all 8 hand-maintained
  routing lists replaced; `EmbeddingProviders.Normalize` now validates through the shared dialect map.
- **OD-4 done**: env/config fallbacks removed from all 5 providers; `IConfiguration` dropped from
  provider constructors; `appsettings.json` `ModelProviders` trimmed to timeout keys only.
  Consequence: legacy `/api/ollama/generate` and `/api/rag/ask` endpoints (unused by frontend,
  backend.http, or docs) were deleted as dead code with their features, contracts, and tests.
- **WS2 done (incl. frontend)**: sidecar Anthropic dialect branch removed; `schemas.py` comment
  updated; Anthropic removed from the `PROVIDER_TYPES` dropdown in `RuntimeEngines.jsx`.
- **WS3 done (incl. frontend)**: `ApiKeySecretRef` → `ApiKey` across Domain/Application/
  Infrastructure/Api/tests and both frontend files. EF column pinned via
  `HasColumnName("ApiKeySecretRef")` — no migration. Frontend now sends `apiKey` / `clearApiKey`
  and reads `hasApiKey`; the temporary `JsonPropertyName` pins were removed from the contracts.
- **WS4 done (incl. frontend)**: stale "Step 4" comments fixed (controller + handler), dead
  `HealthCheckTimeoutSeconds` key removed, sidecar Anthropic branch removed, docs updated
  (`docs/architecture.md`, one-line factual fix in `README.md` for the deleted endpoint).
  Frontend: stale "Llama.CPP" copy fixed, `claude-*` popularity entries removed, unused
  `ExperimentOutlined` import removed, nested `tutor-studio/tutor-studio/` scaffold deleted.
- **Validation**: build 0 errors; unit 401/401; API integration 37/37 (re-run after wire-name
  change); infrastructure integration 28/28; sidecar pytest 15/15; frontend `npm run build`
  exit 0. `npm run lint` has one pre-existing error in `src/main.jsx` (react-refresh rule,
  unrelated to this change).
- **Open**: WS5 (hosted OpenAI benchmark key) — human decision captured 2026-09-19, spec final:
  option (a), pass the stored engine key value in the sidecar payload over the local Unix socket.
  New `api_key` field in the sidecar judge schema; `api_key_env` is removed (single key path,
  consistent with OD-4). The key transits the local UDS only, never the network, never logs.
  Amend the sidecar header comment and `docs/transparency-and-privacy.md` during implementation.
  Not yet implemented (deferred by human at session end). Sec Ops review required for WS3/WS5
  per CIL High. Compose files still set `OLLAMA_BASE_URL` / `OLLAMA_EMBED_MODEL` env vars the
  backend no longer reads — flagged for Sec Ops, not edited here.

## Human Decisions Captured (2026-09-19, open-items walkthrough)

| # | Item | Decision |
|---|---|---|
| 1 | WS5 key transport | **Option (a), spec final**: backend sends the stored `RuntimeEngine.ApiKey` value in the sidecar request payload over the local UDS. New `api_key` field in the sidecar judge schema; `api_key_env` removed (single key path). Backend sets it from the engine for hosted OpenAI runs. Key transits the local socket only, never the network, never logs. Sidecar comment + privacy doc amended during implementation. Implementation deferred to a later session. |
| 2 | `README.md` one-line edit (`ai_gen: false`) | **Keep** — human confirmed the factual removal of the deleted `/api/ollama/generate` row. |
| 3 | Pre-existing lint error in `tutor-studio/src/main.jsx` (react-refresh) | **Leave** — stays queued in the lead-dev inbox as a known pre-existing issue. |
| 4 | Sec Ops F4 follow-up (`FallbackPolicy = RequireAuthenticatedUser`) | **Defer** — stays queued in the planner inbox. |
| 5 | Sec Ops review of WS3 + dead compose env vars | Queued in planner inbox; to run before merge (human will trigger). |
| 6 | Arduino options research doc (Phase 3) | Deferred to its own session; not part of this branch. |
| 7 | Manual spot check + commit/push | Human-only, at handover. |

## Frontend Handover (approved 2026-09-19 — completed by lead-dev)

All items below were implemented and validated on this branch. Kept as the record of what changed.

### 1. WS2 — Remove Anthropic from the engine dropdown ✅
- `src/RuntimeEngines.jsx`: `PROVIDER_TYPES` now lists Ollama (0) and OpenAI (1) only.
- Existing Anthropic engine rows (if any) still load and display; they just cannot be created.

### 2. WS3 — Rename wire fields to match the backend ✅
- `src/RuntimeEngines.jsx`: form field / payload key `apiKeySecretRef` → `apiKey`; payload flag
  `clearApiKeySecret` → `clearApiKey`; response field read `hasApiKeySecret` → `hasApiKey`.
- `src/components/ModelCatalogManager.jsx`: probe payload `apiKeySecretRef: null` → `apiKey: null`.
- The three `JsonPropertyName` pins in `src/Bitcraft.Api/Contracts/Admin/RuntimeEngines.cs` were
  removed and its header comment updated. One integration test payload key was renamed to match.

### 3. WS4 — UI cleanup ✅
- `src/RuntimeEngines.jsx`: stale "Llama.CPP" copy replaced (name placeholder → "Local Ollama or
  OpenAI-compatible runtime"; key-extra text → "Optional for local Ollama.").
- `src/components/ModelCatalogManager.jsx`: `claude-*` entries removed from the popularity list;
  unused `ExperimentOutlined` import removed.
- Nested `tutor-studio/tutor-studio/` folder deleted.

### 4. Validation ✅
- From `tutor-studio/`: `npm run build` exit 0. `npm run lint` reports one pre-existing error in
  `src/main.jsx` (react-refresh/only-export-components) that predates this change — not fixed here.
- Backend re-validated after pin removal: build 0 errors; API integration 37/37.
- Manual drawer/probe check not performed (no running environment in this session).

## Goal

Consolidate the provider surface on two active runtime engine types — **Ollama** and **OpenAI** (one OpenAI
endpoint serves both hosted and self-hosted OpenAI-contract runtimes) — and remove the accumulated drift,
dead code, and naming ambiguity found in the sweep.

## Verified Current State (evidence)

### Provider-type routing is hand-maintained in 8 places with 3 different list shapes

| Site | List |
|---|---|
| `CompositeModelProviders.cs:30-36` GenerateAsync | OpenAI, OpenAICompatible, vLLM, LlamaCpp → OpenAI provider; Anthropic → Anthropic; else Ollama |
| `CompositeModelProviders.cs:54-58` Probe | same, minus Anthropic (probe returns null) |
| `CompositeModelProviders.cs:90-94` Embedding | OpenAI, OpenAICompatible, LlamaCpp → OpenAI (vLLM absent) |
| `TutorRuntime.cs:401-406` interaction | 6 values incl. Anthropic |
| `TutorRuntime.cs:461-464` embedding | 4 values (no vLLM, no Anthropic) |
| `TutorRuntimeFeatures.cs:95` | 6 values |
| `TutorRuntimeFeatures.cs:122` | 4 values |
| `InteractionModelFeatures.cs:226` test-image | 6 values |

`StandardBenchmarkFeatures.cs:89,199,259` already uses the target shape: Ollama/OpenAI only, others rejected.

### Enum and UI drift

- `RuntimeEngineProviderType`: Ollama=0, OpenAI=1 active; OpenAICompatible=2, vLLM=3, LlamaCpp=4 exist only
  as OpenAI aliases; AzureOpenAI=100 and Bedrock=102 are handled nowhere (dead values); Anthropic=101 has a
  full provider.
- Frontend dropdown (`RuntimeEngines.jsx:10-14`): offers Ollama, OpenAI, **Anthropic** — contradicts the
  Ollama+OpenAI scope; Anthropic engines are rejected by standard benchmarks.
- `docs/architecture.md:178` claims UI surfaces 5 providers (Ollama/OpenAI/OpenAICompatible/vLLM/LlamaCpp) —
  wrong in both directions (UI shows 3, one of which is Anthropic).
- UI copy references "Llama.CPP" (`RuntimeEngines.jsx:253,269-270`) — not a selectable type. Stale.
- `ModelCatalogManager.jsx:47-49` ranks `claude-*` models in the popularity list — stale under consolidation.

### Third provider vocabulary

`EmbeddingProviders.cs` (string constants: Ollama/OpenAI/OpenAICompatible/LlamaCpp + `Normalize`) feeds
`KnowledgeFeatures.cs:165-170` via `engine.ProviderType.ToString()`. Parallel to the enum; vLLM absent.

### ApiKeySecretRef misnomer (raw key at rest)

59 references in 32 files. The value is the raw API key (documented in `docs/architecture.md`), sent as
`Bearer`/`x-api-key` verbatim. Touchpoints: entity + EF config, 15+ historical migrations (immutable),
Application handlers/models, 5 Infrastructure providers, Api contracts/controller/mapping, 2 frontend files,
tests, docs.

### Dead code and staleness

- `RuntimeEnginesController.cs:106`: "Step 4 will introduce FK Restrict from Model rows" — the FK **already
  exists** (`ModelConfiguration.cs:36-38`). Stale comment.
- `appsettings.json:31`: `ModelProviders:Ollama:HealthCheckTimeoutSeconds` — read by no code. Dead key.
- Provider env/config fallbacks (`OPENAI_BASE_URL`, `OPENAI_CHAT_MODEL`, `ANTHROPIC_*`, `OLLAMA_*` in 5
  providers) bypass the RuntimeEngine entity — a parallel, ambiguous configuration path.
- `tutor-studio/tutor-studio/` nested folder: separate package.json (react-query, react-hook-form, zod,
  vitest) unused by the root app (redux + antd). Leftover scaffold.
- `ModelCatalogManager.jsx:1`: `ExperimentOutlined` imported, never used.
- Sidecar `deepeval_llm_benchmarks.py:137-146`: Anthropic dialect branch unreachable from the backend
  (`ToSidecarProvider` maps only ollama/openai; handlers reject the rest). Header comment at line 115 already
  says only Ollama and OpenAI are active.
- `ModelKind.Judge/Reranker` (100/101): reserved by design, TPH maps only Interaction/Embedding. Keep, note.
- `deepeval_metrics.py` LLM-judge tool: intentionally unregistered (`main.py:14-17`). Deferred by decision, not dead.
- `mcp-server/`: active (wired via `.vscode/mcp.json`, documented). Not dead.

### Gaps (intended but missing)

1. **Hosted OpenAI standard benchmarks cannot authenticate**: `StandardBenchmarkFeatures.cs:212` sends
   `ApiKeyEnv: null`; the sidecar requires `api_key_env` for `api.openai.com` (`deepeval_llm_benchmarks.py:121-123`).
   Runs fail with a clear error. Biggest functional gap; needs a secret-handling decision (Sec Ops).
2. No `FallbackPolicy` in authorization (Sec Ops F4, already queued).
3. No model discovery probe for Anthropic (if Anthropic is kept at all).

## Non-Goals

- No enum renumbering; persisted int values are schema-locked.
- No secret store or encryption (raw-key-at-rest stays, per locked decision).
- No unification of tutor-benchmark vs standard-benchmark history views (D45 separation is locked).
- No HumanEval enablement, no new providers.

## Options and Recommendations

| # | Question | Options | Recommendation |
|---|---|---|---|
| OD-1 | Enum values 2-4 (OpenAICompatible/vLLM/LlamaCpp) | (a) Keep values, collapse all routing to one shared helper that maps them to the OpenAI dialect; (b) remove values + data migration; (c) leave as-is | **(a)** — zero migration risk, kills the 8-list drift |
| OD-2 | Anthropic | (a) Remove from UI dropdown, keep provider code + enum as reserved; (b) delete provider code entirely; (c) keep UI option | **(a)** — matches scope, preserves schema-locked value; deletion is a separate cleanup if never wanted |
| OD-3 | `ApiKeySecretRef` name | (a) Rename property to `ApiKey`, EF-mapped to the existing column name (no migration); (b) rename + column migration; (c) keep name | **(a)** — honest naming, no schema churn |
| OD-4 | Provider env/config fallbacks | (a) Remove; RuntimeEngine is the only endpoint/key source; (b) keep as fallback | **(a)** — one source of truth; note the probe-path fallback was already removed for security |
| OD-5 | Dead items | nested `tutor-studio/tutor-studio/` folder, `HealthCheckTimeoutSeconds` key, stale "Step 4" comment, unused import, sidecar Anthropic branch, stale UI copy/docs | **Delete/fix all** — low risk, no behavior change |

## Work Streams

### WS1 — Provider routing collapse (backend)
- One normalization helper (Application or Domain): `ToLlmDialect(providerType)` → `ollama` | `openai` | `unsupported`.
- Replace all 8 hand-maintained lists; `EmbeddingProviders` string vocabulary folds into the same helper.
- Owner: lead-dev. CIL: medium. Acceptance: single routing source; all existing tests pass.

### WS2 — Anthropic scope-down (backend + frontend)
- Remove Anthropic from `PROVIDER_TYPES` dropdown; keep `AnthropicGenerationProvider` registered (reserved).
- Remove sidecar Anthropic dialect branch (or guard behind the same dialect map).
- Owner: lead-dev. CIL: medium. Acceptance: UI offers Ollama + OpenAI only; Anthropic engine rows still function if any exist.

### WS3 — ApiKeySecretRef rename (OD-3)
- Property rename with `HasColumnName("ApiKeySecretRef")` mapping; update all non-migration touchpoints.
- Owner: lead-dev. CIL: high (credential-adjacent naming; Sec Ops review). Acceptance: build green, no migration added, docs updated.

### WS4 — Dead-code and staleness sweep
- Items in OD-5 plus `docs/architecture.md:178` provider list, UI copy, popularity list, stale comment.
- Owner: lead-dev (code), secretary (docs). CIL: low. Acceptance: lint/build green; docs match reality.

### WS5 — Hosted OpenAI benchmark key (gap 1)
- Design how the sidecar receives the stored key for hosted runs (env-var name convention vs. direct value).
- Owner: sec-ops design + lead-dev implementation. CIL: high. **Blocked on human decision** (secret handling).

## CSS, DIS, CIL (whole plan)

- CSS: 5 — shared application behavior (tutor runtime routing), no credential-boundary change; WS3 is naming-only.
- DIS: 7 — Domain/Application/Infrastructure/Api + frontend + sidecar + docs.
- CIL: **High** (DIS 7). Protocol `.github/config/cil-high.yaml`: human plan review before implementation,
  small validated slices, Sec Ops review for WS3/WS5.

## Impacted Files (WS1-WS4 estimate)

- Modified: `CompositeModelProviders.cs`, `TutorRuntime.cs`, `TutorRuntimeFeatures.cs`,
  `InteractionModelFeatures.cs`, `KnowledgeFeatures.cs`, `EmbeddingProviders.cs` (or deleted),
  `RuntimeEngine.cs`, `RuntimeEngineConfiguration.cs`, `RuntimeEngines.cs` (contracts),
  `RuntimeEnginesController.cs`, `TutoringMapping.cs`, `RuntimeEngineFeatures.cs`, 5 provider files,
  `RuntimeEngines.jsx`, `ModelCatalogManager.jsx`, `deepeval_llm_benchmarks.py`, `docs/architecture.md`,
  `appsettings.json`, unit/integration tests.
- Deleted: `tutor-studio/tutor-studio/` (OD-5).
- No new migrations.

## Delegation Plan

| Stream | Primary | Supporting |
|---|---|---|
| WS1 routing collapse | lead-dev | — |
| WS2 Anthropic scope-down | lead-dev | — |
| WS3 rename | lead-dev | sec-ops review |
| WS4 sweep | lead-dev + secretary | — |
| WS5 benchmark key | sec-ops | lead-dev |

## Validation Plan

- `dotnet build bitcraft.slnx` — 0 errors.
- `dotnet test` unit + Api integration suites — all pass.
- Sidecar pytest — all pass.
- `npm run lint` + `npm run build` in `tutor-studio/`.
- Manual spot check: engine create/edit drawer offers Ollama + OpenAI only; probe works for both.

## Open Human Decisions

All resolved 2026-09-19 (all per recommendation; OD-6: continue on `feature/deepeval-builtin-benchmarks`).

| # | Question | Blocks |
|---|---|---|
| OD-1 | Enum treatment for values 2-4 | WS1 |
| OD-2 | Anthropic: UI removal vs code deletion | WS2 |
| OD-3 | ApiKeySecretRef rename approach | WS3 |
| OD-4 | Remove provider env/config fallbacks? | WS1 |
| OD-5 | Approve dead-item deletion list | WS4 |
| OD-6 | Branch: continue on `feature/deepeval-builtin-benchmarks` or new branch for this refactor? | All |
