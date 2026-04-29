using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

public interface IModelCatalogRepository
{
    Task<ModelCatalogEntry> AddAsync(ModelCatalogEntry entry, CancellationToken cancellationToken = default);
    Task<ModelCatalogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ModelCatalogEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ModelCatalogEntry entry, CancellationToken cancellationToken = default);
}