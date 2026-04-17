namespace Bitcraft.Api.Contracts.Knowledge;

public record CreateChunkRequest(
    int DocumentId,
    int ChunkIndex,
    string Content,
    float[] Embedding
);
