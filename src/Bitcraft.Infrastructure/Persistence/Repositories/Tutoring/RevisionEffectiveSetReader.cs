using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;

/// <summary>
/// D25 step 7 (plan.erm.md §7.4) — resolves the effective document set + knowledge binding
/// for a tutor at activation time.
/// </summary>
public class RevisionEffectiveSetReader(AppDbContext db) : IRevisionEffectiveSetReader
{
    public async Task<EffectiveDocumentSet> GetEffectiveSetAsync(
        Guid tutorId,
        CancellationToken cancellationToken = default,
        Guid? queryEmbeddingModelId = null)
    {
        var directDocIds = await db.TutorDocuments
            .Where(td => td.TutorId == tutorId)
            .Select(td => td.DocumentId)
            .ToListAsync(cancellationToken);

        // Compatibility read only: new writes use TutorDocument, while pre-D25 links remain
        // effective until operators intentionally migrate or detach them.
        var legacyDocIds = await db.TutorKnowledgeSources
            .Where(link => link.TutorId == tutorId)
            .Select(link => link.DocumentId)
            .ToListAsync(cancellationToken);

        var folderIds = await db.TutorDocumentFolders
            .Where(tdf => tdf.TutorId == tutorId)
            .Select(tdf => tdf.DocumentFolderId)
            .ToListAsync(cancellationToken);

        var effectiveFolderIds = new HashSet<Guid>(folderIds);
        if (effectiveFolderIds.Count > 0)
        {
            // Folder attachment is subtree-aware. The hierarchy is materialized at activation,
            // so later folder moves do not mutate an already-active snapshot.
            var folderTree = await db.DocumentFolders
                .Select(folder => new { folder.Id, folder.ParentFolderId })
                .ToListAsync(cancellationToken);
            var addedDescendant = true;
            while (addedDescendant)
            {
                addedDescendant = false;
                foreach (var folder in folderTree.Where(folder =>
                             folder.ParentFolderId.HasValue &&
                             effectiveFolderIds.Contains(folder.ParentFolderId.Value)))
                {
                    addedDescendant |= effectiveFolderIds.Add(folder.Id);
                }
            }
        }

        var folderDocIds = effectiveFolderIds.Count == 0
            ? new List<Guid>()
            : await db.Documents
                .Where(document => document.FolderId != null && effectiveFolderIds.Contains(document.FolderId.Value))
                .Select(document => document.Id)
                .ToListAsync(cancellationToken);

        var allDocIds = directDocIds
            .Concat(legacyDocIds)
            .Concat(folderDocIds)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var bindings = allDocIds.Count == 0
            ? new List<EffectiveKnowledgeBinding>()
            : await db.KnowledgeChunks
                .Where(c => allDocIds.Contains(c.DocumentId) &&
                            c.EmbeddingModelId.HasValue &&
                            c.EmbeddingDimensions.HasValue &&
                            c.EmbeddingModelId.Value != Guid.Empty)
                .Select(c => new
                {
                    c.DocumentId,
                    EmbeddingModelId = c.EmbeddingModelId!.Value,
                    Dimensions = c.EmbeddingDimensions!.Value,
                    ProviderModelName = c.EmbeddingModelEntity == null ? null : c.EmbeddingModelEntity.ProviderModelName,
                    RuntimeEngineId = c.EmbeddingModelEntity == null ? null : (Guid?)c.EmbeddingModelEntity.RuntimeEngineId,
                    RuntimeEngineName = c.EmbeddingModelEntity == null || c.EmbeddingModelEntity.RuntimeEngine == null
                        ? null
                        : c.EmbeddingModelEntity.RuntimeEngine.Name
                })
                .Distinct()
                .Select(x => new EffectiveKnowledgeBinding(
                    x.DocumentId,
                    x.EmbeddingModelId,
                    x.Dimensions,
                    x.ProviderModelName,
                    x.RuntimeEngineId,
                    x.RuntimeEngineName))
                .ToListAsync(cancellationToken);

        return new EffectiveDocumentSet(allDocIds, bindings);
    }
}
