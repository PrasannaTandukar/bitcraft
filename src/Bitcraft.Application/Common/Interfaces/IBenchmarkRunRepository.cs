using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.Application.Common.Interfaces;

public interface IBenchmarkRunRepository
{
    Task<BenchmarkRun> AddAsync(BenchmarkRun run, CancellationToken cancellationToken = default);
    Task<BenchmarkRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BenchmarkRun?> GetByIdWithCasesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<BenchmarkRun>> ListByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<List<Guid>> ListRecoverableRunIdsAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default);
}
