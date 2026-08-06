---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: planner
editor: planner
ai_gen: true
---
# Plan: Feature Freeze 2026-05-21

> Origin: Planner, 2026-05-19. Human review: revised with product POC decisions.
> Revision v2 by Lead Dev, 2026-05-20: production-grade ERM locked in [plan.erm.md](plan.erm.md); frontend work carved into [plan.frontend.md](plan.frontend.md); decisions in this file reconciled with `plan.yaml.locked_decisions`.
> Revision v3 by Planner, 2026-05-20: human-confirmed decision pass folded in (ABAC-throughout, single-tenant, Twelve-Factor, build-time runtime auth gate, configurable HNSW/IVFFlat, RuntimeEngine entity now, Document/Folder semantic re-frame, TypeScript-now). Open Decisions table reset; companion artifacts [plan.decisions.md](plan.decisions.md) (decision register), [docs/architecture.md](../../../docs/architecture.md) (living architecture report), and [docs/change-notes.md](../../../docs/change-notes.md) (release-batch change notes) are introduced and owned by Secretary/Typewriter.

## Goal

Freeze the Proof-of-Concept Product Core and Tutor Studio feature set by 2026-05-21 with minimal effective work: user/bootstrap template, environment/provider settings, runtime engine/model registry and configuration wizard, tutor-specific text document ingestion, vectorization with a selected embedding model, tutor configuration, local activation, a separate local runtime API for activated tutors, and a benchmarking UI shell. Deployment/export, production runtime security, and multi-agent runtime execution remain post-freeze work.

## Context Words

- **Product**: Bitcraft runtime app: Core API, Domain/Application/Infrastructure, Tutor Studio, sidecar, Documents, Knowledge, Models, Tutors, benchmarking, and future Product MCP/tutor tool connectors.
- **Dev Framework**: GitHub Copilot/Claude Code operating layer: `.github`, agent prompts, planning files, VS Code MCP, and development workflow. This plan does not change Dev Framework behavior unless explicitly stated.
- **Interaction Model**: LLM/VLM model used to interact with users, reason over prompts, and produce runtime answers. This is the main tutor-facing model.
- **Embedding Model**: Model used to turn documents and user questions into vectors for RAG. It is selected before vectorization and may be different from the interaction model.
- **Runtime Engine**: Product runtime host/provider configuration such as Ollama now, and future vLLM, llama.cpp, OpenAI-compatible APIs, or cloud providers. Engines host models; models are cataloged and assigned to tutors.
- **Model Assignment**: Tutor-specific binding between a tutor and one or more cataloged models. POC needs one interaction model and one embedding model. Future assignments can include default orchestrator plus expert models.
- **Tutor**: Product aggregate that binds prompts, selected model assignments, selected knowledge, activation/deployment metadata, Product connector metadata, and snapshots.
- **Activation**: Product operation that captures a runnable tutor configuration and exposes a deterministic local Core API runtime endpoint for testing, benchmarking, and diagnostics. It is not deployment/export.
- **Deployment**: Final publication/export/release of a tutor runtime to a target such as local package, external endpoint, cloud service, or standalone bundle. Deployment is out of scope for this POC freeze except for preserving the vocabulary and profile shape.
- **Management API**: Authenticated Tutor Studio API for settings, users, roles, documents, knowledge, models, tutors, activation, and benchmark setup.
- **Runtime API**: Separate API surface used by activated tutors to answer. For this POC it may be unauthenticated only when explicitly local-gated; it must not reuse management contracts or permissions.

## Non-Goals

- MediatR stays. No custom dispatcher, Native AOT work, FrozenDictionary optimization, or speed-focused stack replacement before freeze.
- No production release work. Production bootstrap scripts, real secrets, production endpoints, and deployment operations are human-owned and untracked.
- No production-grade unauthenticated runtime endpoint. Any unauthenticated runtime route before freeze is local-only, environment-gated, and not a production deployment policy.
- No production microservice split beyond existing sidecar and compose services.
- No PDF/DOCX/OCR ingestion, large file streaming, object storage, or folder ACL complexity before freeze.
- No Product MCP runtime tool execution before freeze. Product connector configuration can exist as metadata only.
- No Dev Framework MCP changes in this product plan. Existing agentic development framework remains as is.
- No broad new frameworks. New libraries are allowed only when they solve a real Product gap without duplicating existing functionality.
- No multi-provider remote model execution before freeze unless Human explicitly approves provider and secret handling.
- No multi-agent orchestration framework adoption before freeze. Future orchestrator/expert hierarchy should be represented in model metadata and assignments, not executed as autonomous agents yet.
- No advanced benchmarking statistics, LLM-as-judge, confidence intervals, or trace UI before freeze.
- No major Tutor Studio redesign. Keep current structure and visual language; add missing views and flows.

## Verified Current State

- Backend build currently fails in `src/Bitcraft.Infrastructure/Persistence/Interceptors/EntitySaveChangesInterceptor.cs` because `userId` is declared twice in overlapping scope.
- Identity roles, user/role admin endpoints, JWT auth, and frontend role management exist, but `IdentityRoleSeeder` currently creates a hardcoded admin user on every startup path.
- Current Product roles are `Admin`, `Editor`, and `Viewer` in `src/Bitcraft.Application/Common/Security/AppRoles.cs`; the requested POC vocabulary is `Admin`, `Editor`, `Auditor`, and reserved `User`.
- `docs/authorization-roles.md` says startup seeds roles only and does not create users, so docs and runtime behavior disagree.
- API surfaces exist for documents, vectorization, knowledge search, tutors, prompts, tools, knowledge links, model catalog, model connection, deployment profile, snapshots, benchmarking, and low-level Ollama generation.
- Current `Document` has no `TutorId`; documents are global owner-scoped source artifacts today, while the requested Product flow treats documents as tutor-specific.
- Current `TutorModelConnection` binds one tutor to one model catalog entry, so the POC must either keep one interaction model plus JSON/activation capture or introduce an assignment shape that can separately identify interaction and embedding models.
- Existing `TutorConfigurationSnapshot` is an immutable point-in-time capture and is the best local anchor for POC activation history.
- Existing `DeploymentProfile` persists target/configuration but does not model activation as a separate runtime state.
- Tutor Studio has login, users, documents, tutors, and model catalog pages, but settings and benchmarking are placeholders.
- Tutor Studio hardcodes `http://localhost:8000` in multiple files instead of using a shared Vite API base URL.
- Document upload currently posts metadata and `rawText: null`; vectorization requires non-empty raw text.
- Knowledge search is tutor-scoped and owner-checked, but callers must supply an embedding vector directly.
- There is no ask-tutor runtime endpoint that accepts natural language, embeds it, retrieves tutor-linked knowledge, assembles the tutor prompt, and calls the selected model.
- `KnowledgeChunk` uses `vector(768)` with an HNSW cosine index. The default configured Ollama embedding model is `qwen3-embedding:0.6b`; external docs say Qwen3 embedding models support flexible output dimensions up to 4096, so dimension policy must be locked or validated.
- `.vscode/mcp.json` belongs to the Dev Framework context, not Product. Product MCP/tutor connectors are a separate future runtime feature.
- Backend build was run and failed as noted. Frontend lint was not runnable because `tutor-studio/node_modules` was absent and packages were not installed during planning.

## Research Conclusions

