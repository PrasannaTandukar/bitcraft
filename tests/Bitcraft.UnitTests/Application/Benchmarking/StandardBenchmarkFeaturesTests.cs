using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
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
                "scoring",
                0.9,
                [new Bitcraft.Application.Common.Models.StandardBenchmarkTaskScore("abstract_algebra", 0.75)],
                1,
                2,
                2,
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
    public async Task Process_ReapsStalledRun_BeforeStartingNewRun()
    {
        // WS3: a run stuck in Running with stale progress is reaped so it cannot
        // hold the single sidecar slot and block later runs.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var stalled = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running,
            StartedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastProgressUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        runs.Runs[stalled.Id] = stalled;

        var current = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[current.Id] = current;

        var handler = StalledProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(current.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Failed, stalled.Status);
        Assert.Equal("StandardBenchmark.Stalled", stalled.FailureCode);
        Assert.Equal(BenchmarkRunStatus.Completed, current.Status);
    }

    [Fact]
    public async Task Process_DoesNotReapFreshRunningRun()
    {
        // A run with recent progress must not be reaped.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var fresh = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running,
            StartedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            LastProgressUtc = new DateTime(2026, 6, 1, 0, 5, 0, DateTimeKind.Utc)
        };
        runs.Runs[fresh.Id] = fresh;

        var current = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[current.Id] = current;

        var handler = StalledProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(current.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Running, fresh.Status);
        Assert.Null(fresh.FailureCode);
    }

    private static StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunHandler StalledProcessHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeModelRepository models,
        FakeBenchmarkEngine engine) =>
        new(
            runs,
            models,
            engine,
            Options.Create(new StandardBenchmarkFeatures.StandardBenchmarkProcessingOptions { StalledAfterSeconds = 3600 }),
            new Bitcraft.UnitTests.Application.Chat.FakeAttachmentTimeProvider(new DateTimeOffset(2026, 6, 1, 1, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Process_CompletedRun_PersistsProgressFields()
    {
        // WS2: engine-reported completed/total counts round-trip onto the run and
        // ProgressPercent is derived from them.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        engine.StatusResults.Enqueue(Bitcraft.Application.Common.Models.Result.Success(
            new Bitcraft.Application.Common.Models.StandardBenchmarkStatus(
                Bitcraft.Application.Common.Models.StandardBenchmarkStatus.StateCompleted,
                "scoring",
                0.75,
                null,
                4,
                3,
                4,
                "deepeval-fake",
                "done",
                null)));
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            NProblems = 4,
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, run.CompletedTasks);
        Assert.Equal(4, run.TotalTasks);
        Assert.Equal(75.0, run.ProgressPercent);
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
                null,
                null,
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
    public async Task Process_WithSidecarBusy_LeavesRunQueuedForRetry()
    {
        // G8 fix: a 409 SidecarBusy start must not fail the run. The worker
        // picks it up again on the next wake-up while it stays inside the
        // standard timeout budget.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine
        {
            StartResultToReturn = Bitcraft.Application.Common.Models.Result.Failure<StandardBenchmarkHandle>(
                new Error("StandardBenchmark.SidecarBusy", "The benchmark sidecar is already running a standardized benchmark."))
        };
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NProblems = 5,
            Status = BenchmarkRunStatus.Queued,
            QueuedUtc = DateTime.UtcNow
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Queued, run.Status);
        Assert.Equal("waiting-for-engine", run.ProgressStage);
        Assert.Null(run.EngineRunId);
        Assert.Null(run.FailureCode);
    }

    [Fact]
    public async Task Process_WithSidecarBusyPastBudget_MarksQueueTimeout()
    {
        // G8 fix: a run that waited longer than StandardTimeoutSeconds for a
        // free sidecar fails with StandardBenchmark.QueueTimeout.
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine
        {
            StartResultToReturn = Bitcraft.Application.Common.Models.Result.Failure<StandardBenchmarkHandle>(
                new Error("StandardBenchmark.SidecarBusy", "The benchmark sidecar is already running a standardized benchmark."))
        };
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "gsm8k",
            NProblems = 5,
            Status = BenchmarkRunStatus.Queued,
            QueuedUtc = DateTime.UtcNow.AddDays(-2)
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Equal("StandardBenchmark.QueueTimeout", run.FailureCode);
    }

    [Fact]
    public async Task GetLog_ForFailedRunWithoutEngineRunId_ReturnsSynthesizedDiagnostics()
    {
        // G9 fix: a run that failed before the sidecar spawned a runner has no
        // engine log. The handler must synthesize a diagnostic block from the
        // stored failure fields instead of returning null.
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Failed,
            FailureCode = "StandardBenchmark.SidecarBusy",
            FailureDescription = "The benchmark sidecar is already running a standardized benchmark.",
            EngineRunId = null
        };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.GetStandardBenchmarkRunLogHandler(runs, engine, Auditor());
        var result = await handler.Handle(new StandardBenchmarkFeatures.GetStandardBenchmarkRunLogQuery(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains("StandardBenchmark.SidecarBusy", result.Value);
        Assert.Contains("No runner log exists", result.Value);
    }

    [Fact]
    public async Task GetLog_ForCompletedRunWithoutEngineRunId_ReturnsNull()
    {
        // A completed run without an engine id is an oddity, not a failure:
        // no diagnostic block is synthesized.
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed,
            EngineRunId = null
        };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.GetStandardBenchmarkRunLogHandler(runs, engine, Auditor());
        var result = await handler.Handle(new StandardBenchmarkFeatures.GetStandardBenchmarkRunLogQuery(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    // ----- Engine status and recovery (HD-9) -----

    [Fact]
    public async Task GetEngineStatus_ForAnyAuthenticatedUser_ReturnsSidecarState()
    {
        // HD-9: the status surface is readable by Auditor and above. It reports
        // the engine state without a role check in the handler (the controller
        // owns the policy).
        var engine = new FakeBenchmarkEngine
        {
            StatusToReturn = new SidecarEngineStatus(
                IsReachable: true,
                Transport: "uds",
                SlotHeld: true,
                CurrentRunId: "abc123",
                EngineVersion: "4.2.3",
                LastError: null)
        };

        var handler = new StandardBenchmarkFeatures.GetBenchmarkEngineStatusHandler(engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.GetBenchmarkEngineStatusQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SlotHeld);
        Assert.Equal("abc123", result.Value.CurrentRunId);
        Assert.Equal("4.2.3", result.Value.EngineVersion);
    }

    [Fact]
    public async Task ClearEngineSlot_AsAdmin_ReturnsSlotCleared()
    {
        var engine = new FakeBenchmarkEngine { ClearSlotResultToReturn = Bitcraft.Application.Common.Models.Result.Success(true) };

        var handler = new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotHandler(engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ClearEngineSlot_AsNonAdmin_ReturnsForbidden()
    {
        var engine = new FakeBenchmarkEngine();

        var handler = new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotHandler(engine, NonAdmin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task ClearEngineSlot_WhenSidecarUnreachable_ReturnsFailure()
    {
        var engine = new FakeBenchmarkEngine
        {
            ClearSlotResultToReturn = Bitcraft.Application.Common.Models.Result.Failure<bool>(
                new Error("Benchmark.SidecarException", "Benchmark sidecar evaluation failed."))
        };

        var handler = new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotHandler(engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.SidecarException", result.Error.Code);
    }

    // ----- Archive (HD-4) -----

    [Fact]
    public async Task Archive_AsAdmin_HidesRunFromDefaultList()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed,
            IsArchived = false
        };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunHandler(runs, Admin(), TimeProvider.System);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(run.IsArchived);
        Assert.NotNull(run.ArchivedUtc);

        var visible = await runs.ListRecentAsync(50, includeArchived: false);
        var all = await runs.ListRecentAsync(50, includeArchived: true);
        Assert.DoesNotContain(visible, x => x.Id == run.Id);
        Assert.Contains(all, x => x.Id == run.Id);
    }

    [Fact]
    public async Task Archive_RunningRun_ReturnsNotArchivable()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running
        };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunHandler(runs, Admin(), TimeProvider.System);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotArchivable", result.Error.Code);
        Assert.False(run.IsArchived);
    }

    [Fact]
    public async Task Archive_AsNonAdmin_ReturnsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun { BenchmarkKey = "mmlu", Status = BenchmarkRunStatus.Completed };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunHandler(runs, NonAdmin(), TimeProvider.System);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Unarchive_AsAdmin_RestoresRunToList()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed,
            IsArchived = true,
            ArchivedUtc = DateTime.UtcNow
        };
        runs.Runs[run.Id] = run;

        var handler = new StandardBenchmarkFeatures.UnarchiveStandardBenchmarkRunHandler(runs, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.UnarchiveStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(run.IsArchived);
        Assert.Null(run.ArchivedUtc);
    }

    // ----- Queue pause (HD-8) -----

    [Fact]
    public async Task SetQueuePaused_AsAdmin_PersistsAndWakesWorkerOnResume()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var queue = new FakeStandardBenchmarkRunQueue();

        var handler = new StandardBenchmarkFeatures.SetBenchmarkQueuePausedHandler(runs, queue, Admin(), TimeProvider.System);

        var paused = await handler.Handle(new StandardBenchmarkFeatures.SetBenchmarkQueuePausedCommand(true), CancellationToken.None);
        Assert.True(paused.IsSuccess);
        Assert.True(paused.Value);
        Assert.True(runs.QueueState.IsPaused);

        var resumed = await handler.Handle(new StandardBenchmarkFeatures.SetBenchmarkQueuePausedCommand(false), CancellationToken.None);
        Assert.True(resumed.IsSuccess);
        Assert.False(runs.QueueState.IsPaused);

        // A resume must wake the worker so queued runs are picked up again.
        Assert.Contains(queue.QueuedRunIds, id => id == Guid.Empty);
    }

    [Fact]
    public async Task SetQueuePaused_AsNonAdmin_ReturnsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var queue = new FakeStandardBenchmarkRunQueue();

        var handler = new StandardBenchmarkFeatures.SetBenchmarkQueuePausedHandler(runs, queue, NonAdmin(), TimeProvider.System);
        var result = await handler.Handle(new StandardBenchmarkFeatures.SetBenchmarkQueuePausedCommand(true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetQueuePaused_ReturnsCurrentState()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        runs.QueueState = new BenchmarkQueueState { IsPaused = true };

        var handler = new StandardBenchmarkFeatures.GetBenchmarkQueuePausedHandler(runs);
        var result = await handler.Handle(new StandardBenchmarkFeatures.GetBenchmarkQueuePausedQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
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
                null,
                null,
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
    public void SuiteRegistry_ContainsEightSuites_WithHumanEvalDisabled()
    {
        // WS5: the Arduino custom benchmark joins the seven deepeval suites.
        Assert.Equal(8, StandardBenchmarkSuiteRegistry.Suites.Count);
        Assert.True(StandardBenchmarkSuiteRegistry.Find("human_eval") is { Enabled: false });
        Assert.True(StandardBenchmarkSuiteRegistry.IsEnabled("mmlu"));
        Assert.True(StandardBenchmarkSuiteRegistry.IsEnabled("arduino"));
        Assert.True(StandardBenchmarkSuiteRegistry.Find("gsm8k")!.SupportsProblemCap);
        Assert.False(StandardBenchmarkSuiteRegistry.Find("truthful_qa")!.SupportsShots);
    }

    [Fact]
    public void SuiteRegistry_AllSuites_DefaultToTextModality()
    {
        // WS0: every shipped suite is text-only. Image suites arrive with WS5.
        Assert.All(StandardBenchmarkSuiteRegistry.Suites, s =>
            Assert.Equal(BenchmarkInputModality.Text, s.Modality));
    }

    // ----- Modality preflight (WS0) -----

    [Fact]
    public async Task Create_ImageSuite_WithNoImageSupport_FailsBeforeQueue()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new NoImageSupportHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.ModalityUnsupported", result.Error.Code);
        Assert.Empty(runs.Runs);
    }

    [Fact]
    public async Task Create_ImageSuite_WithImageSupport_PassesPreflight()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        var handler = new ImageSupportHandler(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(runs.Runs);
    }

    [Fact]
    public async Task Create_TextSuite_SkipsImageProbe()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;

        // A text suite must not probe even when the model cannot accept images.
        var handler = new TextSuiteProbeTracker(runs, models, new FakeStandardBenchmarkRunQueue(), Admin());
        var cmd = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(model.Id, "mmlu", null, 0, 10);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(handler.ProbeCalled);
    }

    private sealed class NoImageSupportHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser)
        : StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runRepository, modelRepository, queue, currentUser)
    {
        public bool ProbeCalled { get; private set; }

        protected internal override Task<Error?> CheckModalitySupportAsync(
            StandardBenchmarkSuiteRegistry.SuiteInfo suite,
            InteractionModel model,
            CancellationToken cancellationToken)
        {
            // Force the image branch so the preflight is exercised without an Image suite in the registry.
            var imageSuite = new StandardBenchmarkSuiteRegistry.SuiteInfo(
                suite.Key, suite.DisplayName, suite.Description,
                suite.SupportsTasks, suite.SupportsShots, suite.SupportsProblemCap,
                suite.MaxShots, suite.Enabled, BenchmarkInputModality.Image);
            return base.CheckModalitySupportAsync(imageSuite, model, cancellationToken);
        }

        internal override Task<bool> ProbeImageSupportAsync(InteractionModel model, CancellationToken cancellationToken)
        {
            ProbeCalled = true;
            return Task.FromResult(false);
        }
    }

    private sealed class ImageSupportHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser)
        : StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runRepository, modelRepository, queue, currentUser)
    {
        protected internal override Task<Error?> CheckModalitySupportAsync(
            StandardBenchmarkSuiteRegistry.SuiteInfo suite,
            InteractionModel model,
            CancellationToken cancellationToken)
        {
            var imageSuite = new StandardBenchmarkSuiteRegistry.SuiteInfo(
                suite.Key, suite.DisplayName, suite.Description,
                suite.SupportsTasks, suite.SupportsShots, suite.SupportsProblemCap,
                suite.MaxShots, suite.Enabled, BenchmarkInputModality.Image);
            return base.CheckModalitySupportAsync(imageSuite, model, cancellationToken);
        }

        internal override Task<bool> ProbeImageSupportAsync(InteractionModel model, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class TextSuiteProbeTracker(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser)
        : StandardBenchmarkFeatures.CreateStandardBenchmarkRunHandler(runRepository, modelRepository, queue, currentUser)
    {
        public bool ProbeCalled { get; private set; }

        internal override Task<bool> ProbeImageSupportAsync(InteractionModel model, CancellationToken cancellationToken)
        {
            ProbeCalled = true;
            return Task.FromResult(false);
        }
    }

    // ----- Cancel (WS3) -----

    private static StandardBenchmarkFeatures.CancelStandardBenchmarkRunHandler CancelHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeBenchmarkEngine engine,
        FakeCurrentUserContext user) =>
        new(runs, engine, user, TimeProvider.System);

    [Fact]
    public async Task Cancel_QueuedRun_MarksItCancelled()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = CancelHandler(runs, engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CancelledUtc);
        Assert.Null(run.FailureCode);
        // A queued run has no engine id, so no engine call is made.
        Assert.Empty(engine.CancelledRunIds);
    }

    [Fact]
    public async Task Cancel_RunningRun_SignalsTheEngine()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running,
            EngineRunId = "engine-run-123"
        };
        runs.Runs[run.Id] = run;

        var handler = CancelHandler(runs, engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Cancelled, run.Status);
        Assert.Equal(["engine-run-123"], engine.CancelledRunIds);
    }

    [Fact]
    public async Task Cancel_CompletedRun_FailsWithNotCancellable()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed
        };
        runs.Runs[run.Id] = run;

        var handler = CancelHandler(runs, engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotCancellable", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Completed, run.Status);
    }

    [Fact]
    public async Task Cancel_NonAdmin_IsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = CancelHandler(runs, engine, NonAdmin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Queued, run.Status);
    }

    [Fact]
    public async Task Cancel_MissingRun_FailsWithNotFound()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();

        var handler = CancelHandler(runs, engine, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Process_EngineReportsCancelled_MarksRunCancelled()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var models = new FakeModelRepository();
        var engine = new FakeBenchmarkEngine();
        engine.StatusResults.Enqueue(Bitcraft.Application.Common.Models.Result.Success(
            new Bitcraft.Application.Common.Models.StandardBenchmarkStatus(
                Bitcraft.Application.Common.Models.StandardBenchmarkStatus.StateCancelled,
                null,
                null,
                null,
                0,
                null,
                null,
                "deepeval-fake",
                "",
                null)));
        var (model, _) = EnabledModel();
        models.InteractionModels[model.Id] = model;
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = model.Id,
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = ProcessHandler(runs, models, engine);
        var result = await handler.Handle(new StandardBenchmarkFeatures.ProcessQueuedStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CancelledUtc);
    }

    // ----- Delete (WS4) -----

    private static StandardBenchmarkFeatures.DeleteStandardBenchmarkRunHandler DeleteHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeCurrentUserContext user) => new(runs, user);

    [Fact]
    public async Task Delete_SettledRun_RemovesIt()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed
        };
        runs.Runs[run.Id] = run;

        var handler = DeleteHandler(runs, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.DeleteStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(runs.Runs);
        Assert.Equal(1, runs.DeleteCalls);
    }

    [Fact]
    public async Task Delete_RunningRun_FailsWithNotDeletable()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running
        };
        runs.Runs[run.Id] = run;

        var handler = DeleteHandler(runs, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.DeleteStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotDeletable", result.Error.Code);
        Assert.Single(runs.Runs);
    }

    [Fact]
    public async Task Delete_NonAdmin_IsForbidden()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed
        };
        runs.Runs[run.Id] = run;

        var handler = DeleteHandler(runs, NonAdmin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.DeleteStandardBenchmarkRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.Forbidden", result.Error.Code);
        Assert.Single(runs.Runs);
    }

    // ----- Requeue (WS4) -----

    private static StandardBenchmarkFeatures.RequeueStandardBenchmarkRunHandler RequeueHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeStandardBenchmarkRunQueue queue,
        FakeCurrentUserContext user) => new(runs, queue, user);

    [Fact]
    public async Task Requeue_FailedRun_CreatesNewQueuedRun()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var queue = new FakeStandardBenchmarkRunQueue();
        var source = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "gsm8k",
            NShots = 2,
            NProblems = 5,
            Status = BenchmarkRunStatus.Failed
        };
        runs.Runs[source.Id] = source;

        var handler = RequeueHandler(runs, queue, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.RequeueStandardBenchmarkRunCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var clone = result.Value;
        Assert.NotEqual(source.Id, clone.Id);
        Assert.Equal(BenchmarkRunStatus.Queued, clone.Status);
        Assert.Equal("gsm8k", clone.BenchmarkKey);
        Assert.Equal(2, clone.NShots);
        Assert.Equal(5, clone.NProblems);
        Assert.Equal([clone.Id], queue.QueuedRunIds);
        // The original row is immutable history and must survive.
        Assert.Equal(BenchmarkRunStatus.Failed, source.Status);
        Assert.Equal(2, runs.Runs.Count);
    }

    [Fact]
    public async Task Requeue_CompletedRun_FailsWithNotRequeueable()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var queue = new FakeStandardBenchmarkRunQueue();
        var source = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Completed
        };
        runs.Runs[source.Id] = source;

        var handler = RequeueHandler(runs, queue, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.RequeueStandardBenchmarkRunCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotRequeueable", result.Error.Code);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task Requeue_CancelledRun_IsAllowed()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var queue = new FakeStandardBenchmarkRunQueue();
        var source = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Cancelled
        };
        runs.Runs[source.Id] = source;

        var handler = RequeueHandler(runs, queue, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.RequeueStandardBenchmarkRunCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(queue.QueuedRunIds);
    }

    // ----- Priority (WS4, DR-5) -----

    private static StandardBenchmarkFeatures.SetStandardBenchmarkRunPriorityHandler PriorityHandler(
        FakeStandardBenchmarkRunRepository runs,
        FakeCurrentUserContext user) => new(runs, user);

    [Fact]
    public async Task SetPriority_QueuedRun_UpdatesPriority()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Queued
        };
        runs.Runs[run.Id] = run;

        var handler = PriorityHandler(runs, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.SetStandardBenchmarkRunPriorityCommand(run.Id, -1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(-1, run.Priority);
    }

    [Fact]
    public async Task SetPriority_RunningRun_FailsWithNotReprioritizable()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var run = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Running
        };
        runs.Runs[run.Id] = run;

        var handler = PriorityHandler(runs, Admin());
        var result = await handler.Handle(new StandardBenchmarkFeatures.SetStandardBenchmarkRunPriorityCommand(run.Id, -1), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StandardBenchmark.NotReprioritizable", result.Error.Code);
        Assert.Equal(0, run.Priority);
    }

    [Fact]
    public async Task ListQueuedOrdered_ReturnsLowestPriorityFirst()
    {
        // DR-5: lower priority values run first, then age.
        var runs = new FakeStandardBenchmarkRunRepository();
        var older = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Queued,
            Priority = 0,
            QueuedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var highPriority = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "gsm8k",
            Status = BenchmarkRunStatus.Queued,
            Priority = -1,
            QueuedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        runs.Runs[older.Id] = older;
        runs.Runs[highPriority.Id] = highPriority;

        var ordered = await runs.ListQueuedOrderedAsync(CancellationToken.None);

        Assert.Equal([highPriority.Id, older.Id], ordered.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task ListQueuedOrdered_ExcludesCancelledRuns()
    {
        var runs = new FakeStandardBenchmarkRunRepository();
        var cancelled = new StandardBenchmarkRun
        {
            InteractionModelId = Guid.NewGuid(),
            BenchmarkKey = "mmlu",
            Status = BenchmarkRunStatus.Cancelled,
            Priority = -5
        };
        runs.Runs[cancelled.Id] = cancelled;

        var ordered = await runs.ListQueuedOrderedAsync(CancellationToken.None);

        Assert.Empty(ordered);
    }
}
