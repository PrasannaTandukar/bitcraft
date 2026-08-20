using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.UnitTests.Application.Tutoring.Fakes;

internal sealed class FakeCurrentUserContext(Guid userId, bool isAdmin = false, bool isEditor = false, bool isAuditor = false) : ICurrentUserContext
{
    public bool IsAuthenticated => userId != Guid.Empty;
    public Guid UserId => IsAuthenticated ? userId : throw new UnauthorizedAccessException("Current user is not authenticated.");
    public Guid? UserIdOrNull => IsAuthenticated ? userId : null;
    public bool IsInRole(string role) =>
        role == AppRoles.Admin && isAdmin ||
        role == AppRoles.Editor && isEditor ||
        role == AppRoles.Auditor && isAuditor;
}

internal sealed class FakeRoleManagementService : IRoleManagementService
{
    public List<string> Roles { get; } = [AppRoles.Admin, AppRoles.Editor, AppRoles.Auditor];
    public Dictionary<Guid, UserRoleMembership> Memberships { get; } = new();
    public int ListCalls { get; private set; }
    public int GetCalls { get; private set; }
    public int AssignCalls { get; private set; }
    public int RevokeCalls { get; private set; }

    public Task<Result<IReadOnlyList<string>>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult(Result.Success<IReadOnlyList<string>>(Roles));
    }

    public Task<Result<IReadOnlyList<UserRoleMembership>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult(Result.Success<IReadOnlyList<UserRoleMembership>>(Memberships.Values.ToList()));
    }

    public Task<Result<UserRoleMembership>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        GetCalls++;
        return Task.FromResult(Memberships.TryGetValue(userId, out var membership)
            ? Result.Success(membership)
            : Result.Failure<UserRoleMembership>(new Error("User.NotFound", $"User '{userId}' not found.")));
    }

    public Task<Result<UserRoleMembership>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        AssignCalls++;

        var normalizedRole = AppRoles.Normalize(role);
        if (normalizedRole is null)
        {
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Auditor.")));
        }

        var existing = Memberships.TryGetValue(userId, out var membership)
            ? membership
            : new UserRoleMembership(userId, "user@example.com", []);

        if (existing.Roles.Contains(normalizedRole))
        {
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.AlreadyAssigned", $"User already has role '{normalizedRole}'.")));
        }

        var updated = existing with { Roles = existing.Roles.Append(normalizedRole).OrderBy(x => x).ToArray() };
        Memberships[userId] = updated;
        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result<UserRoleMembership>> RevokeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        RevokeCalls++;

        var normalizedRole = AppRoles.Normalize(role);
        if (normalizedRole is null)
        {
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Auditor.")));
        }

        if (!Memberships.TryGetValue(userId, out var membership) || !membership.Roles.Contains(normalizedRole))
        {
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.NotAssigned", $"User does not have role '{normalizedRole}'.")));
        }

        var updated = membership with { Roles = membership.Roles.Where(x => x != normalizedRole).ToArray() };
        Memberships[userId] = updated;
        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result<UserRoleMembership>> CreateUserAsync(
        string email,
        string password,
        IReadOnlyList<string>? initialRoles = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var membership = new UserRoleMembership(id, email, initialRoles ?? []);
        Memberships[id] = membership;
        return Task.FromResult(Result.Success(membership));
    }
}

/// <summary>
/// In-memory <see cref="ITutorMembershipReader"/> for tests. Resolves roles from two sources, in
/// order of precedence: (1) explicit <see cref="Grant"/> overrides; (2) the founding membership
/// graph on tutors held in an optional <see cref="FakeTutorRepository"/>. Missing membership
/// returns <see langword="null"/> (i.e. forbidden), matching the production reader contract.
/// </summary>
internal sealed class FakeTutorMembershipReader : ITutorMembershipReader
{
    private readonly FakeTutorRepository? _repository;
    private readonly Dictionary<(Guid TutorId, Guid UserId), ResourceRole> _overrides = new();

