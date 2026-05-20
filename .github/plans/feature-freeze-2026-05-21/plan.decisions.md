---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: planner
editor: planner
ai_gen: true
---
# Feature Freeze 2026-05-21 — Decision Register (v3)

This is the persisted, human-readable register of decisions that drive the [feature-freeze-2026-05-21](plan.md) plan and its companion artifacts ([plan.erm.md](plan.erm.md), [plan.frontend.md](plan.frontend.md)). The machine-readable source of truth remains `plan.yaml.locked_decisions`; this document carries the **why**, the **alternatives considered**, and the **references**.

> Owner: Planner. Updated on every plan revision. Decisions never silently disappear; they are superseded with a new revision and a `Superseded by` row.

## Format

Each decision row contains:

- **ID** — stable identifier (D0a, D1, …).
- **Concern** — the question being decided.
- **Status** — `Locked` | `Open` | `Superseded`.
- **Decision** — the locked option, in one or two sentences.
- **Options considered** — the realistic alternatives evaluated.
- **Reasoning** — why this option won.
- **References** — primary authoritative sources.
- **Blocks / Touches** — affected work streams (WS0–WS9).
- **Revision history** — when the row last changed.

## Decisions

### D0a — Tenancy Model

- **Status**: Locked (v3).
- **Decision**: Bitcraft is **single-tenant per deployment**. The product is deployed as a standalone single-tenant application backed by a single-tenant database. Multi-owner resource link tables (e.g. `TutorMember`) are an internal Clean Architecture pattern, not a multi-tenancy mechanism.
- **Options considered**:
  - **A. Single-tenant per deployment** — locked.
  - **B. Shared multi-tenant with `TenantId` discriminator everywhere.** Heavy ABAC + row-level security cost; not justified for the PoC product shape.
  - **C. Database-per-tenant inside one app instance.** Operationally complex; defers to single-tenant deployment instead.
