namespace Bitcraft.Application.Features.Chunks.SearchChunks;

public record SearchChunksQuery(float[] QueryEmbedding, int TopK = 5);
