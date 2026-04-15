using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Entities;

namespace Bitcraft.Application.Features.Chunks.SearchChunks;

public class SearchChunksHandler(IChunkRepository chunkRepository)
{
    public async Task<List<DocumentChunk>> HandleAsync(SearchChunksQuery query, CancellationToken cancellationToken = default)
    {
        return await chunkRepository.SearchNearestAsync(
            query.QueryEmbedding,
            query.TopK,
            cancellationToken);
    }
}
