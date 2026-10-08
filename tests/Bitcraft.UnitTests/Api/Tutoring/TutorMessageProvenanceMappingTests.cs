using System.Text.Json;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Tutoring;
using Xunit;
using Xunit.Abstractions;
using ApiProvenance = Bitcraft.Api.Contracts.Tutoring.TutorMessageProvenanceResponse;

namespace Bitcraft.UnitTests.Api.Tutoring;

public sealed class TutorMessageProvenanceMappingTests(ITestOutputHelper output)
{
    [Fact]
    public void MapsHistoricalNamesExactScoresSortedRanksMemoryAndPinnedRuntime()
    {
        var provenance = ExampleEvidence();
        var mapped = TutorMessageProvenancePresentation.Map(provenance, 7)!.ToResponse();
        var chunk = Assert.Single(mapped.Knowledge.RetrievedChunks);
        Assert.Equal("lesson.md", chunk.DocumentName);
        Assert.Equal(2, chunk.ChunkIndex);
        Assert.Equal(1, chunk.RetrievalRank);
        Assert.Equal("A motor driver controls power to the motor.", chunk.Excerpt);
        Assert.Equal(0.83, chunk.SimilarityScore);
        Assert.Equal("cosineSimilarity", mapped.Knowledge.SimilarityMetric);
        Assert.True(mapped.Memory.ContextProvided);
        Assert.Equal(2, mapped.Memory.RetrievedItemCount);
        var tool = Assert.Single(mapped.Tools);
        Assert.True(tool.Succeeded);
        Assert.Equal("Succeeded", tool.Status);
        Assert.Equal("Calculator", tool.ToolName);
        Assert.Equal(12, tool.DurationMs);
        Assert.Equal(provenance.TutorId, mapped.Runtime.TutorId);
        Assert.Equal(provenance.TutorRevisionId, mapped.Runtime.TutorRevisionId);
        Assert.Equal(7, mapped.Runtime.RevisionNumber);
        Assert.Equal(provenance.TutorConfigurationSnapshotId, mapped.Runtime.SnapshotId);
        Assert.Equal(provenance.InteractionModelId, mapped.Runtime.InteractionModelId);
        Assert.Equal("example-chat-model", mapped.Runtime.ProviderModelName);
        Assert.Equal("ExampleProvider", mapped.Runtime.ProviderType);
        Assert.Equal(provenance.CapturedUtc, mapped.CapturedUtc);
    }

    [Fact]
    public void NullHistoricalEvidenceAndUnknownRevisionRemainNull()
    {
        Assert.Null(TutorMessageProvenancePresentation.Map(null, 7));
        Assert.Null(TutorMessageProvenancePresentation.Map(ExampleEvidence(), null)!.Runtime.RevisionNumber);
    }