    public FakeTutorMembershipReader() { }

    public FakeTutorMembershipReader(FakeTutorRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Grant an explicit role to a user on a tutor (takes precedence over the repository's <c>Members</c> graph).</summary>
    public void Grant(Guid tutorId, Guid userId, ResourceRole role) => _overrides[(tutorId, userId)] = role;

    public Task<ResourceRole?> GetRoleAsync(Guid tutorId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_overrides.TryGetValue((tutorId, userId), out var role))
        {
            return Task.FromResult<ResourceRole?>(role);
        }

        if (_repository is not null && _repository.Tutors.TryGetValue(tutorId, out var tutor))
        {
            var member = tutor.Members.FirstOrDefault(m => m.UserId == userId);
            if (member is not null)
            {
                return Task.FromResult<ResourceRole?>(member.Role);
            }
        }

        return Task.FromResult<ResourceRole?>(null);
    }

    public Task<IReadOnlyList<TutorMember>> ListMembersAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        if (_repository is not null && _repository.Tutors.TryGetValue(tutorId, out var tutor))
        {
            return Task.FromResult<IReadOnlyList<TutorMember>>(tutor.Members.ToList());
        }

        return Task.FromResult<IReadOnlyList<TutorMember>>([]);
    }
}

internal sealed class FakeTutorRepository : ITutorRepository
{
    public Dictionary<Guid, Tutor> Tutors { get; } = new();
    public List<PromptVersion> PromptVersions { get; } = new();
    public List<KnowledgeSourceLink> KnowledgeSources { get; } = new();
    public List<TutorDocument> TutorDocuments { get; } = new();
    public List<TutorDocumentFolder> TutorDocumentFolders { get; } = new();
    public List<ToolDefinition> Tools { get; } = new();
    public Dictionary<Guid, DeploymentProfile> Profiles { get; } = new();
    public Dictionary<Guid, TutorModelConnection> TutorModelConnections { get; } = new();
    public List<TutorConfigurationSnapshot> Snapshots { get; } = new();
    public int ActivationSaveCalls { get; private set; }

    public Task<Tutor> AddAsync(Tutor tutor, CancellationToken ct = default)
    {
        Tutors[tutor.Id] = tutor;
        return Task.FromResult(tutor);
    }

