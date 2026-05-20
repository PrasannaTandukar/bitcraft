using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Modeling;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Modeling;

public class RuntimeEngineRepository(AppDbContext db) : IRuntimeEngineRepository
{
    public async Task<RuntimeEngine> AddAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        db.RuntimeEngines.Add(engine);
        await db.SaveChangesAsync(cancellationToken);
        return engine;
    }

    public Task<RuntimeEngine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RuntimeEngines.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<RuntimeEngine?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        db.RuntimeEngines.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

    public Task<List<RuntimeEngine>> ListAsync(CancellationToken cancellationToken = default) =>
        db.RuntimeEngines
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        db.RuntimeEngines.Update(engine);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        db.RuntimeEngines.Remove(engine);
        await db.SaveChangesAsync(cancellationToken);
    }
}
