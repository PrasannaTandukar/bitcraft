using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Benchmarking;

public static class BenchmarkFeatures
{
    private const int MaxTemplateKeyLength = 120; // EF BenchmarkTemplate.Key; owner-scoped identity.
    private const int MaxTemplateNameLength = 200; // EF BenchmarkTemplate.Name; editor-facing label.
    private const int MaxTemplateDescriptionLength = 2_000; // EF BenchmarkTemplate.Description; profile summary.
    private const int MaxTemplateGuidancePathLength = 500; // EF BenchmarkTemplate.GuidancePath; guidance artifact reference.
    private const int MaxModelAliasLength = 200; // EF BenchmarkRun.ModelAlias; captured model label.
    private const int MaxCaseTextLength = 20_000; // API/sidecar payload guard for case text fields.
    private const int MaxMetadataJsonLength = 10_000; // API/sidecar payload guard for case metadata JSON.
    private const string SafeSidecarFailureDescription = "Benchmark sidecar evaluation failed.";
    private static readonly HashSet<string> SupportedMetricKeys = new(StringComparer.Ordinal)
    {
        "faithfulness",
        "answer_relevancy",
        "hallucination",
        "correctness"
    };

    public sealed record BenchmarkCaseInput(
        string? InputText,
        string? ActualOutput,
        string? ExpectedOutput,
        string? ContextText,
        string? MetadataJson);

    public sealed record ListBenchmarkTemplatesQuery : IRequest<Result<List<BenchmarkTemplate>>>;

