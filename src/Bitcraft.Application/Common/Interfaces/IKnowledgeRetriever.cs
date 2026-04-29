using Bitcraft.Domain.Knowledge;

namespace Bitcraft.Application.Common.Interfaces;

public interface IKnowledgeRetriever
{
    Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
    IReadOnlyCollection<Guid> documentIds,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
