---
created_at: 2026-09-19
edited_at: 2026-09-19
creator: planner
editor: lead-dev
ai_gen: true
---
# Consolidated Plan: Branch Close-Out — WS5, Review Remediation, Deployment Cleanup, Dataset Cache

> Supersedes `plan.outstanding-items.md` (v1, 2026-09-19) by folding the completed review lane's
> findings into fixed work streams and capturing the remaining human decisions. All prior human
> decisions stand (see `plan.provider-consolidation-cleanup.md` → "Human Decisions Captured" and
> `plan.decisions.md` O-1/O-2). Status: **executed 2026-09-19 — WS-R, WS5, WS-D, WS-C complete
> and validated; human sign-off 2026-09-19: OD-7 and OD-8 approved per recommendations; OD-9
> deferred (Microsoft Agent Framework assessed separately).**

## Execution Record (2026-09-19, lead-dev)

**WS-R — complete.** M1: `KnowledgeFeatures` validates through `ToEmbeddingDialect()` only;
`EmbeddingProviders.cs` and its tests deleted; `TutoringFeaturesTests` uses the `"Ollama"` literal.
L1: probe returns null for unparseable provider type; blank stays documented legacy-Ollama; one
shared `ParseProviderType`. L2: `PROVIDER_FILTERS` in `RuntimeEngines.jsx` includes reserved values
(Anthropic 101, AzureOpenAI 100, Bedrock 102, OpenAICompatible 2, vLLM 3, LlamaCpp 4); dropdown
unchanged. L4: composite alias routing tests (Ollama/OpenAI dialects, unparseable → null,
unsupported dialects → no request) + sidecar anthropic-rejection pytest. STE: four semicolons
removed. N3: duplicated WS2 headings removed from the consolidation sub-plan.

**WS5 — complete.** `StandardLlmBenchmarkRequest.ApiKey` (was `ApiKeyEnv`) carries
`model.RuntimeEngine.ApiKey`; `DeepEvalBenchmarkEngine` payload field `api_key`; sidecar schema
`api_key` with UDS-only policy docstring; `_resolve_api_key` removed, hosted guard reads the value
directly; provider validation added before evaluation in `run_llm_benchmark`; privacy doc amended.
New tests: backend unit (engine key reaches request), sidecar pytest (anthropic rejection, hosted
guard via direct `_SidecarJudgeLLM._call`).

**WS-D — complete.** Dead backend-service env vars removed from `docker-compose.yml`,
`docker-compose.ai.yml`, `docker-compose.ai.jetson.yml`, `docker-compose.prod.yml`,
`.env.example`, `docs/local-setup.md`, and `README.md` (human-confirmed edit). `ollama-pull-*`
consumptions and the `OLLAMA_EMBED_DIMENSIONS` doc row kept.

**WS-C — complete.** Named volume `benchmark_dataset_cache` mounted at
`/root/.cache/huggingface` on `benchmark-sidecar` + top-level volume declaration.

**Validation (2026-09-19):** build 0 errors; unit 406/406; API integration 37/37; infrastructure
integration 28/28; sidecar pytest 18/18 (conda env); `npm run build` exit 0; `docker compose
config` clean for base, ai, and jetson files. Pre-existing, not a regression: `docker-compose.prod.yml`
alone fails `config` ("backend has neither an image nor a build context") — verified against the
pre-change file; the prod overlay inherits image/build from the base file by design.

**Remaining (human-only):** EF scratch-migration check (N1), manual UI spot check, Sec Ops
pre-merge review (WS5 credential transport + WS-D compose changes), commit/push.

Suggested commit message:

```
benchmarks: close out branch — api_key transport, review remediation, compose cleanup

- WS5: pass stored engine ApiKey to sidecar over local UDS (api_key replaces api_key_env);
  provider validation before evaluation; privacy doc amended
- WS-R: dialect-map-only embedding validation (EmbeddingProviders deleted), probe null for
  unparseable provider type, reserved-value labels in RuntimeEngines, composite alias tests,
  sidecar anthropic-rejection pytest, STE comment fixes
- WS-D: remove dead backend-service Ollama env vars from compose/env/docs
- WS-C: benchmark_dataset_cache volume for the sidecar HuggingFace dataset cache
```

