using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Modeling;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bitcraft.Application.Features.Benchmarking;

/// <summary>
/// Model-scoped standardized DeepEval benchmark runs (MMLU, GSM8K, etc.).
/// Deliberately separate from tutor-scoped <see cref="BenchmarkRun"/> scoring to preserve the
/// Model Evaluation vs Tutor Benchmarking separation.
/// </summary>
public static class StandardBenchmarkFeatures
{
    /// <summary>
    /// Poll-loop tuning for standardized benchmark processing. Bound from
    /// Benchmarking:StandardProcessing in Infrastructure. Defaults keep a 30 s
    /// poll cadence and a 6 h total budget per run.
    /// </summary>
    public sealed class StandardBenchmarkProcessingOptions
    {
        public const string SectionName = "Benchmarking:StandardProcessing";

        [System.ComponentModel.DataAnnotations.Range(5, 600)]
        public int PollIntervalSeconds { get; set; } = 30;

        [System.ComponentModel.DataAnnotations.Range(60, 86_400)]
        public int StandardTimeoutSeconds { get; set; } = 21_600;

        /// <summary>
        /// A run whose LastProgressUtc is older than this is reaped as stalled before a
        /// new run starts. Prevents a run stuck in Running from blocking the queue.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Range(60, 86_400)]
        public int StalledAfterSeconds { get; set; } = 3_600;
    }

    // ----- Create -----

    public sealed record CreateStandardBenchmarkRunCommand(
        Guid InteractionModelId,
        string BenchmarkKey,
        IReadOnlyList<string>? Tasks,
        int NShots,
        int NProblems) : IRequest<Result<StandardBenchmarkRun>>;

