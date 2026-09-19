using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Domain.Benchmarking;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class StandardBenchmarkRunWorker(
    IStandardBenchmarkRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<StandardBenchmarkRunWorker> logger) : BackgroundService
{
    private const string WorkerFailureCode = "StandardBenchmark.WorkerException";
    private const string WorkerFailureDescription = "Standardized benchmark worker failed while processing the run.";

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
        var runs = scope.ServiceProvider.GetRequiredService<IStandardBenchmarkRunRepository>();
        var recoverableRunIds = await runs.ListRecoverableRunIdsAsync(stoppingToken);
        foreach (var runId in recoverableRunIds)
        {
            await ProcessRunAsync(runId, stoppingToken);
        }
    }

    private async Task ProcessRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(runId), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process standardized benchmark run {StandardBenchmarkRunId}", runId);
            await MarkRunFailedAsync(runId, stoppingToken);
        }
    }

    private async Task MarkRunFailedAsync(Guid runId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var runs = scope.ServiceProvider.GetRequiredService<IStandardBenchmarkRunRepository>();
            var run = await runs.GetByIdAsync(runId, stoppingToken);
            if (run is null || run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            {
                return;
            }

            run.Status = BenchmarkRunStatus.Failed;
            run.CompletedUtc = DateTime.UtcNow;
            run.FailureCode = WorkerFailureCode;
            run.FailureDescription = WorkerFailureDescription;
            await runs.UpdateAsync(run, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark standardized benchmark run {StandardBenchmarkRunId} as failed after worker exception", runId);
        }
    }
}
