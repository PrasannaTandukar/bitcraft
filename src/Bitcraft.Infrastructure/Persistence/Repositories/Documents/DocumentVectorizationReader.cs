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
        var result = await GetMetadataAsync([documentId], cancellationToken);
        return result.GetValueOrDefault(documentId) ?? EmptyMetadata();
    }

    public async Task<IReadOnlyDictionary<Guid, DocumentVectorizationMetadata>> GetMetadataAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = documentIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var results = distinctIds.ToDictionary(id => id, _ => EmptyMetadata());
        if (distinctIds.Length == 0 ||
            db.Model.FindEntityType(typeof(Bitcraft.Domain.Knowledge.KnowledgeChunk)) is null)
            return results;

        var groups = await db.KnowledgeChunks
            .AsNoTracking()
            .Where(chunk => distinctIds.Contains(chunk.DocumentId))
            .GroupBy(chunk => new
            {
                chunk.DocumentId,
                chunk.EmbeddingModelId,
                EmbeddingModelName = chunk.EmbeddingModelEntity == null ? null : chunk.EmbeddingModelEntity.Name,
                chunk.EmbeddingModel,
                chunk.EmbeddingProvider,
                chunk.EmbeddingDimensions
            })
            .Select(group => new GroupedChunkMetadata(
                group.Key.DocumentId,
                group.Key.EmbeddingModelId,
                group.Key.EmbeddingModelName,
                group.Key.EmbeddingModel,
                group.Key.EmbeddingProvider,
                group.Key.EmbeddingDimensions,
                group.Count(),
                group.Max(chunk => chunk.IngestedUtc)))
            .ToListAsync(cancellationToken);

        foreach (var documentGroups in groups.GroupBy(group => group.DocumentId))
        {
            var first = documentGroups.First();
            var uniform = documentGroups.Count() == 1;
            results[documentGroups.Key] = new DocumentVectorizationMetadata(
                true,
                false,
                documentGroups.Sum(group => group.ChunkCount),
                documentGroups.Max(group => group.LastIngestedUtc),
                uniform ? first.EmbeddingModelId : null,
                uniform ? first.EmbeddingModelName : null,
                uniform ? first.ProviderModelName : null,
                uniform ? first.EmbeddingProvider : null,
                uniform ? first.EmbeddingDimensions : null);
        }

        return results;
    }

    private static DocumentVectorizationMetadata EmptyMetadata() =>
        new(false, false, 0, null, null, null, null, null, null);

    private sealed record GroupedChunkMetadata(
        Guid DocumentId,
        Guid? EmbeddingModelId,
        string? EmbeddingModelName,
        string ProviderModelName,
        string EmbeddingProvider,
        int? EmbeddingDimensions,
        int ChunkCount,
        DateTime LastIngestedUtc);
}
