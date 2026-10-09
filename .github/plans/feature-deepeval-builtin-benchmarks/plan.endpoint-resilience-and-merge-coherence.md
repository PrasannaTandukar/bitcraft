---
created_at: 2026-10-08
edited_at: 2026-10-08
creator: planner
editor: planner
ai_gen: true
branch: feature/deepeval-builtin-benchmarks
status: active
cil: medium
css: 4
dis: 5
---

# Sub-Plan: Provider Endpoint Resilience and Merge Coherence

## Goal
Make OpenAI-compatible chat, diagnostic probe, and embedding endpoints resilient to both `/v1` and root routes without breaking existing configurations. Review and consolidate incoming `origin/dev` merge changes across all `Tutor*` backend components for strict coherence with Microsoft Agent Framework (MAF).

## Non-Goals
- Modifying Microsoft Agent Framework core packages or removing `ChatClientAgent`.
- Adding new provider types to UI dropdowns.
- Reworking benchmark engines or DeepEval schemas.
- Re-introducing deleted legacy generation controllers or providers.

## Context and Evidence
1. **Endpoint Normalization Discrepancy**:
   - `RuntimeChatClientFactory.OpenAiEndpoint` forcibly appends `/v1` unless the endpoint ends with `/v1` or `/chat/completions`.
   - The diagnostic probe (`ProbeAvailableModelsAsync`) calls `GetModelsAsync()`, which queries `<Endpoint>/models` (resolving to `<host>/v1/models`). When an OpenAI-compatible server (e.g. Infinity, llama.cpp, custom proxies) serves at the root without a `/v1` route prefix, the probe fails with HTTP 404.
   - `OpenAiCompatibleEmbeddingProvider.BuildEmbeddingsUri` only preserves root `/embeddings` if explicitly provided.
2. **Merge State**:
   - Merge from `origin/dev` brings PR #64 (Explainable RAG Provenance: `TutorMessageProvenance`, `TutorRuntimeReadinessEvaluator`) into the branch.
   - A stray duplicate method closing brace in `TutorRuntimeFeatures.cs` was identified and cleaned up.
   - All 570 unit tests pass on .NET 10.
3. **Clean Architecture Compliance**:
   - Verified: Domain and Application have zero references to `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, `OpenAI`, or `OllamaSharp`.

## Classification and Risk
- **CSS**: 4 (isolated external service endpoint normalization + diagnostic probe fallback)
- **DIS**: 5 (affects `RuntimeChatClientFactory`, `OpenAiCompatibleEmbeddingProvider`, and diagnostic tests)
- **CIL**: Medium (`.github/config/cil-medium.yaml`)
- **Review Route**: Lead Dev plus specialist review

---

## Work Steps (Strict STE)

### WS1: Add Fallback Probe for Root-Route Models Endpoint
- **Area Skill:** `bitcraft-backend-feature`
- **Files:**
  - `src/Bitcraft.Infrastructure/ExternalServices/Agents/RuntimeChatClientFactory.cs`
- **Action:**
  - In `ProbeAvailableModelsAsync`, if the primary probe to `<Endpoint>/v1/models` returns HTTP 404 or connection failure for an OpenAI-compatible provider, attempt a fallback probe to `<Endpoint>/models`.
  - Return the successful model catalog from either route.
- **Validation:**
  - Run `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj --filter FullyQualifiedName~ModelCatalogProbe`.
  - Expected: All probe unit tests pass.

### WS2: Normalize Embedding Endpoint for Direct and Prefixed Routes
- **Area Skill:** `bitcraft-backend-feature`
- **Files:**
  - `src/Bitcraft.Infrastructure/ExternalServices/OpenAI/OpenAiCompatibleEmbeddingProvider.cs`
- **Action:**
  - Update `BuildEmbeddingsUri` so it supports:
    1. URIs ending in `/embeddings` (use as-is).
    2. URIs ending in `/v1` or `/v1/` (append `embeddings`).
    3. Root URIs without `/v1` (support both direct `/embeddings` and `/v1/embeddings` via fallback or explicit configuration).
- **Validation:**
  - Run `dotnet test tests/Bitcraft.InfrastructureIntegrationTests/Bitcraft.InfrastructureIntegrationTests.csproj --filter FullyQualifiedName~OpenAiCompatibleEmbeddingProvider`.
  - Expected: All embedding provider tests pass.

### WS3: Add Unit Tests for Both Route Flavors
- **Area Skill:** `bitcraft-test-hardening`
- **Files:**
  - `tests/Bitcraft.UnitTests/Infrastructure/Agents/RuntimeChatClientFactoryTests.cs`
  - `tests/Bitcraft.InfrastructureIntegrationTests/Embeddings/OpenAiCompatibleEmbeddingProviderTests.cs`
- **Action:**
  - Add test cases for root-routed endpoint `http://localhost:7997` without `/v1`.
  - Add test cases for standard `/v1`-prefixed endpoint `http://localhost:7997/v1`.
  - Verify that both return valid models and generate embeddings without exceptions.
- **Validation:**
  - Run `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`.
  - Expected: 100% pass with new test cases included.

### WS4: Verify MAF Backend Coherence and Provenance Wiring
- **Area Skill:** `bitcraft-backend-feature`
- **Files:**
  - `src/Bitcraft.Application/Features/Tutoring/TutorRuntime.cs`
  - `src/Bitcraft.Application/Features/Tutoring/TutorRuntimeReadinessEvaluator.cs`
  - `src/Bitcraft.Application/Features/Tutoring/TutorMessageProvenanceCapture.cs`
- **Action:**
  - Verify `TutorRuntime.cs` correctly passes `TutorMessageProvenance` through `ITutorAgentExecutor` without leaking MAF types into the Domain entity or Application records.
  - Verify `TutorRuntimeReadinessEvaluator` correctly validates both LLM dialect and Embedding dialect via `ToLlmDialect()` and `ToEmbeddingDialect()`.
  - Remove any dead or duplicate helper methods identified during merge review.
- **Validation:**
  - Run `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj --filter FullyQualifiedName~TutorMessageProvenance`.
  - Run `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj --filter FullyQualifiedName~CleanArchitecture`.
  - Expected: All provenance and Clean Architecture tests pass.

---

## Acceptance Criteria (Strict STE)

1. The diagnostic probe succeeds against servers with route `/v1/models` and servers with route `/models`.
2. The embedding provider generates embeddings against endpoints with `/v1/embeddings` and endpoints with `/embeddings`.
3. `TutorRuntime` captures message provenance for active conversations without errors.
4. Clean Architecture tests confirm zero forbidden package references in Domain and Application.
5. `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` passes completely with zero failures.

## Open Human Decisions
None. All changes are backward-compatible and preserve existing configuration contracts.

## Delegation Route
- Implementation: **Lead Dev** via `bitcraft-develop`
- Review: **Lead Dev** plus **Sec Ops** (provenance and external HTTP transport verification)