    [Fact]
    public void NullScoreAndMultipleChunksPreserveMetadataWithoutFabricatedPagesOrPercentages()
    {
        var exemplar = ExampleEvidence();
        var documentId = Guid.NewGuid();
        var second = TutorRetrievedKnowledgeEvidence.Capture(documentId, "Notes", Guid.NewGuid(), 9, "Later chunk", 2, null, null, null, null);
        var first = TutorRetrievedKnowledgeEvidence.Capture(documentId, "Notes", Guid.NewGuid(), 3, "Earlier chunk", 1, 0.91, null, null, null);
        var value = TutorConversationMessageProvenance.Capture(exemplar.TutorId, exemplar.TutorRevisionId,
            exemplar.TutorConfigurationSnapshotId, exemplar.InteractionModelId, exemplar.ProviderModelName, exemplar.ProviderType,
            [second, first], [], 0);
        var mapped = TutorMessageProvenancePresentation.Map(value, 7)!.ToResponse();
        Assert.Equal(2, mapped.Knowledge.RetrievedChunkCount);
        Assert.Equal(new[] { 1, 2 }, mapped.Knowledge.RetrievedChunks.Select(chunk => chunk.RetrievalRank));
        Assert.Equal(0.91, mapped.Knowledge.RetrievedChunks[0].SimilarityScore);
        Assert.Null(mapped.Knowledge.RetrievedChunks[1].SimilarityScore);
        Assert.All(mapped.Knowledge.RetrievedChunks, chunk => Assert.Equal("document-1", chunk.DocumentGroupKey));
        var json = JsonSerializer.Serialize(mapped, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("page", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("section", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("91%", json);
    }

    [Fact]
    public void DocumentGroupingIsMessageScopedStableAndDoesNotMergeSameNameDocuments()
    {
        var exemplar = ExampleEvidence();
        var document = Guid.NewGuid();
        var anotherDocument = Guid.NewGuid();
        var evidence = new[] {
            TutorRetrievedKnowledgeEvidence.Capture(anotherDocument, "notes.md", Guid.NewGuid(), 0, "Third", 3, null, null, null, null),
            TutorRetrievedKnowledgeEvidence.Capture(document, "notes.md", Guid.NewGuid(), 0, "First", 1, null, null, null, null),
            TutorRetrievedKnowledgeEvidence.Capture(document, "notes.md", Guid.NewGuid(), 1, "Second", 2, null, null, null, null) };
        var value = TutorConversationMessageProvenance.Capture(exemplar.TutorId, exemplar.TutorRevisionId,
            exemplar.TutorConfigurationSnapshotId, exemplar.InteractionModelId, exemplar.ProviderModelName, exemplar.ProviderType,
            evidence, [], 0);
        var mapped = TutorMessageProvenancePresentation.Map(value, 7)!.ToResponse();
        Assert.Equal(new[] { "document-1", "document-1", "document-2" },
            mapped.Knowledge.RetrievedChunks.Select(chunk => chunk.DocumentGroupKey));
        Assert.Equal(mapped.Knowledge.RetrievedChunks,
            TutorMessageProvenancePresentation.Map(value, 7)!.ToResponse().Knowledge.RetrievedChunks);
        var json = JsonSerializer.Serialize(mapped);
        Assert.DoesNotContain(document.ToString(), json);
        Assert.DoesNotContain(anotherDocument.ToString(), json);
    }

    [Theory]
    [InlineData(TutorToolExecutionStatus.Succeeded, null, true)]
    [InlineData(TutorToolExecutionStatus.Failed, "ExecutionFailed", false)]
    [InlineData(TutorToolExecutionStatus.TimedOut, "Timeout", false)]
    public void ToolDtoTruthfullyMapsSuccessFailureAndTimeout(TutorToolExecutionStatus status, string? category, bool succeeded)
    {
        var exemplar = ExampleEvidence();
        var started = DateTime.UtcNow;
        var value = TutorConversationMessageProvenance.Capture(exemplar.TutorId, exemplar.TutorRevisionId,
            exemplar.TutorConfigurationSnapshotId, exemplar.InteractionModelId, exemplar.ProviderModelName, exemplar.ProviderType,
            [], [TutorToolExecutionEvidence.Capture("Calculator", status, started, started.AddMilliseconds(12), 12, category)], 0);
        var mapped = Assert.Single(TutorMessageProvenancePresentation.Map(value, 7)!.ToResponse().Tools);
        Assert.Equal(status.ToString(), mapped.Status);
        Assert.Equal(succeeded, mapped.Succeeded);
        Assert.Equal(category, mapped.ErrorCategory);
    }

    [Fact]
    public void DtoGraphContainsOnlyAllowlistedMetadataWithoutSourceLinksOrPrivateConfiguration()
    {
        var types = new[] { typeof(ApiProvenance), typeof(Bitcraft.Api.Contracts.Tutoring.TutorKnowledgeProvenanceResponse),
            typeof(Bitcraft.Api.Contracts.Tutoring.TutorRetrievedChunkEvidenceResponse),
            typeof(Bitcraft.Api.Contracts.Tutoring.TutorMemoryProvenanceResponse),
            typeof(Bitcraft.Api.Contracts.Tutoring.TutorToolProvenanceResponse),
            typeof(Bitcraft.Api.Contracts.Tutoring.TutorGenerationProvenanceResponse) };
        var forbidden = new[] { "Prompt", "Instructions", "Reasoning", "Thought", "Secret", "ApiKey", "Endpoint",
            "ConnectionString", "Arguments", "Result", "Session", "SourcePath", "Url", "DocumentId", "ChunkId", "MemoryContent" };
        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), property => forbidden.Any(name =>
                property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain(type.GetProperties(), property => property.PropertyType.Assembly == typeof(Tutor).Assembly);
        }
    }

    [Fact]
    public void SerializesExampleUsingActualApiContractWithFakeValuesOnly()
    {
        var mapped = TutorMessageProvenancePresentation.Map(ExampleEvidence(), 7)! with
            { CapturedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var dto = mapped.ToResponse();
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        var restored = JsonSerializer.Deserialize<ApiProvenance>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(dto.Runtime, restored.Runtime);
        Assert.Equal(dto.Knowledge.RetrievedChunks, restored.Knowledge.RetrievedChunks);
        output.WriteLine(json);
    }

    private static TutorConversationMessageProvenance ExampleEvidence()
    {
        var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return TutorConversationMessageProvenance.Capture(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000004"), "example-chat-model", "ExampleProvider",
            [TutorRetrievedKnowledgeEvidence.Capture(Guid.NewGuid(), "lesson.md", Guid.NewGuid(), 2,
                "A motor driver controls power to the motor.", 1, 0.83, null, null, null)],
            [TutorToolExecutionEvidence.Capture("Calculator", TutorToolExecutionStatus.Succeeded,
                started, started.AddMilliseconds(12), 12, null)], 2);
    }
}
