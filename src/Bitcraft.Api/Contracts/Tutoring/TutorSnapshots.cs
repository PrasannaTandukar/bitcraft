namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Immutable point-in-time tutor configuration snapshot.</summary>
public sealed record TutorConfigurationSnapshotResponse(
    Guid Id,
    Guid TutorId,
    int VersionNumber,
    string TutorName,
    Guid? PromptVersionId,
    string? PromptContent,
    Guid? TutorModelConnectionId,
    Guid? InteractionModelId,
    string? ModelName,
    string? ModelSettingsJson,
    string? ToolsJson,
    string? KnowledgeSourcesJson,
    string? DeploymentProfileJson,
    DateTime CapturedUtc,
    Guid CapturedById);