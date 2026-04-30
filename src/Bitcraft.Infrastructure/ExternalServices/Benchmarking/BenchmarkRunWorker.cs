using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class BenchmarkRunWorker(
    IBenchmarkRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BenchmarkRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPersistedRunsAsync(stoppingToken);

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessRunAsync(runId, stoppingToken);
        }
    }

    private async Task RecoverPersistedRunsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IBenchmarkRunRepository>();
        var recoverableRunIds = await runs.ListRecoverableRunIdsAsync(stoppingToken);
        foreach (var runId in recoverableRunIds)
        {
            await queue.QueueAsync(runId, stoppingToken);
        }
    }

    private async Task ProcessRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(runId), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process benchmark run {BenchmarkRunId}", runId);
        }
    }
}
