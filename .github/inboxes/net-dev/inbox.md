---
created_at: 2026-05-19
edited_at: 2026-05-20
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# NET Dev Inbox

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: net-dev
- Priority: high
- Task: Implement the centralized ABAC policy evaluator (D1) with `Document.Visibility` as the first resource attribute (D1.a). Add the typed enum (`Internal` default, `Public`/`Restricted` reserved) on `Document`, wire the evaluator into Application handlers via `Result<T>`, and add unit tests covering the default-Internal path and reserved-value guards.
- Context: feature-freeze-2026-05-21 WS1/WS4, D1, D1.a, **D24** (evaluator location: `src/Bitcraft.Application/Common/Authorization/IPolicyEvaluator`; Infrastructure adapters under `Bitcraft.Infrastructure/Authorization/`); part of the **D25** 8-step migration sequence (steps 1–2: Identity-And-Roles-Vocabulary → Resource-Membership-Tables). Coordinate with Sec Ops on the policy contract shape before locking the evaluator interface.
- Acceptance/Cleanup: Evaluator interface lives in `src/Bitcraft.Application/Common/Authorization`; `Document.Visibility` enum on Domain; unit tests pass; remove this item.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: net-dev
- Priority: high
- Task: Land the `RuntimeEngine` aggregate (D11) and surface the provider enum per D11.a: surfaced values `Ollama`, `OpenAI`, `OpenAICompatible`, `vLLM`, `LlamaCpp`; reserved-not-surfaced `AzureOpenAI`, `Anthropic`, `Bedrock`. Include the EF Core configuration, `EnableEmbeddingModelCommand` / `DisableEmbeddingModelCommand` updates that read the deployment-level ANN strategy setting per D5.a, and a migration scaffold.
- Context: feature-freeze-2026-05-21 WS3, D5, D5.a, D11, D11.a; part of the **D25** 8-step migration sequence (steps 3–4: RuntimeEngine-Entity → Model-Tph-Hierarchy). Pairs with the proposed `bitcraft-ef-tph-migration` skill once scaffolded.
- Acceptance/Cleanup: Aggregate + enum land with EF mapping; commands read the ANN strategy setting; unit tests on enable/disable paths pass; remove this item.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: net-dev
- Priority: medium
- Task: Implement the `SaveChanges` auditing interceptor (D10) for create/update/delete on persisted aggregates, with `CapturedById` linked to `AppUser`. No PII payloads; structural metadata only. Add unit + infrastructure-integration tests.
- Context: feature-freeze-2026-05-21 WS8, D10; part of the **D25** 8-step migration sequence (step 8: Benchmark-Fk-Realign, alongside which auditing interceptor coverage lands). Coordinate with Sec Ops review before merging.
- Acceptance/Cleanup: Interceptor registered in Infrastructure DI; integration test confirms audit rows on a representative aggregate; remove this item.
