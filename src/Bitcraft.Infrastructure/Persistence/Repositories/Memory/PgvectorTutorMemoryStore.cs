using Bitcraft.Application.Common.Interfaces;
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
        Guid creatorId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(queryEmbedding);

        return db.TutorMemories
            .Where(memory => memory.CreatedById == creatorId && memory.Embedding != null)
            .OrderBy(memory => memory.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}
