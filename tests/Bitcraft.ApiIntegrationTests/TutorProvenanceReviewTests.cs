using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Repositories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests
{
    // Opt-in PostgreSQL is disposable. Provider responses/embeddings are controlled; runtime,
    // Agent Framework, tools, memory storage, migrations and message persistence are real.
    [LocalSessionPostgresFact]
    public async Task RoboTutorReviewStreamsPwmMemoryGreetingLegacyHistoryAndPinnedSnapshot()
    {
        using var localFactory = new BitcraftApiFactory {
            SessionTestPostgres = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES"), UseRealTutorMemoryStore = true };
        var harness = new TutorRuntimeEndpointTests(localFactory);
        using var client = localFactory.CreateClient();
        var owner = await harness.AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await harness.SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-review-rag");
        var embeddingId = await harness.GetQueryEmbeddingIdAsync(tutor);
        const string excerpt = "Equal PWM commands do not ensure equal wheel speeds. Motor tolerances, friction, load and wheel diameter can differ. Encoders measure each wheel's speed for feedback control.";
        var chunk = new KnowledgeChunk { DocumentId = tutor.DocumentId!.Value, ChunkIndex = 2, Content = excerpt,
            EmbeddingModelId = embeddingId, EmbeddingDimensions = 3, Embedding = [1, 0, 0] };
        var retriever = localFactory.Services.GetRequiredService<TestKnowledgeRetriever>();
        retriever.DocumentChunks[tutor.DocumentId.Value] = [chunk];
        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Tutors.FindAsync(tutor.TutorId))!.Name = "RoboTutor integration review";
            (await db.Documents.FindAsync(tutor.DocumentId.Value))!.Name = "07_motors_actuators_and_drivers.md";
            await db.SaveChangesAsync();
        }
        const string pwmQuestion = "Why can equal PWM on two motors still cause a robot to curve?";
        var rag = await harness.AskWithProvenanceAsync(client, tutor, new(pwmQuestion), streaming: true);
        var evidence = Assert.Single(rag.Provenance!.Knowledge.RetrievedChunks);
        Assert.Equal(excerpt, evidence.Excerpt);
        Assert.Equal("07_motors_actuators_and_drivers.md", evidence.DocumentName);
        Assert.Equal(1, evidence.RetrievalRank);
        Assert.Equal(1, evidence.SimilarityScore);
        Assert.Equal(tutor.SnapshotId, rag.Provenance.Runtime.SnapshotId);
        Assert.Equal(tutor.RevisionId, rag.Provenance.Runtime.TutorRevisionId);
        Assert.Equal(rag.Model.Id, rag.Provenance.Runtime.InteractionModelId);
        Assert.Equal(rag.Revision.Number, rag.Provenance.Runtime.RevisionNumber);
        Assert.Single(retriever.Requests);
        var supplied = localFactory.Services.GetRequiredService<TestTutorAgentRecorder>().LastResponse!.Knowledge!.ContextSources;
        Assert.Equal(chunk.Id, Assert.Single(supplied).ChunkId);

        // Memory is saved through the canonical context provider, then supplied in another conversation.
        const string privateMemory = "My robot has a smaller left wheel; remember this private calibration observation.";
        await harness.AskWithProvenanceAsync(client, tutor, new(privateMemory, SaveToMemory: true), streaming: true);
        var memory = await harness.AskWithProvenanceAsync(client, tutor, new(pwmQuestion, UseMemory: true), streaming: true);
        Assert.True(memory.Provenance!.Memory.ContextProvided);
        Assert.Equal(1, memory.Provenance.Memory.RetrievedItemCount);
        var provider = harness.GetProvenanceClient(memory.Model.Id);
        Assert.Contains(provider.Requests[0], message => message.Text.Contains(privateMemory));
        Assert.DoesNotContain(privateMemory, JsonSerializer.Serialize(memory.Provenance));

        // Greeting skips RAG even for a knowledge-enabled tutor.
        var searches = retriever.Requests.Count;
        var greeting = await harness.AskWithProvenanceAsync(client, tutor, new("Hello"), streaming: true);
        Assert.Equal(searches, retriever.Requests.Count);
        Assert.Empty(greeting.Provenance!.Knowledge.RetrievedChunks);
        Assert.False(greeting.Provenance.Memory.ContextProvided);
        Assert.Empty(greeting.Provenance.Tools);

        // A genuine zero-hit retrieval also produces zero context evidence.
        retriever.DocumentChunks[tutor.DocumentId.Value] = [];
        var zeroHits = await harness.AskWithProvenanceAsync(client, tutor, new("Tell me about a topic absent from these documents."), streaming: true);
        Assert.Empty(zeroHits.Provenance!.Knowledge.RetrievedChunks);
        Assert.Equal(searches + 1, retriever.Requests.Count);
        retriever.DocumentChunks[tutor.DocumentId.Value] = [chunk];

        var historical = TutorConversation.Create(owner, tutor.TutorId, tutor.RevisionId, tutor.SnapshotId);
        historical.CompleteTurn("{}", "Before provenance", null, "Historical answer", "old-model", "Ollama");
        using (var scope = localFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().AddAsync(historical, default);
        var old = (await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{historical.Id}"))!;
        Assert.All(old.Messages, message => Assert.Null(message.Provenance));

        // Capture Snapshot B through the existing active-revision endpoint, with ready attachments.
        var metadata = localFactory.Services.GetRequiredService<TestDocumentVectorizationReader>();
        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var extra = new Document { Name = "08_control_pid.md", RawText = "Encoder feedback corrects speed error." };
            extra.UpdateMetadata(owner);
            db.Add(extra);
            db.Add(ReviewDocumentLink(owner, tutor.TutorId, tutor.DocumentId.Value));
            db.Add(ReviewDocumentLink(owner, tutor.TutorId, extra.Id));
            await db.SaveChangesAsync();
            foreach (var documentId in new[] { tutor.DocumentId.Value, extra.Id })
                metadata.Metadata[documentId] = new(true, false, 1, DateTime.UtcNow, embeddingId, "Review embedding", "runtime-embed-model", "Ollama", 3);
        }
        using var captured = await client.PostAsync($"/api/tutors/{tutor.TutorId}/snapshots/active-revision", null);
        Assert.Equal(HttpStatusCode.Created, captured.StatusCode);
        var snapshotB = (await captured.Content.ReadFromJsonAsync<TutorConfigurationSnapshotResponse>())!;
        Assert.NotEqual(tutor.SnapshotId, snapshotB.Id);
        var afterCapture = retriever.Requests.Count;
        var reopened = (await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{rag.ConversationId}"))!;
        Assert.Equal(JsonSerializer.Serialize(rag.Provenance), JsonSerializer.Serialize(reopened.Messages.Last().Provenance));
        Assert.Equal(tutor.SnapshotId, reopened.SnapshotId);
        Assert.Equal(afterCapture, retriever.Requests.Count); // History presentation performs no retrieval.
        var fresh = await harness.AskWithProvenanceAsync(client, tutor, new("Hello"), streaming: true);
        Assert.Equal(snapshotB.Id, fresh.Provenance!.Runtime.SnapshotId);
    }

    [LocalSessionPostgresFact]
    public async Task RoboTutorReviewBatteryInvokesRealCalculatorAndRetrievesKnowledgeOnlyOnce()
    {
        using var localFactory = new BitcraftApiFactory { SessionTestPostgres = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES") };
        var harness = new TutorRuntimeEndpointTests(localFactory);
        using var client = localFactory.CreateClient();
        var owner = await harness.AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await harness.SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-review-calculator");
        var documentId = tutor.DocumentId!.Value;
        var embeddingId = await harness.GetQueryEmbeddingIdAsync(tutor);
        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(ReviewDocumentLink(owner, tutor.TutorId, documentId));
            await db.SaveChangesAsync();
        }
        localFactory.Services.GetRequiredService<TestDocumentVectorizationReader>().Metadata[documentId]
            = new(true, false, 1, DateTime.UtcNow, embeddingId, "Review embedding", "runtime-embed-model", "Ollama", 3);
        tutor = await harness.ActivateCalculatorRevisionAsync(client, tutor);
        var retriever = localFactory.Services.GetRequiredService<TestKnowledgeRetriever>();
        retriever.DocumentChunks[documentId] = [new KnowledgeChunk { DocumentId = documentId,
            Content = "Ideal battery energy in Wh is voltage multiplied by capacity in Ah. Runtime in hours is Wh divided by W.",
            ChunkIndex = 0, EmbeddingModelId = embeddingId, EmbeddingDimensions = 3, Embedding = [1, 0, 0] }];
        var answer = await harness.AskWithProvenanceAsync(client, tutor,
            new("A 12 V 5 Ah battery powers a robot consuming 18 W. Calculate ideal runtime."), streaming: true);
        var tool = Assert.Single(answer.Provenance!.Tools);
        Assert.Equal("Calculator", tool.ToolName);
        Assert.True(tool.Succeeded);
        Assert.Equal("Succeeded", tool.Status);
        Assert.Single(answer.Provenance.Knowledge.RetrievedChunks);
        Assert.Single(retriever.Requests);
        var provider = harness.GetProvenanceClient(answer.Model.Id);
        Assert.Equal(2, provider.Requests.Count);
        var result = Assert.Single(provider.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>());
        Assert.Contains("3.333", result.Result!.ToString());
        Assert.Contains("3.33 hours", answer.Answer);
        Assert.DoesNotContain("expression", JsonSerializer.Serialize(answer.Provenance));
    }

    [LocalSessionPostgresFact]
    public async Task PostgreSqlProvenanceHistoryHasBoundedQueriesAndLightweightBatchedNames()
    {
        var connection = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES")!;
        using var localFactory = new BitcraftApiFactory { SessionTestPostgres = connection };
        var harness = new TutorRuntimeEndpointTests(localFactory);
        using var client = localFactory.CreateClient();
        var owner = await harness.AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await harness.SeedActiveTutorAsync(owner, includeKnowledge: true);
        var counter = new ProvenanceQueryCounter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, options => options.UseVector()).AddInterceptors(counter).Options);
        var repository = new TutorConversationRepository(db);
        foreach (var turnCount in new[] { 1, 20 })
        {
            var conversation = TutorConversation.Create(owner, tutor.TutorId, tutor.RevisionId, tutor.SnapshotId);
            for (var turn = 0; turn < turnCount; turn++)
            {
                var chunks = Enumerable.Range(0, 5).Select(index => TutorRetrievedKnowledgeEvidence.Capture(
                    tutor.DocumentId!.Value, "motors.md", Guid.NewGuid(), index, new string('K', 1201), index + 1, 0.9, null, null, null));
                var now = DateTime.UtcNow;
                var tools = Enumerable.Range(0, 3).Select(_ => TutorToolExecutionEvidence.Capture("Calculator",
                    TutorToolExecutionStatus.Succeeded, now, now, 0, null));
                conversation.CompleteTurn("{}", $"Question {turn}", null, $"Answer {turn}", "review-model", "Ollama",
                    TutorConversationMessageProvenance.Capture(tutor.TutorId, tutor.RevisionId, tutor.SnapshotId,
                        Guid.NewGuid(), "review-model", "Ollama", chunks, tools, 0));
            }
            await repository.AddAsync(conversation, default);
            db.ChangeTracker.Clear();
            counter.Commands.Clear();
            var loaded = (await repository.GetAsync(conversation.Id, owner, tutor.TutorId, default))!;
            Assert.Equal(turnCount * 2, loaded.Messages.Count);
            Assert.All(loaded.Messages.Where(message => message.Role == "assistant"), message => {
                Assert.Equal(5, message.Provenance!.KnowledgeEvidence.Count);
                Assert.Equal(3, message.Provenance.ToolEvidence.Count);
            });
            Assert.Equal(4, counter.Commands.Count); // Independent of message/evidence count.
            counter.Commands.Clear();
            Assert.Empty(await repository.ListAsync(Guid.NewGuid(), tutor.TutorId, default));
            Assert.Single(counter.Commands); // Summaries never load message evidence.
        }
        counter.Commands.Clear();
        var names = await new DocumentStore(db).GetDisplayNamesAsync([tutor.DocumentId!.Value, tutor.DocumentId.Value, Guid.NewGuid()]);
        Assert.Single(names);
        Assert.Single(counter.Commands);
        Assert.DoesNotContain("RawText", counter.Commands[0]);
        counter.Commands.Clear();
        Assert.Empty(await new DocumentStore(db).GetDisplayNamesAsync([]));
        Assert.Empty(counter.Commands);
    }

    private static TutorDocument ReviewDocumentLink(Guid owner, Guid tutorId, Guid documentId)
    {
        var link = new TutorDocument { TutorId = tutorId, DocumentId = documentId };
        link.UpdateMetadata(owner);
        return link;
    }

    private sealed class ProvenanceQueryCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
