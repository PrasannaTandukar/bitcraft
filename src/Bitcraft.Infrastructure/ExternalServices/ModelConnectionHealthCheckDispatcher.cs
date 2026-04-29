using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Ollama;

namespace Bitcraft.Infrastructure.ExternalServices;

/// <summary>
/// Provider-aware dispatcher. Routes a <see cref="ModelCatalogEntry"/> to the
/// appropriate concrete health probe based on
/// <see cref="ModelCatalogEntry.ProviderType"/>. Unsupported providers return
/// a failure result rather than throwing.
/// </summary>
public class ModelConnectionHealthCheckDispatcher(OllamaHealthCheck ollama) : IModelConnectionHealthCheck
{
    public Task<Result<ModelHealthStatus>> CheckAsync(ModelCatalogEntry catalogEntry, CancellationToken cancellationToken = default) =>
        catalogEntry.ProviderType switch
        {
            ModelProviderType.Ollama => ollama.CheckAsync(catalogEntry, cancellationToken),
            _ => Task.FromResult(Result.Failure<ModelHealthStatus>(new Error(
                "ModelConnection.ProviderUnsupported",
                $"Health checks for provider '{catalogEntry.ProviderType}' are not yet implemented."))),
        };
}
