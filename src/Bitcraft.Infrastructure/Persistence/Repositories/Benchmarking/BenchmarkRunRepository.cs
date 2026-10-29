using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Benchmarking;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Benchmarking;

public class BenchmarkRunRepository(AppDbContext db) : IBenchmarkRunRepository
{
    public async Task<BenchmarkRun> AddAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<BenchmarkRun>().Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public Task<BenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkRun>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BenchmarkRun?> GetByIdWithCasesAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkRun>()
            .Include(x => x.BenchmarkTemplate)
            .Include(x => x.TutorRevision)
            .Include(x => x.TutorConfigurationSnapshot)
            .Include(x => x.InteractionModel)
            .Include(x => x.Cases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<BenchmarkRun>> ListByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkRun>()
            .Where(x => x.TutorId == tutorId)
            .Include(x => x.BenchmarkTemplate)
            .Include(x => x.TutorRevision)
            .Include(x => x.InteractionModel)
            .OrderByDescending(x => x.QueuedUtc)
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default) =>
        db.Set<BenchmarkRun>()
            .Where(x => x.Status == BenchmarkRunStatus.Queued || x.Status == BenchmarkRunStatus.Running)
            .OrderBy(x => x.QueuedUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        db.Set<BenchmarkRun>().Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }
}
