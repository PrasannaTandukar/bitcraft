---
created_at: 2026-09-19
edited_at: 2026-09-19
creator: planner
editor: lead-dev
ai_gen: true
---
# Sub-Plan: Outstanding Items — WS5, Review Remediation, Deployment Cleanup, Dataset Cache

> **SUPERSEDED 2026-09-19** by `plan.branch-closeout.md`, which folds the review lane's findings
> into fixed work streams, adds the WS-X dead-code disposition, and captures decisions OD-7..OD-9.
> Kept as history; do not update this file.

> Origin: Planner, 2026-09-19. All human decisions are captured and FINAL (see
> `plan.provider-consolidation-cleanup.md` → "Human Decisions Captured", and `plan.decisions.md`
> O-1/O-2 resolutions). This plan converts them plus the completed review findings into work
> streams. No new human decisions are required to start WS5, WS-R, WS-D, or WS-C.

## Goal

Close every open item on `feature/deepeval-builtin-benchmarks` so the branch is merge-ready:
hosted OpenAI standard benchmarks can authenticate (WS5), review findings are remediated (WS-R),
dead deployment configuration is removed (WS-D), and benchmark datasets survive container
rebuilds (WS-C).

## Verified Current State (evidence)

- `StandardBenchmarkFeatures.cs:212` sends `ApiKeyEnv: null`; hosted OpenAI runs fail at
  `deepeval_llm_benchmarks.py:121-123` ("OpenAI hosted models require `api_key_env`...").
- `StandardLlmBenchmarkRequest` (`BenchmarkingModels.cs:62-70`) carries `ApiKeyEnv`; the sidecar
  judge schema (`schemas.py:63-72`) carries `api_key_env` and its docstring claims "the key value
  itself is never part of the request" — both must change under the final WS5 decision.
- `DeepEvalBenchmarkEngine.cs:115` builds the sidecar payload with `api_key_env`.
- Review (completed 2026-09-19, no Critical/High findings): M1 embedding vocabulary asymmetry
  (`EmbeddingProviders.cs:5-8` omits `vLLM`; `KnowledgeFeatures.cs:169-176` rejects vLLM
  vectorization while `TutorRuntime.cs:454` accepts vLLM search); L1 probe fallback drift +
  duplicated `ParseProviderType` (`CompositeModelProviders.cs:44-92`); L2 legacy Anthropic rows
  render raw `101` (`RuntimeEngines.jsx:13`); L4 missing composite alias tests + sidecar
  anthropic-rejection pytest; 4 STE semicolon violations; N3 duplicated handover headings.
- Dead deployment config: `OLLAMA_BASE_URL`/`OLLAMA_EMBED_MODEL` set on the backend service in
  `docker-compose.yml:65-66`, `docker-compose.ai.yml:49-51`, `docker-compose.ai.jetson.yml:73-75`;
  `ModelProviders__Ollama__BaseUrl` in `docker-compose.prod.yml:13`; commented examples in
  `.env.example:17-20`. The backend reads none of these after OD-4. Note: `OLLAMA_EMBED_MODEL`
  and `OLLAMA_CHAT_MODEL` are ALSO consumed by the `ollama-pull-*` helper services
  (`docker-compose.ai.yml:26,41`, `docker-compose.ai.jetson.yml:47,68`) — those usages stay.
- No cache volume exists for the sidecar; `sidecar/Dockerfile` installs with `--no-cache-dir` and
  mounts nothing, so DeepEval datasets re-download into the container filesystem on every rebuild.
- `plan.decisions.md` O-1 (named cache volume) and O-2 (judge = engine under test) are resolved.

## Non-Goals

- No secret store, encryption, or storage-semantics change (locked: raw key at rest).
- No Tier-2 LLM-judge tool registration (deferred; O-2 records the default for when it resumes).
- No FallbackPolicy change (human deferred), no `main.jsx` lint fix (human deferred).
- No new providers, no enum renumbering, no HumanEval.
- No Git writes; commit/push stays human-owned.

## Assumptions

- The sidecar transport (local UDS, `Benchmarking__DeepEval__SocketPath`) is trusted-internal; the
  human-approved WS5 decision states the key may transit it but never the network or logs.
- DeepEval uses the default HF/dataset cache under the container user's home (typically
  `~/.cache`); the exact path must be confirmed during implementation before naming the volume.

## Work Streams

### WS5 — Hosted OpenAI benchmark key (CIL High)
- Replace `ApiKeyEnv` in `StandardLlmBenchmarkRequest` with `ApiKey` (raw value); set it from
  `model.RuntimeEngine.ApiKey` in `StandardBenchmarkFeatures.cs` (Ollama engines send null).
- Sidecar: rename `api_key_env` → `api_key` in `schemas.py` (`SidecarJudgeModelConfig`), update
  `_resolve_api_key` (`deepeval_llm_benchmarks.py:97`) to read the value, update the hosted guard
  (`:121-123`), and amend the header comment (`:32-34`) + schema docstring to state the new
  policy: key transits the local UDS only, never the network, never logs.
