using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using Bitcraft.InfrastructureIntegrationTests.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Benchmarking;

public class TutorBenchmarkOutputGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_WithContextOverrideSkipsKnowledgeRetrieval()
    {
        var setup = await CreateSetupAsync();
        var effective = new RecordingEffectiveSetReader([Guid.NewGuid()]);
        var embeddings = new RecordingEmbeddingProvider();
        var retriever = new RecordingKnowledgeRetriever();
        var generation = new RecordingGenerationProvider();
        var generator = new TutorBenchmarkOutputGenerator(
            setup.Db,
            effective,
            embeddings,
            retriever,
            generation,
            NullLogger<TutorBenchmarkOutputGenerator>.Instance);

        var result = await generator.GenerateAsync(new TutorBenchmarkGenerationRequest(
            setup.Tutor.Id,
            setup.Revision.Id,
            null,
            "Hello",
            "Manually supplied context",
            true,
            5));

        Assert.True(result.IsSuccess);
        Assert.Equal("Manually supplied context", result.Value.ContextText);
        Assert.Equal(0, effective.Calls);
        Assert.Equal(0, embeddings.Calls);
        Assert.Equal(0, retriever.Calls);
        Assert.Contains("<reference_material>\nManually supplied context\n</reference_material>", generation.Prompt);
    }

    [Fact]
    public async Task GenerateAsync_WithTutorKnowledgeSearchesOnlyEffectiveDocumentsAndPersistsExactContext()
    {
        var setup = await CreateSetupAsync();
        var documentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var effective = new RecordingEffectiveSetReader(documentIds);
        var embeddings = new RecordingEmbeddingProvider();
        var retriever = new RecordingKnowledgeRetriever
        {
            Results = [new KnowledgeChunk { DocumentId = documentIds[0], ChunkIndex = 1, Content = "Kathmandu is the capital of Nepal." }]
        };
        var generator = new TutorBenchmarkOutputGenerator(
            setup.Db,
            effective,
            embeddings,
            retriever,
            new RecordingGenerationProvider(),
            NullLogger<TutorBenchmarkOutputGenerator>.Instance);

        var result = await generator.GenerateAsync(new TutorBenchmarkGenerationRequest(
            setup.Tutor.Id,
            setup.Revision.Id,
            null,
            "What is Nepal's capital?",
            null,
            true,
            3));

        Assert.True(result.IsSuccess);
        Assert.Equal(documentIds, retriever.DocumentIds);
        Assert.Equal(3, retriever.TopK);
        Assert.Contains("Kathmandu is the capital of Nepal.", result.Value.ContextText);
        Assert.Equal("embed-model", embeddings.Model);
    }

    private static async Task<Setup> CreateSetupAsync()
    {
        var userId = Guid.NewGuid();
        var db = PersistenceTestContextFactory.Create(new MutableCurrentUserContext(userId));
        await PersistenceTestContextFactory.AddUserAsync(db, userId);
        var tutor = Tutor.Create("Benchmark tutor", null, userId);
        var prompt = new PromptVersion
        {
            TutorId = tutor.Id,
            VersionNumber = 1,
            Name = "Prompt",
            Content = "Answer clearly and briefly."
        };
        var runtime = new RuntimeEngine
        {
            Name = $"Runtime-{Guid.NewGuid():N}",
            ProviderType = RuntimeEngineProviderType.Ollama,
            Endpoint = "local"
        };
        var interactionModel = new InteractionModel
        {
            Name = $"Interaction-{Guid.NewGuid():N}",
            RuntimeEngine = runtime,
            RuntimeEngineId = runtime.Id,
            ProviderModelName = "llama3",
            IsEnabled = true
        };
        var embeddingModel = new EmbeddingModel
        {
            Name = $"Embedding-{Guid.NewGuid():N}",
            RuntimeEngine = runtime,
            RuntimeEngineId = runtime.Id,
            ProviderModelName = "embed-model",
            EmbeddingDimensions = 2,
            IsEnabled = true
        };
        var revision = new TutorRevision
        {
            Tutor = tutor,
            TutorId = tutor.Id,
            VersionNumber = 1,
            Status = RevisionStatus.Active,
            PromptVersion = prompt,
            PromptVersionId = prompt.Id,
            InteractionModel = interactionModel,
            InteractionModelId = interactionModel.Id,
            QueryEmbeddingModel = embeddingModel,
            QueryEmbeddingModelId = embeddingModel.Id,
            ActivationSlug = $"benchmark-{Guid.NewGuid():N}"
        };
        db.TutorRevisions.Add(revision);
        await db.SaveChangesAsync();
        return new Setup(db, tutor, revision);
    }

    private sealed record Setup(Bitcraft.Infrastructure.Persistence.AppDbContext Db, Tutor Tutor, TutorRevision Revision);

    private sealed class RecordingEffectiveSetReader(IReadOnlyList<Guid> documentIds) : IRevisionEffectiveSetReader
    {
        public int Calls { get; private set; }
        public Task<EffectiveDocumentSet> GetEffectiveSetAsync(
            Guid tutorId,
            CancellationToken cancellationToken = default,
            Guid? queryEmbeddingModelId = null)
        {
            Calls++;
            return Task.FromResult(new EffectiveDocumentSet(documentIds, []));
        }
    }

    private sealed class RecordingEmbeddingProvider : IEmbeddingProvider
    {
        public int Calls { get; private set; }
        public string? Model { get; private set; }
        public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default)
        {
            Calls++;
            Model = embeddingModel;
            return Task.FromResult(new[] { 1f, 0f });
        }
    }

    private sealed class RecordingKnowledgeRetriever : IKnowledgeRetriever
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<Guid>? DocumentIds { get; private set; }
        public int TopK { get; private set; }
        public List<KnowledgeChunk> Results { get; init; } = [];
        public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(IReadOnlyCollection<Guid> documentIds, float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default)
        {
            Calls++;
            DocumentIds = documentIds;
            TopK = topK;
            return Task.FromResult(Results);
        }
    }

    private sealed class RecordingGenerationProvider : ILlmGenerationProvider
    {
        public string Prompt { get; private set; } = string.Empty;
        public Task<Result<LlmGenerationResult>> GenerateAsync(string prompt, string? model, CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            return Task.FromResult(Result.Success(new LlmGenerationResult(model ?? "llama3", "Generated answer", true)));
        }
    }
}
