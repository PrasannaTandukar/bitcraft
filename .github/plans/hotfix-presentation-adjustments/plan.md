---
created_at: 2026-11-09
edited_at: 2026-11-09
creator: planner
editor: planner
ai_gen: true
---

# Plan: Batch Upload, Folder Vectorization, And Local-Model Timeouts

Branch: `hotfix-presentation-adjustments`
CIL: medium (CSS 5, DIS 6)

## Goal

Let an editor import a folder of about 1,000 Markdown files as an Arduino knowledge base in one operation. Let the editor vectorize a whole folder in one operation. Remove short-timeout failures when a slow local model answers a chat question or embeds document chunks.

## Non-Goals

- No zip or recursive-directory upload. The user selects multiple files in one dialog.
- No background job queue for uploads or vectorization. Both operations run as bounded HTTP requests.
- No changes to the benchmarking sidecar or its timeouts.
- No changes to the unrelated uncommitted `docker-compose.yml` edit on this branch.

## Assumptions

- A 1,000-file batch request stays under the 30 MB ASP.NET Core default multipart body limit when each Markdown file is under 10 MB. The batch endpoint enforces a file-count cap.
- The user runs the API behind the Vite dev proxy or direct API access. No nginx proxy timeout applies in this setup. The production `nginx.conf` serves static files only.
- The per-provider `GenerateTimeoutSeconds` config key already exists. Only default values change.

## Risks

- A 1,000-file batch request may exceed the Kestrel multipart body limit. Mitigation: the endpoint enforces a configurable maximum batch file count (default 200) and the frontend sends batches of at most that size.
- Sequential per-chunk embedding of 1,000 documents is slow. This is accepted. The folder vectorization loop reports progress and continues past per-document failures.
- Raising the generation timeout to 300 seconds holds an HTTP connection longer. This is accepted because chat streaming keeps the connection alive with SSE events.

## Open Human Decisions

None. All decisions were resolved during planning:

- HD-1: Bulk upload uses a backend batch endpoint. (Human selected.)
- HD-2: Batch vectorization runs as a parallel workflow. Individual and batch operations both stay available. (Human selected.)
- HD-3: The plan lives on the current branch. (Human selected.)

## Work Steps

### Step 1 — Raise generation and embedding timeouts

Skill: `bitcraft-backend-feature`

Change the default generation timeout from 120 seconds to 300 seconds.

1. Edit `src/Bitcraft.Infrastructure/ExternalServices/Agents/RuntimeChatClientFactory.cs`. In `Create`, change the fallback `?? 120` on line 39 to `?? 300`. In `ProbeAvailableModelsAsync`, change the fallback `?? 120` on line 128 to `?? 300`.
2. Edit `src/Bitcraft.Api/appsettings.json`. Change `ModelProviders:Ollama:GenerateTimeoutSeconds` from `120` to `300`.
3. Edit `src/Bitcraft.Infrastructure/DependencyInjection.cs`. Change the `EmbeddingTimeoutSeconds` fallback on line 181 from `?? 30` to `?? 300`.
4. Edit `src/Bitcraft.Api/appsettings.json`. Change `ModelProviders:Ollama:EmbeddingTimeoutSeconds` from `30` to `300`.

Validation: `dotnet build bitcraft.slnx`. Expected result: build succeeds with zero warnings.

### Step 2 — Add batch upload endpoint

Skill: `bitcraft-backend-feature`

Add `POST /api/documents/upload-batch` that accepts up to 200 files, extracts text for each, and creates one document per file in the given folder.

1. Edit `src/Bitcraft.Application/Common/Models/DocumentIngestion.cs`. Add `public const int DefaultMaxBatchFiles = 200;` inside `DocumentIngestionLimits`. Add `int MaxBatchFiles` as the last parameter of the `DocumentIngestionLimits` record and of the `Default` property initializer.
2. Edit `src/Bitcraft.Infrastructure/DependencyInjection.cs`. Add a fourth `PositiveIntOrDefault` argument reading `Documents:MaxBatchFiles` with fallback `DocumentIngestionLimits.DefaultMaxBatchFiles`.
3. Edit `src/Bitcraft.Application/Features/Documents/DocumentFeatures.cs`. Add a `UploadDocumentBatchCommand(IReadOnlyList<UploadDocumentBatchFile> Files, Guid? FolderId)` record plus an `UploadDocumentBatchFile(string FileName, string? MimeType, long FileSizeBytes, ReadOnlyMemory<byte> Content)` record. Add `UploadDocumentBatchHandler` that: rejects an empty file list with `Document.FilesRequired`; rejects a list above `limits.MaxBatchFiles` with `Document.BatchTooLarge`; then for each file runs the same validation and extraction path as `UploadDocumentCommand`, collects per-file success or error, and adds each successful document with `UpdateMetadata`. Return `UploadDocumentBatchResult(IReadOnlyList<UploadDocumentBatchFileResult> Results)` where each file result carries `FileName`, `DocumentId` on success, or `ErrorCode` and `ErrorDescription` on failure. Reuse `ValidateUploadedFile` and `ValidateExtractedText` unchanged.
4. Edit `src/Bitcraft.Api/Contracts/Documents` contracts file. Add `UploadDocumentBatchResponse` with a `Results` list of `UploadDocumentBatchFileResponse(FileName, DocumentId, ErrorCode, ErrorDescription)`.
5. Edit `src/Bitcraft.Api/Mapping` documents mapping. Add mapping from `UploadDocumentBatchResult` to `UploadDocumentBatchResponse`.
6. Edit `src/Bitcraft.Api/Controllers/Documents/DocumentsController.cs`. Add `UploadBatch` action: route `upload-batch`, `[Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]`, `[Consumes("multipart/form-data")]`, accepts `IFormFileCollection Files` and `Guid? FolderId` from form. Return `200 OK` with the batch response. Return `400` with `Document.FilesRequired` when no files are present and `Document.BatchTooLarge` when the count exceeds the limit. Read each file with the existing `ReadBoundedFileAsync`.

