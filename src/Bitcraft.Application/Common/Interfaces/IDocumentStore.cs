using Bitcraft.Domain.Documents;

namespace Bitcraft.Application.Common.Interfaces;

public interface IDocumentStore
{
    Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Lightweight names for already-authorized runtime document IDs; does not grant source access.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default);
    Task<List<Document>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task DeleteAsync(Document document, CancellationToken cancellationToken = default);
}
