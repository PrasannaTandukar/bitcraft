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
                .ThenInclude(x => x!.ModelCatalogEntry)
            .Include(x => x.ActivePromptVersion)
            .Include(x => x.PromptVersions)
            .Include(x => x.KnowledgeSources)
            .Include(x => x.Tools)
            .Include(x => x.DeploymentProfile)
            .Include(x => x.Snapshots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        db.Set<Tutor>()
            // D2/D3: "owner" is now any membership row — caller-visible tutors are those where
            // the user has a TutorMember entry (any ResourceRole).
            .Where(x => x.Members.Any(m => m.UserId == ownerId))
            .Include(x => x.TutorModelConnection)
            .OrderByDescending(x => x.ModifiedUtc ?? x.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        db.Set<Tutor>().Update(tutor);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PromptVersion> AddPromptVersionAsync(PromptVersion promptVersion, CancellationToken cancellationToken = default)
    {
        db.Set<PromptVersion>().Add(promptVersion);
        await db.SaveChangesAsync(cancellationToken);
        return promptVersion;
    }

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

        existing.ModelCatalogEntryId = connection.ModelCatalogEntryId;
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
}
