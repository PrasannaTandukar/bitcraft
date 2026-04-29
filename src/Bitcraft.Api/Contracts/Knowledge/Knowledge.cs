namespace Bitcraft.Api.Contracts.Knowledge;

/// <summary>Request to search a tutor-scoped knowledge set using a precomputed query embedding.</summary>
public sealed record SearchKnowledgeRequest(Guid TutorId, float[] QueryEmbedding, int TopK = 5);

/// <summary>Knowledge chunk returned from tutor-scoped retrieval.</summary>
public sealed record KnowledgeChunkResponse(
    Guid Id,
    Guid DocumentId,
    int ChunkIndex,
    string Content,
    string EmbeddingModel,
    string EmbeddingProvider,
    DateTime IngestedUtc);