    public Task<Tutor?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Tutors.TryGetValue(id, out var t) ? t : null);

    public Task<Tutor?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        if (!Tutors.TryGetValue(id, out var tutor)) return Task.FromResult<Tutor?>(null);
        tutor.PromptVersions = PromptVersions.Where(p => p.TutorId == id).ToList();
        tutor.KnowledgeSources = KnowledgeSources.Where(k => k.TutorId == id).ToList();
        tutor.TutorDocuments = TutorDocuments.Where(k => k.TutorId == id).ToList();
        tutor.TutorDocumentFolders = TutorDocumentFolders.Where(k => k.TutorId == id).ToList();
        tutor.Tools = Tools.Where(t => t.TutorId == id).ToList();
        tutor.DeploymentProfile = Profiles.TryGetValue(id, out var p) ? p : null;
        tutor.TutorModelConnection = TutorModelConnections.TryGetValue(id, out var connection) ? connection : null;
        tutor.Snapshots = Snapshots.Where(s => s.TutorId == id).ToList();
        tutor.ActivePromptVersion = tutor.ActivePromptVersionId is Guid pid
            ? tutor.PromptVersions.FirstOrDefault(v => v.Id == pid) : null;
        return Task.FromResult<Tutor?>(tutor);
    }

    public Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        Task.FromResult(Tutors.Values.Where(t => t.Members.Any(m => m.UserId == ownerId)).ToList());

    public Task UpdateAsync(Tutor tutor, CancellationToken ct = default)
    {
        Tutors[tutor.Id] = tutor;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tutor tutor, CancellationToken ct = default)
    {
        Tutors.Remove(tutor.Id);
        return Task.CompletedTask;
    }

    public Task<PromptVersion> AddPromptVersionAsync(PromptVersion v, CancellationToken ct = default)
    {
        PromptVersions.Add(v);
        return Task.FromResult(v);
    }

    public Task<List<PromptVersion>> ListPromptVersionsByTutorAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(PromptVersions
            .Where(x => x.TutorId == tutorId)
            .OrderByDescending(x => x.VersionNumber)
            .ToList());

    public Task<PromptVersion?> GetPromptVersionByIdAsync(Guid promptVersionId, CancellationToken ct = default) =>
        Task.FromResult(PromptVersions.FirstOrDefault(x => x.Id == promptVersionId));

    public Task<int> GetNextPromptVersionNumberAsync(Guid tutorId, CancellationToken ct = default)
    {
        var max = PromptVersions.Where(p => p.TutorId == tutorId).Select(p => (int?)p.VersionNumber).Max();
        return Task.FromResult((max ?? 0) + 1);
    }

    public Task<int> GetNextSnapshotVersionNumberAsync(Guid tutorId, CancellationToken ct = default)
    {
        var max = Snapshots.Where(s => s.TutorId == tutorId).Select(s => (int?)s.VersionNumber).Max();
        return Task.FromResult((max ?? 0) + 1);
    }

    public Task<KnowledgeSourceLink> AddKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken ct = default)
    {
        KnowledgeSources.Add(link);
        return Task.FromResult(link);
    }

    public Task<bool> KnowledgeSourceLinkExistsAsync(Guid tutorId, Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(KnowledgeSources.Any(k => k.TutorId == tutorId && k.DocumentId == documentId));

    public Task<List<KnowledgeSourceLink>> ListKnowledgeSourceLinksAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(KnowledgeSources.Where(link => link.TutorId == tutorId).ToList());

    public Task<KnowledgeSourceLink?> GetKnowledgeSourceLinkAsync(Guid tutorId, Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(KnowledgeSources.FirstOrDefault(link => link.TutorId == tutorId && link.DocumentId == documentId));

    public Task RemoveKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken ct = default)
    {
        KnowledgeSources.Remove(link);
        return Task.CompletedTask;
    }

    public Task<TutorDocument> AddTutorDocumentAsync(TutorDocument link, CancellationToken ct = default)
    {
        TutorDocuments.Add(link);
        return Task.FromResult(link);
    }

    public Task<List<TutorDocument>> ListTutorDocumentsAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(TutorDocuments.Where(link => link.TutorId == tutorId).ToList());

    public Task<TutorDocument?> GetTutorDocumentAsync(Guid tutorId, Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(TutorDocuments.FirstOrDefault(link => link.TutorId == tutorId && link.DocumentId == documentId));

    public Task RemoveTutorDocumentAsync(TutorDocument link, CancellationToken ct = default)
    {
        TutorDocuments.Remove(link);
        return Task.CompletedTask;
    }

    public Task<TutorDocumentFolder> AddTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken ct = default)
    {
        TutorDocumentFolders.Add(link);
        return Task.FromResult(link);
    }

    public Task<List<TutorDocumentFolder>> ListTutorDocumentFoldersAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(TutorDocumentFolders.Where(link => link.TutorId == tutorId).ToList());

    public Task<TutorDocumentFolder?> GetTutorDocumentFolderAsync(Guid tutorId, Guid folderId, CancellationToken ct = default) =>
        Task.FromResult(TutorDocumentFolders.FirstOrDefault(link => link.TutorId == tutorId && link.DocumentFolderId == folderId));

    public Task RemoveTutorDocumentFolderAsync(TutorDocumentFolder link, CancellationToken ct = default)
    {
        TutorDocumentFolders.Remove(link);
        return Task.CompletedTask;
    }

    public Task<ToolDefinition> AddToolDefinitionAsync(ToolDefinition tool, CancellationToken ct = default)
    {
        Tools.Add(tool);
        return Task.FromResult(tool);
    }

    public Task<DeploymentProfile> UpsertDeploymentProfileAsync(DeploymentProfile profile, CancellationToken ct = default)
    {
        Profiles[profile.TutorId] = profile;
        return Task.FromResult(profile);
    }

    public Task<TutorModelConnection> UpsertTutorModelConnectionAsync(TutorModelConnection connection, CancellationToken ct = default)
    {
        TutorModelConnections[connection.TutorId] = connection;
        return Task.FromResult(connection);
    }

    public Task<TutorConfigurationSnapshot> AddSnapshotAsync(TutorConfigurationSnapshot s, CancellationToken ct = default)
    {
        Snapshots.Add(s);
        return Task.FromResult(s);
    }

    public Task<List<TutorConfigurationSnapshot>> ListSnapshotsByTutorAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(Snapshots
            .Where(x => x.TutorId == tutorId)
            .OrderByDescending(x => x.CapturedUtc)
            .ToList());

    public Task<TutorConfigurationSnapshot?> GetSnapshotByIdAsync(Guid snapshotId, CancellationToken ct = default) =>
        Task.FromResult(Snapshots.FirstOrDefault(x => x.Id == snapshotId));

    // ---- TutorRevision (D25 step 7) ----
    public List<TutorRevision> Revisions { get; } = new();

    public Task<TutorRevision> AddRevisionAsync(TutorRevision r, CancellationToken ct = default)
    {
        Revisions.Add(r);
        return Task.FromResult(r);
    }

    public Task<TutorRevision?> GetRevisionByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Revisions.FirstOrDefault(r => r.Id == id));

    public Task<List<TutorRevision>> ListRevisionsByTutorAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(Revisions.Where(r => r.TutorId == tutorId).OrderByDescending(r => r.VersionNumber).ToList());

    public Task<TutorRevision?> GetActiveRevisionAsync(Guid tutorId, CancellationToken ct = default) =>
        Task.FromResult(Revisions.FirstOrDefault(r => r.TutorId == tutorId && r.Status == RevisionStatus.Active));

    public Task<TutorRevision?> GetActiveRevisionBySlugAsync(string activationSlug, CancellationToken ct = default) =>
        Task.FromResult(Revisions.FirstOrDefault(revision =>
            revision.Status == RevisionStatus.Active &&
            (revision.ActivationSlug == activationSlug || revision.PreviousActivationSlug == activationSlug)));

    public Task<int> GetNextRevisionVersionNumberAsync(Guid tutorId, CancellationToken ct = default)
    {
        var max = Revisions.Where(r => r.TutorId == tutorId).Select(r => (int?)r.VersionNumber).Max();
        return Task.FromResult((max ?? 0) + 1);
    }

    public Task UpdateRevisionAsync(TutorRevision r, CancellationToken ct = default)
    {
        var idx = Revisions.FindIndex(x => x.Id == r.Id);
        if (idx >= 0) Revisions[idx] = r;
        return Task.CompletedTask;
    }

    public Task ActivateRevisionAsync(
        Tutor tutor,
        TutorRevision revision,
        TutorRevision? previousActiveRevision,
        TutorConfigurationSnapshot snapshot,
        CancellationToken ct = default)
    {
        ActivationSaveCalls++;
        Tutors[tutor.Id] = tutor;
        var revisionIndex = Revisions.FindIndex(x => x.Id == revision.Id);
        if (revisionIndex >= 0) Revisions[revisionIndex] = revision;
        if (previousActiveRevision is not null)
        {
            var previousIndex = Revisions.FindIndex(x => x.Id == previousActiveRevision.Id);
            if (previousIndex >= 0) Revisions[previousIndex] = previousActiveRevision;
        }
        Snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task RemoveRevisionAsync(TutorRevision r, CancellationToken ct = default)
    {
        Revisions.RemoveAll(x => x.Id == r.Id);
        return Task.CompletedTask;
    }

    public Task<bool> ActivationSlugExistsAsync(string slug, Guid? excludeRevisionId, CancellationToken ct = default) =>
        Task.FromResult(Revisions.Any(r => r.ActivationSlug == slug && (excludeRevisionId == null || r.Id != excludeRevisionId)));
}

