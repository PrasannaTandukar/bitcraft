using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace backend.Services;

public class ChunkService(AppDbContext db)
{
    public async Task<DocumentChunk> AddChunkAsync(
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
            Embedding = new Vector(embedding)
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
        var queryVector = new Vector(queryEmbedding);

        return await db.DocumentChunks
            .Where(x => x.Embedding != null)
            .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}