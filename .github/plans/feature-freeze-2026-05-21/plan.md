---
created_at: 2026-05-19
edited_at: 2026-05-20
creator: planner
editor: planner
ai_gen: true
---
# Plan: Feature Freeze 2026-05-21

> Origin: Planner, 2026-05-19. Human review: revised with product POC decisions.

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

## Architecture Decision Gates

These gates should be decided before the relevant development branch starts. They are intentionally narrow so the team can keep moving between pauses.

| Gate | Options | Recommendation For Freeze | Blocks |
|---|---|---|---|
| Role and access model | A. Global roles only; B. global roles plus tutor/project memberships; C. granular permission claims. | Use global roles `Admin`, `Editor`, `Auditor`; keep `User` reserved and unused for internal POC accounts. Continue owner/resource checks for tutor data. Add project/tutor membership only if Human confirms collaboration is required before freeze. | WS1, WS6, WS7 |
| Runtime endpoint auth | A. JWT/owner checked; B. unauthenticated local endpoint; C. scoped API keys. | Management API stays JWT. Runtime API is separate and can be unauthenticated only behind a local-only flag/host constraint for POC activation testing. Scoped API keys are post-freeze. | WS7 |
| Tutor versioning | A. existing snapshots on activation; B. draft/published `TutorRevision`; C. cloned tutor versions; D. event sourcing; E. deployment artifact manifests. | Use existing `TutorConfigurationSnapshot` as the activation snapshot now. Defer `TutorRevision` until workflow pressure proves it is needed. Event sourcing is too costly for this POC. | WS6, WS7 |
| Runtime engine model | A. keep endpoint on `ModelCatalogEntry`; B. introduce `RuntimeEngine` and reference it from catalog entries; C. sidecar/external orchestrator. | POC can keep the current catalog shape if time is tight, but the UI and contracts should use Runtime Engine vocabulary. Add a `RuntimeEngine` entity only if it reduces duplication without delaying activation. | WS2, WS3, WS7 |
| Model assignment | A. one `TutorModelConnection`; B. typed tutor model assignments; C. JSON-only settings. | Prefer typed assignments for interaction and embedding roles if a schema change is already happening. Otherwise keep one interaction connection and record embedding model in vectorization metadata/snapshot. Avoid JSON-only as the long-term shape. | WS3, WS5, WS6, WS7 |
| Document scoping | A. add `TutorId` to `Document`; B. add `TutorDocumentLink`; C. leave global docs and link only knowledge. | Use `TutorId` for the POC if documents are truly tutor-specific. Choose `TutorDocumentLink` only if immediate cross-tutor reuse is required. | WS4, WS5, WS6 |

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

### WS1 - Bootstrap Template And User System

- **Current State**: Roles and hardcoded admin credentials are seeded from startup code; startup logs and continues on seeding failure. Current roles are Admin/Editor/Viewer, but the requested Product roles are Admin/Editor/Auditor/User with `User` reserved for future runtime/end-user scenarios.
- **Decision/Approach**: Production release is out of scope, but the POC must remove hardcoded credentials. Create a tracked bootstrap template with placeholders and clear local/dev/test usage. Actual deployment-specific scripts and secrets stay untracked and human-owned. Roles remain idempotent reference data. Admin/user creation is explicit via template/config, not hidden in normal startup. Rename/replace Viewer with Auditor if Human approves the role vocabulary now; keep runtime end users out of internal account management for POC.
- **Owner**: Sec Ops architecture, NET Dev implementation, Lead Dev orchestration.
- **CIL**: high.
- **Acceptance**: no hardcoded admin password; tracked template has placeholders only; local/dev/test can seed configured users and roles from the template pattern; duplicate runs are idempotent; docs match behavior; tests cover configured bootstrap and disabled/default state.

### WS2 - Product Settings, Runtime Engines, And Provider Configuration

- **Current State**: Configuration is static appsettings/env vars; Settings page is a placeholder.
- **Decision/Approach**: Add an Admin Settings view/API for Product environment/provider/runtime engine configuration. It should support viewing and, where approved, updating Ollama/OpenAI-compatible endpoint references, local mode flags, default engine/model references, and feature flags needed by the POC. Segment the UI into Environment, Runtime Engines, Models, Tutor Defaults, and User/Access. For POC, secrets are represented by secret references or environment variable names, not raw returned values. If Human wants UI-entered provider secrets before freeze, that becomes a separate security-reviewed decision.
- **Owner**: NET Dev and Web Dev, Sec Ops review for any setting that can affect security or provider behavior.
- **CIL**: high if mutable, medium if read-only plus references.
- **Acceptance**: Admin can view and, where approved, update safe Product settings; no secret values are returned to the client; provider config supports Ollama and OpenAI references; Tutor Studio uses shared API base config; settings route is Admin-only and tested.

