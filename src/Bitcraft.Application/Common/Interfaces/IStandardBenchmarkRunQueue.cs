namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Queue of pending standardized benchmark run ids. Separate from <see cref="IBenchmarkRunQueue"/>
/// so tutor-scoped and model-scoped runs are processed independently.
/// </summary>
public interface IStandardBenchmarkRunQueue
{
    ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
