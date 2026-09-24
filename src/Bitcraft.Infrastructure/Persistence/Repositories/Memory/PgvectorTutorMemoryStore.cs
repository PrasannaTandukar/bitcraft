using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Memory;

/// <summary>PostgreSQL pgvector store for user-owned saved tutor conversations.</summary>
public class PgvectorTutorMemoryStore(AppDbContext db) : ITutorMemoryStore
{
    public async Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default)
    {
        db.TutorMemories.Add(memory);
        await db.SaveChangesAsync(cancellationToken);
        return memory;
    }

    public Task<List<TutorMemory>> SearchNearestMemoriesAsync(
        TutorMemorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(request.QueryEmbedding);

        return db.TutorMemories
            .AsNoTracking()
            .Where(memory => memory.UserId == request.UserId && memory.TutorId == request.TutorId &&
                memory.EmbeddingModelId == request.EmbeddingModelId &&
                memory.EmbeddingDimensions == request.EmbeddingDimensions && memory.Embedding != null &&
                (!request.ExcludedConversationId.HasValue || memory.ConversationId == null ||
                    memory.ConversationId != request.ExcludedConversationId))
            .OrderBy(memory => memory.Embedding!.CosineDistance(queryVector))
            .Take(Math.Clamp(request.TopK, 1, 10))
            .ToListAsync(cancellationToken);
    }
}
