using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;

public class ModelCatalogRepository(AppDbContext db) : IModelCatalogRepository
{
    public async Task<ModelCatalogEntry> AddAsync(ModelCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        db.ModelCatalogEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public Task<ModelCatalogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ModelCatalogEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<ModelCatalogEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        db.ModelCatalogEntries
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(ModelCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        db.ModelCatalogEntries.Update(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
