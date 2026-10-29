using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
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

        var queryVector = new Vector(queryEmbedding);

        return await db.KnowledgeChunks
            .Where(x =>
                x.Embedding != null &&
                x.EmbeddingDimensions == queryEmbedding.Length &&
                documentIds.Contains(x.DocumentId))
            .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DocumentIds.Count == 0)
        {
            return [];
        }

        var queryVector = new Vector(request.QueryEmbedding);
        return await db.KnowledgeChunks
            .Where(chunk =>
                chunk.Embedding != null &&
                chunk.EmbeddingModelId == request.EmbeddingModelId &&
                chunk.EmbeddingDimensions == request.EmbeddingDimensions &&
                request.DocumentIds.Contains(chunk.DocumentId))
            .OrderBy(chunk => chunk.Embedding!.CosineDistance(queryVector))
            .Take(request.TopK)
            .ToListAsync(cancellationToken);
    }
}
