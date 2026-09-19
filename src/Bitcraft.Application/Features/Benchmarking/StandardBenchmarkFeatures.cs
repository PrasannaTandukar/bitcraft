using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Modeling;
using MediatR;

namespace Bitcraft.Application.Features.Benchmarking;

/// <summary>
/// Model-scoped standardized DeepEval benchmark runs (MMLU, GSM8K, etc.).
/// Deliberately separate from tutor-scoped <see cref="BenchmarkRun"/> scoring to preserve the
/// Model Evaluation vs Tutor Benchmarking separation.
/// </summary>
public static class StandardBenchmarkFeatures
{
    // ----- Create -----

    public sealed record CreateStandardBenchmarkRunCommand(
        Guid InteractionModelId,
        string BenchmarkKey,
        IReadOnlyList<string>? Tasks,
        int NShots,
        int NProblems) : IRequest<Result<StandardBenchmarkRun>>;

    public sealed class CreateStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IStandardBenchmarkRunQueue queue,
        ICurrentUserContext currentUser)
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

            if (request.NShots is < 0 or > 5)
            {
                return Result.Failure<StandardBenchmarkRun>(new Error("StandardBenchmark.NShotsOutOfRange", "Few-shot count must be between 0 and 5."));
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

    // ----- Process (worker) -----

    public sealed record ProcessQueuedStandardBenchmarkRunCommand(Guid RunId) : IRequest<Result<bool>>;

    public sealed class ProcessQueuedStandardBenchmarkRunHandler(
        IStandardBenchmarkRunRepository runRepository,
        IModelRepository modelRepository,
        IBenchmarkEngine benchmarkEngine)
        : IRequestHandler<ProcessQueuedStandardBenchmarkRunCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(ProcessQueuedStandardBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            var run = await runRepository.GetByIdAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure<bool>(new Error("StandardBenchmark.NotFound", $"Standardized benchmark run '{request.RunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            {
                return Result.Success(true);
            }

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
            run.StartedUtc = DateTime.UtcNow;
            await runRepository.UpdateAsync(run, cancellationToken);

            var tasks = ParseTasks(run.TasksJson);
            var engineRequest = new StandardLlmBenchmarkRequest(
                run.BenchmarkKey,
                Provider: ToSidecarProvider(model.RuntimeEngine.ProviderType),
                ModelName: model.ProviderModelName,
                BaseUrl: model.RuntimeEngine.Endpoint,
                ApiKeyEnv: null,
                Tasks: tasks,
                NShots: run.NShots,
                NProblems: run.NProblems,
                AllowCodeExecution: false);

            var evaluated = await benchmarkEngine.EvaluateStandardLlmBenchmarkAsync(engineRequest, cancellationToken);
            if (evaluated.IsFailure)
            {
                return await MarkFailedAsync(run, runRepository, evaluated.Error.Code, evaluated.Error.Description);
            }

            var result = evaluated.Value;
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                return await MarkFailedAsync(run, runRepository, "StandardBenchmark.EngineError", result.Error);
            }

            run.Status = BenchmarkRunStatus.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            run.OverallScore = result.OverallScore;
            run.TaskCount = result.TaskCount;
            run.EngineName = "DeepEval";
            run.EngineVersion = result.EngineVersion;
            run.Notes = result.Notes;
            await runRepository.UpdateAsync(run, cancellationToken);
            return Result.Success(true);
        }

        private static async Task<Result<bool>> MarkFailedAsync(
            StandardBenchmarkRun run,
            IStandardBenchmarkRunRepository runRepository,
            string code,
            string description)
        {
            run.Status = BenchmarkRunStatus.Failed;
            run.CompletedUtc = DateTime.UtcNow;
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
}
