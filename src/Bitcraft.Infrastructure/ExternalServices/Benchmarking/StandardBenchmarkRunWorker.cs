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

    // The sidecar enforces exactly one runner slot (decision D-R2). The worker must
    // match that: starting a second run in parallel makes it hit 409 and fail as
    // StandardBenchmark.SidecarBusy, which drops a queued run instead of running it.
    // Stall protection comes from ReapStalledRunsAsync and the per-run timeout, not
    // from concurrency.

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPersistedRunsAsync(stoppingToken);

        // The channel is FIFO and cannot be reordered in place (DR-5). Each queued id
        // is a wake-up signal: the worker then picks the highest-priority queued run
        // from the database. A run cancelled while queued is no longer Queued, so the
        // query skips it without a separate check.
        await foreach (var _ in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessNextByPriorityAsync(stoppingToken);
        }
    }

    private async Task ProcessNextByPriorityAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IStandardBenchmarkRunRepository>();
        var queued = await runs.ListQueuedOrderedAsync(stoppingToken);

        foreach (var candidate in queued)
        {
            if (candidate.Status != BenchmarkRunStatus.Queued)
            {
                continue;
            }

            await ProcessRunAsync(candidate.Id, stoppingToken);
            // One runner slot: process a single run per wake-up.
            return;
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
            var run = await runs.GetTrackedByIdAsync(runId, stoppingToken);
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
