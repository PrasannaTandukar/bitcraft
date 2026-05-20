using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Documents;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Documents;

public class DocumentFolderStore(AppDbContext db) : IDocumentFolderStore
{
    public async Task<DocumentFolder> AddAsync(DocumentFolder folder, CancellationToken cancellationToken = default)
    {
        db.DocumentFolders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public Task<DocumentFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.DocumentFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<DocumentFolder>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default) =>
        db.DocumentFolders
            .Where(x => x.CreatedById == creatorId)
            .OrderBy(x => x.ParentFolderId)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        db.DocumentFolders.AnyAsync(x => x.ParentFolderId == folderId, cancellationToken);

    public Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        db.Documents.AnyAsync(x => x.FolderId == folderId, cancellationToken);

    public async Task UpdateAsync(DocumentFolder folder, CancellationToken cancellationToken = default)
    {
        db.DocumentFolders.Update(folder);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DocumentFolder folder, CancellationToken cancellationToken = default)
    {
        db.DocumentFolders.Remove(folder);
        await db.SaveChangesAsync(cancellationToken);
    }
}
