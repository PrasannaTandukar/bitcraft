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

    public Task<List<StandardBenchmarkRun>> ListRecentAsync(int limit, bool includeArchived, CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .AsNoTracking()
            .Include(x => x.InteractionModel)
            .Where(x => includeArchived || !x.IsArchived)
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

    public Task<List<StandardBenchmarkRun>> ListQueuedOrderedAsync(CancellationToken cancellationToken = default) =>
        db.Set<StandardBenchmarkRun>()
            .Where(x => x.Status == BenchmarkRunStatus.Queued)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.QueuedUtc)
            .ToListAsync(cancellationToken);

    public Task DeleteAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<StandardBenchmarkRun>().Remove(run);
        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(StandardBenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<StandardBenchmarkRun>().Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BenchmarkQueueState> GetQueueStateAsync(CancellationToken cancellationToken = default)
    {
        // Singleton row (HD-8): the first row in the table is the queue
        // state. Create it unpaused when absent.
        var state = await db.Set<BenchmarkQueueState>()
            .OrderBy(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is not null)
        {
            return state;
        }

        var created = new BenchmarkQueueState { IsPaused = false };
        db.Set<BenchmarkQueueState>().Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public Task UpdateQueueStateAsync(BenchmarkQueueState state, CancellationToken cancellationToken = default)
    {
        db.Set<BenchmarkQueueState>().Update(state);
        return db.SaveChangesAsync(cancellationToken);
    }
}
