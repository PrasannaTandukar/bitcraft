using Bitcraft.Domain.Knowledge;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorMemoryStore
{
    Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default);

    Task<List<TutorMemory>> SearchNearestMemoriesAsync(
        TutorMemorySearchRequest request,
        CancellationToken cancellationToken = default);
}
