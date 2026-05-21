using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Api.Contracts.Rag;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Knowledge;
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
        var c when c.EndsWith(".NotFound", StringComparison.Ordinal) =>
            new NotFoundObjectResult(new ApiError(error.Code, error.Description)),
        var c when c.EndsWith(".Forbidden", StringComparison.Ordinal) =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status403Forbidden },
        "Ollama.Unavailable" or "Ollama.RequestFailed" or "Rag.EmbeddingFailed" or "Rag.GenerationFailed" or "Tutor.GenerationFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status502BadGateway },
        "Rag.SearchFailed" =>
            new ObjectResult(new ApiError(error.Code, error.Description)) { StatusCode = StatusCodes.Status500InternalServerError },
        _ => new BadRequestObjectResult(new ApiError(error.Code, error.Description)),
    };

    public static DocumentResponse ToResponse(this Document document) => new(
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

    public static KnowledgeChunkResponse ToResponse(this KnowledgeChunk chunk) => new(
        chunk.Id,
        chunk.DocumentId,
        chunk.ChunkIndex,
        chunk.Content,
        chunk.EmbeddingModel,
        chunk.EmbeddingProvider,
        chunk.IngestedUtc);

    public static AskRagQuestionResponse ToResponse(this RagAnswerResult result) => new(
        result.Answer,
        result.Model,
        result.Sources.Select(source => new RagSourceChunkResponse(
            source.DocumentId,
            source.DocumentName,
            source.ChunkId,
            source.ChunkIndex,
            source.Content,
            source.Score)).ToList());

    public static AskTutorQuestionResponse ToResponse(this TutorAnswerResult result) => new(
        result.Answer,
        result.Model,
        result.Mode,
        result.ContextUsed,
        result.MemorySaved,
        result.Sources.Select(source => new TutorSourceResponse(
            source.SourceType,
            source.SourceId,
            source.DocumentId,
            source.DocumentName,
            source.MemoryId,
            source.ChunkId,
            source.ChunkIndex,
            source.Content,
            source.Score)).ToList(),
        result.Message);

    public static ModelCatalogEntryResponse ToResponse(this ModelCatalogEntry entry) => new(
        entry.Id,
        entry.Name,
        entry.ProviderType,
        entry.Endpoint,
        entry.ModelName,
        HasApiKey: !string.IsNullOrWhiteSpace(entry.ApiKeySecretRef),
        entry.SettingsJson,
        entry.IsEnabled,
        entry.CreatedUtc,
        entry.CreatedById,
        entry.EditedUtc,
        entry.EditedById);
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
        model.ModifiedById);

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

    public static RuntimeEngineResponse ToResponse(this RuntimeEngine engine) => new(
        engine.Id,
        engine.Name,
        engine.ProviderType,
        engine.Endpoint,
        HasApiKeySecret: !string.IsNullOrWhiteSpace(engine.ApiKeySecretRef),
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
        tutor.DeploymentProfile?.Id,
        tutor.CreatedUtc,
        tutor.ModifiedUtc);

    public static PromptVersionResponse ToResponse(this PromptVersion v) => new(
        v.Id, v.TutorId, v.VersionNumber, v.Name, v.Content, v.Notes, v.CreatedUtc);

    public static UserRoleMembershipResponse ToResponse(this UserRoleMembership membership) => new(
        membership.UserId,
        membership.Email,
        membership.Roles);

    public static KnowledgeSourceLinkResponse ToResponse(this KnowledgeSourceLink k) => new(
        k.Id, k.TutorId, k.DocumentId, k.LinkedUtc);

    public static ToolDefinitionResponse ToResponse(this ToolDefinition t) => new(
        t.Id, t.TutorId, t.Name, t.Description, t.JsonSchema, t.ExecutionUrl, t.CreatedUtc);

    public static DeploymentProfileResponse ToResponse(this DeploymentProfile d) => new(
        d.Id, d.TutorId, d.Target, d.ConfigurationJson, d.CreatedUtc, d.ModifiedUtc);

    public static TutorModelConnectionResponse ToResponse(this TutorModelConnection c) => new(
        c.Id, c.TutorId, c.InteractionModelId, c.OverrideSettingsJson, c.CreatedUtc, c.ModifiedUtc);

    public static TutorConfigurationSnapshotResponse ToResponse(this TutorConfigurationSnapshot s) => new(
        s.Id, s.TutorId, s.VersionNumber, s.TutorName, s.PromptVersionId, s.PromptContent,
        s.TutorModelConnectionId, s.InteractionModelId, s.ModelName, s.ModelSettingsJson,
        s.ToolsJson, s.KnowledgeSourcesJson, s.DeploymentProfileJson, s.CapturedUtc, s.CapturedById);

    public static TutorRevisionResponse ToResponse(this TutorRevision r) => new(
        r.Id,
        r.TutorId,
        r.VersionNumber,
        r.Status,
        r.PromptVersionId,
        r.InteractionModelId,
        r.QueryEmbeddingModelId,
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
        r.ModifiedUtc);

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
        run.TutorConfigurationSnapshotId,
        run.InteractionModelId,
        run.ModelAlias,
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
}
