using Bitcraft.Domain.Modeling;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Repository for the admin-managed <see cref="RuntimeEngine"/> aggregate (D11).
/// </summary>
public interface IRuntimeEngineRepository
{
    Task<RuntimeEngine> AddAsync(RuntimeEngine engine, CancellationToken cancellationToken = default);

    Task<RuntimeEngine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RuntimeEngine?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<List<RuntimeEngine>> ListAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(RuntimeEngine engine, CancellationToken cancellationToken = default);

    Task DeleteAsync(RuntimeEngine engine, CancellationToken cancellationToken = default);
}
