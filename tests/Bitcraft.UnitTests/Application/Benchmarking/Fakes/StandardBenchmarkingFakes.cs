using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.UnitTests.Application.Benchmarking.Fakes;

internal sealed class FakeStandardBenchmarkRunRepository : IStandardBenchmarkRunRepository
{
    public Dictionary<Guid, StandardBenchmarkRun> Runs { get; } = new();
    public int UpdateCalls { get; private set; }

    public Task<StandardBenchmarkRun> AddAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        Runs[run.Id] = run;
        return Task.FromResult(run);
    }

    public Task<StandardBenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.TryGetValue(id, out var run) ? run : null);

    public Task<StandardBenchmarkRun?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.TryGetValue(id, out var run) ? run : null);

    public Task<List<StandardBenchmarkRun>> ListByModelAsync(Guid interactionModelId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values.Where(x => x.InteractionModelId == interactionModelId).OrderByDescending(x => x.QueuedUtc).ToList());

    public Task<List<StandardBenchmarkRun>> ListRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values.OrderByDescending(x => x.QueuedUtc).Take(limit).ToList());

    public Task<List<StandardBenchmarkRun>> ListRunningAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values
            .Where(x => x.Status == BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .ToList());

    public Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values
            .Where(x => x.Status is BenchmarkRunStatus.Queued or BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .Select(x => x.Id)
            .ToList());

    public Task UpdateAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        Runs[run.Id] = run;
        UpdateCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeStandardBenchmarkRunQueue : IStandardBenchmarkRunQueue
{
    public List<Guid> QueuedRunIds { get; } = [];

    public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        QueuedRunIds.Add(runId);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var runId in QueuedRunIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return runId;
        }

        await Task.CompletedTask;
    }
}