    public sealed class ListBenchmarkTemplatesHandler(
        IBenchmarkTemplateRepository templates,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListBenchmarkTemplatesQuery, Result<List<BenchmarkTemplate>>>
    {
        public async Task<Result<List<BenchmarkTemplate>>> Handle(ListBenchmarkTemplatesQuery request, CancellationToken cancellationToken)
        {
            var list = await templates.ListByOwnerAsync(currentUser.UserId, cancellationToken);
            return Result.Success(list);
        }
    }

    public sealed record CreateBenchmarkTemplateCommand(
        string Key,
        string Name,
        string? Description,
        string? GuidancePath,
        string? MetricsJson,
        bool IsEnabled) : IRequest<Result<BenchmarkTemplate>>;

    public sealed class CreateBenchmarkTemplateHandler(
        IBenchmarkTemplateRepository templates,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateBenchmarkTemplateCommand, Result<BenchmarkTemplate>>
    {
        public async Task<Result<BenchmarkTemplate>> Handle(CreateBenchmarkTemplateCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateKeyRequired", "Template key is required."));
            }

            if (request.Key.Trim().Length > MaxTemplateKeyLength)
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateKeyTooLong", $"Template key cannot exceed {MaxTemplateKeyLength} characters."));
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateNameRequired", "Template name is required."));
            }

            if (request.Name.Trim().Length > MaxTemplateNameLength)
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateNameTooLong", $"Template name cannot exceed {MaxTemplateNameLength} characters."));
            }

            if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > MaxTemplateDescriptionLength)
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateDescriptionTooLong", $"Template description cannot exceed {MaxTemplateDescriptionLength} characters."));
            }

            if (!string.IsNullOrWhiteSpace(request.GuidancePath) && request.GuidancePath.Trim().Length > MaxTemplateGuidancePathLength)
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateGuidancePathTooLong", $"Template guidance path cannot exceed {MaxTemplateGuidancePathLength} characters."));
            }

            if (!TryValidateMetricsJson(request.MetricsJson, out var metricsJson))
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateMetricsInvalid", "Template metrics JSON must be an array or metrics object with supported numeric thresholds between 0 and 1."));
            }

            var ownerId = currentUser.UserId;
            var key = request.Key.Trim();
            var existing = await templates.GetByOwnerAndKeyAsync(ownerId, key, cancellationToken);
            if (existing is not null)
            {
                return Result.Failure<BenchmarkTemplate>(new Error("Benchmark.TemplateKeyAlreadyExists", "A benchmark template with this key already exists."));
            }

            var template = new BenchmarkTemplate
            {
                OwnerId = ownerId,
                Key = key,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                GuidancePath = string.IsNullOrWhiteSpace(request.GuidancePath) ? null : request.GuidancePath.Trim(),
                MetricsJson = metricsJson,
                IsEnabled = request.IsEnabled
            };

            var created = await templates.AddAsync(template, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record DeleteBenchmarkTemplateCommand(Guid TemplateId) : IRequest<Result>;

    public sealed class DeleteBenchmarkTemplateHandler(
        IBenchmarkTemplateRepository templates,
        ICurrentUserContext currentUser)
        : IRequestHandler<DeleteBenchmarkTemplateCommand, Result>
    {
        public async Task<Result> Handle(DeleteBenchmarkTemplateCommand request, CancellationToken cancellationToken)
        {
            var template = await templates.GetByIdAsync(request.TemplateId, cancellationToken);
            if (template is null)
            {
                return Result.Failure(new Error("Benchmark.TemplateNotFound", $"Template '{request.TemplateId}' not found."));
            }

            if (template.OwnerId != currentUser.UserId)
            {
                return Result.Failure(new Error("Benchmark.TemplateForbidden", "Template is not owned by the requesting user."));
            }

            if (await templates.IsInUseAsync(request.TemplateId, cancellationToken))
            {
                return Result.Failure(new Error("Benchmark.TemplateInUse", "Template is currently used by one or more benchmark runs."));
            }

            await templates.DeleteAsync(template, cancellationToken);
            return Result.Success();
        }
    }

    public sealed record CreateTutorBenchmarkRunCommand(
        Guid TutorId,
        Guid BenchmarkTemplateId,
        Guid? TutorConfigurationSnapshotId,
        Guid? InteractionModelId,
        string? ModelAlias,
        int ConsistencyRuns,
        IReadOnlyList<BenchmarkCaseInput> Cases) : IRequest<Result<BenchmarkRun>>;

    public sealed class CreateTutorBenchmarkRunHandler(
        ITutorRepository tutors,
        IBenchmarkTemplateRepository templates,
        IModelRepository modelRepository,
        IBenchmarkRunRepository runs,
        IBenchmarkRunQueue queue,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateTutorBenchmarkRunCommand, Result<BenchmarkRun>>
    {
        public async Task<Result<BenchmarkRun>> Handle(CreateTutorBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            if (request.Cases.Count == 0)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CasesRequired", "At least one benchmark case is required."));
            }

            if (request.Cases.Count > 200)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CasesTooMany", "A benchmark run supports up to 200 cases."));
            }

            var invalidCase = request.Cases.FirstOrDefault(x =>
                string.IsNullOrWhiteSpace(x.ActualOutput));
            if (invalidCase is not null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseInvalid", "Each case requires non-empty actual output."));
            }

            if (request.Cases.Any(ExceedsCaseSizeLimit))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseTooLarge", "Benchmark case text exceeds the supported size limit."));
            }

            if (request.Cases.Any(x => !IsValidOptionalJson(x.MetadataJson)))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseMetadataInvalid", "Case metadata JSON is invalid."));
            }

            if (request.ConsistencyRuns is < 1 or > 10)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.ConsistencyRunsInvalid", "Consistency runs must be between 1 and 10."));
            }

            if (!string.IsNullOrWhiteSpace(request.ModelAlias) && request.ModelAlias.Trim().Length > MaxModelAliasLength)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.ModelAliasTooLong", $"Model alias cannot exceed {MaxModelAliasLength} characters."));
            }

            var tutor = await tutors.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: launching a benchmark run mutates tutor state → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var template = await templates.GetByIdAsync(request.BenchmarkTemplateId, cancellationToken);
            if (template is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.TemplateNotFound", $"Template '{request.BenchmarkTemplateId}' not found."));
            }

            if (template.OwnerId != currentUser.UserId)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.TemplateForbidden", "Template is not owned by the requesting user."));
            }

            if (!template.IsEnabled)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.TemplateDisabled", "Template is disabled."));
            }

            var selectedSnapshot = request.TutorConfigurationSnapshotId.HasValue
                ? tutor.Snapshots.FirstOrDefault(x => x.Id == request.TutorConfigurationSnapshotId.Value)
                : null;
            if (request.TutorConfigurationSnapshotId.HasValue && selectedSnapshot is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.SnapshotNotFound", "Snapshot was not found for the selected tutor."));
            }

            if (request.InteractionModelId.HasValue &&
                selectedSnapshot?.InteractionModelId.HasValue == true &&
                request.InteractionModelId.Value != selectedSnapshot.InteractionModelId.Value)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.InteractionModelMismatch", "Selected interaction model does not match the selected snapshot."));
            }

            var interactionModelId = request.InteractionModelId ?? selectedSnapshot?.InteractionModelId;
            if (interactionModelId.HasValue)
            {
                var interactionModel = await modelRepository.GetInteractionModelByIdAsync(interactionModelId.Value, cancellationToken);
                if (interactionModel is null)
                {
                    return Result.Failure<BenchmarkRun>(new Error("InteractionModel.NotFound", $"Interaction model '{interactionModelId.Value}' not found."));
                }

                if (!interactionModel.IsEnabled)
                {
                    return Result.Failure<BenchmarkRun>(new Error("InteractionModel.Disabled", "Selected interaction model is disabled."));
                }
            }

            var run = new BenchmarkRun
            {
                TutorId = request.TutorId,
                TutorConfigurationSnapshotId = request.TutorConfigurationSnapshotId,
                BenchmarkTemplateId = request.BenchmarkTemplateId,
                InteractionModelId = interactionModelId,
                ModelAlias = string.IsNullOrWhiteSpace(request.ModelAlias) ? null : request.ModelAlias.Trim(),
                ConsistencyRuns = request.ConsistencyRuns,
                Status = BenchmarkRunStatus.Queued,
                QueuedUtc = DateTime.UtcNow,
                TotalCaseCount = request.Cases.Count,
                CompletedCaseCount = 0,
                ProgressPercent = 0,
                ProgressStage = "Queued",
                ProgressMessage = "Benchmark run is queued.",
                LastProgressUtc = DateTime.UtcNow,
                Cases = request.Cases.Select((value, index) => new BenchmarkRunCase
                {
                    CaseIndex = index,
                    InputText = string.IsNullOrWhiteSpace(value.InputText) ? string.Empty : value.InputText.Trim(),
                    ActualOutput = value.ActualOutput!.Trim(),
                    ExpectedOutput = string.IsNullOrWhiteSpace(value.ExpectedOutput) ? null : value.ExpectedOutput.Trim(),
                    ContextText = string.IsNullOrWhiteSpace(value.ContextText) ? null : value.ContextText.Trim(),
                    MetadataJson = string.IsNullOrWhiteSpace(value.MetadataJson) ? null : value.MetadataJson
                }).ToList()
            };

            var created = await runs.AddAsync(run, cancellationToken);
            await queue.QueueAsync(created.Id, CancellationToken.None);
            return Result.Success(created);
        }
    }

    public sealed record ListTutorBenchmarkRunsQuery(Guid TutorId) : IRequest<Result<List<BenchmarkRun>>>;

    public sealed class ListTutorBenchmarkRunsHandler(
        ITutorRepository tutors,
        IBenchmarkRunRepository runs,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListTutorBenchmarkRunsQuery, Result<List<BenchmarkRun>>>
    {
        public async Task<Result<List<BenchmarkRun>>> Handle(ListTutorBenchmarkRunsQuery request, CancellationToken cancellationToken)
        {
            var tutor = await tutors.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<List<BenchmarkRun>>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: read-only listing → any membership role.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<List<BenchmarkRun>>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var list = await runs.ListByTutorAsync(request.TutorId, cancellationToken);
            return Result.Success(list);
        }
    }

    public sealed record GetTutorBenchmarkRunQuery(Guid TutorId, Guid BenchmarkRunId) : IRequest<Result<BenchmarkRun>>;

    public sealed record GetTutorBenchmarkRunProgressQuery(Guid TutorId, Guid BenchmarkRunId) : IRequest<Result<BenchmarkRun>>;

    public sealed class GetTutorBenchmarkRunHandler(
        ITutorRepository tutors,
        IBenchmarkRunRepository runs,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetTutorBenchmarkRunQuery, Result<BenchmarkRun>>
    {
        public async Task<Result<BenchmarkRun>> Handle(GetTutorBenchmarkRunQuery request, CancellationToken cancellationToken)
        {
            var tutor = await tutors.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: read-only detail fetch → any membership role.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var run = await runs.GetByIdWithCasesAsync(request.BenchmarkRunId, cancellationToken);
            if (run is null || run.TutorId != request.TutorId)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.RunNotFound", $"Benchmark run '{request.BenchmarkRunId}' not found."));
            }

            return Result.Success(run);
        }
    }

    public sealed class GetTutorBenchmarkRunProgressHandler(
        ITutorRepository tutors,
        IBenchmarkRunRepository runs,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetTutorBenchmarkRunProgressQuery, Result<BenchmarkRun>>
    {
        public async Task<Result<BenchmarkRun>> Handle(GetTutorBenchmarkRunProgressQuery request, CancellationToken cancellationToken)
        {
            var tutor = await tutors.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: read-only progress fetch → any membership role.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var run = await runs.GetByIdAsync(request.BenchmarkRunId, cancellationToken);
            if (run is null || run.TutorId != request.TutorId)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.RunNotFound", $"Benchmark run '{request.BenchmarkRunId}' not found."));
            }

            return Result.Success(run);
        }
    }

    public sealed record ProcessQueuedBenchmarkRunCommand(Guid BenchmarkRunId) : IRequest<Result>;

    public sealed class ProcessQueuedBenchmarkRunHandler(
        IBenchmarkRunRepository runs,
        IBenchmarkEngine engine)
        : IRequestHandler<ProcessQueuedBenchmarkRunCommand, Result>
    {
        public async Task<Result> Handle(ProcessQueuedBenchmarkRunCommand request, CancellationToken cancellationToken)
        {
            var run = await runs.GetByIdWithCasesAsync(request.BenchmarkRunId, cancellationToken);
            if (run is null)
            {
                return Result.Failure(new Error("Benchmark.RunNotFound", $"Benchmark run '{request.BenchmarkRunId}' not found."));
            }

            if (run.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            {
                return Result.Success();
            }

            run.Status = BenchmarkRunStatus.Running;
            run.StartedUtc = DateTime.UtcNow;
            run.UpdateProgress(0, run.Cases.Count, "Running", "Benchmark evaluation is running.");
            await runs.UpdateAsync(run, cancellationToken);

            var engineRequest = new BenchmarkEngineRequest(
                run.BenchmarkTemplate?.Key ?? "single-turn-llm-eval-v1",
                run.ConsistencyRuns,
                run.Cases.OrderBy(x => x.CaseIndex)
                    .Select(x => new BenchmarkEngineCaseRequest(
                        x.Id,
                        x.CaseIndex,
                        x.InputText,
                        x.ActualOutput,
                        x.ExpectedOutput,
                        x.ContextText,
                        x.MetadataJson))
                    .ToList(),
                run.BenchmarkTemplate?.GuidancePath,
                run.BenchmarkTemplate?.MetricsJson);

            var engineResult = await engine.EvaluateAsync(engineRequest, cancellationToken);
            if (engineResult.IsFailure)
            {
                var failure = GetClientSafeEngineFailure(engineResult.Error);
                run.Status = BenchmarkRunStatus.Failed;
                run.CompletedUtc = DateTime.UtcNow;
                run.FailureCode = failure.Code;
                run.FailureDescription = failure.Description;
                run.UpdateProgress(run.CompletedCaseCount, run.TotalCaseCount, "Failed", failure.Description);
                await runs.UpdateAsync(run, cancellationToken);
                return Result.Failure(failure);
            }

            var value = engineResult.Value;
            if (!TryBuildCaseResultMap(run.Cases, value.Cases, out var byCase, out var caseResultError))
            {
                run.Status = BenchmarkRunStatus.Failed;
                run.CompletedUtc = DateTime.UtcNow;
                run.FailureCode = caseResultError.Code;
                run.FailureDescription = caseResultError.Description;
                run.UpdateProgress(run.CompletedCaseCount, run.TotalCaseCount, "Failed", caseResultError.Description);
                await runs.UpdateAsync(run, cancellationToken);
                return Result.Failure(caseResultError);
            }

            run.Status = BenchmarkRunStatus.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            run.EngineName = value.EngineName;
            run.EngineVersion = value.EngineVersion;
            run.AverageScore = value.Summary.AverageScore;
            run.PassRate = value.Summary.PassRate;
            run.TotalLatencyMs = value.Summary.TotalLatencyMs;
            run.TotalTokens = value.Summary.TotalTokens;
            run.TokensPerSecond = value.Summary.TokensPerSecond;
            run.EstimatedCo2Grams = value.Summary.EstimatedCo2Grams;
            run.SummaryJson = value.Summary.SummaryJson;
            run.SystemMetricsJson = value.Summary.SystemMetricsJson;
            run.RawResultJson = value.RawResultJson;
            run.FailureCode = null;
            run.FailureDescription = null;
            run.UpdateProgress(run.Cases.Count, run.Cases.Count, "Completed", "Benchmark evaluation completed.");

            foreach (var benchmarkRunCase in run.Cases)
            {
                var caseResult = byCase[benchmarkRunCase.Id];
                benchmarkRunCase.Passed = caseResult.Passed;
                benchmarkRunCase.Score = caseResult.Score;
                benchmarkRunCase.Reason = caseResult.Reason;
                benchmarkRunCase.MetricsJson = caseResult.MetricsJson;
                benchmarkRunCase.PerformanceJson = caseResult.PerformanceJson;
                benchmarkRunCase.ResourceJson = caseResult.ResourceJson;
            }

            await runs.UpdateAsync(run, cancellationToken);
            return Result.Success();
        }
    }

    private static bool TryBuildCaseResultMap(
        IEnumerable<BenchmarkRunCase> expectedCases,
        IReadOnlyCollection<BenchmarkEngineCaseResult> actualCases,
        out Dictionary<Guid, BenchmarkEngineCaseResult> byCase,
        out Error error)
    {
        byCase = [];
        error = Error.None;

        var expected = expectedCases.ToDictionary(x => x.Id, x => x.CaseIndex);
        if (actualCases.Count != expected.Count)
        {
            error = BuildEngineCaseResultError();
            return false;
        }

        foreach (var actualCase in actualCases)
        {
            if (!expected.TryGetValue(actualCase.CaseId, out var expectedIndex) || expectedIndex != actualCase.CaseIndex)
            {
                error = BuildEngineCaseResultError();
                return false;
            }

            if (!byCase.TryAdd(actualCase.CaseId, actualCase))
            {
                error = BuildEngineCaseResultError();
                return false;
            }
        }

        return true;
    }

    private static Error BuildEngineCaseResultError() =>
        new("Benchmark.EngineCaseResultsInvalid", "Benchmark engine returned case results that do not match the submitted run cases.");

    private static Error GetClientSafeEngineFailure(Error error) =>
        error.Code == "Benchmark.SidecarException"
            ? new Error(error.Code, SafeSidecarFailureDescription)
            : error;

    private static bool TryValidateMetricsJson(string? json, out string metricsJson)
    {
        metricsJson = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryGetMetricsArray(document.RootElement, out var metricsElement))
            {
                return false;
            }

            foreach (var metricElement in metricsElement.EnumerateArray())
            {
                if (!IsValidMetricElement(metricElement))
                {
                    return false;
                }
            }

            metricsJson = json.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetMetricsArray(JsonElement root, out JsonElement metricsElement)
    {
        metricsElement = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            metricsElement = root;
            return true;
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("metrics", out metricsElement))
        {
            return false;
        }

        return metricsElement.ValueKind == JsonValueKind.Array;
    }

    private static bool IsValidMetricElement(JsonElement metricElement)
    {
        if (metricElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!metricElement.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var key = keyElement.GetString();
        if (string.IsNullOrWhiteSpace(key) || !SupportedMetricKeys.Contains(key))
        {
            return false;
        }

        if (metricElement.TryGetProperty("enabled", out var enabledElement) &&
            enabledElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return false;
        }

        if (!metricElement.TryGetProperty("threshold", out var thresholdElement) ||
            thresholdElement.ValueKind != JsonValueKind.Number ||
            !thresholdElement.TryGetDouble(out var threshold) ||
            !double.IsFinite(threshold))
        {
            return false;
        }

        return threshold is >= 0 and <= 1;
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidOptionalJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        return IsValidJson(json);
    }

    private static bool ExceedsCaseSizeLimit(BenchmarkCaseInput input) =>
        input.InputText?.Length > MaxCaseTextLength ||
        input.ActualOutput?.Length > MaxCaseTextLength ||
        input.ExpectedOutput?.Length > MaxCaseTextLength ||
        input.ContextText?.Length > MaxCaseTextLength ||
        input.MetadataJson?.Length > MaxMetadataJsonLength;
}
