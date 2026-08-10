using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.UnitTests.Application.Benchmarking.Fakes;

internal sealed class FakeBenchmarkTemplateRepository : IBenchmarkTemplateRepository
{
    public Dictionary<Guid, BenchmarkTemplate> Templates { get; } = new();
    public HashSet<Guid> InUseTemplateIds { get; } = [];

    public Task<BenchmarkTemplate> AddAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default)
    {
        Templates[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task<BenchmarkTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.TryGetValue(id, out var template) ? template : null);

    public Task<BenchmarkTemplate?> GetByOwnerAndKeyAsync(Guid ownerId, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.Values.FirstOrDefault(x => x.OwnerId == ownerId && x.Key == key));

    public Task<List<BenchmarkTemplate>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.Values.Where(x => x.OwnerId == ownerId).OrderBy(x => x.Name).ToList());

    public Task<bool> IsInUseAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(InUseTemplateIds.Contains(templateId));

    public Task DeleteAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default)
    {
        Templates.Remove(template.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeBenchmarkRunRepository : IBenchmarkRunRepository
{
    public Dictionary<Guid, BenchmarkRun> Runs { get; } = new();
    public int UpdateCalls { get; private set; }
    public List<double> ProgressUpdates { get; } = [];

    public Task<BenchmarkRun> AddAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        foreach (var runCase in run.Cases)
        {
            runCase.BenchmarkRunId = run.Id;
            runCase.BenchmarkRun = run;
        }

        Runs[run.Id] = run;
        return Task.FromResult(run);
    }

    public Task<BenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.TryGetValue(id, out var run) ? run : null);

    public Task<BenchmarkRun?> GetByIdWithCasesAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.TryGetValue(id, out var run) ? run : null);

    public Task<List<BenchmarkRun>> ListByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values.Where(x => x.TutorId == tutorId).OrderByDescending(x => x.QueuedUtc).ToList());

    public Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.Values
            .Where(x => x.Status is BenchmarkRunStatus.Queued or BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .Select(x => x.Id)
            .ToList());

    public Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        ProgressUpdates.Add(run.ProgressPercent);
        Runs[run.Id] = run;
        return Task.CompletedTask;
    }
}

internal sealed class FakeBenchmarkRunQueue : IBenchmarkRunQueue
{
    public List<Guid> QueuedRunIds { get; } = [];
    public List<CancellationToken> QueueCancellationTokens { get; } = [];

    public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        QueueCancellationTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
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

internal sealed class FakeBenchmarkEngine : IBenchmarkEngine
{
    public BenchmarkEngineRequest? LastRequest { get; private set; }
    public Result<BenchmarkEngineResult>? ResultToReturn { get; set; }

    public Task<Result<BenchmarkEngineResult>> EvaluateAsync(BenchmarkEngineRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(ResultToReturn ?? Result.Failure<BenchmarkEngineResult>(
            new Error("Benchmark.EngineNotConfigured", "Fake benchmark engine result was not configured.")));
    }
}

internal sealed class FakeTutorBenchmarkOutputGenerator : ITutorBenchmarkOutputGenerator
{
    public List<TutorBenchmarkGenerationRequest> Requests { get; } = [];
    public Queue<Result<GeneratedBenchmarkOutput>> Results { get; } = new();

    public Task<Result<GeneratedBenchmarkOutput>> GenerateAsync(
        TutorBenchmarkGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        var result = Results.Count > 0
            ? Results.Dequeue()
            : Result.Success(new GeneratedBenchmarkOutput("Generated answer", "llama3", request.ContextOverride, 12.5));
        return Task.FromResult(result);
    }
}
