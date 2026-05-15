using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Api.Contracts.Rag;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Knowledge;
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
        document.CreatedUtc,
        document.CreatedById,
        document.EditedUtc,
        document.EditedById);

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
        tutor.EditedUtc);

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
        d.Id, d.TutorId, d.Target, d.ConfigurationJson, d.CreatedUtc, d.EditedUtc);

    public static TutorModelConnectionResponse ToResponse(this TutorModelConnection c) => new(
        c.Id, c.TutorId, c.ModelCatalogEntryId, c.OverrideSettingsJson, c.CreatedUtc, c.EditedUtc);

    public static TutorConfigurationSnapshotResponse ToResponse(this TutorConfigurationSnapshot s) => new(
        s.Id, s.TutorId, s.TutorName, s.PromptVersionId, s.PromptContent,
        s.TutorModelConnectionId, s.ModelCatalogEntryId, s.ProviderType, s.ModelName, s.ModelSettingsJson,
        s.ToolsJson, s.KnowledgeSourcesJson, s.DeploymentProfileJson, s.CapturedUtc, s.CapturedById);
}
