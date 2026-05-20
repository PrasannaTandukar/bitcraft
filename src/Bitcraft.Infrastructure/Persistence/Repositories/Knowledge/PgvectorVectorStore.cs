using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;

/// <summary>
/// PostgreSQL pgvector write adapter. Replacement is transactional so a failed
/// ingest cannot leave a document with partially refreshed knowledge chunks.
/// </summary>
public class PgvectorVectorStore(AppDbContext db) : IVectorStore
{
    public async Task<IReadOnlyList<KnowledgeChunk>> ReplaceDocumentChunksAsync(
        Guid documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.KnowledgeChunks
            .Where(x => x.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        db.KnowledgeChunks.RemoveRange(existing);

        var replacements = chunks.Select(chunk => new KnowledgeChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunk.ChunkIndex,
            Content = chunk.Content,
            Embedding = chunk.Embedding,
            EmbeddingModel = chunk.EmbeddingModel,
            EmbeddingProvider = chunk.EmbeddingProvider,
            EmbeddingModelId = chunk.EmbeddingModelId,
            EmbeddingDimensions = chunk.EmbeddingDimensions,
            IngestedUtc = DateTime.UtcNow
        }).ToList();

        db.KnowledgeChunks.AddRange(replacements);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return replacements;
    }
}
