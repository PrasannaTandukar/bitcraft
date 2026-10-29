---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# PR #4-8 (combined) — feature-freeze/05-tutor-document-mn

Consolidated delivery of the D25 mid-sequence steps that touch Knowledge, Documents, and Tutor configuration versioning. The user explicitly authorized merging the intermediate steps into one PR; the frontend step (D25 step 9) remains separate.

## Scope

- **D25 step 4 — Dynamic embedding dimensions.** `KnowledgeChunks.Embedding` moves from a fixed `vector(768)` column to an untyped `vector` column. The HNSW ANN index is dropped (untyped pgvector columns cannot host an HNSW index). Cosine similarity queries still work without ANN; an ANN strategy per-model is deferred to a future per-model partitioning step. `KnowledgeChunks` now also carries `EmbeddingModelId` (FK → `Models`, Restrict) and `EmbeddingDimensions` (`int?`) so each row is traceable to the admin-managed embedding model that produced it.
- **D25 step 5 — Per-user `DocumentFolder` hierarchy.** New `DocumentFolder` aggregate (`BaseModel`), self-referential `ParentFolderId` (Restrict delete), composite index on `{CreatedById, ParentFolderId, Name}`. Folders are per-user and never shared; document sharing still happens at `KnowledgeSourceLink`. Repository + DI registration + CQRS feature module + API controller + contracts all land in one batch.
- **D25 step 6 — Embedding model selection.** `VectorizeDocumentCommand` gains optional `EmbeddingModelId`. When supplied, the handler resolves the admin-managed `EmbeddingModel` (with its `RuntimeEngine`), derives provider + model name from the entity, validates that the returned vector length matches the model's declared `EmbeddingDimensions`, and stamps both `EmbeddingModelId` and `EmbeddingDimensions` onto each chunk. The legacy free-text path still works for back-compat until callers migrate.
- **D25 step 7 — Tutor configuration snapshot versioning.** `TutorConfigurationSnapshot` gains an immutable `VersionNumber` set at capture, mirroring the `PromptVersion.VersionNumber` pattern. The handler now requests the next number from `ITutorRepository.GetNextSnapshotVersionNumberAsync`. A unique index over `{TutorId, VersionNumber}` enforces uniqueness; a backfill SQL block in the migration assigns sequential numbers (ordered by `CapturedUtc`, then `Id`) to any pre-existing rows before the index is built.
- **D25 step 8 — Documents inside folders.** `Document` gains nullable `FolderId` (Restrict delete) and a folder-aware ownership check on Create/Update. New `MoveDocumentToFolderCommand` + `PATCH /api/documents/{id}/folder` route. The folder must be owned by the requesting user.

## Files changed (summary)

### Domain
- `src/Bitcraft.Domain/Documents/Document.cs` — `FolderId` + `Folder` navigation.
- `src/Bitcraft.Domain/Documents/DocumentFolder.cs` — new aggregate.
- `src/Bitcraft.Domain/Knowledge/KnowledgeChunk.cs` — `EmbeddingModelId`, `EmbeddingModelEntity` nav, `EmbeddingDimensions`. Legacy `EmbeddingModel`/`EmbeddingProvider` strings retained.
- `src/Bitcraft.Domain/Tutoring/TutorConfigurationSnapshot.cs` — `VersionNumber`; `Capture()` signature now `(tutor, versionNumber, prompt, conn, toolsJson, knowledgeSourcesJson, profile, capturedById)`.

### Application
- `IDocumentFolderStore`, `DocumentFolderFeatures` (Create/Rename/Move/Delete/List).
- `DocumentFeatures` — `CreateDocumentCommand`/`UpdateDocumentCommand` accept `FolderId`; new `MoveDocumentToFolderCommand`.
- `KnowledgeFeatures.VectorizeDocumentCommand` — optional `EmbeddingModelId`; new error codes `Knowledge.EmbeddingModelNotFound`, `.EmbeddingModelDisabled`, `.EmbeddingDimensionsInvalid`, `.RuntimeEngineMissing`, `.RuntimeEngineDisabled`, `.EmbeddingDimensionsMismatch`.
- `ITutorRepository.GetNextSnapshotVersionNumberAsync` + `ConfigurationSnapshotFeatures` uses it.
- `KnowledgeChunkWriteModel` extended with optional `EmbeddingModelId` + `EmbeddingDimensions`.

