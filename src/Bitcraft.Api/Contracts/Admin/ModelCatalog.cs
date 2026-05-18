using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Api.Contracts.Admin;

/// <summary>Request to create an Admin-managed model catalog entry.</summary>
public sealed record CreateModelCatalogEntryRequest(
    string Name,
    ModelProviderType ProviderType,
    string Endpoint,
    string ModelName,
    string? ApiKeySecretRef,
    string? SettingsJson,
    bool IsEnabled = true);

/// <summary>Admin-managed model endpoint and default model settings available to tutor editors.</summary>
public sealed record ModelCatalogEntryResponse(
    Guid Id,
    string Name,
    ModelProviderType ProviderType,
    string Endpoint,
    string ModelName,
    bool HasApiKey,
    string? SettingsJson,
    bool IsEnabled,
    DateTime CreatedUtc,
    Guid? CreatedById, // TODO: Initial user must be seeded, no nulls allowed in these fields.
    DateTime? LastModifiedUtc,
    Guid? LastModifiedById);

/// <summary>Health probe result for a configured model catalog entry.</summary>
public sealed record ModelHealthResponse(
    bool IsReachable,
    string ProviderType,
    string Endpoint,
    string? ModelName,
    bool ModelAvailable,
    long LatencyMs,
    string? Detail);