- Keep the modular monolith and current Clean Architecture boundaries. The POC needs cohesion more than performance experimentation.
- Keep MediatR and existing application patterns. Speed optimizations are future work.
- Use existing pgvector HNSW for now. Add tuning, halfvec/binary quantization, and reindex jobs after the POC freeze.
- Treat model selection as Product workflow, not static deployment config. Chat and embedding model choices should be configured through Tutor Studio and captured in tutor snapshots.
- Treat the interaction model and embedding model as different roles. LLM/VLM interaction models answer; embedding models vectorize and retrieve for RAG.
- Represent future default-orchestrator and expert model hierarchy in metadata/assignment concepts now, but execute only a single non-agentic interaction path before freeze.
- Vectorization must happen after an embedding model is selected. The embedding model, provider, dimensions, and parameters must be recorded with produced knowledge chunks.
- ASP.NET Core roles are good broad gates, while resource-based authorization is the right pattern for tutor/project ownership and future assignments because the resource must be loaded before the decision.
- Event sourcing is not justified before freeze. Existing immutable snapshots match the POC need for activation history and reproducible benchmark references; draft/published revisions can be added later if needed.
- Use Vite `VITE_*` variables only for non-secret client configuration such as API base URL. Provider API keys must not be bundled into Tutor Studio.
- Environment-specific provider settings belong in Product Settings, but actual secrets should initially be environment/secret references. Storing encrypted provider secrets in the database can be a later feature if Human approves it.
- Product MCP/tutor connectors are distinct from Dev Framework MCP. Product connector runtime execution is gated and not required for the POC freeze.

## Architecture Decisions (Locked v2)

The following decisions are locked as of 2026-05-20 by the human reviewer. They supersede the original "Architecture Decision Gates" table and are enforced by `plan.yaml.locked_decisions`. Schema shapes are normative in [plan.erm.md](plan.erm.md).

## Architecture Decisions (Locked v3)

The table below replaces the v2 lock table. The full decision register with options, references, and reasoning lives in [plan.decisions.md](plan.decisions.md). `plan.yaml.locked_decisions` is the single machine-readable source of truth; this table is a human-readable summary.

