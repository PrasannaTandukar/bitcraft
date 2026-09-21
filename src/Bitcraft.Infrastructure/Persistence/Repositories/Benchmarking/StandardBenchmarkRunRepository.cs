using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Benchmarking;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Benchmarking;

public class StandardBenchmarkRunRepository(AppDbContext db) : IStandardBenchmarkRunRepository
{
    public async Task<StandardBenchmarkRun> AddAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<StandardBenchmarkRun>().Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public Task<StandardBenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .AsNoTracking()
            .Include(x => x.InteractionModel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<StandardBenchmarkRun?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .Include(x => x.InteractionModel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<StandardBenchmarkRun>> ListByModelAsync(Guid interactionModelId, CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .AsNoTracking()
            .Where(x => x.InteractionModelId == interactionModelId)
            .Include(x => x.InteractionModel)
            .OrderByDescending(x => x.QueuedUtc)
            .ToListAsync(cancellationToken);

    public Task<List<StandardBenchmarkRun>> ListRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .AsNoTracking()
            .Include(x => x.InteractionModel)
            .OrderByDescending(x => x.QueuedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<List<StandardBenchmarkRun>> ListRunningAsync(CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .Where(x => x.Status == BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .Where(x => x.Status == BenchmarkRunStatus.Queued || x.Status == BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<StandardBenchmarkRun>().Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }
}