### WS3 - Models View, Capabilities, And Model Wizard

- **Current State**: Model catalog CRUD exists in backend and a basic frontend page exists, but the Product workflow needs capability-aware model configuration.
- **Decision/Approach**: Add or refine internal Models menu/view for CRUD and a minimal model configuration/creation wizard. Preserve current style/structure. The wizard should capture name, runtime engine/provider, model name, endpoint/reference, capabilities, embedding dimensions where relevant, hyperparameter defaults, prompt/template metadata, enabled state, and health status where available. Interaction LLM/VLM models and embedding models must be visibly distinct. Avoid adding a new framework.
- **Owner**: Web Dev primary, NET Dev for contract/domain changes.
- **CIL**: high if domain/API model changes, medium for UI-only refinement.
- **Acceptance**: Admin can create/list/update/delete model catalog entries; models are categorized by capability; vectorization flow can select embedding-capable models; tutor runtime flow can select chat-capable models.

### WS4 - Minimal Tutor-Specific Document Ingestion

- **Current State**: Document CRUD exists; frontend upload posts no raw text, so vectorization cannot work for user-uploaded files. Documents are not tutor-specific in the current domain model.
- **Decision/Approach**: Implement tutor-specific plain text ingestion only. Prefer adding `TutorId` to `Document` if documents are not intended to be shared across tutors before freeze; otherwise add a `TutorDocumentLink`. Client reads `.txt`, `.md`, `.json`, and small text-like files as text and posts `rawText`. Keep local-file/folder concepts as metadata or UI grouping only unless Human approves server-side storage. Add a simple metadata popup; full document viewer is post-freeze.
- **Owner**: Web Dev primary, NET Dev for any contract hardening.
- **CIL**: medium.
- **Acceptance**: Editor can create/list/delete a text document with non-empty raw text; vectorization rejects empty raw text cleanly; UI surfaces vectorization status and chunk count.

### WS5 - Knowledge Vectorization Workflow

- **Current State**: Vectorization uses fixed-size chunks and HNSW `vector(768)`; retrieval is scoped to tutor-linked documents.
- **Decision/Approach**: Vectorization becomes a Product workflow: select tutor document(s), select embedding-capable model, choose simple chunk settings, run vectorization, and record provider/model/dimensions on chunks. Keep fixed chunking for freeze but add guardrails: validate embedding dimension before persistence and return useful errors. Activation may offer an "auto-vectorize missing/outdated tutor documents" checkbox, but the primary action remains explicit. Semantic chunking, overlaps, halfvec, binary quantization, reindex jobs, and hybrid search move after freeze.
- **Owner**: NET Dev, Researcher for model dimension confirmation, Human for model decision.
- **CIL**: high if schema changes, medium if only validation/config hardening.
- **Acceptance**: Editor can select an embedding model during vectorization; vectorization succeeds with the selected model, fails safely on dimension mismatch, replaces chunks transactionally, and retrieval remains tutor/owner scoped.

### WS6 - Tutor Configuration Flow

- **Current State**: Backend supports tutor, prompts, model connection, knowledge source links, tools, deployment profile, and snapshots; frontend only lists tutors and cannot configure details.
- **Decision/Approach**: Build a minimal tutor detail/config route. Required panels: interaction model, embedding/vectorization status, prompt, tutor documents/knowledge, activation, future deployment profile, and test runtime. Product MCP/tutor connector panel remains metadata only and disabled for runtime execution unless Human explicitly promotes it. Before implementation, choose whether POC versioning is snapshot-only or needs a draft/published revision table.
- **Owner**: Web Dev primary, NET Dev for missing read endpoints or contract adjustments.
- **CIL**: high because it crosses frontend/API contract and tutor aggregate behavior.
- **Acceptance**: Editor can create tutor, add prompt, connect enabled chat-capable model catalog entry, attach vectorized document, save deployment profile, activate tutor, capture snapshot, and navigate back to list.

