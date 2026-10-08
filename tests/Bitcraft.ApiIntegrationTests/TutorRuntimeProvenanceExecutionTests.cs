using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests
{
    [Theory]
    [InlineData(false, 80, 5)]
    [InlineData(true, 80, 5)]
    [InlineData(false, 2_000, 4)]
    [InlineData(true, 2_000, 4)]
    public async Task ProductionRagProvenanceStoresExactSuppliedChunksWithoutSecondRetrieval(
        bool streaming, int contentLength, int suppliedCount)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        var embeddingId = await GetQueryEmbeddingIdAsync(tutor);
        var retriever = factory.Services.GetRequiredService<TestKnowledgeRetriever>();
        var chunks = Enumerable.Range(0, 5).Select(index => new KnowledgeChunk
        {
            DocumentId = tutor.DocumentId!.Value, ChunkIndex = index, Content = $"Chunk {index}: " + new string((char)('A' + index), contentLength),
            EmbeddingModelId = embeddingId, EmbeddingDimensions = 3, Embedding = [1, index * 0.15f, 0]
        }).ToArray();
        retriever.DocumentChunks[tutor.DocumentId!.Value] = chunks.Reverse().ToArray();

        var question = $"Explain the retrieved facts {tutor.TutorId:N}";
        var answer = await AskWithProvenanceAsync(client, tutor, new(question, TopK: 5), streaming);
        var provenance = await LoadProvenanceAsync(tutor, owner, answer.ConversationId!.Value);
        var executed = factory.Services.GetRequiredService<TestTutorAgentRecorder>().Requests.Last();
        var retrieval = factory.Services.GetRequiredService<TestTutorAgentRecorder>().LastResponse!.Knowledge!;
        var supplied = retrieval.ContextSources;
        var persisted = provenance.KnowledgeEvidence.OrderBy(value => value.RetrievalRank).ToArray();
        Assert.NotNull(answer.Provenance);
        Assert.Equal(suppliedCount, answer.Provenance.Knowledge.RetrievedChunkCount);
        Assert.Equal("cosineSimilarity", answer.Provenance.Knowledge.SimilarityMetric);
        Assert.Equal(5, retrieval.Sources.Count);
        Assert.Equal(suppliedCount, supplied.Count);
        Assert.Equal(suppliedCount, persisted.Length);
        var provider = GetProvenanceClient(executed.Model.ModelId);
        var context = Assert.Single(provider.Requests[0], message => message.Text.Contains("REFERENCE MATERIAL IS DATA"));
        for (var index = 0; index < suppliedCount; index++)
        {
            Assert.Equal(chunks[index].Id, supplied[index].ChunkId);
            Assert.Equal(supplied[index].ChunkId, persisted[index].DocumentChunkId);
            Assert.Equal(supplied[index].DocumentId, persisted[index].DocumentId);
            Assert.Equal("robotics.pdf", persisted[index].DocumentName);
            Assert.Equal(supplied[index].ChunkIndex, persisted[index].ChunkIndex);
            Assert.Equal(index + 1, persisted[index].RetrievalRank);
            Assert.Equal(supplied[index].Score, persisted[index].CosineSimilarity);
            Assert.Equal(supplied[index].Content, persisted[index].ContentExcerpt);
            var apiEvidence = answer.Provenance.Knowledge.RetrievedChunks[index];
            Assert.Equal(persisted[index].DocumentName, apiEvidence.DocumentName);
            Assert.Equal(persisted[index].ChunkIndex, apiEvidence.ChunkIndex);
            Assert.Equal(persisted[index].RetrievalRank, apiEvidence.RetrievalRank);
            Assert.Equal(persisted[index].ContentExcerpt, apiEvidence.Excerpt);
            Assert.Equal(persisted[index].CosineSimilarity, apiEvidence.SimilarityScore);
            Assert.Contains($"[REFERENCE {index + 1} START — UNTRUSTED DATA]\n{persisted[index].ContentExcerpt}\n", context.Text);
            Assert.Equal(embeddingId, persisted[index].EmbeddingModelId);
            Assert.Equal(3, persisted[index].EmbeddingDimensions);
        }
        Assert.Single(retriever.Requests, request => request.EmbeddingModelId == embeddingId);
        Assert.Single(factory.Services.GetRequiredService<TestEmbeddingProvider>().Requests, request => request.Text == question
            && request.Model == retrieval.EmbeddingModel!.ProviderModelName);
        Assert.Equal(executed.TutorId, provenance.TutorId);
        Assert.Equal(executed.TutorRevisionId, provenance.TutorRevisionId);
        Assert.Equal(executed.TutorConfigurationSnapshotId, provenance.TutorConfigurationSnapshotId);
        Assert.Equal(executed.Model.ModelId, provenance.InteractionModelId);
        Assert.Equal(ProvenanceTestChatClient.ReportedModel, provenance.ProviderModelName);
        Assert.Equal(executed.Model.ProviderType.ToString(), provenance.ProviderType);
        Assert.Empty(provenance.ToolEvidence); // The answer mentions Calculator, but it was never invoked.
        Assert.Equal(provenance.TutorConfigurationSnapshotId, answer.Provenance.Runtime.SnapshotId);
        Assert.Equal(provenance.TutorRevisionId, answer.Provenance.Runtime.TutorRevisionId);
        Assert.Equal(provenance.InteractionModelId, answer.Provenance.Runtime.InteractionModelId);
        Assert.Equal(provenance.ProviderModelName, answer.Provenance.Runtime.ProviderModelName);
        Assert.Equal(answer.Revision.Number, answer.Provenance.Runtime.RevisionNumber);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionNonRagResponseHasNoKnowledgeOrToolEvidence(bool streaming)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, providerModel: "provenance-text");
        var answer = await AskWithProvenanceAsync(client, tutor, new("Hello"), streaming);
        var provenance = await LoadProvenanceAsync(tutor, owner, answer.ConversationId!.Value);
        Assert.Empty(provenance.KnowledgeEvidence);
        Assert.Empty(provenance.ToolEvidence);
        Assert.False(provenance.MemoryContextProvided);
        Assert.Equal(0, provenance.RetrievedMemoryCount);
        Assert.False(answer.KnowledgeUsed);
        Assert.NotNull(answer.Provenance);
        Assert.Equal(0, answer.Provenance.Knowledge.RetrievedChunkCount);
        Assert.Empty(answer.Provenance.Knowledge.RetrievedChunks);
        Assert.DoesNotContain(GetProvenanceClient(provenance.InteractionModelId).Requests[0],
            message => message.Text.Contains("REFERENCE MATERIAL IS DATA"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProductionAgentFrameworkCalculatorPersistsActualSuccessOrFailure(bool streaming, bool fail)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, providerModel: fail ? "provenance-tool-failure" : "provenance-tool-success");
        tutor = await ActivateCalculatorRevisionAsync(client, tutor);
        var answer = await AskWithProvenanceAsync(client, tutor, new("Calculate"), streaming);
        var provenance = await LoadProvenanceAsync(tutor, owner, answer.ConversationId!.Value);
        var tool = Assert.Single(provenance.ToolEvidence);
        Assert.Equal("Calculator", tool.ToolName);
        Assert.Equal(fail ? TutorToolExecutionStatus.Failed : TutorToolExecutionStatus.Succeeded, tool.Status);
        Assert.Equal(fail ? "ExecutionFailed" : null, tool.ErrorCategory);
        var apiTool = Assert.Single(answer.Provenance!.Tools);
        Assert.Equal(tool.ToolName, apiTool.ToolName);
        Assert.Equal(tool.Status.ToString(), apiTool.Status);
        Assert.Equal(!fail, apiTool.Succeeded);
        Assert.Equal(tool.DurationMs, apiTool.DurationMs);
        Assert.Equal(tool.ErrorCategory, apiTool.ErrorCategory);
        Assert.True(tool.DurationMs >= 0);
        Assert.True(tool.CompletedUtc >= tool.StartedUtc);
        var provider = GetProvenanceClient(provenance.InteractionModelId);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Single(provider.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>());
        Assert.DoesNotContain("expression", JsonSerializer.Serialize(provenance));
        Assert.DoesNotContain("Calculated result", JsonSerializer.Serialize(provenance));
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    public async Task ProductionMemoryProvenanceMatchesActualSuppliedContext(bool streaming, bool enabled, int memoryCount)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        var embeddingId = await GetQueryEmbeddingIdAsync(tutor);
        var memories = factory.Services.GetRequiredService<TestTutorMemoryStore>();
        for (var index = 0; index < memoryCount; index++)
            memories.Memories.Enqueue(TutorMemory.Create(owner, tutor.TutorId, null, tutor.RevisionId, tutor.SnapshotId,
                "Old question", "Old answer", $"private-memory-context-{index}", [1, 0, 0], embeddingId, 3));
        var answer = await AskWithProvenanceAsync(client, tutor, new("Explain stored information", UseMemory: enabled), streaming);
        var provenance = await LoadProvenanceAsync(tutor, owner, answer.ConversationId!.Value);
        var count = enabled ? memoryCount : 0;
        Assert.Equal(count, provenance.RetrievedMemoryCount);
        Assert.Equal(count > 0, provenance.MemoryContextProvided);
        Assert.Equal(count > 0, answer.MemoryUsed);
        Assert.Equal(count > 0, answer.Provenance!.Memory.ContextProvided);
        Assert.Equal(count, answer.Provenance.Memory.RetrievedItemCount);
        var provider = GetProvenanceClient(provenance.InteractionModelId);
        Assert.Equal(count > 0, provider.Requests[0].Any(message => message.Text.Contains("LONG-TERM TUTOR MEMORY")));
        Assert.Equal(enabled ? 1 : 0, memories.Requests.Count(request => request.TutorId == tutor.TutorId));
        Assert.DoesNotContain("private-memory-context", JsonSerializer.Serialize(provenance));
    }

    [Fact]
    public async Task ProductionMemoryProvenanceCountsSuppliedItemsRatherThanAllRetrievedCandidates()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        var embeddingId = await GetQueryEmbeddingIdAsync(tutor);
        var memories = factory.Services.GetRequiredService<TestTutorMemoryStore>();
        for (var index = 0; index < 5; index++)
            memories.Memories.Enqueue(TutorMemory.Create(owner, tutor.TutorId, null, tutor.RevisionId, tutor.SnapshotId,
                "Old question", "Old answer", new string((char)('A' + index), 2_000), [1, 0, 0], embeddingId, 3));
        var answer = await AskWithProvenanceAsync(client, tutor, new("Explain memory", UseMemory: true), streaming: true);
        var provenance = await LoadProvenanceAsync(tutor, owner, answer.ConversationId!.Value);
        Assert.Equal(5, factory.Services.GetRequiredService<TestTutorAgentRecorder>().LastResponse!.Memory!.Sources.Count);
        Assert.True(provenance.MemoryContextProvided);
        Assert.Equal(3, provenance.RetrievedMemoryCount);
        var supplied = Assert.Single(GetProvenanceClient(provenance.InteractionModelId).Requests[0],
            message => message.Text.Contains("LONG-TERM TUTOR MEMORY"));
        Assert.Contains(new string('C', 100), supplied.Text);
        Assert.DoesNotContain(new string('D', 100), supplied.Text);
    }

    [Fact]
    public async Task ProductionFollowupAfterChunkAndCatalogChangesPreservesEarlierEvidence()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        var embeddingId = await GetQueryEmbeddingIdAsync(tutor);
        var original = new KnowledgeChunk { DocumentId = tutor.DocumentId!.Value, ChunkIndex = 0,
            Content = "Original excerpt", EmbeddingModelId = embeddingId, EmbeddingDimensions = 3, Embedding = [1, 0, 0] };
        var retriever = factory.Services.GetRequiredService<TestKnowledgeRetriever>();
        retriever.DocumentChunks[tutor.DocumentId.Value] = [original];
        var first = await AskWithProvenanceAsync(client, tutor, new("First question"), streaming: false);
        var originalProvenance = await LoadProvenanceAsync(tutor, owner, first.ConversationId!.Value);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Documents.FindAsync(tutor.DocumentId.Value))!.Name = "Reuploaded document";
            var model = (await db.InteractionModels.FindAsync(originalProvenance.InteractionModelId))!;
            model.Name = "Changed catalog display";
            model.ProviderModelName = "Changed catalog provider model";
            await db.SaveChangesAsync();
        }
        var replacement = new KnowledgeChunk { DocumentId = tutor.DocumentId.Value, ChunkIndex = 0,
            Content = "Regenerated excerpt", EmbeddingModelId = embeddingId, EmbeddingDimensions = 3, Embedding = [1, 0, 0] };
        retriever.DocumentChunks[tutor.DocumentId.Value] = [replacement];
        await AskWithProvenanceAsync(client, tutor, new("Follow-up question", ConversationId: first.ConversationId), streaming: true);
        using var readScope = factory.Services.CreateScope();
        var durable = await readScope.ServiceProvider.GetRequiredService<ITutorConversationRepository>()
            .GetAsync(first.ConversationId.Value, owner, tutor.TutorId, default);
        var messages = durable!.Messages.Where(value => value.Role == "assistant").OrderBy(value => value.Sequence).ToArray();
        Assert.Equal(2, messages.Length);
        Assert.Equal(originalProvenance.CapturedUtc, messages[0].Provenance!.CapturedUtc);
        var storedOriginal = Assert.Single(messages[0].Provenance!.KnowledgeEvidence);
        Assert.Equal(original.Id, storedOriginal.DocumentChunkId);
        Assert.Equal("Original excerpt", storedOriginal.ContentExcerpt);
        Assert.Equal("robotics.pdf", storedOriginal.DocumentName);
        var storedNew = Assert.Single(messages[1].Provenance!.KnowledgeEvidence);
        Assert.Equal(replacement.Id, storedNew.DocumentChunkId);
        Assert.Equal("Regenerated excerpt", storedNew.ContentExcerpt);
        Assert.Equal("Reuploaded document", storedNew.DocumentName);
        Assert.All(messages, message =>
        {
            Assert.Equal(tutor.SnapshotId, message.Provenance!.TutorConfigurationSnapshotId);
            Assert.Equal(tutor.RevisionId, message.Provenance.TutorRevisionId);
            Assert.Equal(ProvenanceTestChatClient.ReportedModel, message.Provenance.ProviderModelName);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionProviderFailureAfterContextDoesNotCommitMessagesOrProvenance(bool streaming)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-generation-failure");
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var conversation = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!;
        using var response = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask{(streaming ? "/stream" : "")}",
            new AskTutorRuntimeRequest("Generate from context", ConversationId: conversation.Id));
        var body = await response.Content.ReadAsStringAsync();
        if (streaming)
        {
            var events = ParseSse(body);
            Assert.Contains(events, value => value.Type == "message.delta");
            Assert.Contains(events, value => value.Type == "error");
            Assert.DoesNotContain(events, value => value.Type == "message.completed");
        }
        else Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain("private-provider-body", body);
        using var scope = factory.Services.CreateScope();
        var durable = await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().GetAsync(conversation.Id, owner, tutor.TutorId, default);
        Assert.Empty(durable!.Messages);
        Assert.Null(durable.AgentSessionJson);
        Assert.Equal(0, durable.Version);
    }

    [Fact]
    public async Task ProductionStreamCancellationAfterDeltaDoesNotCommitProvenance()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-stream-cancel");
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var conversation = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!;
        using var cancellation = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/runtime/tutors/{tutor.Slug}/ask/stream")
        { Content = JsonContent.Create(new AskTutorRuntimeRequest("Explain context", ConversationId: conversation.Id)) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));
        string? line;
        do { line = await reader.ReadLineAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
        while (line is not null && line != "event: message.delta");
        Assert.Equal("event: message.delta", line);
        var modelId = factory.Services.GetRequiredService<TestTutorAgentRecorder>().Requests.Last().Model.ModelId;
        var provider = GetProvenanceClient(modelId);
        await provider.PartialResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        response.Dispose();
        await provider.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var scope = factory.Services.CreateScope();
        var durable = await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().GetAsync(conversation.Id, owner, tutor.TutorId, default);
        Assert.Empty(durable!.Messages);
        Assert.Equal(0, durable.Version);
    }

    [Fact]
    public async Task ProductionBenchmarkAdapterUsesSameAgentRetrievalWithoutCreatingConversation()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, providerModel: "provenance-text");
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ITutorBenchmarkOutputGenerator>().GenerateAsync(
            new TutorBenchmarkGenerationRequest(tutor.TutorId, tutor.RevisionId, tutor.SnapshotId,
                "Explain robotics", null, true, 5, owner));
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(tutor.RevisionId, result.Value.TutorRevisionId);
        Assert.Equal(tutor.SnapshotId, result.Value.TutorConfigurationSnapshotId);
        Assert.Equal(1, result.Value.SourceCount);
        Assert.Contains("Bounded source content", result.Value.ContextText);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().ListAsync(owner, tutor.TutorId, default));
    }

    private ProvenanceTestChatClient GetProvenanceClient(Guid modelId) =>
        ((TestRuntimeChatClientFactory)factory.Services.GetRequiredService<IRuntimeChatClientFactory>()).ProvenanceClients[modelId];

    private async Task<Guid> GetQueryEmbeddingIdAsync(ConfiguredTutor tutor)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().TutorRevisions.FindAsync(tutor.RevisionId))!.QueryEmbeddingModelId!.Value;
    }

    private async Task<TutorConversationMessageProvenance> LoadProvenanceAsync(ConfiguredTutor tutor, Guid owner, Guid conversationId)
    {
        using var scope = factory.Services.CreateScope();
        var conversation = await scope.ServiceProvider.GetRequiredService<ITutorConversationRepository>().GetAsync(conversationId, owner, tutor.TutorId, default);
        var message = Assert.Single(conversation!.Messages, value => value.Role == "assistant");
        Assert.NotNull(message.Provenance);
        Assert.Null(conversation.Messages.Single(value => value.Role == "user").Provenance);
        return message.Provenance;
    }

    private async Task<TutorRuntimeAskResponse> AskWithProvenanceAsync(HttpClient client, ConfiguredTutor tutor, AskTutorRuntimeRequest request, bool streaming)
    {
        using var response = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask{(streaming ? "/stream" : "")}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TutorRuntimeAskResponse answer;
        if (!streaming) answer = (await response.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        else
        {
            var body = await response.Content.ReadAsStringAsync();
            var events = ParseSse(body);
            Assert.DoesNotContain(events, value => value.Type == "error");
            Assert.All(events.Where(value => value.Type != "message.completed"), value => Assert.Null(value.Message));
            Assert.Equal(1, body.Split("\"provenance\"", StringSplitOptions.None).Length - 1);
            answer = Assert.Single(events, value => value.Type == "message.completed").Message!;
        }
        var history = (await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{answer.ConversationId}"))!;
        var historical = history.Messages.Last(message => message.Role == "assistant").Provenance;
        Assert.Equal(JsonSerializer.Serialize(answer.Provenance), JsonSerializer.Serialize(historical));
        Assert.All(history.Messages.Where(message => message.Role == "user"), message => Assert.Null(message.Provenance));
        return answer;
    }

    private async Task<ConfiguredTutor> ActivateCalculatorRevisionAsync(HttpClient client, ConfiguredTutor tutor)
    {
        var root = $"/api/tutors/{tutor.TutorId}";
        var original = (await client.GetFromJsonAsync<TutorRevisionResponse>($"{root}/revisions/{tutor.RevisionId}"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.InteractionModels.FindAsync(original.InteractionModelId))!.Capabilities |= ModelCapabilities.FunctionCalling;
            await db.SaveChangesAsync();
        }
        using var registered = await client.PostAsJsonAsync($"{root}/tools", new RegisterToolDefinitionRequest("Calculator", null,
            ImplementationKey: "builtin.calculator.v1", ExecutionPolicy: TutorToolExecutionPolicy.ReadOnlyAutomatic));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var tool = (await registered.Content.ReadFromJsonAsync<ToolDefinitionResponse>())!;
        using var created = await client.PostAsync($"{root}/revisions", null);
        var draft = (await created.Content.ReadFromJsonAsync<TutorRevisionResponse>())!;
        var revisionRoot = $"{root}/revisions/{draft.Id}";
        using var configured = await client.PutAsJsonAsync(revisionRoot, new UpdateDraftRevisionRequest(original.PromptVersionId,
            original.InteractionModelId, original.QueryEmbeddingModelId, null, DeploymentTarget.Local, null, [tool.Id]));
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        using var published = await client.PostAsync($"{revisionRoot}/publish", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using var activated = await client.PostAsync($"{revisionRoot}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var active = (await activated.Content.ReadFromJsonAsync<TutorRevisionResponse>())!;
        return tutor with { RevisionId = active.Id, SnapshotId = active.LatestSnapshotId!.Value, Slug = active.ActivationSlug! };
    }
}
