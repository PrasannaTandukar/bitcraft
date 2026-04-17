namespace Bitcraft.Application.Features.Knowledge.Commands;

public record CreateDocumentChunkCommand(int DocumentId, int ChunkIndex, string Content, float[] Embedding);
