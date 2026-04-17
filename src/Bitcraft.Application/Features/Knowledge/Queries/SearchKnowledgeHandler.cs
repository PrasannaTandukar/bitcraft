using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Knowledge;

namespace Bitcraft.Application.Features.Knowledge.Queries;

public class SearchKnowledgeHandler(IKnowledgeRepository knowledgeRepository)
{
    public async Task<List<DocumentChunk>> HandleAsync(SearchKnowledgeQuery query, CancellationToken cancellationToken = default)
    {
        return await knowledgeRepository.SearchNearestChunksAsync(
            query.QueryEmbedding,
            query.TopK,
            cancellationToken);
    }
}