### WS7 - Tutor Activation And Separate Runtime API

- **Current State**: Low-level Ollama generation exists, but no runtime endpoint composes tutor model, prompt, knowledge, and generation.
- **Decision/Approach**: Add tutor activation and a non-streaming runtime ask endpoint under a separate runtime controller/contract namespace. Activation captures the runnable snapshot and marks the local endpoint available. The runtime handler should use the activated interaction model, use the selected embedding model for query embedding, retrieve top tutor-linked chunks, assemble a bounded prompt from active prompt plus sources, call the existing generation abstraction, and return answer plus source metadata. For POC, unauthenticated runtime access is acceptable only if local-only guarded; otherwise keep JWT owner/editor checks. No memory, no tools, no streaming, no Product MCP execution.
- **Owner**: NET Dev primary; Sec Ops review for prompt-injection and data boundary; Web Dev consumes in test panel.
- **CIL**: high.
- **Acceptance**: Authenticated editor can activate and ask a tutor; inactive tutors do not serve runtime responses; non-owner cannot use the endpoint; response includes answer, active snapshot/deployment id, model label, retrieved source/chunk metadata, and safe errors for missing model/prompt/knowledge; focused unit/API tests cover ownership and missing configuration.

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

## Open Human Decisions

| # | Question | Recommendation | Blocks |
|---|---|---|---|
| 1 | What exact Product model workflow should Tutor Studio show? | Use Settings -> Models -> Documents -> Vectorize -> Tutor -> Activate -> Test. Human reviews final UI flow before frontend buildout. | WS2-WS7 |
| 2 | Should model capabilities be explicit fields now or represented in settings JSON for POC? | Prefer explicit capability metadata now because it defines interaction vs embedding vs future expert model architecture. | WS3, WS5, WS7 |
| 3 | Should Runtime Engine be a first-class entity now? | Prefer vocabulary/UI now; add an entity only if it reduces duplicated endpoint/provider config without blocking activation. | WS2, WS3 |
| 4 | Can Tutor Studio store provider secrets, or only secret references for POC? | Prefer secret references only. Actual secret scripts remain untracked and human-owned. | WS2, WS3 |
| 5 | What embedding model/dimension is approved for the first vectorization flow? | Select via Models view; validate dimension before persistence; default to 768-compatible output unless schema changes are approved. | WS5, WS7 |
| 6 | Are documents strictly tutor-specific before freeze? | If yes, add `TutorId` to `Document`; if reuse matters immediately, add `TutorDocumentLink`. | WS4, WS5, WS6 |
| 7 | What role strategy should management API use? | Admin/Editor/Auditor as internal global roles; `User` reserved; resource checks for owner/tutor data; scoped membership post-freeze unless required now. | WS1, WS6, WS7 |
| 8 | What versioning option is approved? | Snapshot-only activation for POC; analyze `TutorRevision` after freeze. | WS6, WS7 |
| 9 | Should runtime endpoint be authenticated or local unauthenticated? | Prefer separate `/api/runtime/...`; allow unauthenticated only with local guard and Human approval. | WS7 |
| 10 | Is Product MCP connector metadata required before freeze? | Optional metadata only. No runtime execution. | WS6, WS7 |
| 11 | Is benchmarking shell acceptable with no deep integration? | Yes. UI shell only is accepted for freeze. | WS8 |

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
- Semantic chunking and overlap, re-embedding jobs, halfvec/binary quantization, hybrid search, vector index maintenance commands.
- OpenTelemetry spans, trace visualization, Stream of Thought UI.
- Runtime engine fleet management, vLLM/llama.cpp/cloud runners, orchestrator/expert execution, Product MCP connector runtime execution, per-client OAuth consent, remote Product MCP integrations.
- Statistical benchmarking, confidence intervals, p-values, LLM-as-judge, export/reporting.
- Rich file manager, folders backed by storage, binary ingestion, OCR, PDF/DOCX parsers.
- Tutor revision tables if snapshot-only activation proves insufficient, deployment/export artifacts, conversation history, streaming answers, external runtime API keys, encrypted remote model secret storage, true fine-tuning/training lifecycle.

## Inbox Updates Queued

- Pending Human plan approval; after approval, Lead Dev should create NET Dev, Web Dev, Sec Ops, Dev Ops, and Researcher inbox items from the work streams above rather than duplicating this entire plan.
