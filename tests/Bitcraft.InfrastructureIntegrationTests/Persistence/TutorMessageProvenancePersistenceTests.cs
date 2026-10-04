using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Persistence;

public sealed class TutorMessageProvenancePersistenceTests
{
    [Fact]
    public async Task CompletedTurnRoundTripsIndependentChunksRanksScoresAndGenerationConfiguration()
    {
        var user = Guid.NewGuid();
        await using var db = PersistenceTestContextFactory.Create(new MutableCurrentUserContext(user));
        await PersistenceTestContextFactory.AddUserAsync(db, user);
        var conversation = TutorConversation.Create(user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var modelId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var embeddingId = Guid.NewGuid();
        var evidence = new[]
        {
            TutorRetrievedKnowledgeEvidence.Capture(documentId, "Original doc", Guid.NewGuid(), 0, "First context", 1, 0.95, embeddingId, "embed", 768),
            TutorRetrievedKnowledgeEvidence.Capture(documentId, "Original doc", Guid.NewGuid(), 1, "Second context", 2, 0.85, embeddingId, "embed", 768)
        };
        var started = DateTime.UtcNow;
        var tool = TutorToolExecutionEvidence.Capture("calculator", TutorToolExecutionStatus.Succeeded,
            started, started.AddMilliseconds(25), 25, null);
        var provenance = TutorConversationMessageProvenance.Capture(conversation.TutorId, conversation.TutorRevisionId,
            conversation.TutorConfigurationSnapshotId, modelId, "generation-model", "Ollama", evidence, [tool], 2);
        conversation.CompleteTurn("{}", "Question", null, "Answer", "generation-model", "Ollama", provenance);
        var repository = new TutorConversationRepository(db);
        await repository.AddAsync(conversation, default);
        db.ChangeTracker.Clear();

        var loaded = (await repository.GetAsync(conversation.Id, user, conversation.TutorId, default))!;
        Assert.Null(loaded.Messages.Single(value => value.Role == "user").Provenance);
        var stored = loaded.Messages.Single(value => value.Role == "assistant").Provenance!;
        Assert.Equal(conversation.TutorId, stored.TutorId);
        Assert.Equal(conversation.TutorRevisionId, stored.TutorRevisionId);
        Assert.Equal(conversation.TutorConfigurationSnapshotId, stored.TutorConfigurationSnapshotId);
        Assert.Equal(modelId, stored.InteractionModelId);
        Assert.Equal("generation-model", stored.ProviderModelName);
        Assert.Equal("Ollama", stored.ProviderType);
        Assert.Equal(provenance.CapturedUtc, stored.CapturedUtc);
        Assert.Equal(evidence, stored.KnowledgeEvidence.OrderBy(value => value.RetrievalRank));
        Assert.Equal(tool, Assert.Single(stored.ToolEvidence));
        Assert.True(stored.MemoryContextProvided);
        Assert.Equal(2, stored.RetrievedMemoryCount);

        // The existing repository's update path must save evidence for subsequent turns too.
        loaded.CompleteTurn("{\"updated\":true}", "Follow up", null, "Second answer", "model", "Ollama",
            TutorConversationMessageProvenance.Capture(loaded.TutorId, loaded.TutorRevisionId,
                loaded.TutorConfigurationSnapshotId, modelId, "model", "Ollama", [], [], 0));
        Assert.True(await repository.SaveAsync(loaded, default));
        db.ChangeTracker.Clear();
        var updated = (await repository.GetAsync(conversation.Id, user, conversation.TutorId, default))!;
        Assert.Equal(2, updated.Messages.Count(value => value.Provenance is not null));
        Assert.Equal(2, updated.Messages.Single(value => value.Sequence == 2).Provenance!.KnowledgeEvidence.Count);
        Assert.False(updated.Messages.Single(value => value.Sequence == 4).Provenance!.MemoryContextProvided);
    }

    [Fact]
    public async Task HistoricalMessagesWithoutProvenanceStillLoad()
    {
        var user = Guid.NewGuid();
        await using var db = PersistenceTestContextFactory.Create(new MutableCurrentUserContext(user));
        await PersistenceTestContextFactory.AddUserAsync(db, user);
        var conversation = TutorConversation.Create(user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        conversation.CompleteTurn("{}", "Old question", null, "Old answer", "model", "Ollama");
        var repository = new TutorConversationRepository(db);
        await repository.AddAsync(conversation, default);
        db.ChangeTracker.Clear();
        var loaded = (await repository.GetAsync(conversation.Id, user, conversation.TutorId, default))!;
        Assert.Equal(new[] { "Old question", "Old answer" }, loaded.Messages.OrderBy(value => value.Sequence).Select(value => value.Content));
        Assert.All(loaded.Messages, value => Assert.Null(value.Provenance));
    }

    [Fact]
    public async Task EditingAndDeletingLiveResourcesDoesNotChangeHistoricalEvidence()
    {
        var user = Guid.NewGuid();
        await using var db = PersistenceTestContextFactory.Create(new MutableCurrentUserContext(user));
        await PersistenceTestContextFactory.AddUserAsync(db, user);
        var document = new Document { Name = "Original", RawText = "Original context" };
        var model = new InteractionModel { Name = "Original model", ProviderModelName = "original-model" };
        db.Add(document); db.Add(model);
        var conversation = TutorConversation.Create(user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var chunk = TutorRetrievedKnowledgeEvidence.Capture(document.Id, document.Name, Guid.NewGuid(), 0,
            document.RawText, 1, 0.9, null, null, null);
        conversation.CompleteTurn("{}", "Q", null, "A", model.ProviderModelName, "Ollama",
            TutorConversationMessageProvenance.Capture(conversation.TutorId, conversation.TutorRevisionId,
                conversation.TutorConfigurationSnapshotId, model.Id, model.ProviderModelName, "Ollama", [chunk], [], 0));
        var repository = new TutorConversationRepository(db);
        await repository.AddAsync(conversation, default);
        document.Name = "Changed"; document.RawText = "Changed content"; model.ProviderModelName = "changed-model";
        await db.SaveChangesAsync();
        db.Remove(document); db.Remove(model);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = (await repository.GetAsync(conversation.Id, user, conversation.TutorId, default))!
            .Messages.Single(value => value.Role == "assistant").Provenance!;
        Assert.Equal("original-model", stored.ProviderModelName);
        Assert.Equal(model.Id, stored.InteractionModelId);
        Assert.Equal("Original", Assert.Single(stored.KnowledgeEvidence).DocumentName);
        Assert.Equal("Original context", Assert.Single(stored.KnowledgeEvidence).ContentExcerpt);
        // The EF model enforces ownership only: no FK can cascade from a live source resource.
        foreach (var type in new[] { typeof(TutorConversationMessageProvenance), typeof(TutorRetrievedKnowledgeEvidence), typeof(TutorToolExecutionEvidence) })
            Assert.All(db.Model.FindEntityType(type)!.GetForeignKeys(), key => Assert.True(key.IsOwnership));
    }
}
