using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Modeling;
using Bitcraft.UnitTests.Application.Benchmarking.Fakes;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bitcraft.UnitTests.Application.Benchmarking;

public class StandardBenchmarkFeaturesTests
{
    private static FakeCurrentUserContext Admin() => new(Guid.NewGuid(), isAdmin: true);
    private static FakeCurrentUserContext NonAdmin() => new(Guid.NewGuid(), isAdmin: false);
    private static FakeCurrentUserContext Auditor() => new(Guid.NewGuid(), isAuditor: true);

    private static StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunHandler ProcessHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeModelRepository models,
        FakeBenchmarkEngine engine) =>
        new(runs, models, engine, Options.Create(new StandardBenchmarkFeatures.StandardBenchmarkProcessingOptions()), TimeProvider.System);

    private static (InteractionModel Model, RuntimeEngine Engine) EnabledModel()
    {
        var engine = new RuntimeEngine
        {
            Name = "Local Ollama",
            ProviderType = RuntimeEngineProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            IsEnabled = true
        };
        var model = new InteractionModel
        {
            Name = "Llama 3.3",
            ProviderModelName = "llama3.3:70b",
            RuntimeEngineId = engine.Id,
            RuntimeEngine = engine
        };
        return (model, engine);
    }

    // ----- Create -----

    [Fact]
    public async Task Create_AsAdmin_PersistsAndQueues()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var queue = new FakeStandardBenchmarkRunQueue();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, queue, Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(runs.Runs);
        Assert.Equal("mmlu", result.Value.BenchmarkKey);
        Assert.Equal(10, result.Value.NProblems);
        Assert.Equal(BenchmarkRunStatus.Queued, result.Value.Status);
        Assert.Single(queue.QueuedRunIds);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), NonAdmin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
        Assert.Empty(runs.Runs);
    }

    [Theory]
    [InlineData("not-a-suite", "StandardBenchmark.UnsupportedSuite")]
    [InlineData("human_eval", "StandardBenchmark.UnsupportedSuite")]
    public async Task Create_WithUnknownOrDisabledSuite_ReturnsValidationError(string key, string expectedCode)
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, key, null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Theory]
    [InlineData(-1, 10, "StandardBenchmark.NShotsOutOfRange")]
    [InlineData(6, 10, "StandardBenchmark.NShotsOutOfRange")]
    [InlineData(0, -5, "StandardBenchmark.NProblemsOutOfRange")]
    [InlineData(0, 10_001, "StandardBenchmark.NProblemsOutOfRange")]
    public async Task Create_WithOutOfRangeParameters_ReturnsValidationError(int nShots, int nProblems, string expectedCode)
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, nShots, nProblems);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task Create_BigBenchHard_WithFourShots_IsRejected()
    {
        // AC15: BBH asserts n_shots <= 3 in deepeval; the API must reject before queueing.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "big_bench_hard", null, 4, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NShotsOutOfRange", result.Error.Code);
        Assert.Empty(runs.Runs);
    }

    [Fact]
    public async Task Create_Mmlu_WithFiveShots_IsAccepted()
    {
        // AC15: MMLU allows up to 5 shots.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 5, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(runs.Runs);
    }

    [Fact]
    public async Task Create_WithZeroProblems_IsAccepted()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "gsm8k", null, 0, 0);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.NProblems);
    }

    [Fact]
    public async Task Create_WithUnknownModel_ReturnsNotFound()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(Guid.NewGuid(), "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.ModelNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Create_WithDisabledModel_ReturnsModelDisabled()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        model.IsEnabled = false;
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.ModelDisabled", result.Error.Code);
    }

    [Fact]
    public async Task Create_WithUnsupportedProvider_ReturnsProviderNotSupported()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, engine) = EnabledModel();
        engine.ProviderType = RuntimeEngineProviderType.LlamaCpp;
        models.InteractionModels[model.Id] = model;

        var handler = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.ProviderNotSupported", result.Error.Code);
        Assert.Empty(runs.Runs);
    }

    // ----- List / Get -----

    [Fact]
    public async Task List_AsAuditor_ReturnsRuns()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        runs.Runs[Guid.NewGuid()] = new StandardBenchmarkRun { BenchmarkKey = "mmlu", InteractionModelId = Guid.NewGuid() };

        var handler = new StandardBenchmarkFeatures.ListStandardBenchmarkRunsHandler(runs, Auditor());
        var result = await handler.Handle(new StandardBenchmarkFeatures.ListStandardBenchmarkRunsQuery(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task List_AsNonAdminNonAuditor_ReturnsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var handler = new StandardBenchmarkFeatures.ListStandardBenchmarkRunsHandler(runs, NonAdmin());

        var result = await handler.Handle(new StandardBenchmarkFeatures.ListStandardBenchmarkRunsQuery(null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Get_AsAuditor_ReturnsRun()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun { BenchmarkKey = "gsm8k", InteractionModelId = Guid.NewGuid() };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.GetStandardBenchmarkRunHandler(runs, Auditor());
        var result = await handler.Handle(new StandardBenchmarkFeatures.GetStandardBenchmarkRunQuery(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("gsm8k", result.Value.BenchmarkKey);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var handler = new StandardBenchmarkFeatures.GetStandardBenchmarkRunHandler(runs, Admin());

        var result = await handler.Handle(new StandardBenchmarkFeatures.GetStandardBenchmarkRunQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotFound", result.Error.Code);
    }

    // ----- Process (worker path) -----

    [Fact]
    public async Task Process_CompletesRun_AndPersistsScores()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NShots = 3,
            NProblems = 10,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Completed, run.Status);
        Assert.Equal(0.85, run.OverallScore);
        Assert.Equal("deepeval-fake", run.EngineVersion);
        Assert.NotNull(engine.LastStandardRequest);
        Assert.Equal("gsm8k", engine.LastStandardRequest!.BenchmarkKey);
        Assert.Equal(10, engine.LastStandardRequest.NProblems);
        Assert.Equal(3, engine.LastStandardRequest.NShots);
        Assert.Equal("ollama", engine.LastStandardRequest.Provider);
        Assert.Equal("http://localhost:11434", engine.LastStandardRequest.BaseUrl);
    }

    [Fact]
    public async Task Process_SendsStoredEngineKeyToSidecar()
    {
        // WS5: the stored runtime-engine key travels in the sidecar payload over the
        // local Unix socket so hosted OpenAI benchmarks can authenticate.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var (model, runtimeEngine) = EnabledModel();
        runtimeEngine.ProviderType = RuntimeEngineProviderType.OpenAI;
        runtimeEngine.Endpoint = "https://api.openai.com/v1";
        runtimeEngine.ApiKey = "stored-benchmark-key";
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            NProblems = 5,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(engine.LastStandardRequest);
        Assert.Equal("openai", engine.LastStandardRequest!.Provider);
        Assert.Equal("stored-benchmark-key", engine.LastStandardRequest.ApiKey);
    }

    [Fact]
    public async Task Process_CompletedRun_PersistsTaskScoresAsCamelCase()
    {
        // Regression: TaskScoresJson is parsed by the frontend as { task, score }.
        // PascalCase (Task/Score) would render empty UI columns.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        engine.StatusResults.Enqueue(Bitcraft.Application.Common.Models.Result.Success(
            new Bitcraft.Application.Common.Models.StandardBenchmarkStatus(
                Bitcraft.Application.Common.Models.StandardBenchmarkStatus.StateCompleted,
                "finalizing",
                0.9,
                [new Bitcraft.Application.Common.Models.StandardBenchmarkTaskScore("abstract_algebra", 0.75)],
                1,
                "deepeval-fake",
                "done",
                null)));
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            NProblems = 2,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(run.TaskScoresJson);
        Assert.Contains("\"task\":\"abstract_algebra\"", run.TaskScoresJson);
        Assert.Contains("\"score\":0.75", run.TaskScoresJson);
    }

    [Fact]
    public async Task Process_WithUnknownRunnerState_MarksRunFailed()
    {
        // An unrecognized state must fail fast instead of polling for the whole budget.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        engine.StatusResults.Enqueue(Bitcraft.Application.Common.Models.Result.Success(
            new Bitcraft.Application.Common.Models.StandardBenchmarkStatus(
                "wat",
                "evaluating",
                null,
                null,
                0,
                "deepeval-fake",
                "",
                null)));
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NProblems = 5,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Equal("StandardBenchmark.UnknownState", run.FailureCode);
    }

    [Fact]
    public async Task Process_WithKeylessOllamaEngine_SendsNullKey()
    {
        // Local Ollama engines carry no key. The payload must send null, not a blank string.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NProblems = 5,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(engine.LastStandardRequest);
        Assert.Null(engine.LastStandardRequest!.ApiKey);
    }

    [Fact]
    public async Task Process_WithEngineError_MarksRunFailed()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        engine.StatusResults.Enqueue(Bitcraft.Application.Common.Models.Result.Success(
            new Bitcraft.Application.Common.Models.StandardBenchmarkStatus(
                Bitcraft.Application.Common.Models.StandardBenchmarkStatus.StateFailed,
                "evaluating",
                null,
                null,
                0,
                "deepeval-fake",
                "",
                "model unavailable")));
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NProblems = 10,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Equal("StandardBenchmark.EngineError", run.FailureCode);
        Assert.Contains("model unavailable", run.FailureDescription);
    }

    [Fact]
    public async Task Process_TerminalRun_IsSkipped()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "gsm8k",
            NProblems = 10,
            Status = BenchmarkRunStatus.Completed
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(engine.LastStandardRequest);
        Assert.Equal(BenchmarkRunStatus.Completed, run.Status);
    }

    // ----- Suite registry -----

    [Fact]
    public void SuiteRegistry_ContainsSevenSuites_WithHumanEvalDisabled()
    {
        Assert.Equal(7, StandardBenchmarkSuiteRegistry.Suites.Count);
        Assert.True(StandardBenchmarkSuiteRegistry.Find("human_eval") is { Enabled: false });
        Assert.True(StandardBenchmarkSuiteRegistry.IsEnabled("mmlu"));
        Assert.True(StandardBenchmarkSuiteRegistry.Find("gsm8k")!.SupportsProblemCap);
        Assert.False(StandardBenchmarkSuiteRegistry.Find("truthful_qa")!.SupportsShots);
    }
}
