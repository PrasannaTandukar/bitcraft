namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to link a source document to a tutor's retrieval set.</summary>
public sealed record LinkKnowledgeSourceRequest(Guid DocumentId);

/// <summary>Link between a tutor and a source document used for retrieval.</summary>
public sealed record KnowledgeSourceLinkResponse(
    Guid Id,
    Guid TutorId,
    Guid DocumentId,
    DateTime LinkedUtc);

public sealed record AttachTutorDocumentRequest(Guid DocumentId);

public sealed record TutorKnowledgeDocumentResponse(
    Guid LinkId,
    Guid TutorId,
    Guid DocumentId,
    string DocumentName,
    string AttachedVia,
    bool IsVectorized,
    Guid? EmbeddingModelId,
    string? EmbeddingModelName,
    bool LegacyCompatibility);

public sealed record AttachTutorDocumentFolderRequest(Guid FolderId);

public sealed record TutorKnowledgeFolderResponse(
    Guid LinkId,
    Guid TutorId,
    Guid FolderId,
    string FolderName);