### Infrastructure
- EF configurations updated for `Document.FolderId`, new `DocumentFolderConfiguration`, `KnowledgeChunk` (untyped vector + new FK/dim columns; HNSW dropped), `TutorConfigurationSnapshot` (`VersionNumber` + unique index).
- `DocumentFolderStore` repository.
- `TutorRepository.GetNextSnapshotVersionNumberAsync`.
- `PgvectorVectorStore` writes the new fields.
- `ModelRepository.GetEmbeddingModelByIdAsync` now `Include`s the `RuntimeEngine`.
- DI registration for `IDocumentFolderStore`.
- Migration `20260520190952_Documents_Folders_DynamicDims_SnapshotVersions_20260521` with embedded backfill SQL.

### Api
- `Contracts/Documents/Documents.cs` — `FolderId` on requests/responses, `MoveDocumentRequest`, folder request/response records, `VectorizeDocumentRequest.EmbeddingModelId`.
- `Contracts/Tutoring/TutorSnapshots.cs` — `VersionNumber` on `TutorConfigurationSnapshotResponse`.
- `Controllers/Documents/DocumentsController.cs` — wires `FolderId`, adds `PATCH /api/documents/{id}/folder`.
- `Controllers/Documents/DocumentFoldersController.cs` — new controller at `/api/document-folders`.
- `Mapping/TutoringMapping.cs` — `DocumentFolder` mapping, updated `Document` and snapshot mappings.

### Tests
- Updated all callers of `TutorConfigurationSnapshot.Capture` to the new signature.
- Updated `FakeTutorRepository` to implement `GetNextSnapshotVersionNumberAsync`.
- Updated all `VectorizeDocumentHandler` test instantiations to pass `FakeModelRepository`.
- Full `Bitcraft.UnitTests` suite: **155/155 passing**.

## Locked decisions referenced

| ID | Concern | Use here |
| -- | ------- | -------- |
| **D25 step 4** | Dynamic embedding dimensions | Implemented via untyped pgvector column + per-row dim tracking. |
| **D25 step 5** | Per-user document folders | Folders are owner-scoped, never shared. |
| **D25 step 6** | Embedding model selection | New optional `EmbeddingModelId`; legacy path retained. |
| **D25 step 7** | Snapshot versioning | Mirrors `PromptVersion.VersionNumber`. |
| **D25 step 8** | Documents inside folders | `Document.FolderId` + Move endpoint. |

## Validation

- `dotnet build bitcraft.slnx` — clean (no errors, no warnings).
- `dotnet test tests/Bitcraft.UnitTests` — 155 passed, 0 failed, 0 skipped.
- `dotnet ef migrations add` — migration scaffolded successfully, hand-edited to add the snapshot `VersionNumber` backfill SQL before the unique index is built.
- `dotnet ef database update` **not run** — humans own data application against shared environments.

## Operational notes / heads-up

- **HNSW index dropped.** Cosine queries still execute without ANN and are acceptable for feature-freeze data volumes; revisit when per-model partitioning lands. Dev Ops should monitor `/api/knowledge/...` query latency on staging after first migration.
- **Backfill assumption.** The `VersionNumber` backfill orders by `CapturedUtc, Id`. If a tutor has multiple snapshots with identical `CapturedUtc`, ordering falls back to `Id` (Guid string order, deterministic but arbitrary). The unique index will hold regardless.
- **Hard-delete semantics retained.** Folders use Restrict delete on parent + on `Document.FolderId`. The `DeleteDocumentFolderCommand` blocks deletion when child folders or documents exist (error `DocumentFolder.NotEmpty`).
- **Legacy vectorize path preserved.** Existing callers passing free-text provider/model continue to work; new field-aware path activates only when `EmbeddingModelId` is supplied.
- **Frontend deferred.** Tutor Studio UI for folders + embedding-model selector lands in D25 step 9 under Web Dev.
