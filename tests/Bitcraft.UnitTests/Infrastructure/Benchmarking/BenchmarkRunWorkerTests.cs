using System.Runtime.CompilerServices;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Benchmarking;

public class BenchmarkRunWorkerTests
{
    [Fact]
    public async Task StartAsync_WithMoreRecoveredRunsThanQueueCapacity_ProcessesRecoveredRunsWithoutQueueing()
    {
        // Arrange
        var recoverableRunIds = Enumerable.Range(0, 1_001).Select(_ => Guid.NewGuid()).ToList();
        var repository = new WorkerBenchmarkRunRepository(recoverableRunIds);
        var queue = new ThrowingQueue();
        var sender = new RecordingSender(recoverableRunIds.Count);
        var worker = CreateWorker(repository, queue, sender);

        // Act
        await worker.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(sender.AllExpectedSendsCompleted, Task.Delay(TimeSpan.FromSeconds(5)));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.Same(sender.AllExpectedSendsCompleted, completed);
        Assert.Equal(recoverableRunIds.Count, sender.SentRunIds.Count);
        Assert.Equal(recoverableRunIds, sender.SentRunIds);
        Assert.Equal(0, queue.QueueCalls);
    }

    [Fact]
    public async Task StartAsync_WhenProcessingThrows_MarksRunFailed()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Running,
            TotalCaseCount = 1,
            CompletedCaseCount = 0
        };
        var repository = new WorkerBenchmarkRunRepository([]) { Run = run };
        var queue = new SingleRunQueue(run.Id);
        var sender = new ThrowingSender();
        var worker = CreateWorker(repository, queue, sender);

        // Act
        await worker.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(repository.RunFailed, Task.Delay(TimeSpan.FromSeconds(5)));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.Same(repository.RunFailed, completed);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Equal("Benchmark.WorkerException", run.FailureCode);
        Assert.Equal("Benchmark worker failed while processing the run.", run.FailureDescription);
        Assert.Equal("Failed", run.ProgressStage);
    }

    private static BenchmarkRunWorker CreateWorker(
        IBenchmarkRunRepository repository,
        IBenchmarkRunQueue queue,
        ISender sender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(sender);

        var serviceProvider = services.BuildServiceProvider();

        return new BenchmarkRunWorker(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BenchmarkRunWorker>.Instance);
    }

    private sealed class WorkerBenchmarkRunRepository(IReadOnlyList<Guid> recoverableRunIds) : IBenchmarkRunRepository
    {
        private readonly TaskCompletionSource runFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BenchmarkRun? Run { get; init; }
        public Task RunFailed => runFailed.Task;

        public Task<BenchmarkRun> AddAsync(BenchmarkRun run, CancellationToken cancellationToken = default) =>
            Task.FromResult(run);

        public Task<BenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Run?.Id == id ? Run : null);

        public Task<BenchmarkRun?> GetByIdWithCasesAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Run?.Id == id ? Run : null);

        public Task<List<BenchmarkRun>> ListByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<BenchmarkRun>());

        public Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(recoverableRunIds.ToList());

        public Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
        {
            if (run.Status == BenchmarkRunStatus.Failed)
            {
                runFailed.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingQueue : IBenchmarkRunQueue
    {
        public int QueueCalls { get; private set; }

        public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            QueueCalls++;
            throw new InvalidOperationException("Recovered runs should be processed directly.");
        }

        public async IAsyncEnumerable<Guid> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class SingleRunQueue(Guid runId) : IBenchmarkRunQueue
    {
        public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public async IAsyncEnumerable<Guid> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return runId;
            await Task.CompletedTask;
        }
    }

    private sealed class RecordingSender(int expectedCount) : ISender
    {
        private readonly TaskCompletionSource allExpectedSendsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Guid> SentRunIds { get; } = [];
        public Task AllExpectedSendsCompleted => allExpectedSendsCompleted.Task;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand command)
            {
                SentRunIds.Add(command.BenchmarkRunId);
                if (SentRunIds.Count == expectedCount)
                {
                    allExpectedSendsCompleted.TrySetResult();
                }
            }

            return Task.FromResult((TResponse)(object)Result.Success());
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(Result.Success());

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<object?>();
    }

    private sealed class ThrowingSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Processing failed.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new InvalidOperationException("Processing failed.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Processing failed.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<object?>();
    }
}
