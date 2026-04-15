using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Entities;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories;

public class ChunkRepository(AppDbContext db) : IChunkRepository
{
    public async Task<DocumentChunk> AddAsync(
        int documentId,
        int chunkIndex,
        string content,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var chunk = new DocumentChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            Embedding = embedding
        };

        db.DocumentChunks.Add(chunk);
        await db.SaveChangesAsync(cancellationToken);
        return chunk;
    }

    public async Task<List<DocumentChunk>> SearchNearestAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        return await db.DocumentChunks
            .Where(x => x.Embedding != null)
            .OrderBy(x => x.Embedding!.CosineDistance(queryEmbedding))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}
