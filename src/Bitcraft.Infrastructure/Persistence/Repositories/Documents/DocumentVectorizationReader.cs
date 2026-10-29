using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Documents;

public sealed class DocumentVectorizationReader(AppDbContext db) : IDocumentVectorizationReader
{
    public async Task<DocumentVectorizationMetadata> GetMetadataAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (db.Model.FindEntityType(typeof(Bitcraft.Domain.Knowledge.KnowledgeChunk)) is null)
        {
            return new DocumentVectorizationMetadata(false, false, 0, null, null, null, null, null, null);
        }

        var chunks = await db.KnowledgeChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new ChunkMetadata(
                chunk.IngestedUtc,
                chunk.EmbeddingModelId,
                chunk.EmbeddingModelEntity == null ? null : chunk.EmbeddingModelEntity.Name,
                chunk.EmbeddingModel,
                chunk.EmbeddingProvider,
                chunk.EmbeddingDimensions))
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return new DocumentVectorizationMetadata(false, false, 0, null, null, null, null, null, null);
        }

        var first = chunks[0];
        var uniform = chunks.All(chunk =>
            chunk.EmbeddingModelId == first.EmbeddingModelId &&
            string.Equals(chunk.EmbeddingModelName, first.EmbeddingModelName, StringComparison.Ordinal) &&
            string.Equals(chunk.ProviderModelName, first.ProviderModelName, StringComparison.Ordinal) &&
            string.Equals(chunk.EmbeddingProvider, first.EmbeddingProvider, StringComparison.Ordinal) &&
            chunk.EmbeddingDimensions == first.EmbeddingDimensions);

        return new DocumentVectorizationMetadata(
            true,
            false,
            chunks.Count,
            chunks.Max(chunk => chunk.IngestedUtc),
            uniform ? first.EmbeddingModelId : null,
            uniform ? first.EmbeddingModelName : null,
            uniform ? first.ProviderModelName : null,
            uniform ? first.EmbeddingProvider : null,
            uniform ? first.EmbeddingDimensions : null);
    }

    private sealed record ChunkMetadata(
        DateTime IngestedUtc,
        Guid? EmbeddingModelId,
        string? EmbeddingModelName,
        string ProviderModelName,
        string EmbeddingProvider,
        int? EmbeddingDimensions);
}
