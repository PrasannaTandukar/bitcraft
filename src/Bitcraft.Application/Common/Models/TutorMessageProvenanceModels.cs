namespace Bitcraft.Application.Common.Models;

/// <summary>Safe generation-time evidence for an authorized conversation reader, never asserted model citations.</summary>
public sealed record TutorMessageProvenanceResponse(
    TutorKnowledgeProvenanceResponse Knowledge,
    TutorMemoryProvenanceResponse Memory,
    IReadOnlyList<TutorToolProvenanceResponse> Tools,
    TutorGenerationProvenanceResponse Runtime,
    DateTime CapturedUtc);

/// <summary>Count and excerpts of chunks retrieved AND supplied within the agent's context budget.</summary>
public sealed record TutorKnowledgeProvenanceResponse(int RetrievedChunkCount,
    IReadOnlyList<TutorRetrievedChunkEvidenceResponse> RetrievedChunks,
    string SimilarityMetric);

/// <summary>No source identifiers or URLs: original-document access remains a separate authorization boundary.</summary>
public sealed record TutorRetrievedChunkEvidenceResponse(string? DocumentName, int ChunkIndex,
    int RetrievalRank, string Excerpt, double? SimilarityScore)
{
    /// <summary>Message-scoped grouping token, not a document identifier or source-opening capability.</summary>
    public string? DocumentGroupKey { get; init; }
}

public sealed record TutorMemoryProvenanceResponse(bool ContextProvided, int RetrievedItemCount);

public sealed record TutorToolProvenanceResponse(string ToolName, string Status, bool Succeeded,
    double DurationMs, string? ErrorCategory);

/// <summary>Revision number belongs to the pinned revision; model metadata comes only from stored evidence.</summary>
public sealed record TutorGenerationProvenanceResponse(Guid TutorId, Guid TutorRevisionId,
    int? RevisionNumber, Guid SnapshotId, Guid InteractionModelId, string ProviderModelName, string ProviderType);
