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
    public async Task<EffectiveDocumentSet> GetEffectiveSetAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        var directDocIds = await db.TutorDocuments
            .Where(td => td.TutorId == tutorId)
            .Select(td => td.DocumentId)
            .ToListAsync(cancellationToken);

        var folderIds = await db.TutorDocumentFolders
            .Where(tdf => tdf.TutorId == tutorId)
            .Select(tdf => tdf.DocumentFolderId)
            .ToListAsync(cancellationToken);

        var folderDocIds = folderIds.Count == 0
            ? new List<Guid>()
            : await db.Documents
                .Where(d => d.FolderId != null && folderIds.Contains(d.FolderId.Value))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

        var allDocIds = directDocIds
            .Concat(folderDocIds)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var bindings = allDocIds.Count == 0
            ? new List<EffectiveKnowledgeBinding>()
            : await db.KnowledgeChunks
                .Where(c => allDocIds.Contains(c.DocumentId) && c.EmbeddingModelId != null && c.EmbeddingDimensions != null)
                .Select(c => new { c.DocumentId, EmbeddingModelId = c.EmbeddingModelId!.Value, Dimensions = c.EmbeddingDimensions!.Value })
                .Distinct()
                .Select(x => new EffectiveKnowledgeBinding(x.DocumentId, x.EmbeddingModelId, x.Dimensions))
                .ToListAsync(cancellationToken);

        return new EffectiveDocumentSet(allDocIds, bindings);
    }
}
