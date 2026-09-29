namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Safe read-only interaction-model catalog item for tutor configuration.</summary>
public sealed record TutorInteractionModelCatalogResponse(
    Guid Id,
    string Name,
    string ProviderModelName,
    Guid RuntimeEngineId,
    string? RuntimeEngineName,
    bool IsEnabled,
    bool IsRuntimeEngineEnabled);

/// <summary>Safe read-only embedding-model catalog item for tutor configuration.</summary>
public sealed record TutorEmbeddingModelCatalogResponse(
    Guid Id,
    string Name,
    string ProviderModelName,
    Guid RuntimeEngineId,
    string? RuntimeEngineName,
    bool IsEnabled,
    bool IsRuntimeEngineEnabled,
    int EmbeddingDimensions);
