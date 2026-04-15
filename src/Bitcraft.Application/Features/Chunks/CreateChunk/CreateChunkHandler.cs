using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Entities;

namespace Bitcraft.Application.Features.Chunks.CreateChunk;

public class CreateChunkHandler(IChunkRepository chunkRepository)
{
    public async Task<DocumentChunk> HandleAsync(CreateChunkCommand command, CancellationToken cancellationToken = default)
    {
        return await chunkRepository.AddAsync(
            command.DocumentId,
            command.ChunkIndex,
            command.Content,
            command.Embedding,
            cancellationToken);
    }
}
