namespace Bitcraft.Application.Features.Chunks.CreateChunk;

public record CreateChunkCommand(int DocumentId, int ChunkIndex, string Content, float[] Embedding);
