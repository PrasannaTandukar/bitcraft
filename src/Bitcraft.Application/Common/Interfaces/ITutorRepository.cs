using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorRepository
{
    Task<Tutor> AddAsync(Tutor tutor, CancellationToken cancellationToken = default);
    Task<Tutor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tutor?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tutor tutor, CancellationToken cancellationToken = default);
    Task DeleteAsync(Tutor tutor, CancellationToken cancellationToken = default);

    Task<PromptVersion> AddPromptVersionAsync(PromptVersion promptVersion, CancellationToken cancellationToken = default);
    Task<List<PromptVersion>> ListPromptVersionsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<PromptVersion?> GetPromptVersionByIdAsync(Guid promptVersionId, CancellationToken cancellationToken = default);
    Task<int> GetNextPromptVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default);

    Task<KnowledgeSourceLink> AddKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken cancellationToken = default);
    Task<bool> KnowledgeSourceLinkExistsAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default);
    Task<List<KnowledgeSourceLink>> ListKnowledgeSourceLinksAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceLink?> GetKnowledgeSourceLinkAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default);
    Task RemoveKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken cancellationToken = default);

    Task<TutorDocument> AddTutorDocumentAsync(TutorDocument link, CancellationToken cancellationToken = default);
    Task<List<TutorDocument>> ListTutorDocumentsAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<TutorDocument?> GetTutorDocumentAsync(Guid tutorId, Guid documentId, CancellationToken cancellationToken = default);
    Task RemoveTutorDocumentAsync(TutorDocument link, CancellationToken cancellationToken = default);

    Task<TutorDocumentFolder> AddTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken cancellationToken = default);
    Task<List<TutorDocumentFolder>> ListTutorDocumentFoldersAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<TutorDocumentFolder?> GetTutorDocumentFolderAsync(Guid tutorId, Guid folderId, CancellationToken cancellationToken = default);
    Task RemoveTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken cancellationToken = default);

    Task<List<ToolDefinition>> ListToolDefinitionsAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task UpdateToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default);
    Task RemoveToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default);

    Task<ToolDefinition> AddToolDefinitionAsync(ToolDefinition tool, CancellationToken cancellationToken = default);

    Task<DeploymentProfile> UpsertDeploymentProfileAsync(DeploymentProfile profile, CancellationToken cancellationToken = default);

    Task<TutorModelConnection> UpsertTutorModelConnectionAsync(TutorModelConnection connection, CancellationToken cancellationToken = default);

    Task<TutorConfigurationSnapshot> AddSnapshotAsync(TutorConfigurationSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<List<TutorConfigurationSnapshot>> ListSnapshotsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<TutorConfigurationSnapshot?> GetSnapshotByIdAsync(Guid snapshotId, CancellationToken cancellationToken = default);

    /// <summary>Returns 1 for the first snapshot of a tutor, otherwise <c>max(VersionNumber) + 1</c>.</summary>
    Task<int> GetNextSnapshotVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default);

    // D25 step 7: TutorRevision aggregate.
    Task<TutorRevision> AddRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default);
    Task<TutorRevision?> GetRevisionByIdAsync(Guid revisionId, CancellationToken cancellationToken = default);
    Task<List<TutorRevision>> ListRevisionsByTutorAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<TutorRevision?> GetActiveRevisionAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task<TutorRevision?> GetActiveRevisionBySlugAsync(string activationSlug, CancellationToken cancellationToken = default);
    Task<int> GetNextRevisionVersionNumberAsync(Guid tutorId, CancellationToken cancellationToken = default);
    Task UpdateRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default);
    /// <summary>
    /// Persists the complete activation state transition in one database save so the previous
    /// active revision, new active revision, tutor pointer, and immutable snapshot are atomic.
    /// </summary>
    Task ActivateRevisionAsync(
        Tutor tutor,
        TutorRevision revision,
        TutorRevision? previousActiveRevision,
        TutorConfigurationSnapshot snapshot,
        CancellationToken cancellationToken = default);
    Task RemoveRevisionAsync(TutorRevision revision, CancellationToken cancellationToken = default);
    Task<bool> ActivationSlugExistsAsync(string slug, Guid? excludeRevisionId, CancellationToken cancellationToken = default);
}
