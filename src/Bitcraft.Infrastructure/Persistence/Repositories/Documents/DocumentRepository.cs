using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Documents;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Documents;

public class DocumentStore(AppDbContext db) : IDocumentStore, IDocumentContentStore
{
    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document;
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0) return new Dictionary<Guid, string>();
        return await db.Documents.AsNoTracking().Where(document => documentIds.Contains(document.Id))
            .Select(document => new { document.Id, document.Name })
            .ToDictionaryAsync(document => document.Id, document => document.Name, cancellationToken);
    }

    public Task<List<Document>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default) =>
        db.Documents
            .Where(x => x.CreatedById == creatorId)
            .OrderByDescending(x => x.ModifiedUtc ?? x.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<List<Document>> ListByFolderAsync(Guid? folderId, Guid creatorId, CancellationToken cancellationToken = default) =>
        db.Documents
            .Where(x => x.CreatedById == creatorId && x.FolderId == folderId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        db.Documents.Update(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateContentAndClearChunksAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational())
        {
            await UpdateContentAndClearChunksCoreAsync(document, cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UpdateContentAndClearChunksCoreAsync(document, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpdateContentAndClearChunksCoreAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        db.Documents.Update(document);
        if (db.Model.FindEntityType(typeof(Bitcraft.Domain.Knowledge.KnowledgeChunk)) is not null)
        {
            var existingChunks = await db.KnowledgeChunks
                .Where(chunk => chunk.DocumentId == document.Id)
                .ToListAsync(cancellationToken);
            db.KnowledgeChunks.RemoveRange(existingChunks);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
