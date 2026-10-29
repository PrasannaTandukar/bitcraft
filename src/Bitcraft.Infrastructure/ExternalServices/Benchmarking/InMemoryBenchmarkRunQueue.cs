using System.Threading.Channels;
using Bitcraft.Application.Common.Interfaces;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class InMemoryBenchmarkRunQueue : IBenchmarkRunQueue
{
    private const int Capacity = 1_000;

    private readonly Channel<Guid> channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default) =>
        channel.Writer.WriteAsync(runId, cancellationToken);

    public async IAsyncEnumerable<Guid> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var runId))
            {
                yield return runId;
            }
        }
    }
}
