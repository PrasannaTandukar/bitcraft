using System.Text.Json;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Infrastructure.Agents;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public sealed class TutorMessageProvenanceTests
{
    [Fact]
    public void CaptureStoresOnlySuppliedContextAndSafeExecutionMetadata()
    {
        var request = TutorAgentExecutorTests.Request() with { Instructions = "private-system-instructions" };
        var documentId = Guid.NewGuid();
        var first = new TutorRuntimeSource(documentId, "Original name", Guid.NewGuid(), 0, 0.95, "First excerpt");
        var second = new TutorRuntimeSource(documentId, "Original name", Guid.NewGuid(), 1, 0.85, "Second excerpt");
        var omitted = new TutorRuntimeSource(documentId, "Original name", Guid.NewGuid(), 2, 0.75, "budgeted-out-secret");
        var started = DateTime.UtcNow;
        var response = new TutorAgentResponse("Answer", request.Model.ProviderType, "actual-provider-model",
            new TutorAgentUsage(100, null, null, null), new TutorAgentDiagnostics(false),
            new TutorKnowledgeResult(new(Guid.NewGuid(), "Embedding", "embed-model", 768, Guid.NewGuid(), "Runtime"),
                [first, second, omitted], "context") { ContextSources = [first, second] },
            new TutorMemoryResult(false, null,
                [new(Guid.NewGuid(), request.TutorId, null, null, null, 0.9, "private-memory-content")])
                { ContextMemoryCount = 1 },
            [new("calculator", started, started.AddMilliseconds(10), TutorToolCallStatus.Succeeded,
                10, "private-tool-result-summary", null),
             new("lookup", started, started.AddMilliseconds(20), TutorToolCallStatus.Failed,
                20, null, "ExecutionFailed")], "private-session");

        var provenance = TutorMessageProvenanceCapture.Capture(request, response);

        Assert.Equal(request.TutorId, provenance.TutorId);
        Assert.Equal(request.TutorRevisionId, provenance.TutorRevisionId);
        Assert.Equal(request.TutorConfigurationSnapshotId, provenance.TutorConfigurationSnapshotId);
        Assert.Equal(request.Model.ModelId, provenance.InteractionModelId);
        Assert.Equal("actual-provider-model", provenance.ProviderModelName);
        Assert.Equal(DateTimeKind.Utc, provenance.CapturedUtc.Kind);
        Assert.Equal(0, provenance.CapturedUtc.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(new[] { 1, 2 }, provenance.KnowledgeEvidence.Select(value => value.RetrievalRank));
        Assert.Equal(new double?[] { 0.95, 0.85 }, provenance.KnowledgeEvidence.Select(value => value.CosineSimilarity));
        Assert.Equal(new[] { first.ChunkId, second.ChunkId }, provenance.KnowledgeEvidence.Select(value => value.DocumentChunkId));
        Assert.All(provenance.KnowledgeEvidence, value => Assert.Equal(768, value.EmbeddingDimensions));
        Assert.True(provenance.MemoryContextProvided);
        Assert.Equal(1, provenance.RetrievedMemoryCount);
        Assert.Equal(new[] { TutorToolExecutionStatus.Succeeded, TutorToolExecutionStatus.Failed },
            provenance.ToolEvidence.Select(value => value.Status));
        Assert.Equal("ExecutionFailed", provenance.ToolEvidence[1].ErrorCategory);
        var json = JsonSerializer.Serialize(provenance);
        Assert.DoesNotContain("private-", json);
        Assert.DoesNotContain("budgeted-out", json);
    }

    [Fact]
    public void EmptyContextRecordsNoMemoryContributionOrTools()
    {
        var request = TutorAgentExecutorTests.Request();
        var response = new TutorAgentResponse("Answer", request.Model.ProviderType, request.Model.ProviderModelName,
            new(1, null, null, null), new(false), Memory: new(true, null, []));
        var provenance = TutorMessageProvenanceCapture.Capture(request, response);
        Assert.False(provenance.MemoryContextProvided);
        Assert.Equal(0, provenance.RetrievedMemoryCount);
        Assert.Empty(provenance.KnowledgeEvidence);
        Assert.Empty(provenance.ToolEvidence);
    }

    [Fact]
    public void ConversationRejectsProvenanceFromAnotherSnapshotWithoutAddingMessages()
    {
        var conversation = TutorConversation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var forged = TutorConversationMessageProvenance.Capture(conversation.TutorId, conversation.TutorRevisionId,
            Guid.NewGuid(), Guid.NewGuid(), "model", "Ollama", [], [], 0);
        Assert.Throws<ArgumentException>(() => conversation.CompleteTurn("{}", "Q", null, "A", "model", "Ollama", forged));
        Assert.Empty(conversation.Messages);
        Assert.Equal(0, conversation.Version);
    }

    [Fact]
    public void EvidenceBoundsExcerptsAndExcludesArbitraryErrorText()
    {
        var knowledge = TutorRetrievedKnowledgeEvidence.Capture(Guid.NewGuid(), "Doc", Guid.NewGuid(), 0,
            new string('X', 10_000), 1, null, null, null, null);
        Assert.Equal(TutorRetrievedKnowledgeEvidence.MaximumExcerptCharacters, knowledge.ContentExcerpt.Length);
        Assert.Null(knowledge.CosineSimilarity);
        var now = DateTime.UtcNow;
        var tool = TutorToolExecutionEvidence.Capture("tool", TutorToolExecutionStatus.Failed, now, now, 0, "secret exception");
        Assert.Null(tool.ErrorCategory);
    }

    [Fact]
    public void ProvenanceModelHasNoPromptReasoningOrSensitivePayloadFields()
    {
        var forbidden = new[] { "Instructions", "SystemPrompt", "PromptContent", "ChainOfThought", "Reasoning",
            "Arguments", "Result", "ResultSummary", "ApiKey", "ApiKeySecretRef", "AgentSessionJson", "MemoryContent" };
        foreach (var type in new[] { typeof(TutorConversationMessageProvenance), typeof(TutorRetrievedKnowledgeEvidence), typeof(TutorToolExecutionEvidence) })
            Assert.DoesNotContain(type.GetProperties(), property => forbidden.Contains(property.Name));
    }
}
