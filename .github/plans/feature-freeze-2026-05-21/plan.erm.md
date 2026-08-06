---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# Plan Addendum: Bitcraft Entity-Relational Model (Target v3)

> Origin: Lead Dev, 2026-05-20. Companion to [plan.md](plan.md). Locks the
> production-grade ERM. Supersedes the conflicting items in the original
> "Architecture Decision Gates" table; the `plan.md` table is updated to match.
> Migrations to land this ERM are sequenced per work stream in `plan.md`; this
> file is the canonical reference.
>
> **v3 amendments (Planner, 2026-05-20)** — captured as deltas below for
> traceability. The body of this document has been brought into alignment with
> the v3 deltas (Lead Dev, 2026-05-20). The decision register is
> [plan.decisions.md](plan.decisions.md).
>
> ## v3 Amendments
>
> 1. **RuntimeEngine entity (D11)** — introduce a first-class admin-managed
>    `RuntimeEngine(Id, Name, ProviderType, Endpoint, ApiKeySecretRef,
>    IsEnabled)` aggregate. Move `ProviderType`, `Endpoint`, and
>    `ApiKeySecretRef` off of §5.2 ("Common columns on Model") onto
>    `RuntimeEngine`. Add `Model.RuntimeEngineId` FK (required) and
>    `Model.ProviderModelName` (string). Cascade: restrict delete on
>    `RuntimeEngine` while any `Model` references it.
> 2. **ANN index strategy (D5)** — alongside the partial HNSW index helper in
>    §6.2, document an equivalent partial **IVFFlat** helper. Strategy is
>    configurable per deployment; default HNSW. Both helpers are partial,
>    keyed by `EmbeddingModelId`.
> 3. **DocumentFolder semantics (D4, D4b)** — clarify §4.3: `DocumentFolder` is
>    an administrative grouping owned by Editor/Admin, not a runtime concept.
>    The runtime never reads folders; only `Knowledge` is consumed at retrieval
>    time. Effective document set is materialized into the Snapshot at
>    activation (D23).
> 4. **ABAC policy attributes (D1)** — add an `Document.Visibility` attribute
>    column (enum: `Internal` default; reserved for future `Public` /
>    `Restricted`) to support ABAC-style resource attributes. The policy
>    evaluator lives in the Application layer and is the only place that
>    authorizes resource access; this ERM exposes the attributes it needs.
> 5. **Single-tenancy (D0a)** — confirmed: no `TenantId` columns are added
>    anywhere. Multi-owner relations remain link-table based.
> 6. **Auditing (D10)** — `BaseModel` already carries timestamps + actor FKs;
>    no schema changes here. Implementation lands in WS0/WS1 via a
>    `SaveChanges` interceptor.
> 7. **Migration sequencing (§10)** — recomputed step count grows by one to
>    introduce `RuntimeEngine` ahead of the `Model` TPH migration. Lead Dev
>    confirms the final ordering when v3 ERM body is rewritten.
>
> v3 body rewrite is tracked as Open Decision **O1** in [plan.md](plan.md#open-human-decisions-v3).

## 1. Principles

1. Every persisted aggregate inherits [BaseModel](../../../src/Bitcraft.Domain/Common/BaseModel.cs) (`Id`, `CreatedUtc/By`, `ModifiedUtc/By`). Author/last-editor are first-class on every row.
2. Aggregates **do not** carry an `OwnerId` column. Resource-level access is modeled via explicit `*Member` link tables with a scoped `ResourceRole` (Owner, Editor, Viewer). Editors and owners are sets that change over time without rewriting the aggregate row.
3. Documents are **not** scoped to a single tutor. Tutor↔Document is many-to-many through a [TutorDocument](#42-tutordocument-tutordocumentfolder) link. Documents are also stackable via virtual `DocumentFolder` collections (folder / tag / smart-filter), and a tutor may attach either individual documents or entire folders.
4. Polymorphic concepts use **EF Core TPH** with a discriminator column unless storage layout forces otherwise. First application: the abstract `Model` hierarchy.
5. Embedding dimensions are **dynamic** and bound to the embedding model. `KnowledgeChunk.Embedding` uses untyped pgvector `vector` (no fixed `vector(N)` on the column). ANN indexes are **partial, per-`EmbeddingModelId`**, created/dropped as embedding models are enabled/disabled. Re-embedding scopes by `(Document, EmbeddingModel)`.
6. Tutor versioning is **uniform**. The `TutorRevision` aggregate is the editable, append-only unit of change (Draft → Published → Active → Archived). `TutorConfigurationSnapshot` is a derived immutable projection captured at activation/benchmark time. Runtime, activation, and benchmarking all key off a revision.
7. Linking tables follow `<Left><Right>` naming, inherit `BaseModel`, and declare cascade rules explicitly.
8. Hard delete remains the default. Soft-delete is not introduced.
9. Secret material never lives in any column. `*SecretRef` columns hold a logical key resolved against the deployment secret store (env var, file, vault). Source control carries only `template.*` files (see [plan.md → Template Configuration Convention](plan.md#template-configuration-convention)).

## 2. Bounded Contexts

| Context | Purpose | Key Aggregates |
|---|---|---|
| Identity | Authentication and global roles | `AppUser`, `AppRole` (Identity defaults) |
| Access | Resource-scoped membership | `TutorMember`, `DocumentFolderMember` (optional) |
| Modeling | LLM/VLM/Embedding/Judge/Reranker registry | `Model` (abstract) + concrete TPH leaves |
| Documents | Source-of-truth content + virtual collections | `Document`, `DocumentFolder`, `DocumentFolderEntry` |
| Knowledge | Vectorized derivative for RAG | `KnowledgeChunk` |
| Tutoring | Tutor lifecycle, prompts, tools, revisions | `Tutor`, `PromptVersion`, `ToolDefinition`, `TutorRevision`, `TutorRevisionTool`, `TutorDocument`, `TutorDocumentFolder`, `TutorConfigurationSnapshot` |
| Benchmarking | Reproducible runs against revisions/snapshots | `BenchmarkTemplate`, `BenchmarkRun`, `BenchmarkRunCase` |

## 3. Identity And Access

### 3.1 Global roles (unchanged)

Fixed in code via [AppRoles](../../../src/Bitcraft.Application/Common/Security/AppRoles.cs):

| Role | Purpose |
|---|---|
| `Admin` | Full management surface; all settings; user/role admin. |
| `Editor` | Tutor/document/knowledge authoring; subject to resource membership for tutor data. |
| `Auditor` | Read-only access to audit-relevant data: snapshots, benchmark runs, logs. No mutation. |

`User` is **not** seeded for the freeze. Runtime end-users are out of scope until external runtime is in scope.

### 3.2 Resource membership

Resource-scoped role enum:

```csharp
public enum ResourceRole { Owner = 0, Editor = 1, Viewer = 2 }
```

| Table | Columns (in addition to BaseModel) | Notes |
|---|---|---|
| `TutorMember` | `TutorId`, `UserId`, `Role ResourceRole`, `GrantedUtc`, `GrantedById` | Unique `(TutorId, UserId)`. A tutor must have ≥1 Owner at all times (Application invariant). |
| `DocumentFolderMember` (optional, post-freeze toggle) | `DocumentFolderId`, `UserId`, `Role` | Reserved schema; UI off for freeze. |

Authorization rule (Application layer, no policy bypass at controllers):

> An action on a tutor is permitted iff the principal holds global `Admin`, **or** holds global `Editor` plus a `TutorMember` row whose `Role <= Editor`. `Auditor` is read-only across the board and bypasses membership for read.

Documents inherit access through the tutors they are linked to plus the global `Editor`/`Admin` rules; no per-document ACL for freeze.

## 4. Document Context

### 4.1 Document (source of truth)

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | BaseModel |
| `Name` | `string` | required |
| `MimeType` | `string` | freeze: `text/plain`, `text/markdown`, `application/json` |
| `SizeBytes` | `long?` | optional |
| `Source` | `DocumentSource` enum | `UploadedText`, `Url`, `Synthetic` (future) |
| `Checksum` | `string?` | sha256 hex, for dedup hints |
| `RawText` | `string?` | freeze: required for vectorizable docs |
| `Visibility` | `DocumentVisibility` enum | default `Internal`; reserved `Public`, `Restricted`. Exposed as a resource attribute for the ABAC policy evaluator (D1). |

**No `TutorId`, no `OwnerId`.** Author/last-editor come from `BaseModel`.

### 4.2 TutorDocument, TutorDocumentFolder

| Table | Columns | Unique | Cascade |
|---|---|---|---|
| `TutorDocument` | `TutorId`, `DocumentId`, `AttachedVia AttachmentSource` (Direct, Folder) | `(TutorId, DocumentId)` | Cascade from Tutor; Restrict from Document |
| `TutorDocumentFolder` | `TutorId`, `DocumentFolderId` | `(TutorId, DocumentFolderId)` | Cascade from Tutor; Restrict from Folder |

Resolution: when a tutor is activated or its knowledge is rebuilt, the effective document set = `union(direct TutorDocument, all entries of attached TutorDocumentFolder)`. Resolution is materialized into the revision's snapshot at activation time so the runtime never recomputes membership.

### 4.3 DocumentFolder (administrative grouping)

`DocumentFolder` is an **administrative grouping** owned by Editor/Admin scope for organizing attached documents (D4, D4b). The Tutor runtime never reads folders; only `Knowledge` (vectorized chunks) is consumed at retrieval time. The effective document set is materialized into the Snapshot at activation (D23); documents and folder attachments mutated after activation do not affect the active runtime until a new activation is created.

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `Name` | `string` | required |
| `Description` | `string?` | |
| `Kind` | `FolderKind` enum | `Folder`, `Tag`, `SmartFilter` (reserved) |

For freeze: flat (no parent folder). `SmartFilter` columns are reserved as nullable for a future filter expression payload. End-user file uploads at chat time are out of PoC scope and will be modeled as a separate runtime concept, not as folders.

### 4.4 DocumentFolderEntry

| Column | Notes |
|---|---|
| `DocumentFolderId` | FK |
| `DocumentId` | FK |
| Unique | `(DocumentFolderId, DocumentId)` |

Same document may live in many folders. Many tutors may attach the same folder.

## 5. Modeling Context — RuntimeEngine

### 5.1 RuntimeEngine (admin-managed connectivity)

`RuntimeEngine` is a first-class admin-managed aggregate that owns the connectivity to an upstream model runtime (D11). Many `Model` rows may share one engine (e.g. one Ollama instance serving several models).

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | BaseModel |
| `Name` | `string` | display name; unique |
| `ProviderType` | `RuntimeEngineProviderType` enum | locked values: `Ollama`, `OpenAI`, `OpenAICompatible`, `vLLM`, `LlamaCpp`. Reserved (not surfaced in UI for freeze): `AzureOpenAI`, `Anthropic`, `Bedrock`. |
| `Endpoint` | `string` | base URL or socket reference |
| `ApiKeySecretRef` | `string?` | logical key looked up against the deployment secret store; never a raw secret |
| `IsEnabled` | `bool` | gates selection of any child `Model` in tutor/knowledge UI |

Health checks and richer engine lifecycle are deferred (D11). Disable cascades to UI visibility of all dependent models but does not delete `Model` rows.

## 5b. Modeling Context — abstract Model (TPH)

### 5b.1 Hierarchy (EF Core TPH)

Single `Models` table with a `ModelType` discriminator column. Selected because (a) we query mostly across all models for admin listings, (b) leaf-specific columns are few, (c) TPH is the EF Core team's default recommendation unless leaf-mostly queries dominate ([EF Core inheritance guidance](https://learn.microsoft.com/en-us/ef/core/modeling/inheritance#summary-and-guidance)).

```text
Model (abstract, BaseModel)
├── InteractionModel    // tutor-facing LLM/VLM
├── EmbeddingModel      // produces KnowledgeChunk embeddings
├── JudgeModel          // LLM-as-judge (benchmarking; schema-ready, off for freeze)
└── RerankerModel       // cross-encoder (post-freeze)
```

### 5b.2 Common columns (on `Model`)

Provider connectivity columns (`ProviderType`, `Endpoint`, `ApiKeySecretRef`) live on `RuntimeEngine` (§5.1), not on `Model`. `Model` carries only what is intrinsic to the upstream model identity.

| Column | Type | Notes |
|---|---|---|
| `Name` | `string` | display name |
| `RuntimeEngineId` | `Guid` | **FK RuntimeEngine, required, `Restrict` on delete.** A `RuntimeEngine` with any referencing `Model` cannot be hard-deleted. |
| `ProviderModelName` | `string` | upstream id served by the runtime engine (e.g., `qwen3-embedding:0.6b`) |
| `IsEnabled` | `bool` | gates selection in tutor/knowledge UI (in addition to the engine's `IsEnabled`) |
| `SettingsJson` | `string?` | provider-specific tuning |
| `ModelType` | discriminator | required |

### 5b.3 Leaf-specific columns

| Leaf | Extra columns | Notes |
|---|---|---|
| `InteractionModel` | `ContextWindow int?`, `SupportsVision bool` | |
| `EmbeddingModel` | `EmbeddingDimensions int` **(required)**, `MaxInputTokens int?`, `Normalized bool` | `EmbeddingDimensions` drives partial-index creation. |
| `JudgeModel` | reuses common columns; reserved | |
| `RerankerModel` | reuses common columns; reserved | |

### 5b.4 Disabling/removing an embedding model

A `Restrict` FK from `KnowledgeChunk.EmbeddingModelId` to `Model.Id` enforces: an embedding model with persisted chunks cannot be hard-deleted until those chunks are purged. UI exposes a guided purge action.

## 6. Knowledge Context

### 6.1 KnowledgeChunk

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | BaseModel |
| `DocumentId` | `Guid` | FK Document, `Cascade` |
| `EmbeddingModelId` | `Guid` | FK Model, `Restrict` |
| `EmbeddingDimensions` | `int` | denormalized for query routing and index targeting |
| `ChunkIndex` | `int` | 0-based position within (Document, EmbeddingModel) |
| `Content` | `text` | |
| `Embedding` | `vector` | **no fixed N**; dynamic per model |
| `IngestedUtc` | `timestamp` | |

Constraints:

- Unique `(DocumentId, EmbeddingModelId, ChunkIndex)` so a document can be embedded by multiple models concurrently.
- `CHECK (vector_dims(Embedding) = EmbeddingDimensions)` so a mismatch is rejected at the database boundary (defense in depth alongside Application validation).

### 6.2 Index strategy

The pgvector docs confirm two compatible options for mixed dimensions in one table: a single untyped `vector` column with **partial expression indexes** per discriminator value ([pgvector README — "Can I store vectors with different dimensions in the same column?"](https://github.com/pgvector/pgvector#frequently-asked-questions)). We adopt that pattern keyed on `EmbeddingModelId`.

For each enabled `EmbeddingModel`, an Infrastructure helper creates a partial ANN index. The ANN strategy is **configurable per deployment** (D5); HNSW is the default, IVFFlat is the supported alternative for memory-constrained environments. Both helpers are partial and keyed on `EmbeddingModelId`.

HNSW (default):

```sql
CREATE INDEX IF NOT EXISTS ix_knowledge_chunk_emb_hnsw_<model_id>
  ON knowledge_chunks USING hnsw ((embedding::vector(<dims>)) vector_cosine_ops)
  WHERE embedding_model_id = '<model_id>';
```

IVFFlat (alternative):

```sql
CREATE INDEX IF NOT EXISTS ix_knowledge_chunk_emb_ivf_<model_id>
  ON knowledge_chunks USING ivfflat ((embedding::vector(<dims>)) vector_cosine_ops)
  WITH (lists = <lists>)
  WHERE embedding_model_id = '<model_id>';
```

Disabling/deleting the model drops whichever index is in use. The migration that lands this schema ships the helper SQL functions for both strategies; per-model index lifecycle is driven from Application commands (`EnableEmbeddingModelCommand`, `DisableEmbeddingModelCommand`) which read the deployment-level ANN strategy setting.

Distance operator standardized on cosine for freeze. Other operators (L2, IP) are post-freeze toggles.

### 6.3 Re-embedding

Re-embedding scope = `(DocumentId, EmbeddingModelId)`. The Application command `RebuildKnowledgeCommand` deletes only matching chunks then re-vectorizes. Chunks produced by other models for the same document are untouched.

## 7. Tutoring Context

### 7.1 Tutor (aggregate root)

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `Name` | `string` | required, unique within owner scope (Application invariant) |
| `Description` | `string?` | |
| `Status` | `TutorStatus` | `Draft`, `Active`, `Archived` |
| `ActiveRevisionId` | `Guid?` | FK `TutorRevision`; null until first activation |

No `OwnerId`. Ownership via `TutorMember`. Application invariant: at least one Owner row exists at all times; creation transaction inserts both `Tutor` and the founding `TutorMember(Owner)`.

**Activation slug (D22)**: when a revision is activated the runtime URL slug is derived from `Tutor.Name` via URL-safe slugification (lowercase, ASCII, hyphenated). Uniqueness is enforced across active runtimes. On rename, the new activation recomputes the slug and the previous slug is retained as a **one-revision redirect**; it is dropped on the next activation after the renamed one. Slug storage lives alongside the activation record, not on the `Tutor` row.

### 7.2 TutorRevision (uniform versioning)

`TutorRevision` is the editable, immutable-after-publish composition of everything required to run the tutor. It is the canonical addressable unit for activation, benchmarking, and audit.

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `TutorId` | `Guid` | FK Tutor, `Restrict` (revisions outlive tutor delete only via Archive flow) |
| `VersionNumber` | `int` | monotonically increasing per tutor; unique `(TutorId, VersionNumber)` |
| `Status` | `RevisionStatus` | `Draft`, `Published`, `Active`, `Archived` |
| `PromptVersionId` | `Guid` | FK PromptVersion |
| `InteractionModelId` | `Guid` | FK Model (filtered to `InteractionModel` via Application query) |
| `InteractionParametersJson` | `string?` | temperature, top_p, max_tokens, stop sequences |
| `QueryEmbeddingModelId` | `Guid` | FK Model (filtered to `EmbeddingModel`). Used only to embed runtime queries. |
| `DeploymentTarget` | `DeploymentTarget` | `Local`, `External` (future) |
| `DeploymentConfigJson` | `string?` | future deployment config payload |
| `PublishedUtc` | `DateTime?` | set on Draft → Published transition |
| `PublishedById` | `Guid?` | |
| `ActivationSlug` | `string?` | D22.b. Non-null only on the row whose `Status` is `Active`. Unique partial index `WHERE ActivationSlug IS NOT NULL`. |
| `PreviousActivationSlug` | `string?` | D22.b. Carries the prior active revision's slug for the **one-revision redirect window** defined in D22; cleared on the next activation. |

State machine:

```text
Draft ──publish──▶ Published ──activate──▶ Active
                                  └──archive──▶ Archived
Draft ──discard──▶ (hard delete; allowed only while Draft)
```

Invariants:

- At most one `Active` revision per tutor; activating a new revision archives the previous Active in the same transaction.
- Once a revision leaves `Draft`, all FK-referenced columns and configuration fields are immutable.
- `Tutor.ActiveRevisionId` mirrors the current Active revision's id (single point of truth for runtime lookup; `null` when there is none).

### 7.3 PromptVersion and ToolDefinition

Kept tutor-scoped, but they become **assets** referenced by revisions, not directly owned by an activation.

- `PromptVersion(TutorId, VersionNumber, Name, Content, Notes?)` — unchanged shape; a single prompt can be referenced by many revisions; immutable once a revision that uses it is `Published`.
- `ToolDefinition(TutorId, Name, JsonSchema, ExecutionUrl?)` — unchanged shape.
- `TutorRevisionTool(TutorRevisionId, ToolDefinitionId)` — M:N join, so a revision can compose multiple tools. Unique `(TutorRevisionId, ToolDefinitionId)`. Cascade from `TutorRevision`.

### 7.4 TutorConfigurationSnapshot (derived, immutable)

The existing [TutorConfigurationSnapshot](../../../src/Bitcraft.Domain/Tutoring/TutorConfigurationSnapshot.cs) is preserved, with these adjustments:

- Adds `TutorRevisionId Guid` FK (`Restrict`) as the canonical pointer back.
- Adds `EffectiveDocumentIdsJson string` and `EffectiveKnowledgeBindingJson string` to capture the materialized document set and `(Document, EmbeddingModel)` pairs that produced the chunks the snapshot relies on.
- Continues to be captured by `TutorConfigurationSnapshot.Capture(...)` on activation and on each benchmark run.

A snapshot is a **projection**, not a source. Roll-back means activating an older `Published`/`Archived` revision and capturing a fresh snapshot, not editing the snapshot.

## 8. Benchmarking Context

Minimal FK updates only:

- `BenchmarkRun.TutorRevisionId Guid` (FK, `Restrict`) — canonical reproducibility anchor.
- `BenchmarkRun.TutorConfigurationSnapshotId Guid?` (FK) — kept; pairs the run with its captured payload.
- `BenchmarkRun.InteractionModelId Guid?` (FK Model) — replaces `ModelCatalogEntryId` semantically (column rename or new FK in migration; old column removed in the same migration since data is internal POC only).

`BenchmarkRunCase` is unchanged.

## 9. Cardinality and Cascade Summary

| Left | Right | Cardinality | On left delete | On right delete |
|---|---|---|---|---|
| `AppUser` ↔ `Tutor` (via `TutorMember`) | M:N | Cascade member rows | Restrict (cannot delete user with active memberships; UI guides reassignment) |
| `Model` → `RuntimeEngine` | N:1 | n/a | Restrict (cannot delete an engine while any `Model` references it) |
| `Tutor` ↔ `Document` (via `TutorDocument`) | M:N | Cascade link rows | Restrict |
| `Tutor` ↔ `DocumentFolder` (via `TutorDocumentFolder`) | M:N | Cascade link rows | Restrict |
| `DocumentFolder` ↔ `Document` (via `DocumentFolderEntry`) | M:N | Cascade link rows | Restrict |
| `Tutor` → `PromptVersion` | 1:N | Cascade | n/a |
| `Tutor` → `ToolDefinition` | 1:N | Cascade | n/a |
| `Tutor` → `TutorRevision` | 1:N | Restrict (history preserved) | n/a |
| `TutorRevision` → `TutorRevisionTool` | 1:N | Cascade | n/a |
| `TutorRevision` → `PromptVersion` | N:1 | n/a | Restrict |
| `TutorRevision` → `Model` (Interaction) | N:1 | n/a | Restrict |
| `TutorRevision` → `Model` (Embedding) | N:1 | n/a | Restrict |
| `Document` → `KnowledgeChunk` | 1:N | Cascade | n/a |
| `Model` (Embedding) → `KnowledgeChunk` | 1:N | n/a | Restrict (purge required first) |
| `Tutor` → `TutorConfigurationSnapshot` | 1:N | Restrict | n/a |
| `TutorRevision` → `TutorConfigurationSnapshot` | 1:N | Restrict | n/a |
| `Tutor` → `BenchmarkRun` | 1:N | Restrict | n/a |
| `BenchmarkRun` → `BenchmarkRunCase` | 1:N | Cascade | n/a |

## 10. Migration Sequencing

The schema changes are non-trivial; they land in dependency order so each migration leaves the build green. One EF migration per step, named per the convention `<seq>-<slug>`. Step count is **8** (one new step for `RuntimeEngine` per D11/D21). No Git writes from agents (humans run `dotnet ef migrations add ...` per [AGENTS.md](../../../AGENTS.md)).

1. **`Identity-And-Roles-Vocabulary`**
   - Prerequisites: none (baseline).
   - Data movement: rename Viewer → Auditor; idempotent role seeding.
   - Validation gate: existing identity tests + integration test asserting roles are seeded.

2. **`Resource-Membership-Tables`**
   - Prerequisites: step 1.
   - Data movement: add `TutorMember`; backfill one Owner per existing tutor from `BaseModel.CreatedById`.
   - Validation gate: every existing tutor has ≥1 Owner row; new tutor creation insert covers both rows in one transaction.

3. **`RuntimeEngine-Entity`** *(new in v3)*
   - Prerequisites: step 2.
   - Data movement: create `RuntimeEngine` table. If any existing model rows are present, derive a default engine row per unique `(ProviderType, Endpoint)` pair found on the legacy `ModelCatalogEntry` (PoC data only).
   - Validation gate: integration test on admin CRUD for RuntimeEngine; `IsEnabled` gates engine selection.

4. **`Model-Tph-Hierarchy`**
   - Prerequisites: step 3.
   - Data movement: drop `ModelCatalogEntry`; introduce `Models` TPH with discriminator and leaf columns; add `Model.RuntimeEngineId` FK (`Restrict`) and `Model.ProviderModelName`; migrate inferable rows into the appropriate leaves and bind them to the engine rows produced in step 3.
   - Validation gate: every persisted `Model` row has a valid `RuntimeEngineId`; admin list query returns rows grouped by `ModelType`.

5. **`Tutor-Document-Mn`**
   - Prerequisites: step 2 (membership for authorization), step 4 (model registry stabilized; not strictly required but ordering keeps reviewable surface small).
   - Data movement: introduce `Document` (with new `Visibility` column, default `Internal`), `DocumentFolder`, `DocumentFolderEntry`, `TutorDocument`, `TutorDocumentFolder`. If a `Document.TutorId` column exists from an earlier branch, drop it. Migrate `KnowledgeSourceLink` rows into `TutorDocument` and drop the old table.
   - Validation gate: a document can be attached to multiple tutors; folder membership is many-to-many; folder mutations do not touch any runtime table.

6. **`Knowledge-Dynamic-Dimensions`**
   - Prerequisites: step 4 (EmbeddingModel leaf exists), step 5 (Document FK target stable).
   - Data movement: change `KnowledgeChunk.Embedding` to untyped `vector`; add `EmbeddingDimensions int NOT NULL`; add `EmbeddingModelId` FK (`Restrict`); drop the fixed-dim HNSW index; ship the per-model partial-index helper functions for **both** HNSW and IVFFlat strategies; rebuild indexes for currently-enabled embedding models using the deployment-default strategy (HNSW).
   - Validation gate: `vector_dims` CHECK rejects mismatched inserts; index lifecycle command creates and drops the partial index cleanly when an embedding model is toggled.

7. **`Tutor-Revisions`**
   - Prerequisites: steps 4, 5, 6 (all aggregates referenced by a revision must exist).
   - Data movement: add `TutorRevision`, `TutorRevisionTool`; add `Tutor.ActiveRevisionId`; migrate the current single-connection state into a Draft revision per tutor (skipped if no data); update `TutorConfigurationSnapshot` with the new `TutorRevisionId` FK plus the materialized-set columns (`EffectiveDocumentIdsJson`, `EffectiveKnowledgeBindingJson`). Provision activation-slug storage with one-revision redirect (D22).
   - Validation gate: revision state machine integration test (Draft → Published → Active → Archived); activation enforces single Active revision per tutor; snapshot is captured on activation.

8. **`Benchmark-Fk-Realign`**
   - Prerequisites: step 7.
   - Data movement: point `BenchmarkRun` to `TutorRevisionId` (`Restrict`) and `InteractionModelId` (`Restrict`); drop the legacy `ModelCatalogEntryId` column in the same migration (internal PoC data).
   - Validation gate: benchmarking integration test exercises a run against an Active revision and a captured snapshot.

Each migration must be reviewable in isolation.

## 11. Open Items For Human Decision

The historical open-items list has been resolved into the decision register. See:

- [plan.md → Open Human Decisions (v3)](plan.md#open-human-decisions-v3) for any decisions still open against this plan.
- [plan.decisions.md](plan.decisions.md) for the full locked register (D0a–D23).

No open items are owned by this ERM document; ERM-level decisions are now tracked in those two artifacts.