| # | Concern | Decision | Reference |
|---|---|---|---|
| D0a | Tenancy | Single-tenant per deployment (standalone single-tenant app + single-tenant DB). Multi-owner link tables are an internal pattern, not multi-tenancy. | [Azure SaaS app design patterns](https://learn.microsoft.com/azure/azure-sql/database/saas-tenancy-app-design-patterns#c-standalone-single-tenant-app-with-single-tenant-database) |
| D0b | Twelve-Factor App | Foundational deployment vision. AI Ops produces a Bitcraft 12-factor compliance map under `.github/governance/`. | [12factor.net](https://12factor.net/) |
| D1 | Authorization model | **ABAC throughout.** Roles `Admin`/`Editor`/`Auditor` are coarse subject attributes, not the access decision. Resource attributes (TutorMember role, revision status, document visibility) are evaluated by a centralized policy evaluator. No role-only gates outside boundary controllers. | [NIST SP 800-162 ABAC](https://csrc.nist.gov/Projects/Attribute-Based-Access-Control) |
| D2 | Resource ownership | `TutorMember(TutorId, UserId, ResourceRole)` link table; no `OwnerId` on aggregates. Same shape reused for other resource ownership relationships as needed. | [EF Core M:N](https://learn.microsoft.com/ef/core/modeling/relationships/many-to-many) |
| D3 | Model registry | EF Core TPH; abstract `Model` + `ModelType` discriminator; leaves `InteractionModel`, `EmbeddingModel`, `JudgeModel` (reserved), `RerankerModel` (reserved). | [EF Core inheritance](https://learn.microsoft.com/ef/core/modeling/inheritance) |
| D4 | Document scoping | `Document` is tutor-agnostic. `TutorDocument` (M:N) attaches a Document to a Tutor for administrative selection. `DocumentFolder` is an admin-side organizational grouping (not a runtime entity). | [EF Core M:N](https://learn.microsoft.com/ef/core/modeling/relationships/many-to-many) |
| D4b | Document/Knowledge runtime semantics | Tutor runtime consumes **Knowledge** (vectorized chunks) only. Documents are pre-runtime input. `DocumentFolder` is administrative; folders are not visible to the runtime. End-user file uploads at chat time are out of PoC scope and will be a separate, semantically distinct domain feature. | — |
| D5 | Embedding dimensions and ANN index | `KnowledgeChunk.Embedding` is untyped pgvector `vector`. ANN index strategy is **configurable**: HNSW default, IVFFlat supported. Partial indexes per `EmbeddingModelId`. | [pgvector](https://github.com/pgvector/pgvector) |
| D6 | Tutor versioning | `TutorRevision` aggregate (Draft/Published/Active/Archived) is the editable unit. `TutorConfigurationSnapshot` is a derived immutable projection captured at activation/benchmark. Re-vectorization produces a new snapshot and a new active revision. | [Fowler — Temporal Patterns](https://martinfowler.com/eaaDev/timeNarrative.html) |
| D7 | Activation vs deployment | Activation = `TutorRevision.Status(Active)` + Snapshot. `DeploymentProfile` is preserved for future export/release configuration. | [12-Factor build/release/run](https://12factor.net/build-release-run) |
| D8 | Runtime API auth | **Design target: JWT-always.** Anonymous local-only mode is a **build-time** MSBuild constant (`BITCRAFT_RUNTIME_LOCAL_ONLY`) that physically excludes the anonymous path from non-local builds. No runtime configuration switch can enable anonymous access. | [ASP.NET Core Authorization](https://learn.microsoft.com/aspnet/core/security/authorization/introduction) |
| D9 | Secrets and config | 12-Factor Config: env vars for production-bound deployments. PoC and early iterations use simple local installs and localhost testing. The `template.<file>.<ext>` pattern is used **only when** a tracked configuration sample is genuinely required. | [12-Factor Config](https://12factor.net/config) |
| D10 | Auditing (PoC) | BaseModel timestamps + actor FKs via SaveChanges interceptor. Future iterations adopt Message-History-style audit log. | [EF Core SaveChanges interceptor](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors#savechanges-interceptor), [EIP — Message History](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessageHistory.html) |
| D11 | RuntimeEngine entity | First-class admin-managed entity introduced now (Provider, Endpoint, ApiKeySecretRef, IsEnabled). `Model` carries a FK to `RuntimeEngine`. Health checks and richer engine lifecycle are deferred. | — |
| D12 | DocumentFolderMember | Omitted; folder access derives from tutor membership. | — |
| D13 | TutorRevisionTool override | Reference `ToolDefinition` as-is for PoC; per-revision tool configuration/override is post-PoC. | — |
| D14 | Server state | TanStack Query v5; Redux Toolkit narrowed to client-only state. | [TanStack Query](https://tanstack.com/query/latest) |
| D15 | Forms | React Hook Form + Zod. | [React Hook Form](https://react-hook-form.com/) |
| D16 | Data tables | TanStack Table v8 headless. | [TanStack Table](https://tanstack.com/table/latest) |
| D17 | Document/Folder UI | Folder UI is administrative, not runtime. Re-evaluate `@svar-ui/react-filemanager` fit during frontend planning; replace only if it cannot represent admin-side selection cleanly. | — |
| D18 | Frontend language | **Migrate Tutor Studio to TypeScript now.** Adopt OpenAPI-generated client when backend contracts are stable enough not to break frontend each iteration. | [TypeScript](https://www.typescriptlang.org/) |
| D19 | Frontend testing | Vitest + Testing Library scaffold with at least one smoke test before freeze. | [Vitest](https://vitest.dev/) |
| D20 | Design system | Keep react-bootstrap 5.3 for PoC; re-evaluate post-PoC only if forced by an architectural decision. | — |
| D21 | Migrations | N-step dependency-ordered migrations. Step count recomputed during replanning after each ERM change. | [EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations) |
| D22 | Activation slug | Slug derived from Tutor name (URL-safe). Uniqueness enforced; renames recompute slug and retain previous as a one-revision redirect. | — |
| D23 | Effective document set | Materialized at activation into the Snapshot. Documents and folder attachments mutated after activation do not affect the active runtime until a new activation. | — |

## Template Configuration Convention

Locked: any tracked file that would carry secrets in a real deployment lives in source control **only** in the form `template.<original_file_name>.<original_ext>` with placeholder values. The real file (without the `template.` prefix) is added to `.gitignore` and produced by the human at deploy time from the template. This applies uniformly to bootstrap, appsettings, env files, compose env overrides, and any future settings carrying credentials, tokens, or endpoint references that resolve to a deployment.

Examples (no agent edits to these without explicit human approval):

- `template.appsettings.Bootstrap.json` (tracked) ↔ `appsettings.Bootstrap.json` (ignored).
- `template.appsettings.Production.json` (tracked, if introduced) ↔ `appsettings.Production.json` (ignored).
- `template.env` ↔ `.env`.

Rules:

- Placeholders use `"__REPLACE_ME__"` or empty strings with a documented contract; never plausible-looking real values.
- `ApiKeySecretRef` columns (see [plan.erm.md §5.2](plan.erm.md#52-common-columns-on-model)) hold the **logical key name**, not the secret itself.
- `IdentityRoleSeeder` and any startup seeding paths read configuration via strongly-typed `IOptions<T>` bound to bootstrap sections; missing/blank required fields **fail fast** rather than seeding defaults.
- Agents do not modify `.gitignore` to add or remove template ignore rules without explicit human approval.

## Product Settings Segmentation

Settings should be explicit so Tutor Studio does not become a junk drawer:

- **Environment Settings**: API base, local mode indicators, provider/engine endpoint references, and feature flags.
- **Runtime Engine Settings**: Ollama local engine now; future vLLM, llama.cpp, OpenAI-compatible, and cloud engine records.
- **Model Settings**: catalog entries, capabilities, default hyperparameters, embedding dimensions, prompt templates, and health status.
- **Tutor Settings**: per-tutor interaction model, embedding/vectorization policy, prompts, knowledge links, activation state, and runtime test controls.
- **User and Access Settings**: management users, global roles, future tutor/project membership, and audit read access.

## Runtime Engine And Orchestration Direction

The POC runtime should be boring on purpose: a deterministic request handler that embeds the question, retrieves tutor-scoped knowledge, builds a bounded prompt, calls the selected interaction model through the existing generation provider, and returns answer plus sources.

Future orchestration options to preserve without implementing now:

| Strategy | Fit | POC Decision |
|---|---|---|
| Single interaction model with RAG | Best for two-day freeze and easiest to test. | Implement now. |
| Default orchestrator plus expert model assignments | Fits the user's future hierarchy and model routing vision. | Represent in vocabulary/metadata only if cheap. Do not execute. |
| Semantic Kernel agent orchestration | Strong .NET alignment and agent abstractions, but adds packages and runtime concepts. | Research after freeze. |
| AutoGen/LangGraph-style multi-agent runtime | Good for Python-side experiments and complex workflows, but not aligned with current Core API POC path. | Defer until Product MCP/tool runtime work. |
| External runtime engine manager | Useful once vLLM, llama.cpp, cloud, and local endpoints all coexist. | Keep engine vocabulary now; avoid full manager unless needed for POC model creation. |

## Activation Versus Deployment

- **Activation before freeze**: local, reversible, captures an activation snapshot, exposes or refreshes a runtime slug/id, and is used by Tutor Studio test/benchmark flows.
- **Deployment after freeze**: final publication/export/release to a target with separate credentials, lifecycle, rollback, and operational validation.
- Existing `DeploymentProfile` should not be deleted or repurposed blindly. Lead Dev should either add a small activation state beside it or clearly constrain it to future deployment profile configuration while activation uses snapshots/status.

## Product Core API Architecture

### Management API

Tutor Studio uses the authenticated management API for admin/editor workflows:

- Auth and users: login, role management, bootstrap/template support.
- Settings: environment/provider status and non-secret configuration references.
- Models: model catalog CRUD, capability metadata, provider endpoint/reference configuration, health check, and model creation wizard.
- Documents: text document CRUD and simple folder/group metadata for the POC.
- Knowledge: vectorization from selected document plus selected embedding model, replacement of chunks, and tutor-scoped retrieval.
- Tutors: prompt versions, model connection, knowledge links, deployment profile, snapshots, Product connector metadata, activation.
- Benchmarking: template/run shell, mostly UI until deeper integration.

### Runtime API

The Core API should expose deterministic runtime endpoints for activated tutors. Activation should not generate new controllers or code. It should capture activation state and a snapshot, then the runtime endpoint serves only if the tutor is active.

Recommended POC shape:

- `POST /api/runtime/tutors/{activationSlug}/ask` for local runtime testing. Keep this separate from management routes such as `/api/tutors/{tutorId}`.
- Management API remains authenticated. Runtime API can be unauthenticated only when a local-only setting is enabled and the activated tutor exposes a non-secret local slug/id. Otherwise it should require existing JWT owner/editor checks.
- Response includes answer, active snapshot/deployment id, model label, retrieved source metadata, and safe error codes.
- Runtime contracts live apart from management contracts and must not expose domain entities or editor-only fields.

Post-freeze external integration shape:

- Deployment creates a stable external deployment slug or id.
- Runtime endpoint can be addressed by external tools through a service credential, scoped API key, or Product MCP connector credential.
- Runtime access checks must be separate from Tutor Studio editor permissions.

### Model Registry And Capabilities

The current `ModelCatalogEntry` is the right starting point, but the POC should make model purpose explicit instead of hiding it only in JSON.

Recommended Product direction:

- Keep one model catalog, not separate duplicated chat/embedding catalogs.
- Add explicit capability metadata such as interaction/chat, vision/multimodal, embedding, judge, reranker, or tool-caller.
- Keep provider type, endpoint, model name, enabled status, settings JSON, and secret reference.
- Prefer Runtime Engine vocabulary in Tutor Studio even if the first implementation stores endpoint/provider fields on the catalog entry.
- Store secret references, not raw provider secrets, for the POC.
- Model configuration wizard filters by capability: embedding models for vectorization, interaction LLM/VLM models for tutor runtime, judge models later for benchmarking.
- Future orchestrator/expert hierarchy should be modeled as tutor model assignments, not as separate model catalogs.

### Tutor Configuration And Activation

The cohesive Product flow should be:

1. Admin configures environment/provider settings and model catalog entries.
2. Editor creates or selects source documents.
3. Editor chooses an embedding-capable model and vectorizes selected documents.
4. Editor creates a tutor.
5. Editor chooses an interaction-capable LLM/VLM model and per-tutor hyperparameter overrides.
6. Editor adds prompt/version, links vectorized documents, and optionally records Product connector metadata.
7. Editor activates the tutor, which captures a configuration snapshot and marks the local runtime endpoint available.
8. Editor tests the activated tutor through Tutor Studio.

Activation should capture the configuration. It should not mutate model weights. If true fine-tuning is later added, it should become a separate model-training lifecycle, not overloaded into this POC activation step.

## Work Streams

### WS0 - Build Green Gate

- **Current State**: Backend build is red due to duplicate local variable in the audit metadata interceptor.
- **Decision/Approach**: Fix the compile error first, without changing metadata semantics.
- **Owner**: NET Dev through Lead Dev.
- **CIL**: medium.
- **Acceptance**: `dotnet build bitcraft.slnx` passes; focused interceptor/audit tests pass or are added if behavior is touched.

### WS1 - Bootstrap Template And Role Vocabulary

- **Current State**: Roles and a hardcoded admin (`admin@bitcraft.com` / `Admin1234!`) are seeded from startup code in [IdentityRoleSeeder](../../../src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs); startup logs and continues on seeding failure. Current roles are Admin/Editor/Viewer in [AppRoles](../../../src/Bitcraft.Application/Common/Security/AppRoles.cs); the locked Product vocabulary is Admin/Editor/Auditor (no User).
- **Decision/Approach**:
  1. Rename `Viewer` → `Auditor` in `AppRoles` and in the `ViewerOrAbove` policy in [DependencyInjection](../../../src/Bitcraft.Infrastructure/DependencyInjection.cs) (policy becomes `AuditorOrAbove`). Update all referencing controllers and tests.
  2. Remove all hardcoded credentials from `IdentityRoleSeeder`. Roles remain idempotently seeded; users are not seeded by default.
  3. Add `BootstrapOptions` (`Application/Common/Configuration`) bound from a `Bootstrap` configuration section. Add tracked `template.appsettings.Bootstrap.json` with placeholder values per [Template Configuration Convention](#template-configuration-convention). Real `appsettings.Bootstrap.json` is gitignored (humans add ignore rule; agents do not).
  4. Startup binds `BootstrapOptions`; if section is present and validation succeeds, seed the configured admin user idempotently. If absent or invalid, seed roles only and log clearly. Missing required fields when section is present must **fail fast**.
- **Owner**: Sec Ops architecture review, NET Dev implementation, Lead Dev orchestration.
- **CIL**: high.
- **Acceptance**:
  - No hardcoded credentials anywhere in source.
  - `AppRoles` exports exactly `Admin`, `Editor`, `Auditor`; `AuditorOrAbove` policy exists; old `Viewer`/`ViewerOrAbove` references are gone (build proof).
  - `template.appsettings.Bootstrap.json` is tracked with placeholders only; the runtime file is gitignored.
  - Unit tests cover: configured bootstrap path seeds admin once and is idempotent; missing/invalid bootstrap with section present fails fast; absent section seeds roles only without error.
  - Integration test confirms the new policy gates an `Auditor`-only read endpoint correctly.
  - [docs/authorization-roles.md](../../../docs/authorization-roles.md) is updated to match (requires explicit human permission to edit because `ai_gen: false`).

### WS2 - Product Settings, Runtime Engines, And Provider Configuration

- **Current State**: Configuration is static appsettings/env vars; Settings page is a placeholder.
- **Decision/Approach** (v3): introduce a first-class admin-managed `RuntimeEngine` entity now (per D11) holding Provider, Endpoint, ApiKeySecretRef, IsEnabled. `Model` carries a FK to `RuntimeEngine`; provider/endpoint columns move off `Model`. Admin Settings view/API segments into Environment, Runtime Engines, Models, Tutor Defaults, and User/Access. Per 12-Factor Config (D9) configuration prefers env vars; tracked `template.<file>.<ext>` is used only when a sample file is genuinely required (not for routine bootstrap). Secrets are represented by `ApiKeySecretRef` only; raw values are never returned to the client. Health checks and richer engine lifecycle remain deferred.
- **Owner**: NET Dev and Web Dev, Sec Ops review for any setting that can affect security or provider behavior.
- **CIL**: high if mutable, medium if read-only plus references.
- **Acceptance**: Admin can view and, where approved, update safe Product settings; no secret values are returned to the client; provider config supports Ollama and OpenAI references; Tutor Studio uses shared API base config; settings route is Admin-only and tested.

### WS3 - Model Registry (Polymorphic) And Wizard Contracts

- **Current State**: A flat `ModelCatalogEntry` exists with `TutorModelConnection` binding a single tutor to a single model. There is no capability typing in the domain.
- **Decision/Approach** (per [plan.erm.md §5](plan.erm.md#5-modeling-context-abstract-model--modeltype)):
  1. Backend: introduce abstract `Model` with `ModelType` discriminator using EF Core TPH. Concrete leaves: `InteractionModel`, `EmbeddingModel`, `JudgeModel` (reserved), `RerankerModel` (reserved). Common columns on the base except provider/endpoint, which live on `RuntimeEngine` (D11); `Model` carries `RuntimeEngineId` FK and `ProviderModelName`. Leaf-specific columns (notably `EmbeddingDimensions`) on leaves. Drop `ModelCatalogEntry` in the same migration; data is internal POC and not preserved.
  2. API contracts under [src/Bitcraft.Api/Contracts/Admin](../../../src/Bitcraft.Api/Contracts/Admin): one polymorphic `ModelResponse` keyed by `ModelType`, with explicit per-type request DTOs (`CreateInteractionModelRequest`, `CreateEmbeddingModelRequest`, etc.). Per [api-contracts.instructions.md](../../../.github/instructions/api-contracts.instructions.md) all DTOs are immutable records and do not expose domain entities.
  3. `TutorModelConnection` is removed; model selection moves to `TutorRevision` (see WS6).
  4. Frontend wizard work is **not** in scope here. The frontend iteration plans the wizard UX in [plan.frontend.md §6](plan.frontend.md#6-recommendation-block-pending-human-approval).
- **Owner**: NET Dev primary; Lead Dev orchestration.
- **CIL**: high.
- **Acceptance**:
  - Single `Models` table with `ModelType` discriminator; FK from `KnowledgeChunk.EmbeddingModelId` and from `TutorRevision.InteractionModelId`/`QueryEmbeddingModelId` resolve via the base type.
  - Admin CRUD endpoints accept and return capability-typed payloads; selecting embedding models elsewhere is filtered server-side.
  - Unit tests cover TPH read/write per leaf, embedding-only validation (required `EmbeddingDimensions`), and FK-restricted delete when chunks reference an embedding model.

### WS4 - Documents, Folders, And Tutor Link Tables

- **Current State**: `Document` already has no `TutorId`. `KnowledgeSourceLink` ties tutors to documents 1:1 per row but lacks the folder concept. Frontend uploads raw text incorrectly (null).
- **Decision/Approach** (per [plan.erm.md §4](plan.erm.md#4-document-context)):
  1. Add `DocumentFolder`, `DocumentFolderEntry`, `TutorDocument`, `TutorDocumentFolder`. Migrate `KnowledgeSourceLink` rows into `TutorDocument` and drop the old table.
  2. `Document` keeps its current shape (Name, MimeType, RawText, Checksum, Source). No `TutorId`, no `OwnerId`. Author is `BaseModel.CreatedById`.
  3. Application commands: `AttachDocumentToTutor`, `DetachDocumentFromTutor`, `AttachFolderToTutor`, `DetachFolderFromTutor`, `CreateFolder`, `AddDocumentToFolder`, `RemoveDocumentFromFolder`. Each enforces tutor membership via the rule in [plan.erm.md §3.2](plan.erm.md#32-resource-membership).
  4. Effective document set is the union of direct `TutorDocument` and folder membership. Materialized at activation into the snapshot (D23). Documents and folder attachments mutated after activation do not affect the active runtime until a new activation is created.
  5. Backend accepts plain text only (`text/plain`, `text/markdown`, `application/json`) for the freeze.
  6. **Document/Folder semantic re-frame** (D4b): `DocumentFolder` is an administrative grouping owned by Editor/Admin and is **not** a runtime concept. The runtime consumes `Knowledge` (vectorized chunks) only. End-user file uploads at chat time are out of PoC scope and will be a separate, semantically distinct domain feature.
- **Owner**: NET Dev primary; Lead Dev orchestration. Frontend changes for these surfaces are in [plan.frontend.md](plan.frontend.md) and do not block this WS.
- **CIL**: high.
- **Acceptance**:
  - All new tables present with the cardinality matrix in [plan.erm.md §9](plan.erm.md#9-cardinality-and-cascade-summary).
  - Effective-document-set resolver covered by unit tests for: direct only, folder only, mixed, duplicate suppression.
  - Attach/detach commands enforce `TutorMember` membership; non-member editors are rejected; admin bypasses membership.
  - Vectorization rejects empty `RawText` cleanly with a typed error code.

### WS5 - Knowledge Vectorization (Dynamic Dimensions)

- **Current State**: `KnowledgeChunk.Embedding` is `vector(768)` with a single global HNSW index; `EmbeddingModel`/`EmbeddingProvider` are denormalized strings.
- **Decision/Approach** (per [plan.erm.md §6](plan.erm.md#6-knowledge-context)):
  1. Change `Embedding` column type to untyped `vector` (no N).
  2. Add `EmbeddingModelId Guid` FK to the polymorphic `Model` (restricted to `EmbeddingModel`) and `EmbeddingDimensions int`. Drop the legacy string columns.
  3. Add CHECK constraint `vector_dims(Embedding) = EmbeddingDimensions`.
  4. Drop the fixed-dim HNSW index. Ship Infrastructure migration helpers (`EnsureEmbeddingIndex(modelId, dims)`, `DropEmbeddingIndex(modelId)`) that create/drop partial HNSW indexes per model.
  5. Application commands `EnableEmbeddingModelCommand`/`DisableEmbeddingModelCommand` invoke the helpers.
  6. `RebuildKnowledgeCommand` operates on `(DocumentId, EmbeddingModelId)` pairs only.
  7. Unique `(DocumentId, EmbeddingModelId, ChunkIndex)` constraint enforced.
- **Owner**: NET Dev primary; Researcher confirms `vector_dims()` availability in target pgvector version.
- **CIL**: high.
- **Acceptance**:
  - Migration converts existing chunks to dynamic-dim schema (or wipes them if no embedding model can be inferred; POC data is disposable).
  - Enable/disable model command creates/drops the partial HNSW index transactionally.
  - Persisting a chunk whose vector length mismatches `EmbeddingDimensions` is rejected by the DB CHECK.
  - Multiple embedding models coexist for the same document in tests; retrieval picks chunks for the active revision's `QueryEmbeddingModelId`.

### WS6 - Tutor Revisions, Membership, And Activation Domain

- **Current State**: `Tutor` carries `OwnerId` and uses `TutorModelConnection` plus `TutorConfigurationSnapshot` for ad-hoc versioning; the editor surface in the frontend is missing.
- **Decision/Approach** (per [plan.erm.md §7](plan.erm.md#7-tutoring-context)):
  1. Drop `OwnerId` from `Tutor`. Introduce `TutorMember(TutorId, UserId, ResourceRole, GrantedById, GrantedUtc)` with the membership invariant (≥1 Owner at all times). Founding membership inserted in the same transaction as tutor creation.
  2. Drop `TutorModelConnection`. Introduce `TutorRevision` aggregate with state machine `Draft → Published → Active → Archived`. Add `Tutor.ActiveRevisionId` pointer.
  3. Introduce `TutorRevisionTool` M:N join for tool composition per revision.
  4. Update `TutorConfigurationSnapshot` to carry `TutorRevisionId`, `EffectiveDocumentIdsJson`, `EffectiveKnowledgeBindingJson`.
  5. Application commands: `CreateTutor` (with founding owner), `AddTutorMember` / `RemoveTutorMember` (Owner-only invariant guarded), `CreateRevision` (from latest or blank), `EditDraftRevision`, `PublishRevision`, `ActivateRevision` (atomic: archives previous Active, sets `Tutor.ActiveRevisionId`, captures snapshot), `ArchiveRevision`.
  6. Frontend surfaces for these are deferred to [plan.frontend.md](plan.frontend.md).
- **Owner**: NET Dev primary; Sec Ops review of membership rule and authorization handler.
- **CIL**: high.
- **Acceptance**:
  - Domain unit tests cover membership invariant (cannot remove last Owner), revision state machine (illegal transitions rejected), atomic activation (previous Active → Archived in same transaction), and snapshot capture content.
  - API integration tests cover: non-member editor blocked, member editor permitted, admin bypass, auditor read-only.
  - No references to `OwnerId` or `TutorModelConnection` remain in the codebase.

### WS7 - Runtime API Aligned To Revisions

- **Current State**: Low-level Ollama generation exists; no composed runtime endpoint.
- **Decision/Approach**: Add `POST /api/runtime/tutors/{activationSlug}/ask` served by a dedicated runtime controller. Resolution flow:
  1. Look up the activation slug → `Tutor` → `Tutor.ActiveRevisionId`. Reject with `tutor_inactive` if null.
  2. Load the `TutorRevision` and its derived `TutorConfigurationSnapshot`.
  3. Embed the incoming query with `Revision.QueryEmbeddingModel` (use the partial HNSW index for that model).
  4. Retrieve top-K chunks from the snapshot's materialized document set, filtered to chunks whose `EmbeddingModelId` matches (mismatch surfaced as `knowledge_not_ready_for_active_embedding_model`).
  5. Assemble bounded prompt from `Revision.PromptVersion` + retrieved sources.
  6. Call `Revision.InteractionModel` via the generation abstraction.
  7. Return answer, model label, revision id, snapshot id, retrieved chunk ids + source document ids, safe error codes.
- Auth: management API stays JWT. The runtime endpoint is JWT-protected by default; an anonymous variant of the controller exists **only** when the `BITCRAFT_RUNTIME_LOCAL_ONLY` MSBuild constant is defined for the active configuration. Non-local builds physically exclude the anonymous code path. There is no runtime configuration switch for anonymous access.
- No streaming, no tools, no memory, no Product MCP execution in this WS.
- **Owner**: NET Dev primary; Sec Ops review of local-only guard and prompt-injection surface.
- **CIL**: high.
- **Acceptance**:
  - Endpoint returns `tutor_inactive` for tutors with null `ActiveRevisionId`.
  - In a non-`BITCRAFT_RUNTIME_LOCAL_ONLY` build, the anonymous controller variant is not present in the compiled assembly (verified by reflection-based assembly inspection test).
  - JWT-required path rejects non-member editors and admits member editors per the ABAC policy evaluator (D1).
  - Knowledge mismatch and missing-prompt errors return typed codes, not 500s.
  - End-to-end happy path test against an Ollama mock (if available) or via unit-level fakes.
  - Activation slug derived from Tutor name; rename retains the previous slug as a one-revision redirect (D22).

### WS8 - Basic Benchmarking Shell

- **Current State**: Backend and sidecar benchmarking exist; frontend page is a placeholder; sidecar scoring is deterministic and not yet statistical.
- **Decision/Approach**: Before freeze, expose a simple UI shell only. Show intended flow and any existing backend state without claiming full integration. If time allows, list templates and runs. Advanced execution and statistics are post-freeze.
- **Owner**: Web Dev primary, NET Dev for contract fixes only.
- **CIL**: medium.
- **Acceptance**: Page is no longer placeholder; it fits the existing Tutor Studio style; at minimum it shows shell state and is wired to API base config. Advanced statistics are deferred.

### WS9 - Freeze Validation And Work Item Packaging

- **Current State**: Backend build red; frontend dependencies absent; no end-to-end smoke script exists.
- **Decision/Approach**: Human and agents validate in parallel: agents run focused automated checks; Human runs compose/dev smoke tests and creates work items from this plan.
- **Owner**: Lead Dev coordinates; Human validates environment; Dev Ops supports compose blockers.
- **CIL**: high.
- **Acceptance**: Green backend build, focused tests, frontend lint/build after `npm ci`, and manual smoke path: login -> create text document -> vectorize -> create tutor -> attach document -> select model -> ask -> grounded answer returned.

## Iteration Schedule

Suggested branch shape for Human-managed Git/Jira: `type/JIRA-short-slug`, for example `feature/BIT-123-model-registry-wizard`. If no Jira key exists yet, use the same type and short slug after Human approves local branch naming. Agents pause after each iteration for Human branch/commit/test decisions before the next feature branch starts.

| Date | Iteration | Jira Type | Suggested Branch Type | Product Scope | Pause Gate / Human Parallel Work |
|---|---|---|---|---|---|
| 2026-05-20 | I0 Build gate | Chore | `chore/...-build-green` | Fix compile blocker and validate baseline | Human creates/chooses branch and parent epic/tickets. |
| 2026-05-20 | I1 Bootstrap and roles | Feature/Task | `feature/...-bootstrap-roles` | Replace hardcoded admin, tracked template, role vocabulary decision. | Human provides local/dev/test bootstrap values outside source control and approves Admin/Editor/Auditor/User policy. |
| 2026-05-20 | I2 Settings, engines, models | Feature | `feature/...-settings-models` | Segmented Settings, runtime engine vocabulary, Models CRUD, model wizard, capability metadata. | Human decides provider secret boundary and whether `RuntimeEngine` becomes schema now. |
| 2026-05-20 to 2026-05-21 | I3 Tutor documents and vectorization | Feature | `feature/...-documents-vectorization` | Tutor-specific text ingestion, metadata popup, embedding model selection, vectorize action, dimension validation. | Human pulls/installs selected Ollama models and approves `TutorId` vs `TutorDocumentLink`. |
| 2026-05-21 | I4 Tutor configuration and version gate | Feature | `feature/...-tutor-config` | Tutor detail flow: interaction model, prompts, documents/knowledge, activation profile. | Human approves snapshot-only versioning or revision-table escalation before runtime work. |
| 2026-05-21 | I5 Activation and runtime API | Feature | `feature/...-tutor-runtime` | Activate tutor, capture snapshot, expose separate local runtime ask endpoint, test panel. | Human approves local unauthenticated runtime flag vs JWT-only and runs local smoke against real models. |
| 2026-05-21 | I6 Benchmarking shell and freeze | Feature/Chore | `feature/...-benchmarking-shell` and `chore/...-freeze-validation` | Benchmarking UI shell, validation pass, bug fixes only. | Human declares feature freeze and creates post-freeze backlog. |

## Bottlenecks And Watchpoints

- **Build gate**: current backend compile error blocks all reliable backend validation until fixed.
- **Schema pressure**: role vocabulary, tutor-specific documents, model capabilities, model assignments, and activation state can all request migrations. Bundle only the schema changes required for the next branch.
- **Versioning uncertainty**: do not start runtime implementation until Human accepts snapshot-only POC versioning or escalates to `TutorRevision` work.
- **Embedding dimensions**: `KnowledgeChunk` is fixed at `vector(768)`. The selected embedding model must be locked to 768 output or the schema/index strategy must change.
- **Model availability and latency**: local Ollama models may be absent, slow, or too large for available hardware. Human validation with real models is a critical path.
- **Secret handling**: UI-managed provider secrets can quickly raise the work to critical. Keep references/environment variables for freeze.
- **Runtime access boundary**: unauthenticated runtime is acceptable only as local POC behavior. Accidentally making it production-shaped is a security blocker.
- **Frontend dependency/install path**: `node_modules` is absent, so Web Dev validation depends on Human or agent-approved `npm ci` timing.
- **Document ingestion scope**: text/md/json is enough. Binary viewers, storage, OCR, and PDF/DOCX parsing will blow the deadline.
- **Terminology drift**: Product runtime/activation/deployment/MCP must stay separate from Dev Framework MCP and agentic development files.
- **Branch pause overhead**: Human-managed branches/commits are correct for control, but every iteration needs a tight pause decision to avoid losing the two-day cadence.

## Jira Breakdown

- **Epic**: POC Product Core and Tutor Studio freeze.
- **Chores**: build green, shared API base config, freeze validation.
- **Features**: bootstrap template, settings/providers, models wizard, document ingestion, vectorization workflow, tutor configuration, activation/runtime endpoint, benchmarking shell.
- **Tasks**: focused backend contracts, frontend route wiring, unit/API tests, manual smoke checklist.
- **Bugs**: compile failures, broken existing endpoints, UI regressions found during smoke tests.
- **Spikes**: only for unresolved provider secret storage or embedding dimension behavior; keep spikes time-boxed.

## Open Human Decisions (v3)

Resolved items have moved to `plan.yaml.locked_decisions` and are detailed in [plan.decisions.md](plan.decisions.md). Remaining open questions:

| # | Question | Recommendation | Blocks |
|---|---|---|---|
| _(none)_ | All Open Decisions O1–O8 from prior revisions are resolved. See the **Resolutions** note below. | — | — |

> **Resolutions (2026-05-20, plan v3.4)**: O1 → **D25** (migration sequence approved). O2 → **D26** (`BITCRAFT_RUNTIME_LOCAL_ONLY` confirmed: name, definition site `Directory.Build.props`, CI defaults). O3 → **D27** (OpenAPI client deferred until at least one full PoC iteration of stable contracts). O4 → **D28** (frontend library set + admin-only DocumentFolder UX confirmed cohesive). O5 → D24 (ABAC evaluator placement). O6 → D11 (RuntimeEngine in-window, already locked). O7 → D22 / D22.b (slug redirect lifecycle + storage shape). O8 → reports adopted ([docs/architecture.md](../../../docs/architecture.md), [docs/change-notes.md](../../../docs/change-notes.md), [plan.decisions.md](plan.decisions.md)).
>
> **Next gate (Human, branching strategy)**: see `human_locks` in [plan.yaml](plan.yaml) and the development kickoff brief at [.github/plans/feature-freeze-2026-05-21/kickoff.md](kickoff.md) (once published).

## Companion Reporting Artifacts

Three artifacts are introduced alongside this plan and persist beyond the freeze:

- **[plan.decisions.md](plan.decisions.md)** — in-plan decision register. Each decision row carries: ID, concern, options considered, locked option, references, reasoning, blocked work streams, and revision history. Owned by Planner; updated on every plan revision.
- **[docs/architecture.md](../../../docs/architecture.md)** — living architecture document for the team. High-level system overview, ABAC + 12-Factor compliance map, ERM diagram (Mermaid/PlantUML), context-scoped class diagrams (Identity & Access, Modeling & RuntimeEngine, Documents & Knowledge, Tutoring & Revisions, Benchmarking), API/Infrastructure/Interface relations, and runtime/activation flow. Owned by Typewriter; updated on every accepted ERM/contract change.
- **[docs/change-notes.md](../../../docs/change-notes.md)** — release-batch change notes for developers and team members. Concise, referenced, learner-friendly. Owned by Secretary; one entry per merged batch with links back to plan, decisions, and architecture sections.

## Impacted Files

- Likely backend: `src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs`, `src/Bitcraft.Api/Program.cs`, `src/Bitcraft.Infrastructure/Persistence/Interceptors/EntitySaveChangesInterceptor.cs`, `src/Bitcraft.Application/Common/Security/AppRoles.cs`, `src/Bitcraft.Domain/Documents/Document.cs`, `src/Bitcraft.Domain/Tutoring/*`, `src/Bitcraft.Application/Features/Admin/ModelCatalogFeatures.cs`, `src/Bitcraft.Application/Features/Tutoring/*`, `src/Bitcraft.Api/Controllers/Admin/*`, `src/Bitcraft.Api/Controllers/Tutoring/*`, new or adjusted `src/Bitcraft.Api/Controllers/Runtime/*`, `src/Bitcraft.Api/Contracts/Admin/*`, `src/Bitcraft.Api/Contracts/Tutoring/*`, new `src/Bitcraft.Api/Contracts/Runtime/*`, `src/Bitcraft.Application/Features/Knowledge/KnowledgeFeatures.cs`, `src/Bitcraft.Infrastructure/Persistence/Configurations/KnowledgeChunkConfiguration.cs` if dimension/schema changes.
- Likely frontend: `tutor-studio/src/App.jsx`, `tutor-studio/src/FilesPage.jsx`, `tutor-studio/src/TutorsPage.jsx`, `tutor-studio/src/Settings.jsx`, `tutor-studio/src/Benchmarking.jsx`, `tutor-studio/src/ModelCatalogPage.jsx`, new shared API client/config module, and possible tutor detail/model wizard components.
- Likely tests: `tests/Bitcraft.UnitTests/Application/Tutoring/*`, `tests/Bitcraft.UnitTests/Application/Admin/*`, `tests/Bitcraft.ApiIntegrationTests/*`, possibly infrastructure integration tests for vector persistence.
- Templates/docs/config: tracked bootstrap template with placeholders; `docs/authorization-roles.md` requires explicit human permission before agent edits because `ai_gen: false`; `.env.example`, README, and compose changes require human approval if they touch secrets/deployment behavior.

## CSS, DIS, CIL

- Overall CSS: 8 because the plan touches identity, model/provider configuration, tutor runtime, and data boundaries.
- Overall DIS: 8 because work touches API, Application, Infrastructure, Domain-adjacent tutor/knowledge behavior, frontend, tests, and runtime config.
- Overall CIL: high. Production release and real secrets are out of scope; any later move into actual production secrets, production unauthenticated runtime access, deployment/export, destructive migrations, or Product MCP tool execution raises the relevant slice to critical.

## Delegation Plan

| Work Stream | Primary | Supporting | Notes |
|---|---|---|---|
| WS0 | NET Dev | Lead Dev | First implementation gate. |
| WS1 | Sec Ops | NET Dev, Lead Dev | Template and identity bootstrap. |
| WS2 | NET Dev | Web Dev, Sec Ops | Settings, runtime engines, provider references. |
| WS3 | Web Dev | NET Dev | Models view, capabilities, and wizard. |
| WS4 | Web Dev | NET Dev | Text-only ingestion. |
| WS5 | NET Dev | Researcher, Human | Vectorization workflow and model/dimension validation. |
| WS6 | Web Dev | NET Dev | Minimum tutor detail route and versioning gate. |
| WS7 | NET Dev | Sec Ops, Web Dev | Activation and separate runtime API. |
| WS8 | Web Dev | NET Dev | Shell only. |
| WS9 | Lead Dev | Human, Dev Ops | Validation and freeze decision. |

## Validation Plan

- After WS0: `dotnet build bitcraft.slnx`.
- After WS1: identity/bootstrap template tests plus API auth policy tests.
- After WS2/WS3: model catalog/settings tests and frontend smoke on Settings/Models.
- After WS4/WS5: document/vectorization unit tests and manual vectorize test against selected embedding model.
- After WS6/WS7: focused activation/runtime unit/API tests for inactive tutor, local-only guard, owner/editor when authenticated mode is enabled, missing interaction model, missing embedding model, missing prompt, missing knowledge, and successful local model path where environment allows.
- Frontend: Human or Web Dev runs `npm ci`, then `npm run lint` and `npm run build` from `tutor-studio/`.
- End-to-end: Human runs compose/dev stack and executes login -> Settings/Runtime Engines/Models -> create tutor text document -> select embedding model -> vectorize -> create tutor -> select interaction model -> attach knowledge -> activate -> ask through runtime API smoke test.
- Security review: Sec Ops reviews WS1, provider secret boundary, and WS7 before freeze signoff.

## Defer After Freeze

- MediatR replacement, Native AOT, dispatcher benchmarking.
- Semantic chunking and overlap, halfvec/binary quantization, hybrid search, automated index maintenance jobs beyond the per-model partial-index lifecycle.
- OpenTelemetry spans, trace visualization, Stream of Thought UI.
- Runtime engine fleet management, vLLM/llama.cpp/cloud runners, orchestrator/expert execution, Product MCP connector runtime execution, per-client OAuth consent, remote Product MCP integrations.
- Statistical benchmarking, confidence intervals, p-values, LLM-as-judge, export/reporting.
- Rich file manager backed by object storage, binary ingestion, OCR, PDF/DOCX parsers, smart-filter folder evaluation engine.
- Deployment/export artifacts, conversation history, streaming answers, external runtime API keys, encrypted remote model secret storage, true fine-tuning/training lifecycle.
- `DocumentFolderMember` ACL, hierarchical folders, membership history/audit log, dedicated `RuntimeEngine` entity.
- TypeScript migration of the frontend; Mantine/Radix design-system evaluation; AG Grid; httpOnly-cookie auth rotation.

## Inbox Updates Queued

- Pending Human plan approval; after approval, Lead Dev should create NET Dev, Web Dev, Sec Ops, Dev Ops, and Researcher inbox items from the work streams above rather than duplicating this entire plan.

---

## Pre-Final Plan v3.6 — 3-Iteration PoC Delivery (2026-05-21)

> Origin: Planner, 2026-05-21. All decisions D29–D41 are locked (Human-approved 2026-05-21). This section supersedes the Iteration Schedule table above and is the binding delivery plan for the remaining PoC work. TypeScript migration is deferred to post-PoC (D29). All frontend work is plain JavaScript/JSX.

### Library Install List for Human

Run the following before Iteration 2 development starts (after Iteration 1 PR is merged and Human has confirmed the plan). No new packages are needed for Iteration 1 itself.

```
npm install --prefix tutor-studio @tanstack/react-query@^5 react-hook-form zod @hookform/resolvers @tanstack/react-table@^8 --save
npm install --prefix tutor-studio vitest @testing-library/react @testing-library/user-event jsdom --save-dev
```

> **Human action**: Run these commands from the workspace root (or `cd tutor-studio && npm install ...` without `--prefix`) before Iteration 2 development begins. Confirm versions installed to Net Dev and Web Dev before they start feature work.

### Iteration Plan

| Iteration | Owner | CIL | Scope | Acceptance Criteria | Human Gate |
|-----------|-------|-----|-------|---------------------|------------|
| **1 — Foundation & Preflight** | Web Dev + Net Dev + Dev Ops | medium | See §Iteration 1 detail below | All items in §Iteration 1 scope are complete; `npm run build` is clean; `dotnet build` passes | Review Iteration 1 PR; install npm packages listed above before Iteration 2 starts |
| **2 — Feature Parity** | Web Dev + Net Dev | high | See §Iteration 2 detail below | Every backend surface from D25 steps 1–8 has a working frontend UI; smoke path (login → create doc → vectorize → create tutor → attach doc → activate → ask) verified end-to-end | Review Iteration 2 PRs; confirm smoke path passes manually |
| **3 — Integration, Quality & Freeze** | Web Dev + Net Dev + Sec Ops + Dev Ops | medium | See §Iteration 3 detail below | Lint green; benchmarking shows real data; change-notes + architecture docs updated; freeze declared | Declare feature freeze; create post-freeze backlog |

---

#### Iteration 1 — Foundation & Preflight (CIL: medium)

Goal: land all correctness, security-baseline, and configuration-separation fixes that block Iteration 2 feature work. No new libraries consumed yet.

**Web Dev tasks:**

- **API client centralization (D40)**: create `tutor-studio/src/api/client.js` that reads `import.meta.env.VITE_API_BASE_URL` (no default hardcoded); add `.env.development.example` tracked with `VITE_API_BASE_URL=http://localhost:8000`; add `.env.development` and `.env.production` to `.gitignore`; replace all 7+ hardcoded `http://localhost:8000` occurrences across all component files.
- **EditorRoute fix (D35)**: update `App.jsx` `EditorRoute` to admit both `Editor` and `Admin` roles (`roles.includes('Editor') || roles.includes('Admin')`); add `// TODO post-PoC: extract role-check abstraction` comment.
- **`vite.config.js` `process` fix**: replace `process.env.VITE_USE_POLLING` with `import.meta.env.VITE_USE_POLLING` (this is a build error, not a lint-only issue — D34 lint deferral does not apply here).
- **Register.jsx form drift**: remove the `fullName` field; align form payload to backend `RegisterRequest` (email + password only).
- **CSP header (D32 mitigation)**: add `Content-Security-Policy` response header to `tutor-studio/nginx.conf`.
- **Env file structure (D41)**: confirm `.env.development.example` and `.env.production.example` are tracked; confirm actual env files are git-ignored.

**Net Dev tasks:**

- **`DocumentResponse` split (D33)**: split `DocumentResponse` into `DocumentSummaryResponse` (no `RawText`, for list endpoints) and `DocumentDetailResponse` (includes `RawText`, for single GET). Update `DocumentsController`, `DocumentMapper`, and relevant unit tests.
- **`PolicyDecision.IsPermit()` + `NotApplicable` XML docs (M-2)**: add `bool IsPermit()` helper returning `Effect == PolicyEffect.Permit`; add XML doc to `IPolicyEvaluator.EvaluateAsync` and `PolicyEffect.NotApplicable` stating `NotApplicable` must be treated as Deny; add unit test covering this contract.
- **TutorRevision IDOR guard (M-1)**: add `TutorId` to all command/query records in `TutorRevisionFeatures.cs`; assert `revision.TutorId == request.TutorId` in each handler; return opaque `NotFound` on mismatch; add unit test for mismatch case.
- **`/api/health` version gate + rate limiter (L-1)**: gate the `version` field behind `IHostEnvironment.IsDevelopment()`; add fixed-window rate limiter to the health endpoint or globally via `AddRateLimiter`.

**Dev Ops task:**

- **`appsettings.Development.json` host-shell override (HD-11, D41)**: add `src/Bitcraft.Api/appsettings.Development.json` (git-ignored) with `ConnectionStrings.DefaultConnection` pointing to `Host=localhost;Port=5433`; add tracked `appsettings.Development.example.json` with placeholder values; update `docs/local-setup.md` to document both Compose-run and host-shell migration paths.

**Acceptance criteria:**
- `npm run build` passes; no `process.env` references remain in `vite.config.js`.
- All API calls in `tutor-studio/src/` route through `src/api/client.js`; no hardcoded `http://` URLs in component files.
- Admin role can access Files, Tutors, and Benchmarking routes.
- `Register.jsx` sends only email and password.
- CSP header present in `nginx.conf`.
- `DocumentSummaryResponse` and `DocumentDetailResponse` both present; list endpoint omits `RawText`.
- `PolicyDecision.IsPermit()` exists and is covered by a unit test.
- TutorRevision IDOR guard covered by a unit test.
- `appsettings.Development.example.json` tracked; `appsettings.Development.json` git-ignored.
- `dotnet build bitcraft.slnx` passes.

**Human gate:** Review and merge Iteration 1 PR. Run the `npm install` command above before Iteration 2 development starts.

---

#### Iteration 2 — Feature Parity (CIL: high)

Goal: every backend surface introduced in D25 steps 1–8 has a working frontend UI. By the end of this iteration the PoC is end-to-end testable by a human reviewer.

**Web Dev tasks:**

- **TutorRevision CRUD workflow**: revisions tab on the tutor detail page driven by `GET /api/tutors/{tutorId}/revisions`; list revisions newest-first with `Status` badge; create-draft; edit-draft (prompt / interaction model / query embedding model / interaction params JSON / deployment target); publish; activate; archive. Surface `ActivationSlug` as a read-only copyable field derived from Tutor title (D38) — no manual slug override. Refresh `Tutor.ActiveRevisionId` on activate-success.
- **`TutorMember` management panel**: list members on the tutor detail page; grant member (Owner-only); revoke member (Owner-only); change role. Calls `GrantTutorMembership` / `RevokeTutorMembership` / `ChangeTutorMemberRole` endpoints (provided by Net Dev this iteration).
- **`TutorDocument` M:N**: replace the old `KnowledgeSourceLink`-style single-attach UI with multi-attach/detach. Document picker on the tutor detail page calls `POST /api/tutors/{tutorId}/documents` and `DELETE /api/tutors/{tutorId}/documents/{documentId}`.
- **`DocumentFolder` flat-list component (D37, D31)**: embedded in `FilesPage` (or an admin section); create/rename/delete folders; "Move to Folder" affordance per document row calling `PATCH /api/documents/{id}/folder`. No separate route needed; no tree nesting (D31 flat only).
- **Embedding model selector**: on the vectorize dialog, replace the free-text provider/model fields with a dropdown sourced from `GET /api/models?type=EmbeddingModel`; post `EmbeddingModelId` (D39).
- **Document create fix**: ensure the create-document payload sends actual `RawText` content (currently `null`); use a real textarea field.
- **Models page aligned to TPH (D3/D4)**: surface `InteractionModel` and `EmbeddingModel` types from `/api/models`; remove the old flat `ModelCatalogEntry` shape. Model type badge distinguishes types in the list.
- **`BenchmarkRun.TutorRevisionId` (D39)**: surface the active revision as a read-only column in the benchmark run list.
- **Dashboard stale route**: fix `/benchmarking` link to `/benchmark-templates`.
- **`DocumentSummaryResponse` vs `DocumentDetailResponse` (D33)**: list view binds to summary (no `RawText`); detail/edit panel fetches `GET /api/documents/{id}` for full content.

**Net Dev tasks:**

- **`GrantTutorMembership` / `RevokeTutorMembership` / `ChangeTutorMemberRole` endpoints**: Owner-only invariant enforced; `WouldLeaveNoOwner` guard on revoke/role-change; unit + API integration tests.
- **ABAC `IPolicyEvaluator` concrete implementation + policy catalog (D24/D24.a, WS6)**: implement the evaluator in `src/Bitcraft.Infrastructure/Authorization/`; wire `audit-log`, `redact-pii`, `scope-down` obligations; `require-step-up` throws `NotImplementedException("Implementation pending for this capability.")`; policy catalog for locked obligation vocabulary; unit tests per policy.

**Acceptance criteria:**
- TutorRevision lifecycle (Draft → Published → Active → Archived) is operable end-to-end through the UI.
- `ActivationSlug` is visible as a read-only copyable field after activation.
- Tutor member list is visible; Owner can grant/revoke/change-role; non-Owners cannot.
- Documents can be attached/detached M:N on a tutor.
- Folders can be created/renamed/deleted; documents can be moved to a folder.
- Embedding model selector uses `EmbeddingModelId` from the model catalog.
- Document create sends non-null `RawText`.
- Models page shows `InteractionModel` and `EmbeddingModel` types correctly.
- BenchmarkRun list shows `TutorRevisionId` linkage.
- `IPolicyEvaluator` concrete implementation present; policy catalog wired for locked obligations.
- Smoke path manual verification: login → create text document → vectorize (select embedding model) → create tutor → attach document → create revision → publish → activate → verify activation slug shown.

**Human gate:** Review Iteration 2 PRs. Confirm the manual smoke path passes end-to-end in the local dev stack.

---

#### Iteration 3 — Integration, Quality & Freeze (CIL: medium)

Goal: close all remaining quality, documentation, and integration gaps; declare feature freeze.

**Web Dev tasks:**
- **Lint green gate (D34)**: fix all 9 ESLint errors and 12 warnings to achieve a clean `npm run lint` pass.
- **Benchmarking shell real data (WS8)**: replace placeholder state with real data — list benchmark templates (`GET /api/benchmark-templates`) and benchmark runs (`GET /api/benchmark-runs`) including the new `TutorRevisionId` column.

**Sec Ops task:**
- **`SaveChanges` audit interceptor PII scope review (D10)**: review `EntitySaveChangesInterceptor.cs` for PII leakage in audit metadata fields; file an inbox item for any remediation needed.

**Dev Ops tasks:**
- **Compose smoke tests**: validate all supported host combinations (Windows + Docker Desktop, macOS, Linux + NVIDIA GPU, Jetson Orin) per the smoke-test matrix in Dev Ops inbox.
- **Confirm configuration separation (D41)**: verify production security config (CSP, HSTS, rate limiting) is present in all production paths; sidecar `requirements.txt` / `requirements-dev.txt` are separated.

**Documentation tasks (Typewriter / Secretary):**
- `docs/change-notes.md`: add entry for Iterations 1–3 covering D29–D41 and the 3-iteration delivery.
- `docs/architecture.md`: update to reflect D29–D41 decisions and frontend architectural changes (centralized API client, D29 TS deferral, TutorRevision workflow).

**Acceptance criteria:**
- `npm run lint` exits 0; no ESLint errors or warnings.
- `npm run build` exits 0.
- `dotnet build bitcraft.slnx` exits 0.
- `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` passes all tests.
- Benchmarking page shows real template and run data.
- Sec Ops PII review complete or follow-up items filed.
- Compose smoke tests complete or follow-up items filed.
- `docs/change-notes.md` and `docs/architecture.md` updated.

**Human gate:** Declare feature freeze. Create post-PoC backlog from the list below.

---

### Post-PoC Backlog (do not plan — list only)

The following items are explicitly deferred and must not be added to any PoC iteration:

- TypeScript migration of Tutor Studio (D29 deferred)
- OpenAPI codegen adoption (D27 deferred)
- HttpOnly-cookie auth rotation (D32 deferred)
- Nginx reverse-proxy `/api` prefix for production (D40 deferred)
- Activation slug manual override UI (D38 deferred)
- `DocumentFolderMember` ACL (D12 deferred)
- MCP server security audit (Sec Ops batch 2)
- Metrics capture implementation (parked, pending Human decision on capture surface)
- Vitest scaffold + first smoke test (D19 deferred to post-PoC — TS adoption is the natural trigger)

---

### CSS / DIS / CIL (v3.6)

- **CSS**: 7 (touches auth routing, ABAC evaluator, membership endpoints, document contracts, TutorRevision frontend, API client centralization, nginx config).
- **DIS**: 7 (API, Application, Infrastructure, frontend components, nginx config, docs, agent inboxes).
- **CIL overall**: medium for Iterations 1 and 3; high for Iteration 2. No critical-CIL items (no secrets, no production deployment, no destructive migration).
