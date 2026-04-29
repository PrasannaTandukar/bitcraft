namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to link a source document to a tutor's retrieval set.</summary>
public sealed record LinkKnowledgeSourceRequest(Guid DocumentId);

/// <summary>Link between a tutor and a source document used for retrieval.</summary>
public sealed record KnowledgeSourceLinkResponse(
    Guid Id,
    Guid TutorId,
    Guid DocumentId,
    DateTime LinkedUtc);