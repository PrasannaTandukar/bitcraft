using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Documents;
using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Application.Features.Knowledge;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Mapping;

internal static class TutoringMapping
{
    public static ActionResult ToActionResult(Error error) => error.Code switch
    {
        "TutorRuntime.SessionRestoreFailed" or "TutorRuntime.SessionSerializationFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status500InternalServerError },
        "TutorConversation.Conflict" => new ConflictObjectResult(new ApiError(error.Code, error.Description)),
        "TutorRuntime.RateLimited" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status429TooManyRequests },
        "TutorRuntime.Timeout" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status504GatewayTimeout },
        "TutorRuntime.ContextTooLarge" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status413PayloadTooLarge },
        "TutorRuntime.ProviderUnavailable" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status503ServiceUnavailable },
        "TutorRuntime.TutorNotFound" =>
            new NotFoundObjectResult(new ApiError(error.Code, error.Description)),
        var c when c.EndsWith(".NotFound", StringComparison.Ordinal) =>
            new NotFoundObjectResult(new ApiError(error.Code, error.Description)),
        var c when c.EndsWith(".Forbidden", StringComparison.Ordinal) =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status403Forbidden },
        "Ollama.Unavailable" or "Ollama.RequestFailed" or "Rag.EmbeddingFailed" or "Rag.GenerationFailed" or "Tutor.GenerationFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status502BadGateway },
        "InteractionModel.ImageTestFailed" or "TutorRuntime.ImageRejected" or "Ollama.ModelNotFound" or "Ollama.InvalidResponse" or "TutorRuntime.GenerationFailed" or
        "TutorRuntime.EmbeddingUnavailable" or "TutorRuntime.KnowledgeSearchFailed" or
        "TutorRuntime.MemoryEmbeddingUnavailable" or "TutorRuntime.MemorySearchFailed" or
        "TutorRuntime.MemorySaveFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status502BadGateway },
        "TutorRuntime.ImageInputNotSupported" or "TutorRuntime.NoActiveRevision" or "TutorRuntime.InvalidRevision" or
        "TutorRuntime.PromptNotConfigured" or "TutorRuntime.InteractionModelNotConfigured" or
        "TutorRuntime.InteractionModelDisabled" or "TutorRuntime.EmbeddingModelDisabled" or
        "TutorRuntime.SnapshotMissing" or "TutorRuntime.KnowledgeConfigurationInvalid" or
        "TutorRuntime.MemoryConfigurationInvalid" or "TutorRuntime.FunctionCallingNotSupported" or
        "TutorRuntime.ToolRequestRejected" or "TutorRuntime.ToolConfigurationInvalid" or "TutorRuntime.ToolApprovalRequired" or
        "TutorRuntime.ModelConfigurationInvalid" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status409Conflict },
        "Knowledge.EmbeddingServiceUnavailable" or "Knowledge.EmbeddingRequestFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status502BadGateway },
        "ChatAttachment.FileTooLarge" or "Document.FileTooLarge" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status413PayloadTooLarge },
        "ChatAttachment.UnsupportedType" or "Document.UnsupportedFileType" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status415UnsupportedMediaType },
        "ChatAttachment.Expired" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status410Gone },
        "ChatAttachment.StorageUnavailable" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status503ServiceUnavailable },
        "Rag.SearchFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status500InternalServerError },
        _ => new BadRequestObjectResult(new ApiError(error.Code, error.Description)),
    };

    public static DocumentSummaryResponse ToSummaryResponse(this Document document) => new(
        document.Id,
        document.Name,
        document.SourcePath,
        document.MimeType,
        document.FolderId,
        !string.IsNullOrWhiteSpace(document.RawText),
        document.RawText?.Length ?? 0,
        document.CreatedUtc,
        document.CreatedById,
        document.ModifiedUtc,
        document.ModifiedById,
        null);

    public static DocumentSummaryResponse ToSummaryResponse(this DocumentFeatures.DocumentListItem item)
    {
        var document = item.Document;
        var metadata = item.Vectorization;
        return new DocumentSummaryResponse(
            document.Id,
            document.Name,
            document.SourcePath,
            document.MimeType,
            document.FolderId,
            !string.IsNullOrWhiteSpace(document.RawText),
            document.RawText?.Length ?? 0,
            document.CreatedUtc,
            document.CreatedById,
            document.ModifiedUtc,
            document.ModifiedById,
            metadata is null ? null : new DocumentVectorizationMetadataResponse(
                metadata.IsVectorized,
                metadata.IsStale,
                metadata.ChunkCount,
                metadata.LastVectorizedUtc,
                metadata.EmbeddingModelId,
                metadata.EmbeddingModelName,
                metadata.ProviderModelName,
                metadata.EmbeddingProvider,
                metadata.EmbeddingDimensions));
    }

    public static DocumentDetailResponse ToDetailResponse(this Document document) => new(
        document.Id,
        document.Name,
        document.SourcePath,
        document.MimeType,
        document.RawText,
        document.FolderId,
        document.CreatedUtc,
        document.CreatedById,
        document.ModifiedUtc,
        document.ModifiedById);

    public static DocumentFolderResponse ToResponse(this DocumentFolder folder) => new(
        folder.Id,
        folder.Name,
        folder.ParentFolderId,
        folder.CreatedUtc,
        folder.CreatedById,
        folder.ModifiedUtc,
        folder.ModifiedById);

    public static VectorizeDocumentResponse ToResponse(this KnowledgeFeatures.VectorizeDocumentResult result) => new(
        result.DocumentId,
        result.ChunkCount);

    public static DocumentVectorizationStatusResponse ToResponse(this DocumentVectorizationStatus status) => new(
        status.DocumentId,
        status.HasRawText,
        status.RawTextLength,
        status.IsVectorized,
        status.IsStale,
        status.ChunkCount,
        status.LastVectorizedUtc,
        status.EmbeddingModelId,
        status.EmbeddingModelName,
        status.ProviderModelName,
        status.EmbeddingProvider,
        status.EmbeddingDimensions);

    public static KnowledgeChunkResponse ToResponse(this KnowledgeChunk chunk) => new(
        chunk.Id,
        chunk.DocumentId,
        chunk.ChunkIndex,
        chunk.Content,
        chunk.EmbeddingModel,
        chunk.EmbeddingProvider,
        chunk.IngestedUtc);

    public static TutorRuntimeAskResponse ToResponse(this TutorRuntimeResult result) => new(
        result.Answer,
        new TutorRuntimeTutorResponse(result.TutorId, result.TutorName),
        new TutorRuntimeRevisionResponse(result.TutorRevisionId, result.RevisionNumber),
        result.TutorConfigurationSnapshotId,
        result.Model.ToResponse(),
        result.EmbeddingModel?.ToResponse(),
        result.KnowledgeUsed,
        result.Sources.Select(source => new TutorRuntimeSourceResponse(
            source.DocumentId,
            source.DocumentName,
            source.ChunkId,
            source.ChunkIndex,
            source.Score)).ToList(),
        ConversationId: result.ConversationId,
        MemoryUsed: result.MemoryUsed,
        MemorySaved: result.MemorySaved,
        ToolActivities: (result.ToolActivities ?? []).Select(activity => new TutorToolCallActivityResponse(
            activity.ToolName, activity.StartedUtc, activity.CompletedUtc, activity.Status.ToString(),
            activity.DurationMs, activity.ResultSummary, activity.ErrorCategory)).ToList())
    { Provenance = result.Provenance?.ToResponse() };

    public static Bitcraft.Api.Contracts.Tutoring.TutorMessageProvenanceResponse ToResponse(
        this Bitcraft.Application.Common.Models.TutorMessageProvenanceResponse value) => new(
        new(value.Knowledge.RetrievedChunkCount, value.Knowledge.RetrievedChunks.Select(chunk =>
            new Bitcraft.Api.Contracts.Tutoring.TutorRetrievedChunkEvidenceResponse(chunk.DocumentName,
                chunk.ChunkIndex, chunk.RetrievalRank, chunk.Excerpt, chunk.SimilarityScore)
            { DocumentGroupKey = chunk.DocumentGroupKey }).ToList(), value.Knowledge.SimilarityMetric),
        new(value.Memory.ContextProvided, value.Memory.RetrievedItemCount),
        value.Tools.Select(tool => new Bitcraft.Api.Contracts.Tutoring.TutorToolProvenanceResponse(
            tool.ToolName, tool.Status, tool.Succeeded, tool.DurationMs, tool.ErrorCategory)).ToList(),
        new(value.Runtime.TutorId, value.Runtime.TutorRevisionId, value.Runtime.RevisionNumber,
            value.Runtime.SnapshotId, value.Runtime.InteractionModelId, value.Runtime.ProviderModelName, value.Runtime.ProviderType),
        value.CapturedUtc);

    public static TutorRuntimeStreamEventResponse ToResponse(this TutorRuntimeStreamEvent value)
    {
        var type = value.Kind switch
        {
            TutorRuntimeStreamEventKind.ConversationStarted => "conversation.started",
            TutorRuntimeStreamEventKind.MessageDelta => "message.delta",
            TutorRuntimeStreamEventKind.ToolStarted => "tool.started",
            TutorRuntimeStreamEventKind.ToolCompleted => "tool.completed",
            TutorRuntimeStreamEventKind.SourceRetrieved => "source.retrieved",
            TutorRuntimeStreamEventKind.MessageCompleted => "message.completed",
            TutorRuntimeStreamEventKind.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
        return new TutorRuntimeStreamEventResponse(value.Sequence, type, value.ConversationId,
            value.Delta,
            value.Source is null ? null : new TutorRuntimeSourceResponse(value.Source.DocumentId,
                value.Source.DocumentName, value.Source.ChunkId, value.Source.ChunkIndex, value.Source.Score),
            value.ToolActivity is null ? null : new TutorToolCallActivityResponse(value.ToolActivity.ToolName,
                value.ToolActivity.StartedUtc, value.ToolActivity.CompletedUtc,
                value.ToolActivity.Status.ToString(), value.ToolActivity.DurationMs,
                value.ToolActivity.ResultSummary, value.ToolActivity.ErrorCategory),
            value.Kind == TutorRuntimeStreamEventKind.MessageCompleted ? value.Result?.ToResponse() : null,
            value.Error is null ? null : new ApiError(value.Error.Code, value.Error.Description));
    }

    public static TutorRuntimeDescriptorResponse ToResponse(this TutorRuntimeDescriptor descriptor) => new(
        descriptor.TutorId,
        descriptor.TutorName,
        descriptor.Description,
        descriptor.TutorRevisionId,
        descriptor.RevisionNumber,
        descriptor.ActivationSlug,
        descriptor.TutorConfigurationSnapshotId,
        descriptor.Model.ToResponse(),
        descriptor.EmbeddingModel?.ToResponse(),
        descriptor.KnowledgeDocumentCount);

    private static TutorRuntimeModelResponse ToResponse(this TutorRuntimeModelMetadata model) => new(
        model.Id,
        model.Name,
        model.ProviderModelName,
        model.RuntimeEngineName,
        model.SupportsImageInput,
        model.SupportsFunctionCalling,
        model.SupportsStructuredOutput);

    private static TutorRuntimeEmbeddingModelResponse ToResponse(this TutorRuntimeEmbeddingModelMetadata model) => new(
        model.Id,
        model.Name,
        model.ProviderModelName,
        model.Dimensions,
        model.RuntimeEngineName);

    public static InteractionModelResponse ToResponse(this InteractionModel model) => new(
        model.Id,
        model.Name,
        model.RuntimeEngineId,
        model.ProviderModelName,
        model.SettingsJson,
        model.IsEnabled,
        model.CreatedUtc,
        model.CreatedById,
        model.ModifiedUtc,
        model.ModifiedById,
        model.Capabilities.HasFlag(ModelCapabilities.ImageInput),
        model.Capabilities.HasFlag(ModelCapabilities.FunctionCalling),
        model.Capabilities.HasFlag(ModelCapabilities.StructuredOutput));

    public static EmbeddingModelResponse ToResponse(this EmbeddingModel model) => new(
        model.Id,
        model.Name,
        model.RuntimeEngineId,
        model.ProviderModelName,
        model.EmbeddingDimensions,
        model.SettingsJson,
        model.IsEnabled,
        model.CreatedUtc,
        model.CreatedById,
        model.ModifiedUtc,
        model.ModifiedById);

    public static TutorInteractionModelCatalogResponse ToCatalogResponse(this InteractionModel model) => new(
        model.Id,
        model.Name,
        model.ProviderModelName,
        model.RuntimeEngineId,
        model.RuntimeEngine?.Name,
        model.IsEnabled,
        model.RuntimeEngine?.IsEnabled == true,
        model.Capabilities.HasFlag(ModelCapabilities.FunctionCalling));

    public static TutorEmbeddingModelCatalogResponse ToCatalogResponse(this EmbeddingModel model) => new(
        model.Id,
        model.Name,
        model.ProviderModelName,
        model.RuntimeEngineId,
        model.RuntimeEngine?.Name,
        model.IsEnabled,
        model.RuntimeEngine?.IsEnabled == true,
        model.EmbeddingDimensions);

    public static RuntimeEngineResponse ToResponse(this RuntimeEngine engine) => new(
        engine.Id,
        engine.Name,
        engine.ProviderType,
        engine.Endpoint,
        HasApiKey: !string.IsNullOrWhiteSpace(engine.ApiKey),
        engine.IsEnabled,
        engine.CreatedUtc,
        engine.CreatedById,
        engine.ModifiedUtc,
        engine.ModifiedById);

    public static TutorResponse ToResponse(this Tutor tutor) => new(
        tutor.Id,
        tutor.Name,
        tutor.Description,
        tutor.Status,
        tutor.TutorModelConnection?.Id,
        tutor.ActivePromptVersionId,
        tutor.ActiveRevisionId,
        tutor.DeploymentProfile?.Id,
        tutor.CreatedUtc,
        tutor.ModifiedUtc,
        tutor.ActiveRevision?.VersionNumber,
        tutor.ActiveRevision?.Status,
        tutor.ActiveRevision?.PromptVersion?.VersionNumber ?? tutor.ActivePromptVersion?.VersionNumber,
        tutor.ActiveRevision?.PromptVersion?.Name ?? tutor.ActivePromptVersion?.Name);

    public static PromptVersionResponse ToResponse(this PromptVersion v) => new(
        v.Id, v.TutorId, v.VersionNumber, v.Name, v.Content, v.Notes, v.CreatedUtc);

    public static UserRoleMembershipResponse ToResponse(this UserRoleMembership membership) => new(
        membership.UserId,
        membership.Email,
        membership.Roles);

    public static KnowledgeSourceLinkResponse ToResponse(this KnowledgeSourceLink k) => new(
        k.Id, k.TutorId, k.DocumentId, k.LinkedUtc);

    public static TutorKnowledgeDocumentResponse ToResponse(this KnowledgeSourceFeatures.TutorKnowledgeDocumentResult value) => new(
        value.LinkId,
        value.TutorId,
        value.DocumentId,
        value.DocumentName,
        value.AttachedVia.ToString(),
        value.IsVectorized,
        value.EmbeddingModelId,
        value.EmbeddingModelName,
        value.LegacyCompatibility);

    public static TutorKnowledgeFolderResponse ToResponse(this KnowledgeSourceFeatures.TutorKnowledgeFolderResult value) => new(
        value.LinkId,
        value.TutorId,
        value.FolderId,
        value.FolderName);

    public static ToolDefinitionResponse ToResponse(this ToolDefinition t) => new(
        t.Id, t.TutorId, t.Name, t.Description, t.JsonSchema, t.CreatedUtc,
        t.Kind, t.ImplementationKey, t.ExecutionPolicy);

    public static DeploymentProfileResponse ToResponse(this DeploymentProfile d) => new(
        d.Id, d.TutorId, d.Target, d.ConfigurationJson, d.CreatedUtc, d.ModifiedUtc);

    public static TutorModelConnectionResponse ToResponse(this TutorModelConnection c) => new(
        c.Id, c.TutorId, c.InteractionModelId, c.OverrideSettingsJson, c.CreatedUtc, c.ModifiedUtc);

    public static TutorConfigurationSnapshotResponse ToResponse(this TutorConfigurationSnapshot s) => new(
        s.Id, s.TutorId, s.VersionNumber, s.TutorName, s.PromptVersionId, s.PromptContent,
        s.TutorModelConnectionId, s.InteractionModelId, s.ModelName, s.ModelSettingsJson,
        s.ToolsJson, s.KnowledgeSourcesJson, s.DeploymentProfileJson, s.CapturedUtc, s.CapturedById,
        s.TutorRevisionId, s.TutorRevision?.VersionNumber, s.TutorRevision?.PromptVersion?.Name,
        s.TutorRevision?.InteractionModel?.Name,
        s.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput));

    public static TutorRevisionResponse ToResponse(this TutorRevision r) => new(
        r.Id,
        r.TutorId,
        r.VersionNumber,
        r.Status,
        r.PromptVersionId,
        r.PromptVersion?.Name,
        r.PromptVersion?.VersionNumber,
        r.InteractionModelId,
        r.InteractionModel?.Name,
        r.InteractionModel?.ProviderModelName,
        r.InteractionModel?.RuntimeEngine?.Name,
        r.InteractionModel?.IsEnabled,
        r.InteractionModel?.RuntimeEngine?.IsEnabled,
        r.QueryEmbeddingModelId,
        r.QueryEmbeddingModel?.Name,
        r.QueryEmbeddingModel?.ProviderModelName,
        r.QueryEmbeddingModel?.EmbeddingDimensions,
        r.InteractionParametersJson,
        r.DeploymentTarget,
        r.DeploymentConfigJson,
        r.PublishedUtc,
        r.PublishedById,
        r.ActivatedUtc,
        r.ActivatedById,
        r.ArchivedUtc,
        r.ArchivedById,
        r.ActivationSlug,
        r.PreviousActivationSlug,
        r.CreatedUtc,
        r.ModifiedUtc,
        r.DeploymentTarget.ToString(),
        r.Status == RevisionStatus.Draft &&
            r.PromptVersionId.HasValue &&
            r.InteractionModelId.HasValue &&
            r.InteractionModel?.IsEnabled == true &&
            r.InteractionModel.RuntimeEngine?.IsEnabled == true,
        r.Status == RevisionStatus.Active,
        r.Snapshots.OrderByDescending(x => x.CapturedUtc).FirstOrDefault()?.Id,
        r.RevisionTools.Select(link => link.ToolDefinitionId).ToArray());

    public static BenchmarkTemplateResponse ToResponse(this BenchmarkTemplate template) => new(
        template.Id,
        template.OwnerId,
        template.Key,
        template.Name,
        template.Description,
        template.GuidancePath,
        template.MetricsJson,
        template.IsEnabled,
        template.CreatedUtc,
        template.ModifiedUtc);

    public static TutorBenchmarkRunSummaryResponse ToSummaryResponse(this BenchmarkRun run) => new(
        run.Id,
        run.TutorId,
        run.BenchmarkTemplateId,
        run.BenchmarkTemplate?.Name,
        run.TutorRevisionId,
        run.TutorRevision?.VersionNumber,
        run.TutorConfigurationSnapshotId,
        run.InteractionModelId,
        run.ModelAlias,
        run.GenerateActualOutputs,
        run.UseTutorKnowledge,
        run.RetrievalTopK,
        run.ConsistencyRuns,
        run.Status.ToString(),
        run.QueuedUtc,
        run.StartedUtc,
        run.CompletedUtc,
        run.TotalCaseCount,
        run.CompletedCaseCount,
        run.ProgressPercent,
        run.ProgressStage,
        run.ProgressMessage,
        run.LastProgressUtc,
        run.EngineName,
        run.EngineVersion,
        run.AverageScore,
        run.PassRate,
        run.TotalLatencyMs,
        run.TotalTokens,
        run.TokensPerSecond,
        run.EstimatedCo2Grams,
        run.FailureCode,
        run.FailureDescription);

    public static TutorBenchmarkRunProgressResponse ToProgressResponse(this BenchmarkRun run) => new(
        run.Id,
        run.TutorId,
        run.Status.ToString(),
        run.TotalCaseCount,
        run.CompletedCaseCount,
        run.ProgressPercent,
        run.ProgressStage,
        run.ProgressMessage,
        run.QueuedUtc,
        run.StartedUtc,
        run.CompletedUtc,
        run.LastProgressUtc,
        run.FailureCode,
        run.FailureDescription);

    public static TutorBenchmarkRunCaseResponse ToResponse(this BenchmarkRunCase runCase) => new(
        runCase.Id,
        runCase.CaseIndex,
        runCase.InputText,
        runCase.ActualOutput,
        runCase.ExpectedOutput,
        runCase.ContextText,
        runCase.MetadataJson,
        runCase.Passed,
        runCase.Score,
        runCase.Reason,
        runCase.MetricsJson,
        runCase.PerformanceJson,
        runCase.ResourceJson);

    public static TutorBenchmarkRunResponse ToResponse(this BenchmarkRun run) => new(
        run.ToSummaryResponse(),
        run.Cases.OrderBy(x => x.CaseIndex).Select(x => x.ToResponse()).ToList(),
        run.SummaryJson,
        run.SystemMetricsJson,
        run.RawResultJson);
    public static StandardBenchmarkRunResponse ToResponse(this StandardBenchmarkRun run) => new(
        run.Id,
        run.InteractionModelId,
        run.InteractionModel?.Name,
        run.BenchmarkKey,
        run.TasksJson,
        run.NShots,
        run.NProblems,
        run.Status.ToString(),
        run.QueuedUtc,
        run.StartedUtc,
        run.CompletedUtc,
        run.EngineName,
        run.EngineVersion,
        run.OverallScore,
        run.TaskCount,
        run.TaskScoresJson,
        run.Notes,
        run.ProgressStage,
        run.CompletedTasks,
        run.TotalTasks,
        run.ProgressPercent,
        run.LastProgressUtc,
        run.FailureCode,
        run.FailureDescription,
        run.Priority,
        run.CancelledUtc);

    public static StandardBenchmarkSuiteResponse ToResponse(this StandardBenchmarkSuiteRegistry.SuiteInfo suite) => new(
        suite.Key,
        suite.DisplayName,
        suite.Description,
        suite.SupportsTasks,
        suite.SupportsShots,
        suite.SupportsProblemCap,
        suite.MaxShots,
        suite.Enabled,
        suite.Modality.ToString().ToLowerInvariant());

    public static StandardBenchmarkRunProgressResponse ToProgressResponse(this StandardBenchmarkRun run) => new(
        run.Id,
        run.Status.ToString(),
        run.ProgressStage,
        run.OverallScore,
        run.TaskCount,
        run.CompletedTasks,
        run.TotalTasks,
        run.ProgressPercent,
        run.QueuedUtc,
        run.StartedUtc,
        run.CompletedUtc,
        run.LastProgressUtc,
        run.FailureCode,
        run.FailureDescription);
}
