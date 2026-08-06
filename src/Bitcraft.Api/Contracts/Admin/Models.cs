namespace Bitcraft.Api.Contracts.Admin;

/// <summary>Admin request to create an <c>InteractionModel</c> served by a runtime engine.</summary>
public sealed record CreateInteractionModelRequest(
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    string? SettingsJson,
    bool IsEnabled);

/// <summary>Admin request to update an <c>InteractionModel</c>.</summary>
public sealed record UpdateInteractionModelRequest(
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    string? SettingsJson,
    bool IsEnabled);

/// <summary>Read view of an <c>InteractionModel</c>. Never exposes raw secrets.</summary>
public sealed record InteractionModelResponse(
    Guid Id,
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    string? SettingsJson,
    bool IsEnabled,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? ModifiedUtc,
    Guid? ModifiedById);

/// <summary>Admin request to create an <c>EmbeddingModel</c>. <c>EmbeddingDimensions</c> is immutable after creation.</summary>
public sealed record CreateEmbeddingModelRequest(
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    int EmbeddingDimensions,
    string? SettingsJson,
    bool IsEnabled);

/// <summary>Admin request to update an <c>EmbeddingModel</c>. <c>EmbeddingDimensions</c> must match the stored value.</summary>
public sealed record UpdateEmbeddingModelRequest(
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    int EmbeddingDimensions,
    string? SettingsJson,
    bool IsEnabled);

/// <summary>Read view of an <c>EmbeddingModel</c>. Never exposes raw secrets.</summary>
public sealed record EmbeddingModelResponse(
    Guid Id,
    string Name,
    Guid RuntimeEngineId,
    string ProviderModelName,
    int EmbeddingDimensions,
    string? SettingsJson,
    bool IsEnabled,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? ModifiedUtc,
    Guid? ModifiedById);