## Goal

Make `feature/deepeval-builtin-benchmarks` merge-ready: hosted OpenAI standard benchmarks
authenticate (WS5), all review findings are remediated (WS-R), dead deployment configuration is
removed (WS-D), benchmark datasets survive container rebuilds (WS-C), and the two dead-code
candidates surfaced during plan verification are dispositioned (WS-X).

## Verified Current State (evidence, re-verified 2026-09-19)

**WS5 surface**
- `StandardBenchmarkFeatures.cs:212` sends `ApiKeyEnv: null`; hosted OpenAI runs fail at
  `deepeval_llm_benchmarks.py:121-123`.
- `StandardLlmBenchmarkRequest` (`BenchmarkingModels.cs:62-70`) carries `ApiKeyEnv`;
  `DeepEvalBenchmarkEngine.cs:115` sends `api_key_env`; sidecar schema `schemas.py:63-72` carries
  `api_key_env` with a docstring claiming the key value is never part of the request.
- Sidecar integration tests build judge payloads without any key field
  (`test_sidecar_api.py:245-249`); no pytest pins the anthropic rejection.

**Review findings (from the completed review lane; no Critical/High)**
- M1: `EmbeddingProviders.cs:5-8` omits `vLLM` from `All`. `KnowledgeFeatures.cs:169-176`
  normalizes the provider label through that list, then re-checks the dialect map — so a vLLM
  engine fails vectorization with `Knowledge.EmbeddingProviderUnsupported` while
  `TutorRuntime.cs:454` accepts vLLM for search. Two routing vocabularies remain.
- L1: composite probe falls back blank/unparseable provider type → Ollama
  (`CompositeModelProviders.cs:44-52,60-62,88-92`); `ParseProviderType` is duplicated in both
  composite classes.
- L2: `RuntimeEngines.jsx:13` builds `PROVIDER_LABELS` from the trimmed dropdown list; legacy
  Anthropic rows (101) render the raw number and lose their filter entry.
- L4: no composite routing tests for OpenAICompatible/vLLM/LlamaCpp aliases; no sidecar pytest
  for the `provider="anthropic"` rejection.
- STE: semicolons in `RuntimeEngineProviderType.cs:51`, `AnthropicGenerationProvider.cs:10`,
  `RuntimeEngineConfiguration.cs:27`, `Contracts/Admin/RuntimeEngines.cs:6`.
- N1: `AppDbContextModelSnapshot.cs` still says `ApiKeySecretRef`; next migration is an
  operationally empty snapshot refresh.
- N3: duplicated "### 1. WS2" headings in the consolidation sub-plan handover section.

**Dead deployment config**
- Backend-service env vars no longer read after OD-4: `OLLAMA_BASE_URL`/`OLLAMA_EMBED_MODEL` in
  `docker-compose.yml:65-66`, `docker-compose.ai.yml:49-51`, `docker-compose.ai.jetson.yml:73-75`;
  `ModelProviders__Ollama__BaseUrl` in `docker-compose.prod.yml:13`; commented examples in
  `.env.example:17-20`; doc rows in `docs/local-setup.md:106,108`.
- KEEP: `OLLAMA_EMBED_MODEL`/`OLLAMA_CHAT_MODEL` consumed by `ollama-pull-*` helper services
  (`docker-compose.ai.yml:26,41`; `docker-compose.ai.jetson.yml:47,68`) and the
  `OLLAMA_EMBED_DIMENSIONS` doc row (`docs/local-setup.md:107`) — that value is set by humans in
  `.env` to match the pgvector column, not read from backend code; the doc row stays accurate.
