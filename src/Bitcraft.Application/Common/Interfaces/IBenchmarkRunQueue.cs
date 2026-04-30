namespace Bitcraft.Application.Common.Interfaces;

public interface IBenchmarkRunQueue
{
    ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
