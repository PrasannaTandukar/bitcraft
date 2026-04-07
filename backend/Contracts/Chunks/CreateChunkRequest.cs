namespace backend.Contracts.Chunks;

public record CreateChunkRequest(
    int DocumentId,
    int ChunkIndex,
    string Content,
    float[] Embedding
);