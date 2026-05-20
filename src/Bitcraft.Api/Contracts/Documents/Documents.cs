namespace Bitcraft.Api.Contracts.Documents;

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

/// <summary>Source document response. Vector chunks are exposed through Knowledge endpoints, not Documents.</summary>
public sealed record DocumentResponse(
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
public sealed record VectorizeDocumentRequest(
    string EmbeddingProvider,
    string EmbeddingModel,
    int MaxChunkLength = 1000,
    Guid? EmbeddingModelId = null);

/// <summary>Result of a document vectorization run.</summary>
public sealed record VectorizeDocumentResponse(Guid DocumentId, int ChunkCount);

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
