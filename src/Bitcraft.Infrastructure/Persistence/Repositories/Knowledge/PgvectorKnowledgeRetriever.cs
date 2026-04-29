using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;

/// <summary>
/// PostgreSQL pgvector retrieval adapter. Application code supplies the allowed
/// document ids; this adapter only applies nearest-neighbor ordering inside that scope.
/// </summary>
public class PgvectorKnowledgeRetriever(AppDbContext db) : IKnowledgeRetriever
{
    public async Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        IReadOnlyCollection<Guid> documentIds,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return [];
        }

        return await db.KnowledgeChunks
            .Where(x => x.Embedding != null && documentIds.Contains(x.DocumentId))
            .OrderBy(x => x.Embedding!.CosineDistance(queryEmbedding))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}
