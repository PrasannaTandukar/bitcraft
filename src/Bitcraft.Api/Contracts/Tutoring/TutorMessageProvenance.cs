namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Retrieved context and safe execution metadata, not model-authored citations or reasoning.</summary>
public sealed record TutorMessageProvenanceResponse(
    TutorKnowledgeProvenanceResponse Knowledge,
    TutorMemoryProvenanceResponse Memory,
    IReadOnlyList<TutorToolProvenanceResponse> Tools,
    TutorGenerationProvenanceResponse Runtime,
    DateTime CapturedUtc);

/// <summary>Counts only retrieved chunks actually supplied as context. Cosine similarity is not a relevance percentage.</summary>
public sealed record TutorKnowledgeProvenanceResponse(int RetrievedChunkCount,
    IReadOnlyList<TutorRetrievedChunkEvidenceResponse> RetrievedChunks,
    string SimilarityMetric);

/// <summary>Historical excerpt/name/index. No page or section is inferred, and no original-document access is granted.</summary>
public sealed record TutorRetrievedChunkEvidenceResponse(string? DocumentName, int ChunkIndex,
    int RetrievalRank, string Excerpt, double? SimilarityScore)
{
    /// <summary>Groups one historical document's chunks within this message only. Grants no source access.</summary>
    public string? DocumentGroupKey { get; init; }
}

public sealed record TutorMemoryProvenanceResponse(bool ContextProvided, int RetrievedItemCount);

public sealed record TutorToolProvenanceResponse(string ToolName, string Status, bool Succeeded,
    double DurationMs, string? ErrorCategory);

public sealed record TutorGenerationProvenanceResponse(Guid TutorId, Guid TutorRevisionId,
    int? RevisionNumber, Guid SnapshotId, Guid InteractionModelId, string ProviderModelName, string ProviderType);
