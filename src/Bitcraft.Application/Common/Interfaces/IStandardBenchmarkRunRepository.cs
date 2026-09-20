using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.Application.Common.Interfaces;

public interface IStandardBenchmarkRunRepository
{
    Task<StandardBenchmarkRun> AddAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default);
    Task<StandardBenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked fetch for the worker path, which mutates and saves the row.</summary>
    Task<StandardBenchmarkRun?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<StandardBenchmarkRun>> ListByModelAsync(Guid interactionModelId, CancellationToken cancellationToken = default);
    Task<List<StandardBenchmarkRun>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default);
}
