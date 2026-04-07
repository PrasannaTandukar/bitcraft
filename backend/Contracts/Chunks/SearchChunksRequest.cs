namespace backend.Contracts.Chunks;

public record SearchChunksRequest(
    float[] QueryEmbedding,
    int TopK = 5
);