using Bitcraft.Domain.Benchmarking;

namespace Bitcraft.Application.Common.Interfaces;

public interface IBenchmarkTemplateRepository
{
    Task<BenchmarkTemplate> AddAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default);
    Task<BenchmarkTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BenchmarkTemplate?> GetByOwnerAndKeyAsync(Guid ownerId, string key, CancellationToken cancellationToken = default);
    Task<List<BenchmarkTemplate>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> IsInUseAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task DeleteAsync(BenchmarkTemplate template, CancellationToken cancellationToken = default);
}
