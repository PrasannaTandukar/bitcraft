---
created_at: 2026-09-18
edited_at: 2026-09-18
creator: planner
editor: lead-dev
ai_gen: true
---
# Context: n_problems, Provider Fixes, and Evaluation Research (v1.1)

Human-approved scope for this iteration on `feature/deepeval-builtin-benchmarks`.

## Locked decisions (human, 2026-09-18)

1. Standard benchmark wiring: full async (new entity + queue worker + API + wizard). `n_problems` default 10; 0 = full dataset.
2. HumanEval: excluded from selection, shown disabled with "requires code execution sandbox — deferred" note.
3. Provider dropdown: fix probe bugs only. Do not add OpenAICompatible/vLLM/LlamaCpp to the UI dropdown this round.
4. API key: preserve-on-null semantics. Explicit Clear control. Honest doc/UI text. Raw-key storage stays for now.
5. deepeval: raise floor to `>=4.2,<5.0`. Verify non-breaking via sidecar tests.
6. Tasks 3–5: research/options docs only, no implementation. Task 5 = Arduino options doc (G-Eval vs DAG vs custom metric). No smoke test code this round.

## Grounded findings driving the work

- Standard benchmark path is dead-ended: sidecar `/internal/tools/llm-benchmark` + `EvaluateStandardLlmBenchmarkAsync` have zero production callers. UI "standard" mode runs preset cases through the tutor case-based pipeline. `nShots` UI state is never sent.
- `n_problems` exists nowhere in the repo. Sidecar gates ctor kwargs via `inspect.signature` (version-safe seam).
- `RuntimeEngineFeatures.cs` update handler unconditionally overwrites `ApiKeySecretRef` (null wipes it). API responses never return the ref. `ModelCatalogManager.jsx` always probes with a null key.
- `ApiKeySecretRef` is used as the raw key by providers. No secret store exists despite doc claims.
- Probe bugs: early `Array.Empty` return in OpenAI-compatible probe; swallowed errors; UI discards backend diagnostics; misleading placeholders.
- No observability: no `/metrics`, no OTel, chat usage discarded, no chat-turn persistence.
- Suite list drift: sidecar 7 keys / UI 6 keys / no backend registry.

## Phases

1. Phase 1: provider save (preserve-on-null + Clear) and model discovery probe fixes with error surfacing. Sec Ops review required (credential-adjacent).
2. Phase 2: sidecar `n_problems`, deepeval floor bump, `StandardBenchmarkRun` entity + migration, Application features + canonical suite registry, API controller, frontend wizard + results view, tests.
3. Phase 3: research docs under `.github/research/` — DeepEval gap analysis, observability options, Arduino benchmark options. Human decision gates.
4. Phase 4: full validation sweep, review pass, change notes.

## Boundaries

- Excluded: HumanEval execution, provider dropdown expansion, secret store/encryption, Anthropic model listing, Grafana implementation, Arduino smoke test code, chat-turn telemetry.
- EF migration apply requires human approval. Git writes stay human-owned.
