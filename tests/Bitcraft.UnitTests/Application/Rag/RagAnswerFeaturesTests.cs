using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Rag;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Rag;

public class RagAnswerFeaturesTests
{
    [Fact]
    public async Task AskRagQuestionHandler_WithEmptyQuestion_ReturnsFailure()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand(" ", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Rag.QuestionRequired", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task AskRagQuestionHandler_WithInvalidTopK_ReturnsFailure(int topK)
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand("Explain Docker", null, topK),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Rag.TopKInvalid", result.Error.Code);
    }

    [Fact]
    public async Task AskRagQuestionHandler_WithMatchingChunks_CallsPipelineInOrder()
    {
        var calls = new List<string>();
        var ownerId = Guid.NewGuid();
        var document = CreateOwnedDocument(ownerId, "Docker Notes");
        var chunk = new KnowledgeChunk
        {
            DocumentId = document.Id,
            ChunkIndex = 0,
            Content = "Docker Compose coordinates multiple containers.",
            Embedding = [1f, 0f]
        };
        var documentStore = new RecordingDocumentStore(calls, [document]);
        var embeddingProvider = new RecordingEmbeddingProvider(calls, [1f, 0f]);
        var retriever = new RecordingKnowledgeRetriever(calls, [chunk]);
        var generationProvider = new RecordingGenerationProvider(calls);
        var handler = CreateHandler(documentStore, embeddingProvider, retriever, generationProvider, ownerId);

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand(" Explain Docker Compose ", " llama3.2 ", 3),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["documents", "embedding", "search", "generation"], calls);
        Assert.Equal([document.Id], retriever.DocumentIds);
        Assert.Equal(3, retriever.TopK);
        Assert.Contains("[Source 1", generationProvider.Prompt);
        Assert.Contains("Explain Docker Compose", generationProvider.Prompt);
    }

    [Fact]
    public async Task AskRagQuestionHandler_WithNoContext_ReturnsFriendlyAnswerWithoutGeneration()
    {
        var calls = new List<string>();
        var ownerId = Guid.NewGuid();
        var document = CreateOwnedDocument(ownerId, "Empty Notes");
        var generationProvider = new RecordingGenerationProvider(calls);
        var handler = CreateHandler(
            new RecordingDocumentStore(calls, [document]),
            new RecordingEmbeddingProvider(calls, [1f, 0f]),
            new RecordingKnowledgeRetriever(calls, []),
            generationProvider,
            ownerId);

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand("Explain entropy", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("I could not find relevant information in the uploaded documents.", result.Value.Answer);
        Assert.Empty(result.Value.Sources);
        Assert.Equal(["documents", "embedding", "search"], calls);
        Assert.Null(generationProvider.Prompt);
    }

    [Fact]
    public async Task AskRagQuestionHandler_WhenGenerationFails_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var document = CreateOwnedDocument(ownerId, "Docker Notes");
        var handler = CreateHandler(
            new RecordingDocumentStore([], [document]),
            new RecordingEmbeddingProvider([], [1f, 0f]),
            new RecordingKnowledgeRetriever([],
            [
                new KnowledgeChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = 0,
                    Content = "Docker Compose coordinates multiple containers.",
                    Embedding = [1f, 0f]
                }
            ]),
            new RecordingGenerationProvider([], Result.Failure<LlmGenerationResult>(new Error("Ollama.Unavailable", "offline"))),
            ownerId);

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand("Explain Docker", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.Unavailable", result.Error.Code);
    }

    [Fact]
    public async Task AskRagQuestionHandler_WithMatchingChunks_ReturnsAnswerAndSources()
    {
        var ownerId = Guid.NewGuid();
        var document = CreateOwnedDocument(ownerId, "Docker Notes");
        var chunk = new KnowledgeChunk
        {
            DocumentId = document.Id,
            ChunkIndex = 2,
            Content = "Docker Compose defines services in YAML.",
            Embedding = [1f, 0f]
        };
        var handler = CreateHandler(
            new RecordingDocumentStore([], [document]),
            new RecordingEmbeddingProvider([], [1f, 0f]),
            new RecordingKnowledgeRetriever([], [chunk]),
            new RecordingGenerationProvider([]),
            ownerId);

        var result = await handler.Handle(
            new RagAnswerFeatures.AskRagQuestionCommand("Explain Docker Compose", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Grounded answer", result.Value.Answer);
        Assert.Equal("llama3.2", result.Value.Model);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal(document.Id, source.DocumentId);
        Assert.Equal("Docker Notes", source.DocumentName);
        Assert.Equal(chunk.Id, source.ChunkId);
        Assert.Equal(2, source.ChunkIndex);
        Assert.Equal(chunk.Content, source.Content);
        Assert.Equal(1d, source.Score, precision: 6);
    }

    private static RagAnswerFeatures.AskRagQuestionHandler CreateHandler(
        IDocumentStore? documentStore = null,
        IEmbeddingProvider? embeddingProvider = null,
        IKnowledgeRetriever? knowledgeRetriever = null,
        ILlmGenerationProvider? generationProvider = null,
        Guid? ownerId = null)
    {
        var userId = ownerId ?? Guid.NewGuid();
        return new RagAnswerFeatures.AskRagQuestionHandler(
            documentStore ?? new RecordingDocumentStore([], []),
            embeddingProvider ?? new RecordingEmbeddingProvider([], [1f, 0f]),
            knowledgeRetriever ?? new RecordingKnowledgeRetriever([], []),
            generationProvider ?? new RecordingGenerationProvider([]),
            new FakeCurrentUserContext(userId));
    }

    private static Document CreateOwnedDocument(Guid ownerId, string name)
    {
        var document = new Document { Name = name };
        document.UpdateMetadata(ownerId);
        return document;
    }

    private sealed class RecordingDocumentStore(List<string> calls, IReadOnlyList<Document> documents) : IDocumentStore
    {
        public Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Document>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default)
        {
            calls.Add("documents");
            return Task.FromResult(documents.Where(document => document.CreatedById == creatorId).ToList());
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Document document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingEmbeddingProvider(List<string> calls, float[] embedding) : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default)
        {
            calls.Add("embedding");
            return Task.FromResult(embedding);
        }
    }

    private sealed class RecordingKnowledgeRetriever(List<string> calls, IReadOnlyList<KnowledgeChunk> chunks) : IKnowledgeRetriever
    {
        public IReadOnlyCollection<Guid> DocumentIds { get; private set; } = [];
        public int TopK { get; private set; }

        public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
            IReadOnlyCollection<Guid> documentIds,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            calls.Add("search");
            DocumentIds = documentIds;
            TopK = topK;
            return Task.FromResult(chunks.ToList());
        }
    }

    private sealed class RecordingGenerationProvider(
        List<string> calls,
        Result<LlmGenerationResult>? result = null) : ILlmGenerationProvider
    {
        public string? Prompt { get; private set; }

        public Task<Result<LlmGenerationResult>> GenerateAsync(
            string prompt,
            string? model,
            CancellationToken cancellationToken = default)
        {
            calls.Add("generation");
            Prompt = prompt;
            return Task.FromResult(result ?? Result.Success(new LlmGenerationResult(
                model ?? "llama3.2",
                "Grounded answer",
                Done: true)));
        }
    }
}
