using Bitcraft.Infrastructure.ExternalServices.Agents;
using Microsoft.Extensions.AI;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Bitcraft.Infrastructure.Persistence.Repositories.Memory;
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
    private readonly string attachmentRoot = Path.Combine(Path.GetTempPath(), $"bitcraft-api-images-{Guid.NewGuid():N}");

    public string? SessionTestPostgres { get; init; }
    public bool UseRealOllama { get; init; }
    public bool UseRealTutorMemoryStore { get; init; }
    public bool UseLegacySessionJsonb { get; set; }

    public BitcraftApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(attachmentRoot)) Directory.Delete(attachmentRoot, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=bitcraft_tests",
                ["ChatAttachments:RootPath"] = attachmentRoot
            });
        });

        builder.ConfigureServices(services =>
        {
            if (SessionTestPostgres is not null)
            {
                // Opt-in only: callers must supply a disposable test database.
                using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(SessionTestPostgres, provider => provider.UseVector()).Options);
                db.Database.Migrate();
            }
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IDocumentVectorizationReader>();
            services.RemoveAll<IRuntimeModelDiagnostic>();
            services.RemoveAll<ITutorAgentExecutor>();
            services.RemoveAll<IRuntimeChatClientFactory>();
            services.RemoveAll<IEmbeddingProvider>();
            services.RemoveAll<IKnowledgeRetriever>();
            services.RemoveAll<ITutorMemoryStore>();

            services.AddScoped<AppDbContext>(serviceProvider =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .AddInterceptors(serviceProvider.GetRequiredService<EntitySaveChangesInterceptor>());
                if (SessionTestPostgres is not null)
                {
                    options.UseNpgsql(SessionTestPostgres, provider => provider.UseVector());
                    return UseLegacySessionJsonb ? new LegacySessionAppDbContext(options.Options) : new AppDbContext(options.Options);
                }
                options.UseInMemoryDatabase(databaseName, databaseRoot);
                return new TestAppDbContext(options.Options);
            });
            services.AddSingleton<TestDocumentVectorizationReader>();
            services.AddSingleton<IDocumentVectorizationReader>(serviceProvider =>
                serviceProvider.GetRequiredService<TestDocumentVectorizationReader>());
            services.AddSingleton<TestRuntimeModelDiagnostic>();
            services.AddSingleton<IRuntimeModelDiagnostic>(provider =>
                provider.GetRequiredService<TestRuntimeModelDiagnostic>());
            if (UseRealOllama) services.AddScoped<IRuntimeChatClientFactory, RuntimeChatClientFactory>();
            else services.AddSingleton<IRuntimeChatClientFactory, TestRuntimeChatClientFactory>();
            services.AddSingleton<TestTutorAgentRecorder>();
            services.AddScoped<TutorAgentExecutor>();
            services.AddScoped<TestTutorAgentExecutor>();
            services.AddScoped<ITutorAgentExecutor>(serviceProvider =>
                serviceProvider.GetRequiredService<TestTutorAgentExecutor>());
            services.AddSingleton<TestEmbeddingProvider>();
            services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<TestEmbeddingProvider>());
            services.AddSingleton<TestKnowledgeRetriever>();
            services.AddSingleton<IKnowledgeRetriever>(serviceProvider =>
                serviceProvider.GetRequiredService<TestKnowledgeRetriever>());
            services.AddSingleton<TestTutorMemoryStore>();
            if (UseRealTutorMemoryStore)
                services.AddScoped<ITutorMemoryStore, PgvectorTutorMemoryStore>();
            else
                services.AddSingleton<ITutorMemoryStore>(serviceProvider =>
                    serviceProvider.GetRequiredService<TestTutorMemoryStore>());

            services.Configure<RateLimiterOptions>(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter("integration-tests"));
            });
        });
    }
}

internal sealed class TestTutorAgentRecorder
{
    public ConcurrentQueue<TutorAgentRequest> Requests { get; } = new();
    public Error? Failure { get; set; }
    public TutorAgentResponse? LastResponse { get; set; }
    public Func<CancellationToken, Task>? BeforeExecuteAsync { get; set; }
}

internal sealed class TestTutorMemoryStore : ITutorMemoryStore
{
    public ConcurrentQueue<TutorMemory> Memories { get; } = new();
    public ConcurrentQueue<TutorMemorySearchRequest> Requests { get; } = new();

    public Task<TutorMemory> AddAsync(TutorMemory memory, CancellationToken cancellationToken = default)
    {
        Memories.Enqueue(memory);
        return Task.FromResult(memory);
    }

    public Task<List<TutorMemory>> SearchNearestMemoriesAsync(TutorMemorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Memories.Where(memory => memory.UserId == request.UserId &&
            memory.TutorId == request.TutorId && memory.EmbeddingModelId == request.EmbeddingModelId &&
            memory.EmbeddingDimensions == request.EmbeddingDimensions &&
            (!request.ExcludedConversationId.HasValue || memory.ConversationId != request.ExcludedConversationId))
            .Take(request.TopK).ToList());
    }
}

internal sealed class TestTutorAgentExecutor(TutorAgentExecutor executor, TestTutorAgentRecorder recorder) : ITutorAgentExecutor
{
    public ConcurrentQueue<TutorAgentRequest> Requests => recorder.Requests;
    public Error? Failure { get => recorder.Failure; set => recorder.Failure = value; }
    public TutorAgentResponse? LastResponse => recorder.LastResponse;

    public async Task<Result<TutorAgentResponse>> ExecuteAsync(TutorAgentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue(request);
        if (Failure is { } error) return Result.Failure<TutorAgentResponse>(error);
        if (recorder.BeforeExecuteAsync is { } before) await before(cancellationToken);
        var result = await executor.ExecuteAsync(request, cancellationToken);
        recorder.LastResponse = result.IsSuccess ? result.Value : null;
        return result;
    }

    public async Task<Result<TutorAgentResponse>> ExecuteStreamingAsync(TutorAgentRequest request,
        Func<TutorAgentStreamEvent, CancellationToken, ValueTask> observer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue(request);
        if (Failure is { } error) return Result.Failure<TutorAgentResponse>(error);
        if (recorder.BeforeExecuteAsync is { } before) await before(cancellationToken);
        var result = await executor.ExecuteStreamingAsync(request, observer, cancellationToken);
        recorder.LastResponse = result.IsSuccess ? result.Value : null;
        return result;
    }
}

internal sealed class TestRuntimeChatClientFactory : IRuntimeChatClientFactory
{
    public Result<IChatClient> Create(TutorAgentModelConfiguration model) => Result.Success<IChatClient>(new TestChatClient());

    private sealed class TestChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Deterministic agent answer")));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Deterministic agent ");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "answer");
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
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

internal sealed class TestRuntimeModelDiagnostic : IRuntimeModelDiagnostic
{
    public ConcurrentQueue<RuntimeModelGenerationDiagnosticRequest> Requests { get; } = new();

    public Task<Result<RuntimeModelCatalogDiagnostic>> ProbeModelsAsync(RuntimeModelCatalogDiagnosticRequest request,
        CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(
        new RuntimeModelCatalogDiagnostic(["test-model"])));

    public Task<Result<RuntimeModelGenerationDiagnostic>> GenerateAsync(RuntimeModelGenerationDiagnosticRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Result.Success(new RuntimeModelGenerationDiagnostic(
            "A configured diagnostic answer.")));
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

internal sealed class LegacySessionAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Bitcraft.Domain.Tutoring.TutorConversation>().Property(value => value.AgentSessionJson).HasColumnType("jsonb");
    }
}
