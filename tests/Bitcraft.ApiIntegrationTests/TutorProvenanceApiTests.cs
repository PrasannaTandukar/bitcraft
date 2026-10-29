using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests
{
    [Fact]
    public async Task HistoricalAssistantWithoutEvidenceReturnsExplicitNullProvenance()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var conversation = TutorConversation.Create(owner, tutor.TutorId, tutor.RevisionId, tutor.SnapshotId);
        conversation.CompleteTurn("{}", "Old question", null, "Old answer", "old-model", "Ollama");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().AddAsync(conversation, default);
        using var response = await client.GetAsync($"/api/runtime/tutors/{tutor.Slug}/conversations/{conversation.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new[] { "Old question", "Old answer" }, history.Messages.Select(value => value.Content));
        Assert.All(history.Messages, value => Assert.Null(value.Provenance));
        using var body = JsonDocument.Parse(json);
        Assert.All(body.RootElement.GetProperty("messages").EnumerateArray(), value =>
            Assert.Equal(JsonValueKind.Null, value.GetProperty("provenance").ValueKind));
        // An old payload omitting the newly added property still deserializes.
        var legacy = JsonSerializer.Deserialize<TutorConversationStoredMessageResponse>(
            """{"id":"00000000-0000-0000-0000-000000000001","sequence":2,"role":"assistant","content":"Old answer","createdUtc":"2026-01-01T00:00:00Z","attachmentIds":[],"providerModelName":"old-model","providerType":"Ollama"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(legacy.Provenance);
        Assert.Equal("Old answer", legacy.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizedStudentCanReadMessageExcerptButCannotOpenOwnersSourceDocument(bool streaming)
    {
        using var ownerClient = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(ownerClient, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        using var studentClient = factory.CreateClient();
        var student = await AuthorizeAsRoleAsync(studentClient, AppRoles.Auditor);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TutorMembers.Add(new TutorMember { TutorId = tutor.TutorId, UserId = student,
                Role = ResourceRole.Auditor, GrantedById = owner, GrantedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var answer = await AskWithProvenanceAsync(studentClient, tutor, new("Explain the knowledge"), streaming);
        var path = $"/api/runtime/tutors/{tutor.Slug}/conversations/{answer.ConversationId}";
        var history = (await studentClient.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(path))!;
        var provenance = Assert.Single(history.Messages, value => value.Role == "assistant").Provenance!;
        var chunk = Assert.Single(provenance.Knowledge.RetrievedChunks);
        Assert.Equal("robotics.pdf", chunk.DocumentName);
        Assert.Equal("Bounded source content.", chunk.Excerpt);
        using var source = await studentClient.GetAsync($"/api/documents/{tutor.DocumentId}");
        using var sourceStatus = await studentClient.GetAsync($"/api/documents/{tutor.DocumentId}/vectorization-status");
        using var ownedSource = await ownerClient.GetAsync($"/api/documents/{tutor.DocumentId}");
        Assert.Equal(HttpStatusCode.Forbidden, source.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sourceStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownedSource.StatusCode);
        using var otherReadersConversation = await ownerClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, otherReadersConversation.StatusCode); // Even a tutor owner cannot read another user's conversation.
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("documentId", provenanceJson);
        Assert.DoesNotContain("chunkId", provenanceJson);
        Assert.DoesNotContain("url", provenanceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teach robotics clearly", provenanceJson);
        Assert.DoesNotContain("http://localhost", provenanceJson);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TutorMembers.RemoveRange(db.TutorMembers.Where(value => value.TutorId == tutor.TutorId && value.UserId == student));
            await db.SaveChangesAsync();
        }
        using var revoked = await studentClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.DoesNotContain("Bounded source content", await revoked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HistoryProvenanceUsesPinnedRevisionNumberRatherThanSnapshotVersionOrCurrentCatalog()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        Guid snapshotId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var original = (await db.TutorConfigurationSnapshots.FindAsync(tutor.SnapshotId))!;
            var revision = (await db.TutorRevisions.Include(value => value.InteractionModel).Include(value => value.PromptVersion)
                .SingleAsync(value => value.Id == tutor.RevisionId))!;
            var aggregate = (await db.Tutors.FindAsync(tutor.TutorId))!;
            var laterSnapshot = TutorConfigurationSnapshot.Capture(aggregate, 99, revision.PromptVersion, null, "[]", "[]", null, owner,
                revision, original.EffectiveDocumentIdsJson, original.EffectiveKnowledgeBindingJson);
            laterSnapshot.UpdateMetadata(owner);
            db.Add(laterSnapshot);
            await db.SaveChangesAsync();
            snapshotId = laterSnapshot.Id;
        }
        var answer = await AskWithProvenanceAsync(client, tutor, new("Explain snapshot knowledge"), streaming: true);
        Assert.Equal(snapshotId, answer.Provenance!.Runtime.SnapshotId);
        Assert.Equal(3, answer.Provenance.Runtime.RevisionNumber);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var model = (await db.InteractionModels.FindAsync(answer.Provenance.Runtime.InteractionModelId))!;
            model.Name = "Changed current catalog display"; model.ProviderModelName = "changed-current-provider-model";
            db.Documents.Remove((await db.Documents.FindAsync(tutor.DocumentId!.Value))!);
            await db.SaveChangesAsync();
        }
        var history = (await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{answer.ConversationId}"))!;
        Assert.Equal(3, history.RevisionNumber);
        var historical = Assert.Single(history.Messages, value => value.Role == "assistant").Provenance!;
        Assert.Equal(JsonSerializer.Serialize(answer.Provenance), JsonSerializer.Serialize(historical));
        Assert.Equal("robotics.pdf", Assert.Single(historical.Knowledge.RetrievedChunks).DocumentName);
    }
}
