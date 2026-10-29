---
created_at: 2026-09-30
edited_at: 2026-09-30
creator: planner
editor: planner
ai_gen: true
branch: feature/deepeval-builtin-benchmarks
status: active
cil: high
css: 7
dis: 7
---

# Sub-Plan: Dev Integration & MAF Architecture Realignment

## Goal
Reintegrate `origin/dev` (containing PR #44 Microsoft Agent Framework and PR #48 pipeline stages) into `feature/deepeval-builtin-benchmarks` with zero conflict markers, zero broken build seams, and complete coherence with MAF architecture.

## Non-Goals
- Modifying Microsoft Agent Framework core packages or removing `ChatClientAgent`.
- Re-introducing deleted legacy generation controllers (`/api/ollama/generate`, `/api/rag`, `/api/tutor/ask`).
- Reworking benchmark suites themselves (MMLU, GSM8K, Arduino skeletons remain intact).

## Context & Past Traps
1. **Ancestry Trap**: `git merge origin/dev` previously reported "Already up to date" because commit `2b6d6bd` was reverted by `06a4515`, leaving `dev` in git history ancestry.
2. **Execution Choice**: Option 1 (`git revert 06a4515`) is a non-destructive forward commit that re-applies the merge content cleanly and brings MAF into the working tree for explicit conflict resolution.
3. **Seam Coherence**: Benchmark generation on dev already uses `ITutorBenchmarkOutputGenerator` delegating to `ITutorRuntime`. We align benchmarking to use that seam and reconcile `ApiKeySecretRef` vs `ApiKey`.

## Classification & Metrics
- CSS: 7 (touches Benchmarking, Modeling/RuntimeEngine, TutorRuntime, EF Migrations, Frontend App.jsx)
- DIS: 7 (EF snapshot, DI container, secrets resolution)
- Change Impact Level (CIL): **high** (requires structured validation, specialist review before merge to dev)

## Work Steps

### WS1: Revert Revert Commit to Restore MAF Diff
- **Area Skill:** `bitcraft-devops-release`
- **Command:** `git revert 06a4515 --no-edit`
- **Action:** Restore the incoming MAF merge content onto the branch as a forward git commit.
- **Validation:** `git status` shows uncommitted or committed merge state; confirm presence of `src/Bitcraft.Infrastructure/ExternalServices/Agents/TutorAgentExecutor.cs`.

### WS2: Resolve Conflict Markers & Align Interfaces
- **Area Skill:** `bitcraft-backend-feature`
- **Files:**
  - `src/Bitcraft.Application/Features/Tutoring/TutorRuntime.cs`
  - `src/Bitcraft.Api/Mapping/TutoringMapping.cs`
  - `src/Bitcraft.Application/Common/Models/BenchmarkingModels.cs`
  - `src/Bitcraft.Application/Common/Models/TutorRuntimeModels.cs`
  - `src/Bitcraft.Application/Features/Admin/InteractionModelFeatures.cs`
  - `src/Bitcraft.Application/Features/Admin/RuntimeEngineFeatures.cs`
  - `src/Bitcraft.Infrastructure/ExternalServices/CompositeModelProviders.cs`
  - `src/Bitcraft.Infrastructure/ExternalServices/OpenAI/OpenAiCompatibleEmbeddingProvider.cs`
  - `tutor-studio/src/App.jsx`
- **Action:**
  - In `TutorRuntime.cs`: Accept the dev branch implementation using `ITutorAgentExecutor`. Discard obsolete inline `RetrieveAsync` (now in `TutorKnowledgeContextProvider`).
  - In `CompositeModelProviders.cs` & deleted providers: Discard obsolete deleted files or clean remaining references.
  - In `RuntimeEngine.cs`: Retain `dev` standard (`ApiKeySecretRef`) and update call sites in benchmark runner.
  - In `App.jsx`: Preserve dev route structure while including standard benchmarking routes.
- **Validation:** `git grep -n '^<<<<<<<'` returns zero matches across the entire repo.

### WS3: Harmonize Benchmarking with MAF Seams & Clean Architecture
- **Area Skill:** `bitcraft-backend-feature`
- **Files:**
  - `src/Bitcraft.Application/Bitcraft.Application.csproj`
  - `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs`
  - `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/StandardBenchmarkRunWorker.cs`
  - `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/DeepEvalBenchmarkEngine.cs`
- **Action:**
  - Ensure `Bitcraft.Application.csproj` has no forbidden package references (`Microsoft.Extensions.Options` refactored if present).
  - Wire standard benchmark run output generation through `ITutorBenchmarkOutputGenerator` / `ITutorRuntime`.
  - Resolve runtime engine credentials via `IRuntimeApiKeyResolver` / `IConfiguration`.
- **Validation:** `dotnet build bitcraft.slnx` compiles with 0 errors and 0 warnings.

### WS4: EF Core Snapshot & Migration Reconciliation
- **Area Skill:** `bitcraft-devops-release`
- **Files:**
  - `src/Bitcraft.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- **Action:**
  - Ensure all migrations from both dev and feature branch are intact and valid.
  - Re-generate or reconcile `AppDbContextModelSnapshot.cs` to reflect both MAF tables (`TutorConversation`, safe tools) and benchmarking tables (`StandardBenchmarkRun`).
- **Validation:** `dotnet build src/Bitcraft.Infrastructure/Bitcraft.Infrastructure.csproj` succeeds and `dotnet ef migrations list --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api` succeeds.

### WS5: Test Suite Verification
- **Area Skill:** `bitcraft-test-hardening`
- **Files:**
  - `tests/Bitcraft.UnitTests`
  - `tests/Bitcraft.SidecarIntegration`
- **Action:**
  - Run all unit tests. Fix any mocked `ILlmGenerationProvider` in older tests to use the new `ITutorRuntime` or `ITutorAgentExecutor` test fakes.
- **Validation:** `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` passes 100%.

### WS6: Frontend Validation
- **Area Skill:** `bitcraft-frontend-feature`
- **Files:**
  - `tutor-studio/`
- **Action:**
  - Run linter and production build on `tutor-studio`.
- **Validation:** `npm run lint` and `npm run build` succeed in `tutor-studio/`.

## Open Human Decisions
- None. Strategy follows Option 1 (revert-of-revert forward commit) and standard MAF seam integration.
