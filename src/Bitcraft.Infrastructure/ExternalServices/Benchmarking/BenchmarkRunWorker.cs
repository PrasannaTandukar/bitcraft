using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Benchmarking;
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
    // Client-safe terminal failure used only when the worker infrastructure fails outside normal Result flow.
    private const string WorkerFailureCode = "Benchmark.WorkerException";
    private const string WorkerFailureDescription = "Benchmark worker failed while processing the run.";

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
            await ProcessRunAsync(runId, stoppingToken);
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
            await MarkRunFailedAsync(runId, stoppingToken);
        }
    }

    private async Task MarkRunFailedAsync(Guid runId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var runs = scope.ServiceProvider.GetRequiredService<IBenchmarkRunRepository>();
            var run = await runs.GetByIdAsync(runId, stoppingToken);
            if (run is null || run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            {
                return;
            }

            run.Status = BenchmarkRunStatus.Failed;
            run.CompletedUtc = DateTime.UtcNow;
            run.FailureCode = WorkerFailureCode;
            run.FailureDescription = WorkerFailureDescription;
            run.UpdateProgress(run.CompletedCaseCount, run.TotalCaseCount, "Failed", WorkerFailureDescription);
            await runs.UpdateAsync(run, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark benchmark run {BenchmarkRunId} as failed after worker exception", runId);
        }
    }
}
