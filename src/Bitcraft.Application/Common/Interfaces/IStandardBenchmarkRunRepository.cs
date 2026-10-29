using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.Application.Common.Interfaces;

public interface IStandardBenchmarkRunRepository
{
    Task<StandardBenchmarkRun> AddAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default);
    Task<StandardBenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked fetch for the worker path, which mutates and saves the row.</summary>
    Task<StandardBenchmarkRun?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<StandardBenchmarkRun>> ListByModelAsync(Guid interactionModelId, CancellationToken cancellationToken = default);

    /// <summary>Recent runs, excluding archived rows unless includeArchived is set (HD-4).</summary>
    Task<List<StandardBenchmarkRun>> ListRecentAsync(int limit, bool includeArchived, CancellationToken cancellationToken = default);

    /// <summary>Tracked fetch of runs currently in Running, for stall reclamation.</summary>
    Task<List<StandardBenchmarkRun>> ListRunningAsync(CancellationToken cancellationToken = default);

    Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked fetch of queued runs in execution order: priority first, then age (DR-5).</summary>
    Task<List<StandardBenchmarkRun>> ListQueuedOrderedAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a run row. Deletion is hard by default in this repository.</summary>
    Task DeleteAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the singleton queue state row, creating it (unpaused) when absent (HD-8).
    /// </summary>
    Task<BenchmarkQueueState> GetQueueStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a mutation of the singleton queue state row (HD-8).</summary>
    Task UpdateQueueStateAsync(BenchmarkQueueState state, CancellationToken cancellationToken = default);
}
