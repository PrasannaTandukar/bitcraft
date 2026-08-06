using Bitcraft.Domain.Documents;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Per-user document folder tree store (D25 step 5). Ownership and authorization
/// are enforced by the calling Application handler (folders are scoped by
/// <see cref="Bitcraft.Domain.Common.BaseModel.CreatedById"/>).
/// </summary>
public interface IDocumentFolderStore
{
    Task<DocumentFolder> AddAsync(DocumentFolder folder, CancellationToken cancellationToken = default);
    Task<DocumentFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentFolder>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default);
    Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentFolder folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(DocumentFolder folder, CancellationToken cancellationToken = default);
}
