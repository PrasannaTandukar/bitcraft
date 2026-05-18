using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Knowledge;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
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
        _ => new BadRequestObjectResult(new ApiError(error.Code, error.Description)),
    };

    public static DocumentResponse ToResponse(this Document document) => new(
        document.Id,
        document.Name,
        document.SourcePath,
        document.MimeType,
        document.RawText,
        document.CreatedUtc,
        document.CreatedById,
        document.ModifiedUtc,
        document.ModifiedById);

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
        entry.ModifiedUtc,
        entry.ModifiedById);

    public static TutorResponse ToResponse(this Tutor tutor) => new(
        tutor.Id,
        tutor.OwnerId,
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

    public static ModelHealthResponse ToResponse(this ModelHealthStatus s) => new(
        s.IsReachable, s.ProviderType, s.Endpoint, s.ModelName, s.ModelAvailable, s.LatencyMs, s.Detail);

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
        c.Id, c.TutorId, c.ModelCatalogEntryId, c.OverrideSettingsJson, c.CreatedUtc, c.ModifiedUtc);

    public static TutorConfigurationSnapshotResponse ToResponse(this TutorConfigurationSnapshot s) => new(
        s.Id, s.TutorId, s.TutorName, s.PromptVersionId, s.PromptContent,
        s.TutorModelConnectionId, s.ModelCatalogEntryId, s.ProviderType, s.ModelName, s.ModelSettingsJson,
        s.ToolsJson, s.KnowledgeSourcesJson, s.DeploymentProfileJson, s.CapturedUtc, s.CapturedById);

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
        run.ModelCatalogEntryId,
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
