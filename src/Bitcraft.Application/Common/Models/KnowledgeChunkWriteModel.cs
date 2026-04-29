namespace Bitcraft.Application.Common.Models;

public sealed record KnowledgeChunkWriteModel(
    int ChunkIndex,
    string Content,
    float[] Embedding,
    string EmbeddingModel,
    string EmbeddingProvider);
