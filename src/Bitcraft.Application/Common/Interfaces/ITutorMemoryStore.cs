using Bitcraft.Domain.Knowledge;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorMemoryStore
{
    Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default);

    Task<List<TutorMemory>> SearchNearestMemoriesAsync(
        Guid creatorId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