- `DeepEvalBenchmarkEngine.cs:115`: send `api_key` instead of `api_key_env`.
- Docs: amend `docs/transparency-and-privacy.md` (sidecar/credential statements, ~lines 70-77).
- Tests: sidecar pytest for key-in-payload behavior + hosted guard; backend unit test asserting
  the engine key reaches the request; keep the anthropic-rejection pytest from WS-R/L4.
- Owner: lead-dev; Sec Ops review required (CIL High). Validation: build, unit, API/infra
  integration, sidecar pytest.

### WS-R — Review findings remediation (CIL Medium)
- M1: add `vLLM` to `EmbeddingProviders.All` (or route `KnowledgeFeatures` validation through
  `ToEmbeddingDialect` directly — pick one, do not keep both lists authoritative).
- L1: decide composite probe fallback for blank/unparseable provider type (restore null or
  document Ollama fallback as intentional); extract the duplicated `ParseProviderType`.
- L2: add display labels for reserved provider values in `RuntimeEngines.jsx` so legacy
  Anthropic rows show "Anthropic", not `101`.
- L4: composite routing test rows for OpenAICompatible/vLLM/LlamaCpp → OpenAI provider; sidecar
  pytest pinning the `provider="anthropic"` rejection.
- STE: remove semicolons in `RuntimeEngineProviderType.cs:51`, `AnthropicGenerationProvider.cs:10`,
  `RuntimeEngineConfiguration.cs:27`, `Contracts/Admin/RuntimeEngines.cs:6`.
- N3: de-duplicate the "### 1. WS2" headings in the consolidation sub-plan Frontend Handover.
- Owner: lead-dev. Validation: build, unit tests, sidecar pytest, frontend build.

### WS-D — Dead deployment config cleanup (CIL Medium, Sec Ops territory)
- Remove backend-service env vars the backend no longer reads: `OLLAMA_BASE_URL`,
  `OLLAMA_EMBED_MODEL` from `docker-compose.yml:65-66`, `docker-compose.ai.yml:49-51`,
  `docker-compose.ai.jetson.yml:73-75`; `ModelProviders__Ollama__BaseUrl` from
  `docker-compose.prod.yml:13`; stale commented examples from `.env.example:17-20`.
- KEEP `OLLAMA_EMBED_MODEL`/`OLLAMA_CHAT_MODEL` where the `ollama-pull-*` helper services consume
  them (`docker-compose.ai.yml:26,41`; `docker-compose.ai.jetson.yml:47,68`).
- Update stale references in `docs/local-setup.md`, `docs/transparency-and-privacy.md`, `README.md`.
- Owner: sec-ops (guidance) + lead-dev (edit); human approves compose changes. Validation:
  `docker compose config` parse check per touched file; doc consistency grep.

### WS-C — DeepEval dataset cache volume (CIL Medium, Sec Ops territory)
- Add a named volume (e.g. `benchmark_dataset_cache`) to the `benchmark-sidecar` service in
  `docker-compose.yml` (~lines 82-100), mounted at the confirmed DeepEval cache path.
- Confirm the path during implementation (sidecar runs as root by default → `/root/.cache`).
- Owner: sec-ops (guidance) + lead-dev (edit). Validation: `docker compose config`; one manual
  run-then-rebuild check that datasets persist (human or local environment).

## Sequencing

1. **WS-R first** (smallest, unblocks clean review state; no dependencies).
2. **WS5 second** (independent of WS-R; needs Sec Ops review before merge).
3. **WS-D + WS-C together in one Sec Ops pass** (both touch compose; single review cycle).
4. Human: EF scratch-migration awareness check (N1 — next `dotnet ef migrations add` yields an
   operationally empty snapshot refresh), manual UI spot check, then commit/push.

WS5 and WS-R may proceed in parallel if staffed separately; both must land before the Sec Ops
pre-merge review so it sees the final state.

## CSS, DIS, CIL (whole plan)

- CSS: 5 — shared application behavior (benchmark request path) + deployment configuration.
- DIS: 6 — Application, Infrastructure, sidecar, compose, docs, tests (no Domain/Api contract
  change beyond the request record).
- CIL: **High** (WS5 is credential-adjacent; DIS 6-7). Protocol `.github/config/cil-high.yaml`:
  Sec Ops review for WS5; human approval for compose edits.

## Validation Plan

- `dotnet build bitcraft.slnx` — 0 errors.
- Unit + API integration + infrastructure integration suites — all pass.
- Sidecar pytest (conda env) — all pass, including new WS5/L4 tests.
- `npm run build` in `tutor-studio/` — exit 0 (L2 label change).
- `docker compose config` for each touched compose file — parses clean.
- Human: EF scratch-migration check, manual UI spot check, commit/push.

## Open Human Decisions

None new. All decisions are captured (WS5 transport + `api_key_env` removal; O-1 cache volume;
O-2 judge default; deferred items list). The L1 probe-fallback choice (restore null vs document
Ollama fallback) is an implementation-level call delegated to lead-dev within the review finding's
two options — flag the choice in the WS-R completion note.
