using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;

public class TutorRepository(AppDbContext db) : ITutorRepository
{
    public async Task<Tutor> AddAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        db.Set<Tutor>().Add(tutor);
        await db.SaveChangesAsync(cancellationToken);
        return tutor;
    }

    public Task<Tutor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<Tutor>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Tutor?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<Tutor>()
            .Include(x => x.TutorModelConnection)
                .ThenInclude(x => x!.InteractionModel)
            .Include(x => x.ActivePromptVersion)
            .Include(x => x.PromptVersions)
            .Include(x => x.KnowledgeSources)
                .ThenInclude(x => x.Document)
            .Include(x => x.Tools)
            .Include(x => x.DeploymentProfile)
            .Include(x => x.Snapshots)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.PromptVersion)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.InteractionModel)
                    .ThenInclude(x => x!.RuntimeEngine)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.QueryEmbeddingModel)
                    .ThenInclude(x => x!.RuntimeEngine)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.Snapshots)
            .Include(x => x.Revisions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        db.Set<Tutor>()
            // D2/D3: "owner" is now any membership row — caller-visible tutors are those where
            // the user has a TutorMember entry (any ResourceRole).
            .Where(x => x.Members.Any(m => m.UserId == ownerId))
            .Include(x => x.TutorModelConnection)
            .Include(x => x.ActivePromptVersion)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.PromptVersion)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.InteractionModel)
                    .ThenInclude(x => x!.RuntimeEngine)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.QueryEmbeddingModel)
                    .ThenInclude(x => x!.RuntimeEngine)
            .Include(x => x.ActiveRevision)
                .ThenInclude(x => x!.Snapshots)
            .AsSplitQuery()
            .OrderByDescending(x => x.ModifiedUtc ?? x.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        db.Set<Tutor>().Update(tutor);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        db.Set<Tutor>().Remove(tutor);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PromptVersion> AddPromptVersionAsync(PromptVersion promptVersion, CancellationToken cancellationToken = default)
    {
        db.Set<PromptVersion>().Add(promptVersion);
        await db.SaveChangesAsync(cancellationToken);
        return promptVersion;
    }

    public Task<List<PromptVersion>> ListPromptVersionsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<PromptVersion>()
            .Where(x => x.TutorId == tutorId)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

    public Task<PromptVersion?> GetPromptVersionByIdAsync(Guid promptVersionId, CancellationToken cancellationToken = default) =>
        db.Set<PromptVersion>()
            .FirstOrDefaultAsync(x => x.Id == promptVersionId, cancellationToken);

    public async Task<int> GetNextPromptVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        var max = await db.Set<PromptVersion>()
            .Where(x => x.TutorId == tutorId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    public async Task<KnowledgeSourceLink> AddKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken cancellationToken = default)
    {
        db.Set<KnowledgeSourceLink>().Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return link;
    }

    public Task<bool> KnowledgeSourceLinkExistsAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default) =>
        db.Set<KnowledgeSourceLink>()
            .AnyAsync(x => x.TutorId == tutorId && x.DocumentId == documentId, cancellationToken);

    public Task<List<KnowledgeSourceLink>> ListKnowledgeSourceLinksAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.TutorKnowledgeSources
            .Where(link => link.TutorId == tutorId)
            .Include(link => link.Document)
            .OrderBy(link => link.Document!.Name)
            .ToListAsync(cancellationToken);

    public Task<KnowledgeSourceLink?> GetKnowledgeSourceLinkAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default) =>
        db.TutorKnowledgeSources
            .Include(link => link.Document)
            .FirstOrDefaultAsync(link => link.TutorId == tutorId && link.DocumentId == documentId, cancellationToken);

    public async Task RemoveKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken cancellationToken = default)
    {
        db.TutorKnowledgeSources.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TutorDocument> AddTutorDocumentAsync(TutorDocument link, CancellationToken cancellationToken = default)
    {
        db.TutorDocuments.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return link;
    }

    public Task<List<TutorDocument>> ListTutorDocumentsAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.TutorDocuments
            .Where(link => link.TutorId == tutorId)
            .Include(link => link.Document)
            .OrderBy(link => link.Document!.Name)
            .ToListAsync(cancellationToken);

    public Task<TutorDocument?> GetTutorDocumentAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default) =>
        db.TutorDocuments
            .Include(link => link.Document)
            .FirstOrDefaultAsync(link => link.TutorId == tutorId && link.DocumentId == documentId, cancellationToken);

    public async Task RemoveTutorDocumentAsync(TutorDocument link, CancellationToken cancellationToken = default)
    {
        db.TutorDocuments.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TutorDocumentFolder> AddTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken cancellationToken = default)
    {
        db.TutorDocumentFolders.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return link;
    }

    public Task<List<TutorDocumentFolder>> ListTutorDocumentFoldersAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.TutorDocumentFolders
            .Where(link => link.TutorId == tutorId)
            .Include(link => link.DocumentFolder)
            .OrderBy(link => link.DocumentFolder!.Name)
            .ToListAsync(cancellationToken);

    public Task<TutorDocumentFolder?> GetTutorDocumentFolderAsync(Guid tutorId, Guid folderId, CancellationToken cancellationToken = default) =>
        db.TutorDocumentFolders
            .Include(link => link.DocumentFolder)
            .FirstOrDefaultAsync(link => link.TutorId == tutorId && link.DocumentFolderId == folderId, cancellationToken);

    public async Task RemoveTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken cancellationToken = default)
    {
        db.TutorDocumentFolders.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ToolDefinition>> ListToolDefinitionsAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<ToolDefinition>().Where(tool => tool.TutorId == tutorId).OrderBy(tool => tool.Name).ToListAsync(cancellationToken);

    public async Task UpdateToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        db.Entry(tool).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        db.Set<ToolDefinition>().Remove(tool);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ToolDefinition> AddToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        db.Set<ToolDefinition>().Add(tool);
        await db.SaveChangesAsync(cancellationToken);
        return tool;
    }

    public async Task<DeploymentProfile> UpsertDeploymentProfileAsync(DeploymentProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await db.Set<DeploymentProfile>()
            .FirstOrDefaultAsync(x => x.TutorId == profile.TutorId, cancellationToken);

        if (existing is null)
        {
            db.Set<DeploymentProfile>().Add(profile);
            await db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        existing.Target = profile.Target;
        existing.ConfigurationJson = profile.ConfigurationJson;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<TutorModelConnection> UpsertTutorModelConnectionAsync(TutorModelConnection connection, CancellationToken cancellationToken = default)
    {
        var existing = await db.Set<TutorModelConnection>()
            .FirstOrDefaultAsync(x => x.TutorId == connection.TutorId, cancellationToken);

        if (existing is null)
        {
            db.Set<TutorModelConnection>().Add(connection);
            await db.SaveChangesAsync(cancellationToken);
            return connection;
        }

        existing.InteractionModelId = connection.InteractionModelId;
        existing.OverrideSettingsJson = connection.OverrideSettingsJson;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<TutorConfigurationSnapshot> AddSnapshotAsync(TutorConfigurationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        db.Set<TutorConfigurationSnapshot>().Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public Task<List<TutorConfigurationSnapshot>> ListSnapshotsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<TutorConfigurationSnapshot>()
            .Where(x => x.TutorId == tutorId)
            .Include(x => x.TutorRevision)
                .ThenInclude(x => x!.PromptVersion)
            .Include(x => x.TutorRevision)
                .ThenInclude(x => x!.InteractionModel)
            .OrderByDescending(x => x.CapturedUtc)
            .ToListAsync(cancellationToken);

    public Task<TutorConfigurationSnapshot?> GetSnapshotByIdAsync(Guid snapshotId, CancellationToken cancellationToken = default) =>
        db.Set<TutorConfigurationSnapshot>()
            .Include(x => x.TutorRevision)
                .ThenInclude(x => x!.PromptVersion)
            .Include(x => x.TutorRevision)
                .ThenInclude(x => x!.InteractionModel)
            .FirstOrDefaultAsync(x => x.Id == snapshotId, cancellationToken);

    public async Task<int> GetNextSnapshotVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        var max = await db.Set<TutorConfigurationSnapshot>()
            .Where(x => x.TutorId == tutorId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    // -------- TutorRevision (D25 step 7) --------

    public async Task<TutorRevision> AddRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default)
    {
        db.Set<TutorRevision>().Add(revision);
        await db.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public Task<TutorRevision?> GetRevisionByIdAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
        db.Set<TutorRevision>()
            .Include(r => r.Tutor)
            .Include(r => r.PromptVersion)
            .Include(r => r.InteractionModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.QueryEmbeddingModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.RevisionTools)
                .ThenInclude(rt => rt.ToolDefinition)
            .Include(r => r.Snapshots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == revisionId, cancellationToken);

    public Task<List<TutorRevision>> ListRevisionsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<TutorRevision>()
            .Where(r => r.TutorId == tutorId)
            .Include(r => r.PromptVersion)
            .Include(r => r.InteractionModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.QueryEmbeddingModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.RevisionTools).ThenInclude(link => link.ToolDefinition)
            .Include(r => r.Snapshots)
            .OrderByDescending(r => r.VersionNumber)
            .ToListAsync(cancellationToken);

    public Task<TutorRevision?> GetActiveRevisionAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        db.Set<TutorRevision>()
            .Include(r => r.PromptVersion)
            .Include(r => r.InteractionModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.QueryEmbeddingModel)
                .ThenInclude(m => m!.RuntimeEngine)
            .Include(r => r.RevisionTools).ThenInclude(link => link.ToolDefinition)
            .Include(r => r.Snapshots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.TutorId == tutorId && r.Status == RevisionStatus.Active, cancellationToken);

    public Task<TutorRevision?> GetActiveRevisionBySlugAsync(string activationSlug, CancellationToken cancellationToken = default) =>
        db.Set<TutorRevision>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                revision => revision.Status == RevisionStatus.Active &&
                    (revision.ActivationSlug == activationSlug || revision.PreviousActivationSlug == activationSlug),
                cancellationToken);

    public async Task<int> GetNextRevisionVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        var max = await db.Set<TutorRevision>()
            .Where(r => r.TutorId == tutorId)
            .Select(r => (int?)r.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    public async Task UpdateRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default)
    {
        // New links have application-assigned IDs; explicitly mark them Added before saving the tracked revision.
        foreach (var link in revision.RevisionTools.Where(link => db.Entry(link).State == EntityState.Detached))
            db.Set<TutorRevisionTool>().Add(link);
        db.Entry(revision).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateRevisionAsync(
        Tutor tutor,
        TutorRevision revision,
        TutorRevision? previousActiveRevision,
        TutorConfigurationSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (previousActiveRevision is not null)
        {
            db.Entry(previousActiveRevision).State = EntityState.Modified;
        }

        db.Entry(revision).State = EntityState.Modified;
        db.Entry(tutor).State = EntityState.Modified;
        db.Set<TutorConfigurationSnapshot>().Add(snapshot);

        // EF Core wraps this SaveChanges call in a transaction. No lifecycle state is persisted
        // unless every revision, tutor pointer, and snapshot write succeeds.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default)
    {
        db.Set<TutorRevision>().Remove(revision);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ActivationSlugExistsAsync(string slug, Guid? excludeRevisionId, CancellationToken cancellationToken = default) =>
        db.Set<TutorRevision>()
            .AnyAsync(r => r.ActivationSlug == slug && (excludeRevisionId == null || r.Id != excludeRevisionId), cancellationToken);
}
