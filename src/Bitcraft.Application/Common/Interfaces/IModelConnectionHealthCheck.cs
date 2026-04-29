using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Health-check abstraction for an Admin-managed LLM model catalog entry.
/// Implementations dispatch by <see cref="ModelCatalogEntry.ProviderType"/>.
/// </summary>
public interface IModelConnectionHealthCheck
{
    Task<Result<ModelHealthStatus>> CheckAsync(ModelCatalogEntry catalogEntry, CancellationToken cancellationToken = default);
}
