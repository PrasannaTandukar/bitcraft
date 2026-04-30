using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.UnitTests.Application.Tutoring.Fakes;

internal sealed class FakeCurrentUserContext(Guid userId, bool isAdmin = false) : ICurrentUserContext
{
    public bool IsAuthenticated => userId != Guid.Empty;
    public Guid UserId => IsAuthenticated ? userId : throw new UnauthorizedAccessException("Current user is not authenticated.");
    public Guid? UserIdOrNull => IsAuthenticated ? userId : null;
    public bool IsInRole(string role) => role == AppRoles.Admin && isAdmin;
}

internal sealed class FakeRoleManagementService : IRoleManagementService
{
    public List<string> Roles { get; } = [AppRoles.Admin, AppRoles.Editor, AppRoles.Viewer];
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
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Viewer.")));
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
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Viewer.")));
        }

        if (!Memberships.TryGetValue(userId, out var membership) || !membership.Roles.Contains(normalizedRole))
        {
            return Task.FromResult(Result.Failure<UserRoleMembership>(new Error("Role.NotAssigned", $"User does not have role '{normalizedRole}'.")));
        }

        var updated = membership with { Roles = membership.Roles.Where(x => x != normalizedRole).ToArray() };
        Memberships[userId] = updated;
        return Task.FromResult(Result.Success(updated));
    }
}

internal sealed class FakeTutorRepository : ITutorRepository
{
    public Dictionary<Guid, Tutor> Tutors { get; } = new();
    public List<PromptVersion> PromptVersions { get; } = new();
    public List<KnowledgeSourceLink> KnowledgeSources { get; } = new();
    public List<ToolDefinition> Tools { get; } = new();
    public Dictionary<Guid, DeploymentProfile> Profiles { get; } = new();
    public Dictionary<Guid, TutorModelConnection> TutorModelConnections { get; } = new();
    public List<TutorConfigurationSnapshot> Snapshots { get; } = new();

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
        tutor.Tools = Tools.Where(t => t.TutorId == id).ToList();
        tutor.DeploymentProfile = Profiles.TryGetValue(id, out var p) ? p : null;
        tutor.TutorModelConnection = TutorModelConnections.TryGetValue(id, out var connection) ? connection : null;
        tutor.Snapshots = Snapshots.Where(s => s.TutorId == id).ToList();
        tutor.ActivePromptVersion = tutor.ActivePromptVersionId is Guid pid
            ? tutor.PromptVersions.FirstOrDefault(v => v.Id == pid) : null;
        return Task.FromResult<Tutor?>(tutor);
    }

    public Task<List<Tutor>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        Task.FromResult(Tutors.Values.Where(t => t.OwnerId == ownerId).ToList());

    public Task UpdateAsync(Tutor tutor, CancellationToken ct = default)
    {
        Tutors[tutor.Id] = tutor;
        return Task.CompletedTask;
    }

    public Task<PromptVersion> AddPromptVersionAsync(PromptVersion v, CancellationToken ct = default)
    {
        PromptVersions.Add(v);
        return Task.FromResult(v);
    }

    public Task<int> GetNextPromptVersionNumberAsync(Guid tutorId, CancellationToken ct = default)
    {
        var max = PromptVersions.Where(p => p.TutorId == tutorId).Select(p => (int?)p.VersionNumber).Max();
        return Task.FromResult((max ?? 0) + 1);
    }

    public Task<KnowledgeSourceLink> AddKnowledgeSourceLinkAsync(KnowledgeSourceLink link, CancellationToken ct = default)
    {
        KnowledgeSources.Add(link);
        return Task.FromResult(link);
    }

    public Task<bool> KnowledgeSourceLinkExistsAsync(Guid tutorId, Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(KnowledgeSources.Any(k => k.TutorId == tutorId && k.DocumentId == documentId));

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
}

internal sealed class FakeModelCatalogRepository : IModelCatalogRepository
{
    public Dictionary<Guid, ModelCatalogEntry> Entries { get; } = new();

    public Task<ModelCatalogEntry> AddAsync(ModelCatalogEntry entry, CancellationToken ct = default)
    {
        Entries[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    public Task<ModelCatalogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Entries.TryGetValue(id, out var entry) ? entry : null);

    public Task<List<ModelCatalogEntry>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult(Entries.Values.ToList());

    public Task UpdateAsync(ModelCatalogEntry entry, CancellationToken ct = default)
    {
        Entries[entry.Id] = entry;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDocumentStore : IDocumentStore
{
    public Dictionary<Guid, Document> Documents { get; } = new();

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
            EmbeddingProvider = chunk.EmbeddingProvider
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

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public List<string> EmbeddedTexts { get; } = new();

    public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default)
    {
        EmbeddedTexts.Add(text);
        return Task.FromResult(new[] { 0.1f, 0.2f, 0.3f });
    }
}

internal sealed class StubHealthCheck(Result<ModelHealthStatus> result) : IModelConnectionHealthCheck
{
    public Task<Result<ModelHealthStatus>> CheckAsync(ModelCatalogEntry catalogEntry, CancellationToken cancellationToken = default) =>
        Task.FromResult(result);
}
