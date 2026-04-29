namespace Bitcraft.Api.Contracts.Documents;

/// <summary>Request to create a source document used by the knowledge pipeline.</summary>
public sealed record CreateDocumentRequest(
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText);

/// <summary>Request to replace editable source document metadata and raw text.</summary>
public sealed record UpdateDocumentRequest(
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText);

/// <summary>Source document response. Vector chunks are exposed through Knowledge endpoints, not Documents.</summary>
public sealed record DocumentResponse(
    Guid Id,
    string Name,
    string? SourcePath,
    string? MimeType,
    string? RawText,
    DateTime CreatedUtc,
    Guid CreatedById,
    DateTime? LastModifiedUtc,
    Guid? LastModifiedById);

/// <summary>Request to re-create knowledge chunks for a source document using the selected embedding model.</summary>
public sealed record VectorizeDocumentRequest(
    string EmbeddingProvider,
    string EmbeddingModel,
    int MaxChunkLength = 1000);

/// <summary>Result of a document vectorization run.</summary>
public sealed record VectorizeDocumentResponse(Guid DocumentId, int ChunkCount);