- No cache volume for the sidecar; datasets re-download on every container rebuild.

**Dead-code candidates found during plan verification (new, WS-X)**
- `ITutorMemoryStore` (`Application/Common/Interfaces/ITutorMemoryStore.cs`) is registered
  (`DependencyInjection.cs:133`) and implemented (`PgvectorTutorMemoryStore.cs`), but **no
  production code calls it** — `TutorRuntime` has no memory dependency; the only consumer of the
  legacy `/api/tutor/ask` memory flags (`TutorAskFeatures.cs:88-89`) returns a fixed
  "not enabled" message; `TutorAnswerResult.MemorySaved` is always `false`. A `FakeTutorMemoryStore`
  exists in unit-test fakes but is never constructed. The `TutorMemories` table (migration
  `20260515182249_AddTutorMemories`, `vector(768)` column in `TutorMemoryConfiguration.cs:27`) is
  persisted schema with no writer.
- `TutorRuntimeExecutionMode.Benchmark` is set only by `TutorBenchmarkOutputGenerator.cs:31`;
  `TutorRuntime.cs:255,297` branches on it (revision-status relaxation for benchmark runs). This
  is live behavior — NOT dead. Recorded here to close the review question; no action.

## Non-Goals

- No secret store, encryption, or storage-semantics change (locked).
- No Tier-2 LLM-judge registration (deferred; O-2 default recorded for when it resumes).
- No FallbackPolicy change, no `main.jsx` lint fix (human-deferred).
- No new providers, no enum renumbering, no HumanEval.
- No Git writes; commit/push stays human-owned.

## Assumptions

- The sidecar UDS transport is trusted-internal; the approved WS5 decision allows the key value
  in that payload, never on the network, never in logs.
- DeepEval dataset cache lives under the sidecar container user's home (root → `/root/.cache`);
  confirm at implementation before naming the volume.

## Work Streams

### WS5 — Hosted OpenAI benchmark key (CIL High; lead-dev; Sec Ops review)
1. `BenchmarkingModels.cs`: `ApiKeyEnv` → `ApiKey` (raw value) in `StandardLlmBenchmarkRequest`.
2. `StandardBenchmarkFeatures.cs:212`: send `model.RuntimeEngine.ApiKey` (null for Ollama).
3. `DeepEvalBenchmarkEngine.cs:115`: payload field `api_key` instead of `api_key_env`.
4. `schemas.py`: `api_key_env` → `api_key`; docstring states the new policy (key transits the
   local UDS only, never the network, never logs).
5. `deepeval_llm_benchmarks.py`: `_resolve_api_key` (`:97`) reads the value; hosted guard
   (`:121-123`) requires it; header comment (`:32-34`) amended.
6. `docs/transparency-and-privacy.md` (~lines 70-77): sidecar credential statements amended.
7. Tests: backend unit test asserting the engine key reaches the request; sidecar pytest for
   key-in-payload + hosted guard; keep the anthropic-rejection pytest (WS-R/L4).
- Validation: build, unit, API/infra integration, sidecar pytest.

### WS-R — Review findings remediation (CIL Medium; lead-dev)
- **M1 (per OD-7)**: route `KnowledgeFeatures` provider validation through the dialect map only —
  `Enum.TryParse` on the raw input (case-insensitive), then `ToEmbeddingDialect() != Unsupported`;
  delete the `EmbeddingProviders` string list and its `Normalize`; update the
  `Knowledge.EmbeddingProviderUnsupported` message to name the supported enum values; update
  `EmbeddingProvidersTests` and the `TutoringFeaturesTests` provider-label tests accordingly.
  The stored `KnowledgeChunk.EmbeddingProvider` string keeps the canonical enum name.
- **L1 (per OD-8)**: probe returns null for unparseable provider type (restore pre-consolidation
  semantics); blank stays Ollama (documented legacy path); extract one shared `ParseProviderType`.
