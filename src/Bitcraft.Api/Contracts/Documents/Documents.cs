namespace Bitcraft.Api.Contracts.Documents;

/// <summary>Multipart form used to upload and extract a source document.</summary>
public sealed class UploadDocumentRequest
{
    public IFormFile? File { get; init; }
    public Guid? FolderId { get; init; }
}

/// <summary>Multipart form used to replace an existing document's source content.</summary>
public sealed class ReplaceDocumentContentRequest
{
    public IFormFile? File { get; init; }
}

/// <summary>Multipart form used to upload and extract multiple source documents in one request.</summary>
public sealed class UploadDocumentBatchRequest
{
    public IFormFileCollection Files { get; init; } = default!;
    public Guid? FolderId { get; init; }
}

/// <summary>Request to create a source document used by the knowledge pipeline.</summary>
public sealed record CreateDocumentRequest(
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText,
    Guid? FolderId = null);

/// <summary>Request to replace editable source document metadata and raw text.</summary>
public sealed record UpdateDocumentRequest(
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText);

/// <summary>Request to move a document into a folder (or to the user root when null).</summary>
public sealed record MoveDocumentRequest(Guid? FolderId);

/// <summary>
/// Lightweight source document response returned by list endpoints. Excludes
/// <c>RawText</c> so directory listings stay cheap and avoid streaming large
/// payloads. Use <see cref="DocumentDetailResponse"/> when the body is needed.
/// </summary>
public sealed record DocumentSummaryResponse(
    Guid Id,
    string Name,
    string? SourcePath,
    string? MimeType,
    Guid? FolderId,
    bool HasRawText,
    int RawTextLength,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? LastModifiedUtc,
    Guid? LastModifiedById,
    DocumentVectorizationMetadataResponse? Vectorization);

/// <summary>Lightweight vectorization state included with a document summary.</summary>
public sealed record DocumentVectorizationMetadataResponse(
    bool IsVectorized,
    bool IsStale,
    int ChunkCount,
    DateTime? LastVectorizedUtc,
    Guid? EmbeddingModelId,
    string? EmbeddingModelName,
    string? ProviderModelName,
    string? EmbeddingProvider,
    int? EmbeddingDimensions);

/// <summary>
/// Detailed source document response including <c>RawText</c>. Returned from
/// single-document endpoints (get-by-id, create, update, move). Vector chunks
/// are exposed through Knowledge endpoints, not Documents.
/// </summary>
public sealed record DocumentDetailResponse(
    Guid Id,
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText,
    Guid? FolderId,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? LastModifiedUtc,
    Guid? LastModifiedById);

/// <summary>Request to re-create knowledge chunks for a source document.</summary>
/// <param name="EmbeddingProvider">Legacy provider label (e.g. <c>Ollama</c>). Ignored when <paramref name="EmbeddingModelId"/> is set.</param>
/// <param name="EmbeddingModel">Legacy provider-side model name. Ignored when <paramref name="EmbeddingModelId"/> is set.</param>
/// <param name="MaxChunkLength">Maximum chunk size (characters) used by the text splitter.</param>
/// <param name="EmbeddingModelId">Optional admin-managed <c>EmbeddingModel</c> id. When supplied, drives provider, model name, and expected dimensions.</param>
/// <param name="ChunkOverlap">Number of characters repeated between adjacent chunks.</param>
public sealed record VectorizeDocumentRequest(
    string? EmbeddingProvider = null,
    string? EmbeddingModel = null,
    int MaxChunkLength = 1000,
    Guid? EmbeddingModelId = null,
    int ChunkOverlap = 100);

/// <summary>Result of a document vectorization run.</summary>
public sealed record VectorizeDocumentResponse(Guid DocumentId, int ChunkCount);

/// <summary>Request to re-create knowledge chunks for every document in a folder (or the user root when <paramref name="FolderId"/> is null).</summary>
public sealed record VectorizeDocumentBatchRequest(
    Guid? FolderId = null,
    string? EmbeddingProvider = null,
    string? EmbeddingModel = null,
    int MaxChunkLength = 1000,
    Guid? EmbeddingModelId = null,
    int ChunkOverlap = 100);

/// <summary>Per-document outcome of a batch vectorization run. Exactly one of chunk count or error is set.</summary>
public sealed record VectorizeDocumentBatchItemResponse(
    Guid DocumentId,
    int? ChunkCount,
    string? ErrorCode,
    string? ErrorDescription);

/// <summary>Result of a batch vectorization run. A per-document failure does not stop the batch.</summary>
public sealed record VectorizeDocumentBatchResponse(IReadOnlyList<VectorizeDocumentBatchItemResponse> Results);

/// <summary>Per-file outcome of a batch upload. Exactly one of document id or error is set.</summary>
public sealed record UploadDocumentBatchFileResponse(
    string FileName,
    Guid? DocumentId,
    string? ErrorCode,
    string? ErrorDescription);

/// <summary>Result of a batch upload. A per-file failure does not stop the batch.</summary>
public sealed record UploadDocumentBatchResponse(IReadOnlyList<UploadDocumentBatchFileResponse> Results);

/// <summary>Lightweight vectorization metadata for an owned document.</summary>
public sealed record DocumentVectorizationStatusResponse(
    Guid DocumentId,
    bool HasRawText,
    int RawTextLength,
    bool IsVectorized,
    bool IsStale,
    int ChunkCount,
    DateTime? LastVectorizedUtc,
    Guid? EmbeddingModelId,
    string? EmbeddingModelName,
    string? ProviderModelName,
    string? EmbeddingProvider,
    int? EmbeddingDimensions);

/// <summary>Request to create a per-user document folder.</summary>
public sealed record CreateDocumentFolderRequest(string Name, Guid? ParentFolderId);

/// <summary>Request to rename a document folder.</summary>
public sealed record RenameDocumentFolderRequest(string Name);

/// <summary>Request to move a folder under a different parent (or to root when null).</summary>
public sealed record MoveDocumentFolderRequest(Guid? ParentFolderId);

/// <summary>Document folder response.</summary>
public sealed record DocumentFolderResponse(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? LastModifiedUtc,
    Guid? LastModifiedById);
