using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public class TutorAskFeaturesTests
{
    [Fact]
    public async Task AskTutorQuestionHandler_WithEmptyQuestion_ReturnsValidationFailure()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand(" ", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.QuestionRequired", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task AskTutorQuestionHandler_WithInvalidTopK_ReturnsValidationFailure(int topK)
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Tell me about RAG", null, TopK: topK),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.TopKInvalid", result.Error.Code);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithGeneralChat_SkipsMemorySearchAndReturnsGeneralMode()
    {
        var retriever = new FakeKnowledgeRetriever();
        var memoryStore = new FakeTutorMemoryStore();
        var generation = new RecordingGenerationProvider();
        var handler = CreateHandler(knowledgeRetriever: retriever, tutorMemoryStore: memoryStore, generationProvider: generation);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Tell me about RAG", null, UseMemory: false, SaveToMemory: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("general", result.Value.Mode);
        Assert.False(result.Value.ContextUsed);
        Assert.False(result.Value.MemorySaved);
        Assert.Empty(result.Value.Sources);
        Assert.Equal(0, retriever.LastTopK);
        Assert.Equal(0, memoryStore.LastTopK);
        Assert.Contains("RAG means Retrieval-Augmented Generation", generation.Prompt);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithSaveOnly_SavesConversationMemory()
    {
        var memoryStore = new FakeTutorMemoryStore();
        var embedding = new RecordingEmbeddingProvider([1f, 0f]);
        var handler = CreateHandler(embeddingProvider: embedding, tutorMemoryStore: memoryStore);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand(
                "In this project, RAG means retrieval before generation.",
                null,
                UseMemory: false,
                SaveToMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.MemorySaved);
        var memory = Assert.Single(memoryStore.Memories);
        Assert.Contains("User asked:", memory.Content);
        Assert.Contains("Tutor answered:", memory.Content);
        Assert.Equal(1, embedding.Calls);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithSavedConversationMemory_UsesConversationSource()
    {
        var userId = Guid.NewGuid();
        var memoryStore = new FakeTutorMemoryStore();
        var memory = new TutorMemory
        {
            Question = "What does RAG mean here?",
            Answer = "Retrieval before generation.",
            Content = "User asked: What does RAG mean here? Tutor answered: Retrieval before generation.",
            Embedding = [1f, 0f]
        };
        memory.UpdateMetadata(userId);
        memoryStore.Memories.Add(memory);
        var handler = CreateHandler(
            ownerId: userId,
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f]),
            tutorMemoryStore: memoryStore);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("What did I say RAG means?", null, UseMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ContextUsed);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("conversation", source.SourceType);
        Assert.Equal(memory.Id, source.MemoryId);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WhenDocumentSearchFails_StillUsesConversationMemory()
    {
        var userId = Guid.NewGuid();
        var document = new Document { Name = "Broken document index" };
        document.UpdateMetadata(userId);
        var documentStore = new FakeDocumentStore();
        documentStore.Documents[document.Id] = document;
        var memoryStore = new FakeTutorMemoryStore();
        var memory = new TutorMemory
        {
            Question = "RAG means Retrieval-Augmented Generation in this project.",
            Answer = "RAG means Retrieval-Augmented Generation.",
            Content = "User asked: RAG means Retrieval-Augmented Generation in this project. Tutor answered: RAG means Retrieval-Augmented Generation.",
            Embedding = [1f, 0f]
        };
        memory.UpdateMetadata(userId);
        memoryStore.Memories.Add(memory);
        var handler = CreateHandler(
            ownerId: userId,
            documentStore: documentStore,
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f]),
            knowledgeRetriever: new ThrowingKnowledgeRetriever(),
            tutorMemoryStore: memoryStore);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("What does RAG mean in this project?", null, UseMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ContextUsed);
        Assert.DoesNotContain("could not be searched", result.Value.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("conversation", source.SourceType);
        Assert.Equal(memory.Id, source.MemoryId);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WhenConversationSearchFailsAndNoDocuments_ReportsSearchFailure()
    {
        var handler = CreateHandler(
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f]),
            tutorMemoryStore: new ThrowingTutorMemoryStore());

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("What does RAG mean?", null, UseMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ContextUsed);
        Assert.Empty(result.Value.Sources);
        Assert.Contains("Tutor memory could not be searched", result.Value.Message);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithMemoryEnabledButNoRelevantContext_StillGeneratesAnswer()
    {
        var memoryStore = new FakeTutorMemoryStore();
        var memory = new TutorMemory
        {
            Question = "Old topic",
            Answer = "Old answer",
            Content = "Old content",
            Embedding = [0f, 1f]
        };
        memoryStore.Memories.Add(memory);
        var generation = new RecordingGenerationProvider();
        var handler = CreateHandler(
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f]),
            tutorMemoryStore: memoryStore,
            generationProvider: generation);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Completely unrelated question", null, UseMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ContextUsed);
        Assert.Empty(result.Value.Sources);
        Assert.Equal("Grounded or general answer", result.Value.Answer);
        Assert.Contains("No relevant tutor memory was found", result.Value.Message);
        Assert.Contains("RAG means Retrieval-Augmented Generation", generation.Prompt);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithDocumentChunk_UsesDocumentSource()
    {
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "RAG Notes" };
        document.UpdateMetadata(ownerId);
        var documentStore = new FakeDocumentStore();
        documentStore.Documents[document.Id] = document;
        var retriever = new FakeKnowledgeRetriever();
        retriever.Results.Add(new KnowledgeChunk
        {
            DocumentId = document.Id,
            ChunkIndex = 3,
            Content = "RAG means Retrieval-Augmented Generation.",
            Embedding = [1f, 0f]
        });
        var handler = CreateHandler(
            ownerId: ownerId,
            documentStore: documentStore,
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f]),
            knowledgeRetriever: retriever);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("What is RAG?", null, UseMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("document", source.SourceType);
        Assert.Equal(document.Id, source.DocumentId);
        Assert.Equal("RAG Notes", source.DocumentName);
        Assert.Equal(3, source.ChunkIndex);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithUseAndSave_SearchesBeforeSavingNewConversation()
    {
        var calls = new List<string>();
        var handler = CreateHandler(
            embeddingProvider: new RecordingEmbeddingProvider([1f, 0f], calls),
            tutorMemoryStore: new RecordingTutorMemoryStore(calls),
            generationProvider: new RecordingGenerationProvider(calls));

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Tell me about RAG", null, UseMemory: true, SaveToMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.MemorySaved);
        Assert.Equal(["embedding", "memory-search", "generation", "embedding", "memory-save"], calls);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WithObviousSecret_DoesNotSaveConversation()
    {
        var memoryStore = new FakeTutorMemoryStore();
        var handler = CreateHandler(tutorMemoryStore: memoryStore);

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("My API key is abc123", null, SaveToMemory: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.MemorySaved);
        Assert.Empty(memoryStore.Memories);
    }

    [Fact]
    public async Task AskTutorQuestionHandler_WhenGenerationFails_ReturnsFailure()
    {
        var handler = CreateHandler(generationProvider: new RecordingGenerationProvider(
            result: Result.Failure<LlmGenerationResult>(new Error("Ollama.Unavailable", "offline"))));

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Tell me about RAG", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.Unavailable", result.Error.Code);
    }

    private static TutorAskFeatures.AskTutorQuestionHandler CreateHandler(
        Guid? ownerId = null,
        IDocumentStore? documentStore = null,
        IEmbeddingProvider? embeddingProvider = null,
        IKnowledgeRetriever? knowledgeRetriever = null,
        ITutorMemoryStore? tutorMemoryStore = null,
        ILlmGenerationProvider? generationProvider = null)
    {
        return new TutorAskFeatures.AskTutorQuestionHandler(
            documentStore ?? new FakeDocumentStore(),
            embeddingProvider ?? new RecordingEmbeddingProvider([1f, 0f]),
            knowledgeRetriever ?? new FakeKnowledgeRetriever(),
            tutorMemoryStore ?? new FakeTutorMemoryStore(),
            generationProvider ?? new RecordingGenerationProvider(),
            new FakeCurrentUserContext(ownerId ?? Guid.NewGuid()),
            NullLogger<TutorAskFeatures.AskTutorQuestionHandler>.Instance);
    }

    private sealed class RecordingEmbeddingProvider(float[] embedding, List<string>? calls = null) : IEmbeddingProvider
    {
        public int Calls { get; private set; }

        public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default)
        {
            Calls++;
            calls?.Add("embedding");
            return Task.FromResult(embedding);
        }
    }

    private sealed class RecordingGenerationProvider(
        List<string>? calls = null,
        Result<LlmGenerationResult>? result = null) : ILlmGenerationProvider
    {
        public string? Prompt { get; private set; }

        public Task<Result<LlmGenerationResult>> GenerateAsync(
            string prompt,
            string? model,
            CancellationToken cancellationToken = default)
        {
            calls?.Add("generation");
            Prompt = prompt;
            return Task.FromResult(result ?? Result.Success(new LlmGenerationResult(
                model ?? "llama3.2",
                "Grounded or general answer",
                Done: true)));
        }
    }

    private sealed class RecordingTutorMemoryStore(List<string> calls) : ITutorMemoryStore
    {
        public Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default)
        {
            calls.Add("memory-save");
            return Task.FromResult(memory);
        }

        public Task<List<TutorMemory>> SearchNearestMemoriesAsync(
            Guid creatorId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            calls.Add("memory-search");
            return Task.FromResult(new List<TutorMemory>());
        }
    }

    private sealed class ThrowingKnowledgeRetriever : IKnowledgeRetriever
    {
        public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
            IReadOnlyCollection<Guid> documentIds,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Document vector search failed.");
    }

    private sealed class ThrowingTutorMemoryStore : ITutorMemoryStore
    {
        public Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default) =>
            Task.FromResult(memory);

        public Task<List<TutorMemory>> SearchNearestMemoriesAsync(
            Guid creatorId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Conversation memory search failed.");
    }
}