internal sealed class FakeModelRepository : IModelRepository
{
    public Dictionary<Guid, InteractionModel> InteractionModels { get; } = new();
    public Dictionary<Guid, EmbeddingModel> EmbeddingModels { get; } = new();

    public Task<Model?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (InteractionModels.TryGetValue(id, out var im)) return Task.FromResult<Model?>(im);
        if (EmbeddingModels.TryGetValue(id, out var em)) return Task.FromResult<Model?>(em);
        return Task.FromResult<Model?>(null);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
    {
        bool match(Model m) => m.Name == name && (excludeId is null || m.Id != excludeId.Value);
        return Task.FromResult(InteractionModels.Values.Any(match) || EmbeddingModels.Values.Any(match));
    }

    public Task<InteractionModel?> GetInteractionModelByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(InteractionModels.TryGetValue(id, out var m) ? m : null);

    public Task<List<InteractionModel>> ListInteractionModelsAsync(CancellationToken ct = default) =>
        Task.FromResult(InteractionModels.Values.ToList());

    public Task<InteractionModel> AddAsync(InteractionModel model, CancellationToken ct = default)
    {
        InteractionModels[model.Id] = model;
        return Task.FromResult(model);
    }

    public Task UpdateAsync(InteractionModel model, CancellationToken ct = default)
    {
        InteractionModels[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(InteractionModel model, CancellationToken ct = default)
    {
        InteractionModels.Remove(model.Id);
        return Task.CompletedTask;
    }

    public Task<EmbeddingModel?> GetEmbeddingModelByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(EmbeddingModels.TryGetValue(id, out var m) ? m : null);

    public Task<List<EmbeddingModel>> ListEmbeddingModelsAsync(CancellationToken ct = default) =>
        Task.FromResult(EmbeddingModels.Values.ToList());

    public Task<EmbeddingModel> AddAsync(EmbeddingModel model, CancellationToken ct = default)
    {
        EmbeddingModels[model.Id] = model;
        return Task.FromResult(model);
    }

    public Task UpdateAsync(EmbeddingModel model, CancellationToken ct = default)
    {
        EmbeddingModels[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(EmbeddingModel model, CancellationToken ct = default)
    {
        EmbeddingModels.Remove(model.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTutorRuntimeEngineRepository : IRuntimeEngineRepository
{
    public Dictionary<Guid, RuntimeEngine> RuntimeEngines { get; } = new();

    public Task<RuntimeEngine> AddAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        RuntimeEngines[engine.Id] = engine;
        return Task.FromResult(engine);
    }

    public Task<RuntimeEngine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(RuntimeEngines.TryGetValue(id, out var engine) ? engine : null);

    public Task<RuntimeEngine?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(RuntimeEngines.Values.FirstOrDefault(x => x.Name == name));

    public Task<List<RuntimeEngine>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(RuntimeEngines.Values.ToList());

    public Task UpdateAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        RuntimeEngines[engine.Id] = engine;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        RuntimeEngines.Remove(engine.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDocumentStore : IDocumentStore, IDocumentContentStore
{
    public Dictionary<Guid, Document> Documents { get; } = new();
    public int ContentReplacementCalls { get; private set; }

    public Task<Document> AddAsync(Document document, CancellationToken ct = default)
    {
        Documents[document.Id] = document;
        return Task.FromResult(document);
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Documents.TryGetValue(id, out var document) ? document : null);

    public Task<List<Document>> ListByCreatorAsync(Guid creatorId, CancellationToken ct = default) =>
        Task.FromResult(Documents.Values.Where(document => document.CreatedById == creatorId).ToList());

    public Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        Documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Document document, CancellationToken ct = default)
    {
        Documents.Remove(document.Id);
        return Task.CompletedTask;
    }

    public Task UpdateContentAndClearChunksAsync(Document document, CancellationToken ct = default)
    {
        ContentReplacementCalls++;
        Documents[document.Id] = document;
        return Task.CompletedTask;
    }
}

internal sealed class FakeVectorStore : IVectorStore
{
    public List<KnowledgeChunk> Chunks { get; } = new();
    public List<Guid> ReplacedDocumentIds { get; } = new();

    public Task<IReadOnlyList<KnowledgeChunk>> ReplaceDocumentChunksAsync(
        Guid documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken ct = default)
    {
        ReplacedDocumentIds.Add(documentId);
        Chunks.RemoveAll(x => x.DocumentId == documentId);

        var replacements = chunks.Select(chunk => new KnowledgeChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunk.ChunkIndex,
            Content = chunk.Content,
            Embedding = chunk.Embedding,
            EmbeddingModel = chunk.EmbeddingModel,
            EmbeddingProvider = chunk.EmbeddingProvider,
            EmbeddingModelId = chunk.EmbeddingModelId,
            EmbeddingDimensions = chunk.EmbeddingDimensions
        }).ToList();

        Chunks.AddRange(replacements);
        return Task.FromResult<IReadOnlyList<KnowledgeChunk>>(replacements);
    }
}

internal sealed class FakeKnowledgeRetriever : IKnowledgeRetriever
{
    public IReadOnlyCollection<Guid> LastDocumentIds { get; private set; } = [];
    public float[] LastQueryEmbedding { get; private set; } = [];
    public int LastTopK { get; private set; }
    public List<KnowledgeChunk> Results { get; } = new();

    public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        IReadOnlyCollection<Guid> documentIds,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        LastDocumentIds = documentIds;
        LastQueryEmbedding = queryEmbedding;
        LastTopK = topK;
        return Task.FromResult(Results.ToList());
    }
}

internal sealed class FakeRevisionEffectiveSetReader(IReadOnlyList<Guid>? documentIds = null) : IRevisionEffectiveSetReader
{
    public IReadOnlyList<Guid> DocumentIds { get; set; } = documentIds ?? [];
    public Guid? QueryEmbeddingModelId { get; private set; }

    public Task<EffectiveDocumentSet> GetEffectiveSetAsync(
        Guid tutorId,
        CancellationToken cancellationToken = default,
        Guid? queryEmbeddingModelId = null)
    {
        QueryEmbeddingModelId = queryEmbeddingModelId;
        return Task.FromResult(new EffectiveDocumentSet(DocumentIds, []));
    }
}

internal sealed class FakeTutorMemoryStore : ITutorMemoryStore
{
    public List<TutorMemory> Memories { get; } = new();
    public Guid LastCreatorId { get; private set; }
    public float[] LastQueryEmbedding { get; private set; } = [];
    public int LastTopK { get; private set; }

    public Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default)
    {
        Memories.Add(memory);
        return Task.FromResult(memory);
    }

    public Task<List<TutorMemory>> SearchNearestMemoriesAsync(
        Guid creatorId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        LastCreatorId = creatorId;
        LastQueryEmbedding = queryEmbedding;
        LastTopK = topK;
        return Task.FromResult(Memories
            .Where(memory => memory.CreatedById == creatorId || memory.CreatedById is null)
            .ToList());
    }
}

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public List<string> EmbeddedTexts { get; } = new();

    public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default)
    {
        EmbeddedTexts.Add(text);
        return Task.FromResult(new[] { 0.1f, 0.2f, 0.3f });
    }
}
