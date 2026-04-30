using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Benchmarking;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Benchmarking;

public class BenchmarkTemplateRepository(AppDbContext db) : IBenchmarkTemplateRepository
{
    public async Task<BenchmarkTemplate> AddAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default)
    {
        db.Set<BenchmarkTemplate>().Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public Task<BenchmarkTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkTemplate>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BenchmarkTemplate?> GetByOwnerAndKeyAsync(Guid ownerId, string key, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkTemplate>()
            .FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Key == key, cancellationToken);

    public Task<List<BenchmarkTemplate>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkTemplate>()
            .Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> IsInUseAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkRun>().AnyAsync(x => x.BenchmarkTemplateId == templateId, cancellationToken);

    public async Task DeleteAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default)
    {
        db.Set<BenchmarkTemplate>().Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }
}
