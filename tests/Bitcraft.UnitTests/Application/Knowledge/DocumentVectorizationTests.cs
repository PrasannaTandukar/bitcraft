using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Knowledge;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.UnitTests.Application.Knowledge;

public class DocumentVectorizationTests
{
    [Theory]
    [InlineData(99, 0, "Knowledge.ChunkSizeInvalid")]
    [InlineData(8001, 0, "Knowledge.ChunkSizeInvalid")]
    [InlineData(100, -1, "Knowledge.ChunkOverlapInvalid")]
    [InlineData(100, 100, "Knowledge.ChunkOverlapInvalid")]
    public async Task Vectorize_ValidatesChunkSettings(int chunkSize, int overlap, string errorCode)
    {
        var setup = CreateSetup();
        var result = await setup.Handler.Handle(new KnowledgeFeatures.VectorizeDocumentCommand(
            setup.Document.Id,
            "Ollama",
            "embed",
            chunkSize,
            null,
            overlap), default);

        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Empty(setup.VectorStore.ReplacedDocumentIds);
    }

    [Fact]
    public async Task Vectorize_EnforcesConfiguredChunkCountLimit()
    {
        var setup = CreateSetup(rawText: new string('x', 201), limits: new DocumentIngestionLimits(1000, 1000, 2));
        var result = await setup.Handler.Handle(new KnowledgeFeatures.VectorizeDocumentCommand(
            setup.Document.Id, "Ollama", "embed", 100, null, 0), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Knowledge.ChunkCountExceeded", result.Error.Code);
        Assert.Empty(setup.EmbeddingProvider.Texts);
    }

    [Fact]
    public async Task Vectorize_RejectsDisabledEmbeddingModelAndRuntime()
    {
        var disabledModelSetup = CreateSetup(modelEnabled: false);
        var disabledModel = await disabledModelSetup.Handler.Handle(Command(disabledModelSetup), default);
        Assert.Equal("Knowledge.EmbeddingModelDisabled", disabledModel.Error.Code);

        var disabledRuntimeSetup = CreateSetup(runtimeEnabled: false);
        var disabledRuntime = await disabledRuntimeSetup.Handler.Handle(Command(disabledRuntimeSetup), default);
        Assert.Equal("Knowledge.RuntimeEngineDisabled", disabledRuntime.Error.Code);
    }

    [Fact]
    public async Task Vectorize_SelectedModelPersistsModelMetadata()
    {
        var setup = CreateSetup();
        var result = await setup.Handler.Handle(Command(setup), default);

        Assert.True(result.IsSuccess);
        var chunk = Assert.Single(setup.VectorStore.Chunks);
        Assert.Equal(setup.Model.Id, chunk.EmbeddingModelId);
        Assert.Equal(setup.Model.ProviderModelName, chunk.EmbeddingModel);
        Assert.Equal(setup.Model.EmbeddingDimensions, chunk.EmbeddingDimensions);
        Assert.Equal("Ollama", chunk.EmbeddingProvider);
    }

    [Fact]
    public async Task Vectorize_DimensionMismatchDoesNotReplaceExistingChunks()
    {
        var setup = CreateSetup(embedding: [0.1f, 0.2f]);
        setup.VectorStore.Chunks.Add(new KnowledgeChunk { DocumentId = setup.Document.Id, Content = "old" });

        var result = await setup.Handler.Handle(Command(setup), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Knowledge.EmbeddingDimensionsMismatch", result.Error.Code);
        Assert.Empty(setup.VectorStore.ReplacedDocumentIds);
        Assert.Equal("old", Assert.Single(setup.VectorStore.Chunks).Content);
    }

    [Fact]
    public async Task Vectorize_ExternalFailurePreservesExistingChunks()
    {
        var setup = CreateSetup(failure: new EmbeddingProviderException(
            EmbeddingProviderFailureKind.ServiceUnavailable,
            "internal detail"));
        setup.VectorStore.Chunks.Add(new KnowledgeChunk { DocumentId = setup.Document.Id, Content = "old" });

        var result = await setup.Handler.Handle(Command(setup), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Knowledge.EmbeddingServiceUnavailable", result.Error.Code);
        Assert.Empty(setup.VectorStore.ReplacedDocumentIds);
        Assert.Equal("old", Assert.Single(setup.VectorStore.Chunks).Content);
        Assert.DoesNotContain("internal", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeFeatures.VectorizeDocumentCommand Command(Setup setup) => new(
        setup.Document.Id,
        EmbeddingModelId: setup.Model.Id,
        MaxChunkLength: 1000,
        ChunkOverlap: 100);

    private static Setup CreateSetup(
        string rawText = "A useful document body.",
        bool modelEnabled = true,
        bool runtimeEnabled = true,
        float[]? embedding = null,
        Exception? failure = null,
        DocumentIngestionLimits? limits = null)
    {
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "Doc", RawText = rawText };
        document.UpdateMetadata(ownerId);
        var documents = new FakeDocumentStore();
        documents.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var runtime = new RuntimeEngine
        {
            Name = "Local Ollama",
            ProviderType = RuntimeEngineProviderType.Ollama,
            IsEnabled = runtimeEnabled
        };
        var model = new EmbeddingModel
        {
            Name = "Nomic",
            ProviderModelName = "nomic-embed-text",
            RuntimeEngine = runtime,
            RuntimeEngineId = runtime.Id,
            IsEnabled = modelEnabled,
            EmbeddingDimensions = 3
        };
        var models = new FakeModelRepository();
        models.EmbeddingModels[model.Id] = model;
        var provider = new ConfigurableEmbeddingProvider(embedding ?? [0.1f, 0.2f, 0.3f], failure);
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documents,
            vectorStore,
            provider,
            models,
            new FakeCurrentUserContext(ownerId),
            limits ?? DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
        return new Setup(handler, document, vectorStore, provider, model);
    }

    private sealed record Setup(
        KnowledgeFeatures.VectorizeDocumentHandler Handler,
        Document Document,
        FakeVectorStore VectorStore,
        ConfigurableEmbeddingProvider EmbeddingProvider,
        EmbeddingModel Model);

    private sealed class ConfigurableEmbeddingProvider(float[] embedding, Exception? failure) : IEmbeddingProvider
    {
        public List<string> Texts { get; } = [];

        public Task<float[]> GenerateEmbeddingAsync(
            string text,
            string embeddingModel,
            CancellationToken cancellationToken = default)
        {
            Texts.Add(text);
            if (failure is not null) throw failure;
            return Task.FromResult(embedding);
        }
    }
}