    public class CreateStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser,
        IRuntimeModelDiagnostic? modelDiagnostic = null)
        : IRequestHandler<CreateStandardBenchmarkRunCommand, Result<StandardBenchmarkRun>>
    {
        private const int MaxProblems = 10_000;

        public async Task<Result<StandardBenchmarkRun>> Handle(CreateStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.Forbidden", "Only administrators can launch standardized benchmark runs."));
            }

            var suite = StandardBenchmarkSuiteRegistry.Find(request.BenchmarkKey);
            if (suite is null || !suite.Enabled)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error(
                    "StandardBenchmark.UnsupportedSuite",
                    $"Benchmark '{request.BenchmarkKey}' is not an available standardized suite."));
            }

            if (request.NShots < 0 || request.NShots > suite.MaxShots)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error(
                    "StandardBenchmark.NShotsOutOfRange",
                    suite.MaxShots == 0
                        ? $"Benchmark '{suite.Key}' does not support few-shot examples."
                        : $"Few-shot count must be between 0 and {suite.MaxShots} for benchmark '{suite.Key}'."));
            }

            if (request.NProblems is < 0 or > MaxProblems)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.NProblemsOutOfRange", $"Problem cap must be 0 (full dataset) or between 1 and {MaxProblems}."));
            }

            if (request.Tasks is { Count: > 0 } && !suite.SupportsTasks)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.TasksNotSupported", $"Benchmark '{suite.Key}' does not support task filtering."));
            }

            var model = await modelRepository.GetInteractionModelByIdAsync(request.InteractionModelId, cancellationToken);
            if (model is null)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.ModelNotFound", $"Interaction model '{request.InteractionModelId}' not found."));
            }

            if (!model.IsEnabled)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.ModelDisabled", "The interaction model is disabled."));
            }

            if (string.IsNullOrWhiteSpace(model.ProviderModelName))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.ModelNotConfigured", "The interaction model has no provider model name configured."));
            }

            if (model.RuntimeEngine is null || !model.RuntimeEngine.IsEnabled)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.EngineUnavailable", "The model's runtime engine is missing or disabled."));
            }

            if (model.RuntimeEngine.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error(
                    "StandardBenchmark.ProviderNotSupported",
                    "Standardized benchmarks currently support Ollama and OpenAI runtime engines only."));
            }

            var modalityError = await CheckModalitySupportAsync(suite, model, cancellationToken);
            if (modalityError is not null)
            {
                return Result.Failure<StandardBenchmarkRun>(modalityError);
            }

            var run = new StandardBenchmarkRun
            {
                InteractionModelId = model.Id,
                BenchmarkKey = suite.Key,
                TasksJson = request.Tasks is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Tasks) : null,
                NShots = request.NShots,
                NProblems = request.NProblems,
                Status = BenchmarkRunStatus.Queued,
                QueuedUtc = DateTime.UtcNow
            };

            var created = await runRepository.AddAsync(run, cancellationToken);
            await queue.QueueAsync(created.Id, cancellationToken);
            return Result.Success(created);
        }

        /// <summary>
        /// Fails the run before it starts when the suite requires image input and the
        /// model cannot accept it. Returns null when the modality is satisfied.
        /// </summary>
        protected internal virtual async Task<Error?> CheckModalitySupportAsync(
            StandardBenchmarkSuiteRegistry.SuiteInfo suite,
            InteractionModel model,
            CancellationToken cancellationToken)
        {
            if (suite.Modality != BenchmarkInputModality.Image)
            {
                return null;
            }

            var supported = await ProbeImageSupportAsync(model, cancellationToken);
            return supported
                ? null
                : new Error(
                    "StandardBenchmark.ModalityUnsupported",
                    $"The model '{model.Name}' cannot accept image input required by benchmark '{suite.Key}'.");
        }

        /// <summary>
        /// Internal seam so unit tests can stub the probe without a live provider.
        /// </summary>
        internal virtual Task<bool> ProbeImageSupportAsync(InteractionModel model, CancellationToken cancellationToken)
        {
            if (modelDiagnostic is null)
            {
                return Task.FromResult(false);
            }

            return ProbeViaDiagnosticAsync(modelDiagnostic, model, cancellationToken);
        }

        private static async Task<bool> ProbeViaDiagnosticAsync(
            IRuntimeModelDiagnostic modelDiagnostic,
            InteractionModel model,
            CancellationToken cancellationToken)
        {
            var engine = model.RuntimeEngine;
            if (engine is null || string.IsNullOrWhiteSpace(model.ProviderModelName))
            {
                return false;
            }

            var probe = await modelDiagnostic.GenerateAsync(
                new RuntimeModelGenerationDiagnosticRequest(
                    engine.Id,
                    engine.ProviderType,
                    engine.Endpoint,
                    engine.ApiKey,
                    model.Id,
                    model.ProviderModelName,
                    model.Capabilities,
                    "Reply with the single word: ok.",
                    Images: null),
                cancellationToken);
            return probe.IsSuccess;
        }
    }

    // ----- List / Get -----

    public sealed record ListStandardBenchmarkSuitesQuery : IRequest<Result<IReadOnlyList<StandardBenchmarkSuiteRegistry.SuiteInfo>>>;

    public sealed class ListStandardBenchmarkSuitesHandler
        : IRequestHandler<ListStandardBenchmarkSuitesQuery, Result<IReadOnlyList<StandardBenchmarkSuiteRegistry.SuiteInfo>>>
    {
        public Task<Result<IReadOnlyList<StandardBenchmarkSuiteRegistry.SuiteInfo>>> Handle(ListStandardBenchmarkSuitesQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success(StandardBenchmarkSuiteRegistry.Suites));
        }
    }

    public sealed record ListStandardBenchmarkRunsQuery(Guid? InteractionModelId, int Limit = 50, bool IncludeArchived = false) : IRequest<Result<List<StandardBenchmarkRun>>>;

    public sealed class ListStandardBenchmarkRunsHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListStandardBenchmarkRunsQuery, Result<List<StandardBenchmarkRun>>>
    {
        public async Task<Result<List<StandardBenchmarkRun>>> Handle(ListStandardBenchmarkRunsQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                return Result.Failure<List<StandardBenchmarkRun>>(new Error("StandardBenchmark.Forbidden", "Only administrators and auditors can view standardized benchmark runs."));
            }

            if (request.InteractionModelId is { } modelId)
            {
                var modelRuns = await runRepository.ListByModelAsync(modelId, cancellationToken);
                return Result.Success(request.IncludeArchived
                    ? modelRuns
                    : modelRuns.Where(x => !x.IsArchived).ToList());
            }

            var runs = await runRepository.ListRecentAsync(Math.Clamp(request.Limit, 1, 200), request.IncludeArchived, cancellationToken);
            return Result.Success(runs);
        }
    }

    public sealed record GetStandardBenchmarkRunQuery(Guid RunId) : IRequest<Result<StandardBenchmarkRun>>;

    public sealed class GetStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetStandardBenchmarkRunQuery, Result<StandardBenchmarkRun>>
    {
        public async Task<Result<StandardBenchmarkRun>> Handle(GetStandardBenchmarkRunQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.Forbidden", "Only administrators and auditors can view standardized benchmark runs."));
            }

            var run = await runRepository.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            return Result.Success(run);
        }
    }

    /// <summary>Lightweight progress read for polling while a standardized run is queued or executing.</summary>
    public sealed record GetStandardBenchmarkRunProgressQuery(Guid RunId) : IRequest<Result<StandardBenchmarkRun>>;

    public sealed class GetStandardBenchmarkRunProgressHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetStandardBenchmarkRunProgressQuery, Result<StandardBenchmarkRun>>
    {
        public async Task<Result<StandardBenchmarkRun>> Handle(GetStandardBenchmarkRunProgressQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.Forbidden", "Only administrators and auditors can view standardized benchmark runs."));
            }

            var run = await runRepository.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            return Result.Success(run);
        }
    }

    // ----- Log -----

    public sealed record GetStandardBenchmarkRunLogQuery(Guid RunId) : IRequest<Result<string?>>;

    public sealed class GetStandardBenchmarkRunLogHandler(
        IStandardBenchmarkRunRepository runRepository,
        IBenchmarkEngine benchmarkEngine,
        ICurrentUserContext currentUser) : IRequestHandler<GetStandardBenchmarkRunLogQuery, Result<string?>>
    {
        public async Task<Result<string?>> Handle(GetStandardBenchmarkRunLogQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                return Result.Failure<string?>(new Error("StandardBenchmark.Forbidden", "Only administrators and auditors can view standardized benchmark run logs."));
            }

            var run = await runRepository.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<string?>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (string.IsNullOrWhiteSpace(run.EngineRunId))
            {
                // G9 fix: a run that failed before the sidecar spawned a runner
                // has no engine log. Synthesize a diagnostic block from the
                // stored failure fields so the log panel always shows why the
                // run died instead of an empty 404.
                return Result.Success<string?>(SynthesizeStartupLog(run));
            }

            return await benchmarkEngine.GetStandardLlmBenchmarkLogAsync(
                new StandardBenchmarkHandle(run.EngineRunId),
                cancellationToken);
        }

        /// <summary>
        /// Builds a diagnostic log block for a run that never reached the runner
        /// (G9). Mirrors the runner log's plain-text format.
        /// </summary>
        private static string? SynthesizeStartupLog(StandardBenchmarkRun run)
        {
            if (run.Status is not (BenchmarkRunStatus.Failed or BenchmarkRunStatus.Cancelled))
            {
                return null;
            }

            var lines = new List<string>
            {
                "[diagnostics] No runner log exists for this run.",
                $"[diagnostics] The sidecar never started a runner process (no engine run id).",
                $"[diagnostics] Queued at: {run.QueuedUtc:yyyy-MM-dd HH:mm:ss} UTC",
            };
            if (run.StartedUtc is { } started)
            {
                lines.Add($"[diagnostics] Worker pickup at: {started:yyyy-MM-dd HH:mm:ss} UTC");
            }
            if (!string.IsNullOrWhiteSpace(run.FailureCode))
            {
                lines.Add($"[diagnostics] Failure code: {run.FailureCode}");
            }
            if (!string.IsNullOrWhiteSpace(run.FailureDescription))
            {
                lines.Add($"[diagnostics] Failure detail: {run.FailureDescription}");
            }
            if (run.Status == BenchmarkRunStatus.Cancelled)
            {
                lines.Add($"[diagnostics] The run was cancelled before the engine reported a log.");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    // ----- Process (worker) -----

    public sealed record ProcessQueuedStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class ProcessQueuedStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IBenchmarkEngine benchmarkEngine,
        IOptions<StandardBenchmarkProcessingOptions> processingOptions,
        TimeProvider timeProvider) : IRequestHandler<ProcessQueuedStandardBenchmarkRunCommand, Result<bool>>
    {
        /// <summary>
        /// TaskScoresJson is read back by the frontend as { task, score } records, so it is
        /// persisted with camelCase names. The default serializer would emit PascalCase
        /// (Task/Score) and the UI table would render empty columns.
        /// </summary>
        private static readonly System.Text.Json.JsonSerializerOptions TaskScoresJsonOptions = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        public async Task<Result<bool>> Handle(ProcessQueuedStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            {
                return Result.Success(true);
            }

            // WS3: reap any other run stuck in Running with stale progress before starting.
            // A single sidecar slot means only one run can execute; a stalled run would
            // otherwise block this one (and every later one) behind it.
            await ReapStalledRunsAsync(run, cancellationToken);

            var model = await modelRepository.GetInteractionModelByIdAsync(run.InteractionModelId, cancellationToken);
            if (model?.RuntimeEngine is null)
            {
                return await MarkFailedAsync(run, runRepository, "StandardBenchmark.EngineUnavailable", "The model's runtime engine is missing.");
            }

            if (model.RuntimeEngine.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI))
            {
                return await MarkFailedAsync(run, runRepository, "StandardBenchmark.ProviderNotSupported", "Standardized benchmarks support Ollama and OpenAI runtime engines only.");
            }

            run.Status = BenchmarkRunStatus.Running;
            run.StartedUtc = timeProvider.GetUtcNow().UtcDateTime;
            await runRepository.UpdateAsync(run, cancellationToken);

            var tasks = ParseTasks(run.TasksJson);
            var engineRequest = new StandardLlmBenchmarkRequest(
                run.BenchmarkKey,
                Provider: ToSidecarProvider(model.RuntimeEngine.ProviderType),
                ModelName: model.ProviderModelName,
                BaseUrl: model.RuntimeEngine.Endpoint,
                ApiKey: model.RuntimeEngine.ApiKey,
                Tasks: tasks,
                NShots: run.NShots,
                NProblems: run.NProblems,
                AllowCodeExecution: false);

            var started = await benchmarkEngine.StartStandardLlmBenchmarkAsync(engineRequest, cancellationToken);
            if (started.IsFailure)
            {
                // G8 fix: a busy sidecar (409 SidecarBusy) must not fail the run.
                // Leave the run queued and let the worker pick it up again on the
                // next wake-up. Only a run that waited longer than the standard
                // timeout budget fails with StandardBenchmark.QueueTimeout.
                if (string.Equals(started.Error.Code, "StandardBenchmark.SidecarBusy", StringComparison.Ordinal))
                {
                    var waited = timeProvider.GetUtcNow().UtcDateTime - run.QueuedUtc;
                    if (waited <= TimeSpan.FromSeconds(processingOptions.Value.StandardTimeoutSeconds))
                    {
                        run.Status = BenchmarkRunStatus.Queued;
                        run.StartedUtc = null;
                        run.EngineRunId = null;
                        run.ProgressStage = "waiting-for-engine";
                        run.LastProgressUtc = timeProvider.GetUtcNow().UtcDateTime;
                        await runRepository.UpdateAsync(run, cancellationToken);
                        return Result.Success(true);
                    }

                    return await MarkFailedAsync(run, runRepository, "StandardBenchmark.QueueTimeout", $"The run waited longer than {processingOptions.Value.StandardTimeoutSeconds} seconds for the benchmark sidecar to become free.");
                }

                return await MarkFailedAsync(run, runRepository, started.Error.Code, started.Error.Description);
            }

            // Persist the engine run id so a cancel can reach the live runner later.
            run.EngineRunId = started.Value.RunId;
            await runRepository.UpdateAsync(run, cancellationToken);

            return await PollUntilTerminalAsync(run, started.Value, cancellationToken);
        }

        private async Task<Result<bool>> PollUntilTerminalAsync(
            StandardBenchmarkRun run,
            StandardBenchmarkHandle handle,
            CancellationToken cancellationToken)
        {
            var startedAt = timeProvider.GetUtcNow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var statusResult = await benchmarkEngine.GetStandardLlmBenchmarkStatusAsync(handle, cancellationToken);
                if (statusResult.IsFailure)
                {
                    return await MarkFailedAsync(run, runRepository, statusResult.Error.Code, statusResult.Error.Description);
                }

                var status = statusResult.Value;
                run.ProgressStage = status.Stage;
                run.LastProgressUtc = timeProvider.GetUtcNow().UtcDateTime;
                ApplyProgress(run, status.CompletedTasks, status.TotalTasks);
                if (status.State == StandardBenchmarkStatus.StateCompleted)
                {
                    run.Status = BenchmarkRunStatus.Completed;
                    run.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
                    run.OverallScore = status.OverallScore;
                    run.TaskCount = status.TaskCount;
                    run.EngineName = "DeepEval";
                    run.EngineVersion = status.EngineVersion;
                    run.Notes = status.Notes;
                    run.TaskScoresJson = status.TaskScores is { Count: > 0 }
                        ? System.Text.Json.JsonSerializer.Serialize(status.TaskScores, TaskScoresJsonOptions)
                        : null;
                    await runRepository.UpdateAsync(run, cancellationToken);
                    return Result.Success(true);
                }

                if (status.State == StandardBenchmarkStatus.StateFailed)
                {
                    return await MarkFailedAsync(
                        run,
                        runRepository,
                        "StandardBenchmark.EngineError",
                        status.Error ?? "The benchmark runner reported a failure without an error description.");
                }

                // A cancel terminates the runner process group. The engine reports the
                // cancelled state; mark it terminal so the poll loop exits immediately.
                if (status.State == StandardBenchmarkStatus.StateCancelled)
                {
                    return await MarkCancelledAsync(run, runRepository, cancellationToken);
                }

                // Fail fast on an unrecognized state instead of polling for the whole
                // timeout budget. The runner only emits running/completed/failed.
                if (status.State != StandardBenchmarkStatus.StateRunning)
                {
                    return await MarkFailedAsync(
                        run,
                        runRepository,
                        "StandardBenchmark.UnknownState",
                        $"The benchmark runner reported an unrecognized state '{status.State}'.");
                }

                if (timeProvider.GetUtcNow() - startedAt > TimeSpan.FromSeconds(processingOptions.Value.StandardTimeoutSeconds))
                {
                    return await MarkFailedAsync(
                        run,
                        runRepository,
                        "StandardBenchmark.Timeout",
                        $"The benchmark run exceeded the {processingOptions.Value.StandardTimeoutSeconds}-second budget.");
                }

                run.Status = BenchmarkRunStatus.Running;
                await runRepository.UpdateAsync(run, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(processingOptions.Value.PollIntervalSeconds), cancellationToken);
            }
        }

        /// <summary>
        /// Marks any other Running run whose LastProgressUtc is older than
        /// StalledAfterSeconds as Failed with StandardBenchmark.Stalled. The current run is
        /// excluded so it is never reaped by itself.
        /// </summary>
        private async Task ReapStalledRunsAsync(StandardBenchmarkRun currentRun, CancellationToken cancellationToken)
        {
            var threshold = TimeSpan.FromSeconds(processingOptions.Value.StalledAfterSeconds);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var running = await runRepository.ListRunningAsync(cancellationToken);

            foreach (var candidate in running)
            {
                if (candidate.Id == currentRun.Id)
                {
                    continue;
                }

                var lastProgress = candidate.LastProgressUtc ?? candidate.StartedUtc ?? candidate.QueuedUtc;
                if (now - lastProgress > threshold)
                {
                    candidate.Status = BenchmarkRunStatus.Failed;
                    candidate.CompletedUtc = now;
                    candidate.FailureCode = "StandardBenchmark.Stalled";
                    candidate.FailureDescription =
                        $"No progress for over {processingOptions.Value.StalledAfterSeconds} seconds; the run was reaped.";
                    await runRepository.UpdateAsync(candidate, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Applies engine-reported progress counts to the run. ProgressPercent is derived
        /// from completed/total and clamped to 0-100; null counts leave the fields untouched.
        /// </summary>
        private static void ApplyProgress(StandardBenchmarkRun run, int? completedTasks, int? totalTasks)
        {
            if (completedTasks is not null)
            {
                run.CompletedTasks = completedTasks;
            }

            if (totalTasks is not null)
            {
                run.TotalTasks = totalTasks;
            }

            if (run.CompletedTasks is { } completed && run.TotalTasks is { } total and > 0)
            {
                var clamped = Math.Clamp(completed, 0, total);
                run.ProgressPercent = Math.Round((double)clamped / total * 100, 2);
            }
        }

        private async Task<Result<bool>> MarkFailedAsync(
            StandardBenchmarkRun run,
            IStandardBenchmarkRunRepository runRepository,
            string code,
            string description)
        {
            run.Status = BenchmarkRunStatus.Failed;
            run.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            run.FailureCode = code.Length > 200 ? code[..200] : code;
            run.FailureDescription = description.Length > 2000 ? description[..2000] : description;
            await runRepository.UpdateAsync(run);
            return Result.Failure<bool>(new Error(code, description));
        }

        /// <summary>
        /// Maps the engine provider type to the sidecar judge dialect. Only Ollama and OpenAI
        /// are active; both handlers reject other provider types before a run reaches this map.
        /// </summary>
        private static string ToSidecarProvider(RuntimeEngineProviderType providerType) => providerType switch
        {
            RuntimeEngineProviderType.Ollama => "ollama",
            RuntimeEngineProviderType.OpenAI => "openai",
            _ => providerType.ToString(),
        };

        private async Task<Result<bool>> MarkCancelledAsync(
            StandardBenchmarkRun run,
            IStandardBenchmarkRunRepository runRepository,
            CancellationToken cancellationToken)
        {
            run.Status = BenchmarkRunStatus.Cancelled;
            run.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            run.CancelledUtc = run.CompletedUtc;
            run.FailureCode = null;
            run.FailureDescription = "Cancelled by request.";
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }

        private static IReadOnlyList<string>? ParseTasks(string? tasksJson)
        {
            if (string.IsNullOrWhiteSpace(tasksJson))
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(tasksJson);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }

    // ----- Archive (HD-4) -----

    public sealed record ArchiveStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class ArchiveStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser,
        TimeProvider timeProvider) : IRequestHandler<ArchiveStandardBenchmarkRunCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(ArchiveStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can archive standardized benchmark runs."));
            }

            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Queued or BenchmarkRunStatus.Running)
            {
                return Result.Failure<bool>(new Error(
                    "StandardBenchmark.NotArchivable",
                    "Cancel the run before archiving it."));
            }

            run.IsArchived = true;
            run.ArchivedUtc = timeProvider.GetUtcNow().UtcDateTime;
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }
    }

    public sealed record UnarchiveStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class UnarchiveStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser) : IRequestHandler<UnarchiveStandardBenchmarkRunCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(UnarchiveStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can unarchive standardized benchmark runs."));
            }

            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (!run.IsArchived)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotArchived", $"Run '{request.RunId}' is not archived."));
            }

            run.IsArchived = false;
            run.ArchivedUtc = null;
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }
    }

    // ----- Queue pause (HD-8) -----

    /// <summary>
    /// Reads the global queue pause flag. Readable by Auditor and above so the
    /// queue view can show the state.
    /// </summary>
    public sealed record GetBenchmarkQueuePausedQuery : IRequest<Result<bool>>;

    public sealed class GetBenchmarkQueuePausedHandler(
        IStandardBenchmarkRunRepository runRepository) : IRequestHandler<GetBenchmarkQueuePausedQuery, Result<bool>>
    {
        public async Task<Result<bool>> Handle(GetBenchmarkQueuePausedQuery request, CancellationToken cancellationToken)
        {
            var state = await runRepository.GetQueueStateAsync(cancellationToken);
            return Result.Success(state.IsPaused);
        }
    }

    /// <summary>
    /// Pauses or resumes the global queue (HD-8). Admin only. A paused queue
    /// stops picking up new runs; a run already executing finishes.
    /// </summary>
    public sealed record SetBenchmarkQueuePausedCommand(bool IsPaused) : IRequest<Result<bool>>;

    public sealed class SetBenchmarkQueuePausedHandler(
        IStandardBenchmarkRunRepository runRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser,
        TimeProvider timeProvider) : IRequestHandler<SetBenchmarkQueuePausedCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(SetBenchmarkQueuePausedCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can pause or resume the benchmark queue."));
            }

            var state = await runRepository.GetQueueStateAsync(cancellationToken);
            state.IsPaused = request.IsPaused;
            state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await runRepository.UpdateQueueStateAsync(state, cancellationToken);

            // A resume must wake the worker: queued runs wait for a signal.
            if (!request.IsPaused)
            {
                await queue.QueueAsync(Guid.Empty, cancellationToken);
            }
            return Result.Success(request.IsPaused);
        }
    }

    // ----- Engine status and recovery (HD-9) -----

    /// <summary>
    /// Sidecar health and runner slot state for the Admin control surface.
    /// Readable by Auditor and above; the recovery action is separate.
    /// </summary>
    public sealed record GetBenchmarkEngineStatusQuery : IRequest<Result<SidecarEngineStatus>>;

    public sealed class GetBenchmarkEngineStatusHandler(
        IBenchmarkEngine benchmarkEngine) : IRequestHandler<GetBenchmarkEngineStatusQuery, Result<SidecarEngineStatus>>
    {
        public async Task<Result<SidecarEngineStatus>> Handle(GetBenchmarkEngineStatusQuery request, CancellationToken cancellationToken) =>
            await benchmarkEngine.GetSidecarStatusAsync(cancellationToken);
    }

    /// <summary>
    /// Clears the sidecar's persisted runner slot marker (HD-9 recovery).
    /// Admin only: the action unblocks the queue when a dead runner holds the slot.
    /// </summary>
    public sealed record ClearBenchmarkEngineSlotCommand : IRequest<Result<bool>>;

    public sealed class ClearBenchmarkEngineSlotHandler(
        IBenchmarkEngine benchmarkEngine,
        ICurrentUserContext currentUser) : IRequestHandler<ClearBenchmarkEngineSlotCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(ClearBenchmarkEngineSlotCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can clear the benchmark sidecar slot."));
            }

            return await benchmarkEngine.ClearSidecarSlotAsync(cancellationToken);
        }
    }

    // ----- Cancel -----

    public sealed record CancelStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class CancelStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IBenchmarkEngine benchmarkEngine,
        ICurrentUserContext currentUser,
        TimeProvider timeProvider) : IRequestHandler<CancelStandardBenchmarkRunCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(CancelStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can cancel standardized benchmark runs."));
            }

            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed or BenchmarkRunStatus.Cancelled)
            {
                return Result.Failure<bool>(new Error(
                    "StandardBenchmark.NotCancellable",
                    $"Run '{request.RunId}' has already finished with status '{run.Status}'."));
            }

            // Signal the engine first so the runner stops doing work, then mark the
            // row terminal. A queued run has no engine id yet, so the engine call is
            // skipped and the worker skips the row on dequeue.
            if (!string.IsNullOrWhiteSpace(run.EngineRunId))
            {
                var cancelled = await benchmarkEngine.CancelStandardLlmBenchmarkAsync(
                    new StandardBenchmarkHandle(run.EngineRunId),
                    cancellationToken);
                if (cancelled.IsFailure)
                {
                    return Result.Failure<bool>(cancelled.Error);
                }
            }

            run.Status = BenchmarkRunStatus.Cancelled;
            run.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            run.CancelledUtc = run.CompletedUtc;
            run.FailureCode = null;
            run.FailureDescription = "Cancelled by request.";
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }
    }

    // ----- Delete -----

    public sealed record DeleteStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class DeleteStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser) : IRequestHandler<DeleteStandardBenchmarkRunCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can delete standardized benchmark runs."));
            }

            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Running)
            {
                return Result.Failure<bool>(new Error(
                    "StandardBenchmark.NotDeletable",
                    "Cancel the run before deleting it."));
            }

            await runRepository.DeleteAsync(run, cancellationToken);
            return Result.Success(true);
        }
    }

    // ----- Requeue -----

    public sealed record RequeueStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<StandardBenchmarkRun>>;

    public sealed class RequeueStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser) : IRequestHandler<RequeueStandardBenchmarkRunCommand, Result<StandardBenchmarkRun>>
    {
        public async Task<Result<StandardBenchmarkRun>> Handle(RequeueStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.Forbidden", "Only administrators can requeue standardized benchmark runs."));
            }

            var source = await runRepository.GetByIdAsync(request.RunId, cancellationToken);
            if (source is null)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (source.Status is not (BenchmarkRunStatus.Failed or BenchmarkRunStatus.Cancelled))
            {
                return Result.Failure<StandardBenchmarkRun>(new Error(
                    "StandardBenchmark.NotRequeueable",
                    $"Only failed or cancelled runs can be requeued. Run '{request.RunId}' has status '{source.Status}'."));
            }

            // The original row is immutable history. The clone carries the same
            // parameters so the user does not refill the wizard.
            var clone = new StandardBenchmarkRun
            {
                InteractionModelId = source.InteractionModelId,
                BenchmarkKey = source.BenchmarkKey,
                TasksJson = source.TasksJson,
                NShots = source.NShots,
                NProblems = source.NProblems,
                Priority = source.Priority,
                Status = BenchmarkRunStatus.Queued,
                QueuedUtc = DateTime.UtcNow
            };

            var created = await runRepository.AddAsync(clone, cancellationToken);
            await queue.QueueAsync(created.Id, cancellationToken);
            return Result.Success(created);
        }
    }

    // ----- Priority -----

    public sealed record SetStandardBenchmarkRunPriorityCommand(Guid RunId, int Priority) : IRequest<Result<bool>>;

    public sealed class SetStandardBenchmarkRunPriorityHandler(
        IStandardBenchmarkRunRepository runRepository,
        ICurrentUserContext currentUser) : IRequestHandler<SetStandardBenchmarkRunPriorityCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(SetStandardBenchmarkRunPriorityCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.Forbidden", "Only administrators can change standardized benchmark run priority."));
            }

            var run = await runRepository.GetTrackedByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is not BenchmarkRunStatus.Queued)
            {
                return Result.Failure<bool>(new Error(
                    "StandardBenchmark.NotReprioritizable",
                    $"Only queued runs can be reprioritized. Run '{request.RunId}' has status '{run.Status}'."));
            }

            run.Priority = request.Priority;
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }
    }
}