- **Reasoning**: Aligns with the Azure SaaS "standalone single-tenant app with single-tenant database" pattern and lets the team avoid tenant-discriminator overhead in queries, indexes, and ABAC attributes during the freeze.
- **References**:
  - [Azure SaaS — standalone single-tenant app with single-tenant database](https://learn.microsoft.com/azure/azure-sql/database/saas-tenancy-app-design-patterns#c-standalone-single-tenant-app-with-single-tenant-database)
- **Blocks/Touches**: All WS.
- **Revision history**: v3 — initial lock.

### D0b — Foundational Deployment Vision

- **Status**: Locked (v3).
- **Decision**: The Twelve-Factor App is the foundational deployment vision. AI Ops produces a Bitcraft-specific 12-factor compliance map and stores it under `.github/governance/twelve-factor.md`.
- **Options considered**:
  - **A. Twelve-Factor as foundation** — locked.
  - **B. Ad-hoc deployment doctrine.** Rejected; the team needs a shared rubric.
- **Reasoning**: Provides a shared, well-documented baseline for Config, Build/Release/Run, Backing services, and Disposability that is already implicitly assumed elsewhere in the plan.
- **References**:
  - [The Twelve-Factor App](https://12factor.net/)
- **Blocks/Touches**: All WS, especially WS0, WS1, WS2, WS7.
- **Revision history**: v3 — initial lock.

### D1 — Authorization Model

- **Status**: Locked (v3).
- **Decision**: **ABAC throughout the application.** Roles `Admin` / `Editor` / `Auditor` are coarse subject attributes, not access decisions. Resource attributes (`TutorMember.ResourceRole`, `TutorRevision.Status`, `Document.Visibility`, runtime build flag, etc.) are evaluated by a **centralized policy evaluator** in the Application layer. No role-only attribute gates outside boundary controllers.
- **Options considered**:
  - **A. Pure RBAC with three global roles.** Cannot model multi-owner Tutors, draft/active revision permissions, or admin-vs-member document operations cleanly.
  - **B. RBAC + scattered membership checks in handlers.** Current shape; produces drift and inconsistent enforcement.
  - **C. ABAC with centralized policy evaluator (locked).** Predictable, auditable, testable.
- **Reasoning**: NIST SP 800-162 ABAC is the reference model for this kind of policy where access depends on a combination of subject, resource, action, and environment attributes (build flag, IP loopback, time, etc.).
- **References**:
  - [NIST SP 800-162 — Attribute-Based Access Control](https://csrc.nist.gov/Projects/Attribute-Based-Access-Control)
  - [ASP.NET Core Authorization](https://learn.microsoft.com/aspnet/core/security/authorization/introduction)
- **Blocks/Touches**: WS1, WS6, WS7, WS8.
- **Revision history**: v3 — supersedes "RBAC-only" framing implicit in v2.

### D1.a — First ABAC Resource Attribute (Document.Visibility)

- **Status**: Locked (v3.1).
- **Decision**: `Document.Visibility` is the first ABAC resource attribute surfaced on a domain entity. Enum values: `Internal` (default, visible to all members of any Tutor the Document is attached to), `Public` (reserved, not surfaced in PoC UI), `Restricted` (reserved, not surfaced in PoC UI). The policy evaluator (O5) consumes the attribute; controllers do not branch on it directly.
- **Reasoning**: Establishes the resource-attribute shape (typed enum, default value, reserved values) before any other resource attribute is introduced, so the centralized policy evaluator contract can be designed against a real example rather than a placeholder.
- **References**: D1 (ABAC), [NIST SP 800-162](https://csrc.nist.gov/Projects/Attribute-Based-Access-Control).
- **Blocks/Touches**: WS1, WS4, WS6.
- **Revision history**: v3.1 — initial lock (Lead Dev minor decision).

### D2 — Resource Ownership Pattern

- **Status**: Locked (v3).
- **Decision**: Multi-owner resource ownership is modeled as `TutorMember(TutorId, UserId, ResourceRole)`. The same link-table shape is reused for any future resource ownership relationships. **No `OwnerId` columns** on aggregates.
- **Options considered**:
  - **A. `OwnerId` FK on each aggregate.** Single-owner only; blocks the "multiple humans build a tutor together" use case.
  - **B. `TutorMember` link table (locked).** Multi-owner from day one; ABAC-friendly.
- **Reasoning**: EF Core skip navigations + explicit join entities are the documented many-to-many shape; this is the same pattern reused for ownership, not just generic association.
- **References**:
  - [EF Core — Many-to-many relationships](https://learn.microsoft.com/ef/core/modeling/relationships/many-to-many)
- **Blocks/Touches**: WS1, WS6.
- **Revision history**: v3 — generalized from v2.

### D3 — Model Registry Shape

- **Status**: Locked (v3).
- **Decision**: EF Core TPH; abstract `Model` + `ModelType` discriminator; concrete leaves `InteractionModel`, `EmbeddingModel`, `JudgeModel` (reserved), `RerankerModel` (reserved). Common columns on the base except provider/endpoint, which live on `RuntimeEngine` (D11). Embedding-specific columns (notably `EmbeddingDimensions`) on the embedding leaf only.
- **Options considered**:
  - **A. Flat `ModelCatalogEntry` with capability flags.** Current shape; produces nullable embedding columns and weak typing.
  - **B. TPH polymorphic `Model` (locked).** Strong typing, leaf-specific columns.
  - **C. Separate tables per capability (TPT).** Heavier query cost for little gain.
- **Reasoning**: Capability-typed models let the API contract surface `ModelType`-keyed payloads cleanly and let queries like "list embedding models" run on a constrained shape.
- **References**:
  - [EF Core — Inheritance](https://learn.microsoft.com/ef/core/modeling/inheritance)
- **Blocks/Touches**: WS2, WS3, WS5, WS7.
- **Revision history**: v2 lock retained; v3 narrows base by moving provider/endpoint to `RuntimeEngine`.

### D4 — Document Scoping

- **Status**: Locked (v3).
- **Decision**: `Document` is tutor-agnostic. `TutorDocument` (M:N) attaches a Document to a Tutor for administrative selection. `DocumentFolder` is an administrative grouping (Editor/Admin) — not a runtime entity.
- **Options considered**:
  - **A. `Document.TutorId` FK.** Forces re-upload to share across tutors.
  - **B. `Document` + `TutorDocument` link + admin folders (locked).** Reusable assets, multi-tutor stacking.
- **Reasoning**: Documents are reusable RAG inputs. Folders are an admin organization tool.
- **References**:
  - [EF Core — Many-to-many relationships](https://learn.microsoft.com/ef/core/modeling/relationships/many-to-many)
- **Blocks/Touches**: WS4, WS5, WS6.
- **Revision history**: v3 — clarifies folder semantics relative to v2.

### D4b — Document/Knowledge Runtime Semantics

- **Status**: Locked (v3, new).
- **Decision**: At Tutor runtime, **only `Knowledge` (vectorized chunks)** is consumed. `Document` is a pre-runtime input. `DocumentFolder` is administrative; the runtime never sees folders. End-user file uploads at chat time are out of PoC scope and will be a separate, semantically distinct domain feature.
- **Options considered**:
  - **A. Folders as virtual stackable runtime collections.** v2 framing; conflates admin organization with retrieval scope.
  - **B. Knowledge-only runtime, admin-only folders (locked).**
  - **C. Mixed: runtime sees folders for filtering.** Rejected; turns folders into permission boundaries.
- **Reasoning**: Cleanly separates the source-of-truth (Documents) from the vectorized retrieval index (Knowledge). Re-vectorization can produce a fresh Snapshot without touching `Document` or its folder placement.
- **References**:
  - [Microsoft RAG concepts](https://learn.microsoft.com/azure/search/retrieval-augmented-generation-overview)
- **Blocks/Touches**: WS4, WS5, WS6, WS7, frontend.
- **Revision history**: v3 — supersedes v2's "stackable virtual collections" framing.

### D5 — Embedding Dimensions and ANN Index

- **Status**: Locked (v3).
- **Decision**: `KnowledgeChunk.Embedding` is untyped pgvector `vector` with a `vector_dims` CHECK constraint per `EmbeddingModelId`. ANN index strategy is **configurable**: HNSW default with IVFFlat as a supported alternative. Indexes are partial per `EmbeddingModelId`, created on enable, dropped on disable.
- **Options considered**:
  - **A. Fixed N dimension on the column.** Hard-codes a single embedding model.
  - **B. Untyped vector + per-model partial HNSW (v2).** Flexible.
  - **C. Untyped vector + configurable HNSW/IVFFlat partials (locked).** Same flexibility plus operator choice for tradeoffs.
- **Reasoning**: pgvector officially supports per-row mixed dimensions via partial indexes. HNSW gives better recall/latency at higher memory cost; IVFFlat is preferred where index memory is constrained.
- **References**:
  - [pgvector README](https://github.com/pgvector/pgvector)
- **Blocks/Touches**: WS5, WS7.
- **Revision history**: v3 — adds IVFFlat alternative.

### D5.a — Default ANN Strategy Selection

- **Status**: Locked (v3.1).
- **Decision**: The default ANN strategy per deployment is **HNSW**. IVFFlat is the alternative, selected via a deployment-level setting consumed by `EnableEmbeddingModelCommand` / `DisableEmbeddingModelCommand` when creating or dropping the per-`EmbeddingModelId` partial index. The setting is read at command-execution time; switching strategies later requires re-enabling the embedding model.
- **Reasoning**: Pins the default so deployments without explicit configuration get the better-recall option; routes the alternative through the existing enable/disable command surface instead of introducing a separate index-management API.
- **References**: D5, [pgvector README](https://github.com/pgvector/pgvector).
- **Blocks/Touches**: WS5.
- **Revision history**: v3.1 — initial lock (Lead Dev minor decision).

### D6 — Tutor Versioning

- **Status**: Locked (v3).
- **Decision**: `TutorRevision` aggregate (Draft / Published / Active / Archived) is the editable unit. `TutorConfigurationSnapshot` is a derived immutable projection captured at activation/benchmark. Re-vectorization of Documents into Knowledge produces a new Snapshot and a new active TutorRevision.
- **Options considered**:
  - **A. Mutate the Tutor in place + Snapshots only.** Conflates source and projection.
  - **B. Revision aggregate + derived Snapshot (locked).** Temporal pattern; clean rollback.
- **Reasoning**: Fowler's temporal patterns — separating the editable revision from the immutable historical projection — gives benchmarking, replay, and rollback clean semantics.
- **References**:
  - [Martin Fowler — Patterns for Modeling Time](https://martinfowler.com/eaaDev/timeNarrative.html)
- **Blocks/Touches**: WS6, WS7, WS8.
- **Revision history**: v2 lock retained.

### D7 — Activation vs Deployment

- **Status**: Locked (v3).
- **Decision**: Activation = `TutorRevision.Status(Active)` + Snapshot. `DeploymentProfile` is preserved for future export/release configuration aligned with 12-Factor build/release/run separation.
- **Options considered**:
  - **A. Activation owns deployment shape.** Conflates run-state with release-config.
  - **B. Activation is a runtime state; deployment is separate (locked).**
- **Reasoning**: 12-Factor separates build, release, and run as distinct stages; mirroring this in the domain leaves room for export/release tooling later.
- **References**:
  - [12-Factor — Build, release, run](https://12factor.net/build-release-run)
- **Blocks/Touches**: WS6, WS7.
- **Revision history**: v3 — first explicit lock.

### D8 — Runtime API Authentication

- **Status**: Locked (v3).
- **Decision**: **JWT-always design target.** Anonymous local-only mode is a **build-time** MSBuild constant (`BITCRAFT_RUNTIME_LOCAL_ONLY`) that physically excludes the anonymous code path from non-local builds. **No runtime configuration switch** can enable anonymous access.
- **Options considered**:
  - **A. Runtime configuration flag with loopback IP check (v2 proposal).** A misconfiguration in production could expose anonymous access; not acceptable.
  - **B. Compile-time constant excluding the path entirely (locked).** Defense in depth: the path simply does not exist in shipped builds.
  - **C. Always JWT, no local mode at all.** Inconvenient for local PoC iteration.
- **Reasoning**: Trades a minor build-config burden for a strong security guarantee that production builds cannot serve anonymous traffic.
- **References**:
  - [ASP.NET Core Authorization](https://learn.microsoft.com/aspnet/core/security/authorization/introduction)
  - [MSBuild conditional compilation symbols](https://learn.microsoft.com/dotnet/csharp/language-reference/preprocessor-directives#conditional-compilation)
- **Blocks/Touches**: WS7.
- **Revision history**: v3 — supersedes v2's runtime flag proposal.

### D9 — Secrets and Configuration

- **Status**: Locked (v3).
- **Decision**: 12-Factor Config: environment variables for production-bound deployments. PoC and early iterations use simple local installs and localhost testing without tracked-template ceremony beyond what is essential. The `template.<file>.<ext>` pattern is reserved for cases where a tracked sample is genuinely required; it is **not** mandatory for routine bootstrap.
- **Options considered**:
  - **A. Mandatory `template.<file>.<ext>` for every settings file (v2).** Heavy ceremony, ignored or violated in practice.
  - **B. Env-vars-first with template-on-demand (locked).**
- **Reasoning**: The Twelve-Factor App's Config principle is the canonical guidance; tracked templates serve only as documented samples when env vars are not enough.
- **References**:
  - [12-Factor — Config](https://12factor.net/config)
- **Blocks/Touches**: WS1, WS2.
- **Revision history**: v3 — supersedes v2's blanket template requirement.

### D10 — Auditing (PoC)

- **Status**: Locked (v3).
- **Decision**: PoC auditing is `BaseModel` timestamps + actor FKs via an EF Core `SaveChanges` interceptor. Future iterations adopt a Message-History-style audit log.
- **Options considered**:
  - **A. Dedicated audit history table per aggregate now.** Premature given PoC scope.
  - **B. Interceptor + `BaseModel` columns + future Message History (locked).**
- **References**:
  - [EF Core — SaveChanges interceptor](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors#savechanges-interceptor)
  - [EIP — Message History](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessageHistory.html)
- **Blocks/Touches**: WS1, WS6.
- **Revision history**: v2 lock retained.

### D11 — RuntimeEngine Entity

- **Status**: Locked (v3).
- **Decision**: `RuntimeEngine` is a first-class admin-managed entity introduced **now**: `Id`, `Name`, `ProviderType`, `Endpoint`, `ApiKeySecretRef`, `IsEnabled`. `Model.RuntimeEngineId` FK replaces provider/endpoint columns on `Model`. Health checks and richer engine lifecycle are deferred.
- **Options considered**:
  - **A. Defer the entity, store provider/endpoint on `Model` (v2).** Couples engine identity to each model and forces duplication when many models share an engine.
  - **B. Introduce now (locked).** Single source of truth for engine connectivity, prerequisite for clean admin UI.
- **Reasoning**: Avoids carrying provider/endpoint columns on every `Model` row and prepares the ground for engine health checks and rotation without another schema migration.
- **Blocks/Touches**: WS2, WS3, WS7.
- **Revision history**: v3 — supersedes v2's deferral.

### D11.a — RuntimeEngineProviderType Enum Surface

- **Status**: Locked (v3.1).
- **Decision**: `RuntimeEngineProviderType` enum values **surfaced** in the admin UI for PoC: `Ollama`, `OpenAI`, `OpenAICompatible`, `vLLM`, `LlamaCpp`. **Reserved-not-surfaced**: `AzureOpenAI`, `Anthropic`, `Bedrock` (defined in the enum so future providers do not require a schema migration, but hidden from selection dropdowns until per-provider configuration shapes are designed).
- **Reasoning**: Fixes the enum surface area now so admin contracts and UI selectors do not churn each iteration; keeps reserved providers in the schema so adding them later is a UI/wiring change rather than a migration.
- **References**: D11.
- **Blocks/Touches**: WS2, WS3.
- **Revision history**: v3.1 — initial lock (Lead Dev minor decision).

### D12 — DocumentFolderMember

- **Status**: Locked (v3).
- **Decision**: Omitted; folder access derives from tutor membership and folder ownership in `BaseModel`. To be revisited only if a concrete sharing scenario emerges.
- **Blocks/Touches**: WS4.
- **Revision history**: v2 lock retained.

### D13 — TutorRevisionTool Override

- **Status**: Locked (v3).
- **Decision**: `TutorRevisionTool` references `ToolDefinition` as-is for PoC. Per-revision tool configuration/override is post-PoC.
- **Blocks/Touches**: WS6.
- **Revision history**: v2 lock retained.

### D14–D20 — Frontend Stack

- **Status**: Locked (v3).
- **D14 Server state**: TanStack Query v5; Redux Toolkit narrowed to client-only state. ([TanStack Query](https://tanstack.com/query/latest))
- **D15 Forms**: React Hook Form + Zod. ([React Hook Form](https://react-hook-form.com/))
- **D16 Tables**: TanStack Table v8 headless. ([TanStack Table](https://tanstack.com/table/latest))
- **D17 Document/Folder UI**: Folder UI is administrative; re-evaluate `@svar-ui/react-filemanager` fit during frontend planning.
- **D18 Language**: **Migrate Tutor Studio to TypeScript now.** Adopt OpenAPI-generated client when backend contracts are stable enough not to break the frontend each iteration. ([TypeScript](https://www.typescriptlang.org/))
- **D19 Testing**: Vitest + Testing Library scaffold with at least one smoke test before freeze. ([Vitest](https://vitest.dev/))
- **D20 Design system**: Keep react-bootstrap 5.3 for PoC.
- **Reasoning**: These selections fill clear gaps in the existing stack without forcing an unnecessary design-system migration mid-freeze. TS-now removes the pain of a later JS→TS rewrite once OpenAPI codegen is adopted.
- **Blocks/Touches**: All frontend WS.
- **Revision history**: v3 — D18 flipped from v2's "stay on JS" to TS-now.

### D18.a — TypeScript Migration Order

- **Status**: Locked (v3.1).
- **Decision**: Tutor Studio is migrated to TypeScript in this order: **Settings → ModelCatalog → Tutors → Files → Benchmarking**. Shared modules (API client, store, hooks) are migrated only after at least three pages are converted, so shared types are extracted from real usage. `strict: true` and `noUncheckedIndexedAccess` are enabled at the **end** of the migration, after all pages compile, to avoid noise during incremental conversion.
- **Reasoning**: Settings and ModelCatalog have the simplest local state and the cleanest backend contracts, so they catch contract-shape bugs early. Tutors and Files are the largest surfaces and benefit from already-migrated shared modules. Enabling strict flags last avoids a wall of errors that block the migration mid-flight.
- **References**: D18, [TypeScript handbook — strict mode](https://www.typescriptlang.org/tsconfig/#strict).
- **Blocks/Touches**: All frontend WS.
- **Revision history**: v3.1 — initial lock (Lead Dev minor decision).

### D21 — Migration Sequencing

- **Status**: Locked (v3).
- **Decision**: N-step dependency-ordered migrations. Step count is recomputed during replanning after each ERM change; current target lives in [plan.erm.md §10](plan.erm.md#10-migration-sequencing).
- **References**:
  - [EF Core — Managing schemas with migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations)
- **Blocks/Touches**: WS2–WS7.
- **Revision history**: v2 lock retained; step count expected to grow by one for `RuntimeEngine`.

### D22 — Activation Slug

- **Status**: Locked (v3).
- **Decision**: Slug derived from Tutor name (URL-safe, human-readable). Uniqueness enforced; renames recompute the slug and retain the previous slug as a one-revision redirect, then drop it on the next activation.
- **Blocks/Touches**: WS7, frontend.
- **Revision history**: v3 — first explicit lock.

### D22.a — Activation Slug Storage Location

- **Status**: Locked (v3.1).
- **Decision**: Activation slug storage lives **on the activation record** (the `TutorRevision` row whose `Status` is `Active`, or an adjacent activation entity if introduced), **not on `Tutor`**. The previous slug is retained as a one-revision redirect (per D22) on the activation that owned it and is dropped on the next activation. Lookup by slug resolves through the activation surface, not through `Tutor` columns.
- **Reasoning**: Keeps `Tutor` free of runtime-state columns and aligns slug history with the revision/activation lifecycle that already owns the one-revision redirect window. Avoids a stale slug column on `Tutor` when no revision is active.
- **References**: D22, D6, D7.
- **Blocks/Touches**: WS6, WS7.
- **Revision history**: v3.1 — initial lock (Lead Dev minor decision).

### D23 — Effective Document Set

- **Status**: Locked (v3).
- **Decision**: Materialized at activation time into the Snapshot. Documents and folder attachments mutated after activation do not affect the active runtime until a new activation is created.
- **Blocks/Touches**: WS4, WS5, WS6, WS7.
- **Revision history**: v3 — first explicit lock.

### D22.b — Activation Slug Storage Shape

- **Status**: Locked (v3.2).
- **Decision**: Activation slug is persisted as two **nullable columns on the `TutorRevision` row whose `Status` is `Active`**: `ActivationSlug` and `PreviousActivationSlug`. No separate `TutorActivation` entity is introduced for the freeze.
- **Options considered**:
  - **A. Columns on `Tutor`.** Couples slug history to the aggregate root and complicates rename semantics.
  - **B. Columns on the active `TutorRevision` (locked).** Slug is naturally scoped to the activation; one-revision redirect (D22) maps directly to `PreviousActivationSlug` carried on the new active revision.
  - **C. Separate `TutorActivation` entity.** Overkill for PoC; revisit if multiple concurrent activations (e.g. blue/green) are introduced.
- **Reasoning**: Smallest schema delta that supports D22 redirect lifecycle cleanly. Resolves the inconsistency flagged by Typewriter between [plan.decisions.md D22.a](#d22a--activation-slug-storage) and [plan.erm.md §7.1](plan.erm.md#7-tutoring-context).
- **Blocks/Touches**: WS7.
- **Revision history**: v3.2 — Planner lock (closes Typewriter inconsistency report).

### D24 — Centralized ABAC Policy Evaluator Location

- **Status**: Locked (v3.2).
- **Decision**: Centralized ABAC policy evaluator lives at **`src/Bitcraft.Application/Common/Authorization/IPolicyEvaluator`** and returns a structured `PolicyDecision { Effect: Permit/Deny/NotApplicable, Obligations: IReadOnlyList<Obligation>, Reasons: IReadOnlyList<string> }`. Attribute-fetching adapters (subject, resource, environment) live in **`src/Bitcraft.Infrastructure/Authorization/`** and are registered via DI. Sec Ops co-owns the policy catalog and is the reviewer for new policies.
- **Options considered**:
  - **A. Application-layer evaluator + Infrastructure adapters (locked).** Domain stays pure; testable in unit tests; aligns with Clean Architecture dependency flow.
  - **B. Shared module spanning Application + Infrastructure.** Breaks layer rules; rejected.
  - **C. Inline membership checks in handlers.** Status quo; produces drift; rejected by D1.
- **Reasoning**: Application owns the policy contract because authorization is a use-case concern. Infrastructure owns attribute resolution because attributes live in EF Core, JWT claims, build flags, and request context. Returning obligations rather than a boolean leaves room for "permit-with-audit", "permit-with-redaction", and other ABAC extensions without re-plumbing.
- **References**:
  - [NIST SP 800-162 — ABAC](https://csrc.nist.gov/Projects/Attribute-Based-Access-Control)
  - [ASP.NET Core Authorization](https://learn.microsoft.com/aspnet/core/security/authorization/introduction)
- **Blocks/Touches**: WS1, WS6, WS7.
- **Revision history**: v3.2 — Planner lock (closes Open Decision O5).

### D25 — Backend Migration Sequence (8 Steps)

- **Status**: Locked (v3.3).
- **Decision**: The 8-step migration sequence in [plan.erm.md §10](plan.erm.md#10-migration-sequencing) is approved as the binding order for all backend schema work: **Identity-And-Roles-Vocabulary → Resource-Membership-Tables → RuntimeEngine-Entity → Model-Tph-Hierarchy → Tutor-Document-Mn → Knowledge-Dynamic-Dimensions → Tutor-Revisions → Benchmark-Fk-Realign**. Each step must build clean before the next is added.
- **Reasoning**: Order is dependency-driven (resource membership tables read the roles vocabulary; Model TPH depends on RuntimeEngine; Tutor revisions and benchmarking FKs require all prior tables). Keeps the build green per step, enables per-step PR review, and prevents large rebase storms.
- **Blocks/Touches**: WS2, WS3, WS4, WS5, WS6, WS7.
- **Revision history**: v3.3 — Human approval (closes Open Decision O1).

### D26 — BITCRAFT_RUNTIME_LOCAL_ONLY Definition Site And CI Defaults

- **Status**: Locked (v3.3).
- **Decision**: The build-time runtime auth gate is the conditional compilation symbol **`BITCRAFT_RUNTIME_LOCAL_ONLY`**, defined in repo-root **`Directory.Build.props`** under a `Condition` guard for `Configuration == 'Local'`. Off in `Debug` and `Release`. **CI never sets it.** When set, runtime chat endpoints permit anonymous local-only access; otherwise JWT is always required (D8).
- **References**: [MSBuild conditional compilation symbols](https://learn.microsoft.com/dotnet/csharp/language-reference/preprocessor-directives#conditional-compilation), [Directory.Build.props](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory).
- **Blocks/Touches**: WS7.
- **Revision history**: v3.3 — Human confirmation (closes Open Decision O2).

### D27 — Frontend OpenAPI Client Adoption Timing

- **Status**: Locked (v3.3).
- **Decision**: Frontend OpenAPI-generated client adoption is **deferred until after the PoC**, defined as at least one full PoC iteration in which backend contracts hold shape without breaking the frontend. Until then, hand-written TypeScript clients live alongside the converted pages and are typed from the same Zod schemas used by React Hook Form (D15).
- **Reasoning**: Backend contracts will churn during WS2–WS7 implementation. Codegen now would force regenerate-and-fix loops every iteration. Hand-written clients keep the frontend moving with TS-now (D18) typing benefits, and the Zod schemas form a single source of truth that can later be replaced by generated types without rewriting consumers.
- **Blocks/Touches**: All frontend WS.
- **Revision history**: v3.3 — Human deferral (closes Open Decision O3).

### D28 — Frontend Library Set And Admin-Only DocumentFolder UX (Cohesion Confirmed)

- **Status**: Locked (v3.4).
- **Decision**: [plan.frontend.md](plan.frontend.md) v3 **library set** — TanStack Query v5 (D14), React Hook Form + Zod (D15), TanStack Table v8 headless (D16), Vitest + Testing Library (D19), retain react-bootstrap 5.3 (D20) — and the **admin-only `DocumentFolder` UX** (D4b / D17) are approved as cohesive with the backend contract shape produced by the D25 8-step migration. Web Dev owns the `@svar-ui/react-filemanager` keep-or-replace call within the admin-only constraint.
- **Reasoning**: All Open Decisions on which this gate depended are now closed: D25 fixes the contract evolution order, D27 defers OpenAPI codegen until contracts hold shape (avoiding regenerate-break churn during frontend buildout), D18 / D18.a lock TypeScript-now and the migration order, and D4b / D17 keep `DocumentFolder` out of the runtime surface. There is no remaining frontend↔backend contract conflict to resolve before Web Dev starts.
- **Blocks/Touches**: All frontend WS (WS9).
- **Revision history**: v3.4 — Human approval (closes Open Decision O4).

## Open Decisions (Tracking)

The current set of open decisions is the **Open Human Decisions (v3)** table in [plan.md](plan.md#open-human-decisions-v3). They are not duplicated here to avoid drift; only when a decision is locked does it migrate into this register.

## Companion Artifacts

- [plan.md](plan.md) — work-stream plan and acceptance criteria.
- [plan.erm.md](plan.erm.md) — entity-relationship model and migration sequencing.
- [plan.frontend.md](plan.frontend.md) — frontend iteration plan.
- [plan.yaml](plan.yaml) — machine-readable plan metadata (single source of truth for `locked_decisions`).
- [docs/architecture.md](../../../docs/architecture.md) — living architecture document (Typewriter).
- [docs/change-notes.md](../../../docs/change-notes.md) — release-batch change notes (Secretary).
- [.github/governance/twelve-factor.md](../../governance/twelve-factor.md) — 12-Factor compliance map (AI Ops, to be created).
