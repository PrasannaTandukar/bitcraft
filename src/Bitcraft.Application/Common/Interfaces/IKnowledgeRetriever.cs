using Bitcraft.Domain.Knowledge;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IKnowledgeRetriever
{
    Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        IReadOnlyCollection<Guid> documentIds,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);

    Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchNearestChunksAsync(
            request.DocumentIds,
            request.QueryEmbedding,
            request.TopK,
            cancellationToken);
}
