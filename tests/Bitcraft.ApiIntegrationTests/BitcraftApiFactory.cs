using Bitcraft.Domain.Knowledge;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.RateLimiting;
using System.Collections.Concurrent;

namespace Bitcraft.ApiIntegrationTests;

public sealed class BitcraftApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"bitcraft-api-integration-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot databaseRoot = new();

    public BitcraftApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=bitcraft_tests"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IDocumentVectorizationReader>();
            services.RemoveAll<ILlmGenerationProvider>();
            services.RemoveAll<IEmbeddingProvider>();
            services.RemoveAll<IKnowledgeRetriever>();

            services.AddScoped<AppDbContext>(serviceProvider =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(databaseName, databaseRoot)
                    .AddInterceptors(serviceProvider.GetRequiredService<EntitySaveChangesInterceptor>());

                return new TestAppDbContext(options.Options);
            });
            services.AddSingleton<TestDocumentVectorizationReader>();
            services.AddSingleton<IDocumentVectorizationReader>(serviceProvider =>
                serviceProvider.GetRequiredService<TestDocumentVectorizationReader>());
            services.AddSingleton<TestLlmGenerationProvider>();
            services.AddSingleton<ILlmGenerationProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<TestLlmGenerationProvider>());
            services.AddSingleton<TestEmbeddingProvider>();
            services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<TestEmbeddingProvider>());
            services.AddSingleton<TestKnowledgeRetriever>();
            services.AddSingleton<IKnowledgeRetriever>(serviceProvider =>
                serviceProvider.GetRequiredService<TestKnowledgeRetriever>());

            services.Configure<RateLimiterOptions>(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter("integration-tests"));
            });
        });
    }
}

internal sealed class TestEmbeddingProvider : IEmbeddingProvider
{
    public ConcurrentQueue<EmbeddingGenerationRequest> Requests { get; } = new();

    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        string embeddingModel,
        CancellationToken cancellationToken = default) =>
        GenerateEmbeddingAsync(new EmbeddingGenerationRequest(text, embeddingModel), cancellationToken);

    public Task<float[]> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(new[] { 1f, 0f, 0f });
    }
}

internal sealed class TestKnowledgeRetriever : IKnowledgeRetriever
{
    public ConcurrentQueue<KnowledgeSearchRequest> Requests { get; } = new();

    public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        IReadOnlyCollection<Guid> documentIds,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default) =>
        SearchNearestChunksAsync(
            new KnowledgeSearchRequest(documentIds, Guid.Empty, queryEmbedding.Length, queryEmbedding, topK),
            cancellationToken);

    public Task<List<KnowledgeChunk>> SearchNearestChunksAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        var chunks = request.DocumentIds
            .Take(request.TopK)
            .Select((documentId, index) => new KnowledgeChunk
            {
                DocumentId = documentId,
                ChunkIndex = index + 1,
                Content = "Bounded source content.",
                Embedding = new[] { 1f, 0f, 0f },
                EmbeddingModelId = request.EmbeddingModelId,
                EmbeddingDimensions = request.EmbeddingDimensions,
                EmbeddingModel = "test-embed",
                EmbeddingProvider = "Ollama"
            })
            .ToList();
        return Task.FromResult(chunks);
    }
}

internal sealed class TestLlmGenerationProvider : ILlmGenerationProvider
{
    public ConcurrentQueue<LlmGenerationRequest> Requests { get; } = new();

    public Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(new LlmGenerationRequest(prompt, model), cancellationToken);

    public Task<Result<LlmGenerationResult>> GenerateAsync(
        LlmGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Result.Success(new LlmGenerationResult(
            request.Model ?? string.Empty,
            "A configured tutor answer.",
            true)));
    }
}

internal sealed class TestDocumentVectorizationReader : IDocumentVectorizationReader
{
    public ConcurrentDictionary<Guid, DocumentVectorizationMetadata> Metadata { get; } = new();

    public Task<DocumentVectorizationMetadata> GetMetadataAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) => Task.FromResult(
        Metadata.GetValueOrDefault(documentId) ??
        new DocumentVectorizationMetadata(false, false, 0, null, null, null, null, null, null));
}

internal sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<KnowledgeChunk>();
        modelBuilder.Ignore<TutorMemory>();
    }
}
