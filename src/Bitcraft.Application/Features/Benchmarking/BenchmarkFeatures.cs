using System.Text.Json;
using System.Text.Json.Nodes;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Modeling;
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
    private const string SafeGenerationFailureDescription = "Tutor output generation failed.";
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
        IReadOnlyList<BenchmarkCaseInput> Cases,
        Guid? TutorRevisionId = null,
        bool GenerateActualOutputs = false,
        bool UseTutorKnowledge = true,
        int? TopK = null) : IRequest<Result<BenchmarkRun>>;

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

            if (request.ConsistencyRuns is < 1 or > 10)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.ConsistencyRunsInvalid", "Consistency runs must be between 1 and 10."));
            }

            if (!request.GenerateActualOutputs && request.ConsistencyRuns != 1)
            {
                return Result.Failure<BenchmarkRun>(new Error(
                    "Benchmark.ManualConsistencyRunsInvalid",
                    "Existing-output evaluation requires consistency runs to be 1. Submit separate cases for separate outputs."));
            }

            var expandedCaseCount = request.GenerateActualOutputs
                ? (long)request.Cases.Count * request.ConsistencyRuns
                : request.Cases.Count;
            if (expandedCaseCount > 200)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CasesTooMany", "A benchmark run supports up to 200 persisted cases after consistency expansion."));
            }

            if (request.GenerateActualOutputs && request.Cases.Any(x => string.IsNullOrWhiteSpace(x.InputText)))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseInputRequired", "Each automatically generated case requires non-empty input text."));
            }

            if (!request.GenerateActualOutputs && request.Cases.Any(x => string.IsNullOrWhiteSpace(x.ActualOutput)))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseInvalid", "Each existing-output case requires non-empty actual output."));
            }

            if (request.Cases.Any(ExceedsCaseSizeLimit))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseTooLarge", "Benchmark case text exceeds the supported size limit."));
            }

            if (request.Cases.Any(x => !IsValidOptionalJson(x.MetadataJson)))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.CaseMetadataInvalid", "Case metadata JSON is invalid."));
            }

            var topK = request.TopK ?? 5;
            if (topK is < 1 or > 10)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.TopKInvalid", "Retrieval Top K must be between 1 and 10."));
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

            TutorRevision? selectedRevision = null;
            if (request.TutorRevisionId.HasValue)
            {
                selectedRevision = await tutors.GetRevisionByIdAsync(request.TutorRevisionId.Value, cancellationToken);
                if (selectedRevision is null || selectedRevision.TutorId != tutor.Id)
                {
                    return Result.Failure<BenchmarkRun>(new Error("Benchmark.RevisionNotFound", "Revision was not found for the selected tutor."));
                }
            }
            else if (request.GenerateActualOutputs)
            {
                selectedRevision = await tutors.GetActiveRevisionAsync(tutor.Id, cancellationToken);
            }

            if (request.GenerateActualOutputs && selectedRevision is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.RevisionRequired", "The tutor has no active revision available for automatic benchmark generation."));
            }

            if (selectedRevision is not null &&
                (selectedRevision.PromptVersionId is null || selectedRevision.InteractionModelId is null))
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.RevisionIncomplete", "The selected revision requires both a prompt and an interaction model."));
            }

            if (selectedRevision?.Status == RevisionStatus.Archived)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.RevisionArchived", "Archived revisions cannot be used to create benchmark runs."));
            }

            if (request.GenerateActualOutputs &&
                selectedRevision?.Status is not RevisionStatus.Published and not RevisionStatus.Active)
            {
                return Result.Failure<BenchmarkRun>(new Error(
                    "Benchmark.RevisionNotPublished",
                    "Automatic benchmarks require an Active or Published tutor revision."));
            }

            var selectedSnapshot = request.TutorConfigurationSnapshotId.HasValue
                ? tutor.Snapshots.FirstOrDefault(x => x.Id == request.TutorConfigurationSnapshotId.Value)
                : selectedRevision is null
                    ? null
                    : tutor.Snapshots
                        .Where(x => x.TutorRevisionId == selectedRevision.Id)
                        .OrderByDescending(x => x.CapturedUtc)
                        .FirstOrDefault();
            if (request.TutorConfigurationSnapshotId.HasValue && selectedSnapshot is null)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.SnapshotNotFound", "Snapshot was not found for the selected tutor."));
            }


            if (request.GenerateActualOutputs && selectedRevision is not null && selectedSnapshot is null)
            {
                return Result.Failure<BenchmarkRun>(new Error(
                    "TutorRevision.NoSnapshot",
                    "The selected revision has no activation snapshot. Activate the revision before benchmarking it."));
            }

            if (selectedRevision is not null && selectedSnapshot is not null && selectedSnapshot.TutorRevisionId != selectedRevision.Id)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.SnapshotRevisionMismatch", "Selected snapshot does not belong to the selected revision."));
            }

            if (request.InteractionModelId.HasValue &&
                selectedRevision?.InteractionModelId.HasValue == true &&
                request.InteractionModelId.Value != selectedRevision.InteractionModelId.Value)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.InteractionModelMismatch", "Selected interaction model does not match the selected revision."));
            }

            if (request.InteractionModelId.HasValue &&
                selectedRevision is null &&
                selectedSnapshot?.InteractionModelId.HasValue == true &&
                request.InteractionModelId.Value != selectedSnapshot.InteractionModelId.Value)
            {
                return Result.Failure<BenchmarkRun>(new Error("Benchmark.InteractionModelMismatch", "Selected interaction model does not match the selected snapshot."));
            }

            var interactionModelId = selectedRevision?.InteractionModelId ?? request.InteractionModelId ?? selectedSnapshot?.InteractionModelId;
            InteractionModel? interactionModel = null;
            if (interactionModelId.HasValue)
            {
                interactionModel = await modelRepository.GetInteractionModelByIdAsync(interactionModelId.Value, cancellationToken);
                if (interactionModel is null)
                {
                    return Result.Failure<BenchmarkRun>(new Error("InteractionModel.NotFound", $"Interaction model '{interactionModelId.Value}' not found."));
                }

                if (!interactionModel.IsEnabled)
                {
                    return Result.Failure<BenchmarkRun>(new Error("InteractionModel.Disabled", "Selected interaction model is disabled."));
                }
            }

            var persistedCases = request.GenerateActualOutputs
                ? request.Cases.SelectMany((value, logicalCaseIndex) =>
                    Enumerable.Range(0, request.ConsistencyRuns).Select(consistencyRunIndex => new BenchmarkRunCase
                    {
                        InputText = value.InputText!.Trim(),
                        ActualOutput = string.Empty,
                        ExpectedOutput = string.IsNullOrWhiteSpace(value.ExpectedOutput) ? null : value.ExpectedOutput.Trim(),
                        ContextText = string.IsNullOrWhiteSpace(value.ContextText) ? null : value.ContextText.Trim(),
                        MetadataJson = MergeCaseMetadata(value.MetadataJson, logicalCaseIndex, consistencyRunIndex)
                    }))
                    .Select((value, caseIndex) =>
                    {
                        value.CaseIndex = caseIndex;
                        return value;
                    })
                    .ToList()
                : request.Cases.Select((value, index) => new BenchmarkRunCase
                {
                    CaseIndex = index,
                    InputText = string.IsNullOrWhiteSpace(value.InputText) ? string.Empty : value.InputText.Trim(),
                    ActualOutput = value.ActualOutput!.Trim(),
                    ExpectedOutput = string.IsNullOrWhiteSpace(value.ExpectedOutput) ? null : value.ExpectedOutput.Trim(),
                    ContextText = string.IsNullOrWhiteSpace(value.ContextText) ? null : value.ContextText.Trim(),
                    MetadataJson = string.IsNullOrWhiteSpace(value.MetadataJson) ? null : value.MetadataJson
                }).ToList();

            var run = new BenchmarkRun
            {
                TutorId = request.TutorId,
                TutorRevisionId = selectedRevision?.Id,
                TutorRevision = selectedRevision,
                TutorConfigurationSnapshotId = selectedSnapshot?.Id,
                TutorConfigurationSnapshot = selectedSnapshot,
                BenchmarkTemplateId = request.BenchmarkTemplateId,
                BenchmarkTemplate = template,
                InteractionModelId = interactionModelId,
                InteractionModel = interactionModel,
                ModelAlias = request.GenerateActualOutputs
                    ? selectedSnapshot?.ModelName
                    : string.IsNullOrWhiteSpace(request.ModelAlias) ? null : request.ModelAlias.Trim(),
                GenerateActualOutputs = request.GenerateActualOutputs,
                // Automatic generation always executes the tutor's activated knowledge
                // configuration. The request flag remains for manual-run contract compatibility.
                UseTutorKnowledge = request.GenerateActualOutputs || request.UseTutorKnowledge,
                RetrievalTopK = topK,
                ConsistencyRuns = request.ConsistencyRuns,
                Status = BenchmarkRunStatus.Queued,
                QueuedUtc = DateTime.UtcNow,
                TotalCaseCount = persistedCases.Count,
                CompletedCaseCount = 0,
                ProgressPercent = 0,
                ProgressStage = "Queued",
                ProgressMessage = "Benchmark run is queued.",
                LastProgressUtc = DateTime.UtcNow,
                Cases = persistedCases
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
        IBenchmarkEngine engine,
        ITutorBenchmarkOutputGenerator? outputGenerator = null)
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
            run.StartedUtc ??= DateTime.UtcNow;

            if (run.GenerateActualOutputs)
            {
                var generationResult = await GenerateMissingOutputsAsync(run, cancellationToken);
                if (generationResult.IsFailure)
                {
                    return generationResult;
                }
            }

            if (run.Cases.Any(x => string.IsNullOrWhiteSpace(x.ActualOutput)))
            {
                return await FailRunAsync(
                    run,
                    new Error("Benchmark.ActualOutputMissing", "Benchmark evaluation cannot start because a case has no generated or supplied answer."),
                    cancellationToken);
            }

            var evaluationStartPercent = run.GenerateActualOutputs ? 70d : 10d;
            run.SetProgress(
                run.GenerateActualOutputs ? run.Cases.Count : 0,
                run.Cases.Count,
                evaluationStartPercent,
                "Evaluating",
                run.GenerateActualOutputs
                    ? "Generated tutor outputs are being evaluated."
                    : "Existing tutor outputs are being evaluated.");
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
                return await FailRunAsync(run, failure, cancellationToken);
            }

            var value = engineResult.Value;
            if (!TryBuildCaseResultMap(run.Cases, value.Cases, out var byCase, out var caseResultError))
            {
                return await FailRunAsync(run, caseResultError, cancellationToken);
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
            run.SetProgress(run.Cases.Count, run.Cases.Count, 100, "Completed", "Benchmark evaluation completed.");

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

        private async Task<Result> GenerateMissingOutputsAsync(BenchmarkRun run, CancellationToken cancellationToken)
        {
            if (!run.TutorRevisionId.HasValue || outputGenerator is null)
            {
                return await FailRunAsync(
                    run,
                    new Error("Benchmark.GenerationConfigurationMissing", "Automatic benchmark generation is missing its tutor revision configuration."),
                    cancellationToken);
            }

            var orderedCases = run.Cases.OrderBy(x => x.CaseIndex).ToList();
            var generatedCount = orderedCases.Count(x => !string.IsNullOrWhiteSpace(x.ActualOutput));
            if (generatedCount < orderedCases.Count)
            {
                run.SetProgress(
                    generatedCount,
                    orderedCases.Count,
                    CalculateGenerationProgress(generatedCount, orderedCases.Count),
                    "Generating",
                    $"Generating tutor output {generatedCount + 1} of {orderedCases.Count}.");
                await runs.UpdateAsync(run, cancellationToken);
            }

            foreach (var runCase in orderedCases.Where(x => string.IsNullOrWhiteSpace(x.ActualOutput)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var generated = await outputGenerator.GenerateAsync(
                    new TutorBenchmarkGenerationRequest(
                        run.TutorId,
                        run.TutorRevisionId.Value,
                        run.TutorConfigurationSnapshotId,
                        runCase.InputText,
                        runCase.ContextText,
                        run.UseTutorKnowledge,
                        run.RetrievalTopK,
                        run.CreatedById ?? Guid.Empty),
                    cancellationToken);

                if (generated.IsFailure)
                {
                    var failure = new Error(generated.Error.Code, SafeGenerationFailureDescription);
                    return await FailRunAsync(run, failure, cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(generated.Value.ActualOutput))
                {
                    return await FailRunAsync(
                        run,
                        new Error("Benchmark.GeneratedOutputEmpty", SafeGenerationFailureDescription),
                        cancellationToken);
                }

                runCase.ActualOutput = generated.Value.ActualOutput.Trim();
                runCase.ContextText = string.IsNullOrWhiteSpace(generated.Value.ContextText)
                    ? null
                    : generated.Value.ContextText;
                runCase.MetadataJson = MergeGenerationMetadata(
                    runCase.MetadataJson,
                    generated.Value.ModelUsed,
                    generated.Value.GenerationLatencyMs);
                run.ModelAlias ??= generated.Value.ModelUsed;

                generatedCount++;
                run.SetProgress(
                    generatedCount,
                    orderedCases.Count,
                    CalculateGenerationProgress(generatedCount, orderedCases.Count),
                    "Generating",
                    $"Generated tutor output {generatedCount} of {orderedCases.Count}.");
                await runs.UpdateAsync(run, cancellationToken);
            }

            return Result.Success();
        }

        private async Task<Result> FailRunAsync(BenchmarkRun run, Error failure, CancellationToken cancellationToken)
        {
            run.Status = BenchmarkRunStatus.Failed;
            run.CompletedUtc = DateTime.UtcNow;
            run.FailureCode = failure.Code;
            run.FailureDescription = failure.Description;
            run.SetProgress(run.CompletedCaseCount, run.TotalCaseCount, run.ProgressPercent, "Failed", failure.Description);
            await runs.UpdateAsync(run, cancellationToken);
            return Result.Failure(failure);
        }

        private static double CalculateGenerationProgress(int completed, int total) =>
            total == 0 ? 5 : Math.Round(5 + 65d * completed / total, 2);
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

    private static string MergeCaseMetadata(string? metadataJson, int logicalCaseIndex, int consistencyRunIndex)
    {
        var metadata = ParseMetadataObject(metadataJson);
        metadata["logicalCaseIndex"] = logicalCaseIndex;
        metadata["consistencyRunIndex"] = consistencyRunIndex;
        return metadata.ToJsonString();
    }

    private static string MergeGenerationMetadata(string? metadataJson, string modelUsed, double? generationLatencyMs)
    {
        var metadata = ParseMetadataObject(metadataJson);
        metadata["generationModel"] = modelUsed;
        if (generationLatencyMs.HasValue && double.IsFinite(generationLatencyMs.Value))
        {
            metadata["generationLatencyMs"] = Math.Round(generationLatencyMs.Value, 3);
        }

        return metadata.ToJsonString();
    }

    private static JsonObject ParseMetadataObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new JsonObject();
        }

        var parsed = JsonNode.Parse(metadataJson);
        if (parsed is JsonObject metadata)
        {
            return metadata;
        }

        return new JsonObject { ["userMetadata"] = parsed };
    }

    private static bool ExceedsCaseSizeLimit(BenchmarkCaseInput input) =>
        input.InputText?.Length > MaxCaseTextLength ||
        input.ActualOutput?.Length > MaxCaseTextLength ||
        input.ExpectedOutput?.Length > MaxCaseTextLength ||
        input.ContextText?.Length > MaxCaseTextLength ||
        input.MetadataJson?.Length > MaxMetadataJsonLength;
}