Validation: `dotnet build bitcraft.slnx`. Expected result: build succeeds.

### Step 3 — Add batch vectorize endpoint

Skill: `bitcraft-backend-feature`

Add `POST /api/documents/vectorize-batch` that vectorizes every document in a folder, or every owned document when no folder is given.

1. Edit `src/Bitcraft.Application/Features/Knowledge/KnowledgeFeatures.cs`. Add `VectorizeDocumentBatchCommand(Guid? FolderId, string EmbeddingProvider, string EmbeddingModel, int? MaxChunkLength, Guid? EmbeddingModelId, int ChunkOverlap)` and result `VectorizeDocumentBatchResult(IReadOnlyList<VectorizeDocumentBatchItemResult> Results)` with per-document `DocumentId`, `ChunkCount`, `ErrorCode`, `ErrorDescription`. Add `VectorizeDocumentBatchHandler` that: resolves the embedding model, provider, and runtime engine once using the same checks as `VectorizeDocumentHandler`; lists target documents from `IDocumentStore` by folder (or all owned documents when `FolderId` is null); then loops and runs the existing per-document logic (chunk, embed each chunk, `ReplaceDocumentChunksAsync`) per document, catching per-document failures into the result list instead of aborting the batch. Extract the shared per-document body into a private static helper so `VectorizeDocumentHandler` and the batch handler call the same code. Do not duplicate the embedding error mapping.
2. Add `ListByFolderAsync(Guid? folderId, Guid userId, CancellationToken)` to `IDocumentStore` and implement it in the DocumentStore implementation. It returns documents owned by the user in the folder, or in the root when `folderId` is null.
3. Edit `src/Bitcraft.Api/Controllers/Documents/DocumentsController.cs`. Add `VectorizeBatch` action: route `vectorize-batch`, `[Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]`, body `VectorizeDocumentBatchRequest(FolderId, EmbeddingProvider, EmbeddingModel, MaxChunkLength, EmbeddingModelId, ChunkOverlap)`. Return `200 OK` with the batch result.
4. Edit `src/Bitcraft.Api/Contracts/Documents` and the mapping file. Add `VectorizeDocumentBatchResponse` with per-document result records.

Validation: `dotnet build bitcraft.slnx`. Expected result: build succeeds.

### Step 4 — Frontend batch upload and batch vectorize

Skill: `bitcraft-frontend-feature`

1. Edit `tutor-studio/src/features/editor/utils/documentApi.js`. Add `uploadDocumentsBatch(files, folderId, signal)` posting multipart `files` entries to `/api/documents/upload-batch`. Add `vectorizeDocumentsBatch(payload, signal)` posting JSON to `/api/documents/vectorize-batch`. Add `MAX_BATCH_FILES` export set to 200.
2. Edit `tutor-studio/src/features/editor/components/DocumentUploadDialog.jsx`. Change the `Upload.Dragger` to `multiple` with `maxCount` removed so the user can select many files. In `handleSubmit`, when more than one file is selected, chunk the file list into groups of `MAX_BATCH_FILES` and call `uploadDocumentsBatch` per group. Show a per-file result list in the modal: file name, success, or error description. Keep single-file behavior on the existing `uploadDocument` path when exactly one file is selected and no document is being replaced. Keep the replace-content single-file flow unchanged.
3. Edit `tutor-studio/src/pages/editor/FilesPage.jsx` (and the folder view component it uses). Add a "Vectorize folder" action that opens a small modal to pick the embedding model, then calls `vectorizeDocumentsBatch` with the folder id. Show progress text and a per-document result summary. Reuse the existing embedding-model picker logic used by the single-document vectorize dialog.

Validation: from `tutor-studio/` run `npm run lint`. Expected result: zero errors.

### Step 5 — Tests

Skill: `bitcraft-test-hardening`

1. Add unit tests in `tests/Bitcraft.UnitTests` for `UploadDocumentBatchHandler`: empty list fails, over-limit list fails, per-file extraction failure produces a per-file error while other files succeed.
2. Add unit tests for `VectorizeDocumentBatchHandler`: one document failing embedding does not stop later documents, and results report chunk counts.
3. Add an integration test in `tests/Bitcraft.ApiIntegrationTests` for `POST /api/documents/upload-batch` covering: authorization (editor or admin), the file-count cap, and a successful two-file batch.
4. Add an integration test for `POST /api/documents/vectorize-batch` covering authorization and a successful folder batch.

Validation: `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`. Expected result: all tests pass.

## Acceptance Criteria

- An editor can select about 1,000 Markdown files in the upload dialog. The frontend sends them in groups of 200. Each file becomes one document in the chosen folder.
- A per-file failure does not stop the batch. The UI shows which files failed and why.
- An editor can vectorize every document in a folder with one action. Per-document failures do not stop the batch.
- A local chat model has 300 seconds to produce a response before the runtime reports a timeout.
- A local embedding model has 300 seconds per embedding call before the call fails.
- Existing single-file upload, single-document vectorize, and replace-content flows keep their current behavior.

## Delegation Route

- Steps 1-3: Lead Dev with `bitcraft-backend-feature`.
- Step 4: Lead Dev with `bitcraft-frontend-feature`.
- Step 5: Lead Dev with `bitcraft-test-hardening`.
- Review: `bitcraft-review` at CIL medium.

## Phase 2 Prompt

Run the `bitcraft-develop` skill for `.github/plans/hotfix-presentation-adjustments/plan.md`. Execute work steps in order. Validate each step before the next.
