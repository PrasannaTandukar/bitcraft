using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorRepository
{
    Task<Tutor> AddAsync(Tutor tutor, CancellationToken cancellationToken = default);
    Task<Tutor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tutor?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tutor tutor, CancellationToken cancellationToken = default);

    Task<PromptVersion> AddPromptVersionAsync(PromptVersion promptVersion, CancellationToken cancellationToken = default);
    Task<int> GetNextPromptVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default);

    Task<KnowledgeSourceLink> AddKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken cancellationToken = default);
    Task<bool> KnowledgeSourceLinkExistsAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default);

    Task<ToolDefinition> AddToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default);

    Task<DeploymentProfile> UpsertDeploymentProfileAsync(DeploymentProfile profile, CancellationToken cancellationToken = default);

    Task<TutorModelConnection> UpsertTutorModelConnectionAsync(TutorModelConnection connection, CancellationToken cancellationToken = default);

    Task<TutorConfigurationSnapshot> AddSnapshotAsync(TutorConfigurationSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Returns 1 for the first snapshot of a tutor, otherwise <c>max(VersionNumber) + 1</c>.</summary>
    Task<int> GetNextSnapshotVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default);
}
