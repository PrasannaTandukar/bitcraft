using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Read model for a <see cref="TutorRevision"/> (D25 step 7).</summary>
public sealed record TutorRevisionResponse(
    Guid Id,
    Guid TutorId,
    int VersionNumber,
    RevisionStatus Status,
    Guid? PromptVersionId,
    string? PromptVersionName,
    int? PromptVersionNumber,
    Guid? InteractionModelId,
    string? InteractionModelName,
    string? ProviderModelName,
    string? RuntimeEngineName,
    bool? InteractionModelEnabled,
    bool? RuntimeEngineEnabled,
    Guid? QueryEmbeddingModelId,
    string? QueryEmbeddingModelName,
    string? QueryEmbeddingProviderModelName,
    int? QueryEmbeddingDimensions,
    string? InteractionParametersJson,
    DeploymentTarget DeploymentTarget,
    string? DeploymentConfigJson,
    DateTime? PublishedUtc,
    Guid? PublishedById,
    DateTime? ActivatedUtc,
    Guid? ActivatedById,
    DateTime? ArchivedUtc,
    Guid? ArchivedById,
    string? ActivationSlug,
    string? PreviousActivationSlug,
    DateTime CreatedUtc,
    DateTime? ModifiedUtc,
    string DeploymentTargetDisplay,
    bool IsPublishable,
    bool IsActive,
    Guid? LatestSnapshotId);

/// <summary>Editable Draft revision fields.</summary>
public sealed record UpdateDraftRevisionRequest(
    Guid? PromptVersionId,
    Guid? InteractionModelId,
    Guid? QueryEmbeddingModelId,
    string? InteractionParametersJson,
    DeploymentTarget DeploymentTarget,
    string? DeploymentConfigJson);