- **L2**: add a reserved-value label map (Anthropic → "Anthropic") in `RuntimeEngines.jsx` for
  display and filtering; dropdown stays Ollama + OpenAI.
- **L4**: composite InlineData rows for the three OpenAI aliases; sidecar anthropic-rejection pytest.
- **STE**: rewrite the four flagged comments as separate sentences (no semicolons).
- **N3**: de-duplicate the handover headings in the consolidation sub-plan.
- Validation: build, unit, sidecar pytest, frontend build.

### WS-D — Dead deployment config cleanup (CIL Medium; sec-ops guidance + lead-dev edit)
- Remove the backend-service env vars and doc rows listed in Verified Current State; keep the
  `ollama-pull-*` consumptions and the `OLLAMA_EMBED_DIMENSIONS` doc row.
- Validation: `docker compose config` per touched file; grep for stale references.

### WS-C — DeepEval dataset cache volume (CIL Medium; sec-ops guidance + lead-dev edit)
- Named volume `benchmark_dataset_cache` mounted at the confirmed cache path in the
  `benchmark-sidecar` service (`docker-compose.yml:82-100`).
- Validation: `docker compose config`; one run-then-rebuild persistence check (local env).

### WS-X — Dead-code disposition (CIL Low; DEFERRED per OD-9)
- **Human decision 2026-09-19: defer.** The long-term-memory direction is being assessed as part
  of a separate Microsoft Agent Framework evaluation. Keep `ITutorMemoryStore`,
  `PgvectorTutorMemoryStore`, the DI registration, the `TutorMemories` table, and the legacy
  memory request flags untouched on this branch. No code changes. Revisit after the framework
  assessment concludes; if the memory feature is built on the new framework, the dead seam and
  the unused table can be removed in that dedicated plan.

## Sequencing

1. WS-R (smallest; unblocks clean state).
2. WS5 (independent; parallel with WS-R if staffed).
3. WS-D + WS-C in one Sec Ops pass (both touch compose).
4. WS-X: no work — deferred per OD-9 (Microsoft Agent Framework assessment owns the direction).
5. Human: EF scratch-migration check (N1), manual UI spot check, Sec Ops pre-merge review, commit/push.

## CSS, DIS, CIL (whole plan)

- CSS: 5 — shared application behavior (benchmark path, knowledge validation) + deployment config.
- DIS: 6 — Application, Infrastructure, sidecar, compose, docs, tests.
- CIL: **High** (WS5 credential-adjacent). `cil-high.yaml`: Sec Ops review for WS5; human approval
  for compose edits and OD-7/OD-8/OD-9.

## Validation Plan

- `dotnet build bitcraft.slnx` — 0 errors.
- Unit + API integration + infrastructure integration — all pass.
- Sidecar pytest (conda env) — all pass, including new WS5/L4 tests.
- `npm run build` in `tutor-studio/` — exit 0.
- `docker compose config` per touched compose file — parses clean.
- Human: EF scratch-migration check, manual UI spot check, commit/push.

## Resolved Human Decisions (all final)

| # | Question | Decision |
|---|---|---|
| OD-7 | M1 fix shape | **(a) Dialect-map-only** (approved per recommendation 2026-09-19): route `KnowledgeFeatures` validation through `ToEmbeddingDialect`; delete the `EmbeddingProviders` string list. |
| OD-8 | L1 probe fallback | **(a) Null for unparseable** (approved per recommendation 2026-09-19): restore fail-closed probe semantics; blank stays the documented legacy-Ollama path. |
| OD-9 | WS-X dead memory store | **Deferred** (human, 2026-09-19): out of scope for this branch. The long-term-memory direction is assessed separately under the Microsoft Agent Framework evaluation. No code changes. |

Prior decisions all captured: WS5 transport + `api_key_env` removal; O-1 cache volume; O-2 judge
default; README edit confirmed; main.jsx lint left; FallbackPolicy deferred; Arduino research
deferred; Sec Ops review queued pre-merge.
