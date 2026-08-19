using Bitcraft.Domain.Modeling;

namespace Bitcraft.Api.Contracts.Admin;

/// <summary>Request to create an admin-managed runtime engine (D11).</summary>
public sealed record CreateRuntimeEngineRequest(
    string Name,
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef,
    bool IsEnabled = true);

/// <summary>Request to update an admin-managed runtime engine.</summary>
public sealed record UpdateRuntimeEngineRequest(
    string Name,
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef,
    bool IsEnabled);

/// <summary>Admin-visible projection of a <see cref="RuntimeEngine"/>. Never exposes the raw API key.</summary>
public sealed record RuntimeEngineResponse(
    Guid Id,
    string Name,
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    bool HasApiKeySecret,
    bool IsEnabled,
    DateTime CreatedUtc,
    Guid? CreatedById,
    DateTime? ModifiedUtc,
    Guid? ModifiedById);

/// <summary>Request to test connectivity to a runtime engine.</summary>
public sealed record TestRuntimeEngineRequest(
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef);

/// <summary>Result of testing connectivity to a runtime engine or model.</summary>
public sealed record TestConnectionResponse(
    bool IsSuccess,
    double LatencyMs,
    string Message,
    IReadOnlyList<string>? AvailableModels = null);
