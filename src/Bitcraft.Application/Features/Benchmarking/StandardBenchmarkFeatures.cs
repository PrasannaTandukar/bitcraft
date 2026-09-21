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
        ILlmGenerationProvider? generationProvider = null)
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
            if (generationProvider is null)
            {
                return Task.FromResult(false);
            }

            return ProbeViaProviderAsync(generationProvider, model, cancellationToken);
        }

        private static async Task<bool> ProbeViaProviderAsync(
            ILlmGenerationProvider generationProvider,
            InteractionModel model,
            CancellationToken cancellationToken)
        {
            var engine = model.RuntimeEngine;
            if (engine is null || string.IsNullOrWhiteSpace(model.ProviderModelName))
            {
                return false;
            }

            var probe = await generationProvider.ProbeImageInputSupportAsync(
                engine.Endpoint,
                model.ProviderModelName,
                engine.ApiKey,
                engine.ProviderType.ToString(),
                cancellationToken);
            return probe.IsSuccess && probe.Value;
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

    public sealed record ListStandardBenchmarkRunsQuery(Guid? InteractionModelId, int Limit = 50) : IRequest<Result<List<StandardBenchmarkRun>>>;

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

            var runs = request.InteractionModelId is { } modelId
                ? await runRepository.ListByModelAsync(modelId, cancellationToken)
                : await runRepository.ListRecentAsync(Math.Clamp(request.Limit, 1, 200), cancellationToken);
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
                return Result.Success<string?>(null);
            }

            return await benchmarkEngine.GetStandardLlmBenchmarkLogAsync(
                new StandardBenchmarkHandle(run.EngineRunId),
                cancellationToken);
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
