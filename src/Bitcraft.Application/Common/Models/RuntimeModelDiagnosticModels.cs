using Bitcraft.Domain.Modeling;

namespace Bitcraft.Application.Common.Models;

public sealed record RuntimeModelCatalogDiagnosticRequest(
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef);

public sealed record RuntimeModelCatalogDiagnostic(IReadOnlyList<string> AvailableModels);

public sealed record RuntimeModelGenerationDiagnosticRequest(
    Guid RuntimeEngineId,
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef,
    Guid ModelId,
    string ProviderModelName,
    ModelCapabilities Capabilities,
    string Prompt,
    IReadOnlyList<LlmImageInput>? Images = null,
    int? MaxOutputTokens = null);

public sealed record RuntimeModelGenerationDiagnostic(string Answer);
