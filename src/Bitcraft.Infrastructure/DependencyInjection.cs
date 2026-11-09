using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Bitcraft.Infrastructure.ExternalServices;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.Documents;
using Bitcraft.Infrastructure.Identity;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Bitcraft.Infrastructure.Persistence.Repositories.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Repositories.Documents;
using Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;
using Bitcraft.Infrastructure.Persistence.Repositories.Memory;
using Bitcraft.Infrastructure.Persistence.Repositories.Modeling;
using Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Bitcraft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        var chatLimits = new ChatAttachmentLimits(
            configuration.GetValue<long?>("ChatAttachments:MaxFileBytes") ?? 5 * 1024 * 1024,
            configuration.GetValue<int?>("ChatAttachments:MaxImagesPerMessage") ?? 4,
            configuration.GetValue<int?>("ChatAttachments:MaxDimension") ?? 2048,
            configuration.GetValue<int?>("ChatAttachments:RetentionHours") ?? 24,
            configuration.GetValue<int?>("ChatAttachments:MaxImagesPerRequest") ?? 20,
            configuration.GetValue<long?>("ChatAttachments:MaxDecodedPixels") ?? 40_000_000);
        if (chatLimits.MaxFileBytes is < 1 or > 20 * 1024 * 1024 ||
            chatLimits.MaxImagesPerMessage is < 1 or > 20 ||
            chatLimits.MaxImagesPerRequest < chatLimits.MaxImagesPerMessage || chatLimits.MaxImagesPerRequest > 80 ||
            chatLimits.MaxDimension is < 1 or > 4096 || chatLimits.RetentionHours is < 1 or > 168 ||
            chatLimits.MaxDecodedPixels is < 1 or > 40_000_000)
            throw new InvalidOperationException("ChatAttachments limits are outside their supported ranges.");
        services.AddSingleton(chatLimits);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IChatAttachmentStore, Bitcraft.Infrastructure.Chat.FileSystemChatAttachmentStore>();
        services.AddSingleton<IChatImageNormalizer, Bitcraft.Infrastructure.Chat.ChatImageNormalizer>();
        services.AddScoped<IChatAttachmentRepository, Bitcraft.Infrastructure.Chat.ChatAttachmentRepository>();
        services.AddHostedService<Bitcraft.Infrastructure.Chat.ChatAttachmentCleanupWorker>();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
        services.AddScoped<EntitySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector())
                .AddInterceptors(serviceProvider.GetRequiredService<EntitySaveChangesInterceptor>()));

        services.AddIdentity<AppUser, AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwtSecret = configuration["JwtSettings:Secret"] ?? "super_secret_key_used_for_local_dev_only_should_be_in_env";
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Principal?.FindFirstValue("sub");

                    if (!Guid.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("Token subject is missing or invalid.");
                        return;
                    }

                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                    if (await userManager.FindByIdAsync(userId.ToString()) is null)
                    {
                        context.Fail("Token subject no longer exists.");
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ViewerOrAbove, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.EditorOrAdmin, policy => policy.RequireRole(AppRoles.Editor, AppRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.AuditorOrAbove, policy => policy.RequireRole(AppRoles.Auditor, AppRoles.Editor, AppRoles.Admin));
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRoleManagementService, IdentityService>();
        var documentLimits = new DocumentIngestionLimits(
            PositiveLongOrDefault(
                configuration.GetValue<long?>("Documents:MaxUploadBytes"),
                DocumentIngestionLimits.DefaultMaxUploadBytes),
            PositiveIntOrDefault(
                configuration.GetValue<int?>("Documents:MaxExtractedCharacters"),
                DocumentIngestionLimits.DefaultMaxExtractedCharacters),
            PositiveIntOrDefault(
                configuration.GetValue<int?>("Documents:MaxVectorizationChunks"),
                DocumentIngestionLimits.DefaultMaxVectorizationChunks),
            PositiveIntOrDefault(
                configuration.GetValue<int?>("Documents:MaxBatchFiles"),
                DocumentIngestionLimits.DefaultMaxBatchFiles));
        services.AddSingleton(documentLimits);
        services.AddScoped<DocumentStore>();
        services.AddScoped<IDocumentStore>(provider => provider.GetRequiredService<DocumentStore>());
        services.AddScoped<IDocumentContentStore>(provider => provider.GetRequiredService<DocumentStore>());
        services.AddScoped<IDocumentVectorizationReader, DocumentVectorizationReader>();
        services.AddScoped<IDocumentTextExtractionService, DocumentTextExtractionService>();
        services.AddScoped<IDocumentFolderStore, DocumentFolderStore>();
        services.AddScoped<ITutorMemoryStore, PgvectorTutorMemoryStore>();
        services.AddScoped<Bitcraft.Infrastructure.ExternalServices.Embeddings.OllamaEmbeddingProvider>();
        services.AddScoped<Bitcraft.Infrastructure.ExternalServices.OpenAI.OpenAiCompatibleEmbeddingProvider>();
        services.AddScoped<IEmbeddingProvider, Bitcraft.Infrastructure.ExternalServices.Embeddings.CompositeEmbeddingProvider>();
        services.AddScoped<IRuntimeApiKeyResolver, ConfigurationRuntimeApiKeyResolver>();
        services.AddScoped<IRuntimeChatClientFactory, RuntimeChatClientFactory>();
        services.AddScoped<IRuntimeModelDiagnostic, RuntimeModelDiagnostic>();
        services.AddScoped<ITutorConversationRepository, Bitcraft.Infrastructure.Persistence.TutorConversationRepository>();
        services.AddScoped<ITutorAgentExecutor, TutorAgentExecutor>();
        services.AddScoped<TutorKnowledgeContextProviderFactory>();
        services.AddScoped<TutorMemoryContextProviderFactory>();
        services.AddSingleton<ITutorToolHandler, CalculatorTutorToolHandler>();
        services.AddSingleton(TutorToolRuntimeOptions.Default);
        services.AddSingleton<ITutorToolCatalog, TutorToolCatalog>();
        services.AddSingleton<ITutorToolRegistry, TutorToolRegistry>();
        services.AddHttpClient(RuntimeChatClientFactory.HttpClientName).RemoveAllLoggers();
        services.AddHttpClient(RuntimeChatClientFactory.OllamaHttpClientName)
            .AddHttpMessageHandler(() => new TutorAgentHttpStatusHandler())
            .RemoveAllLoggers();
        services.AddKnowledgePersistence(configuration);
        services.AddOptions<DeepEvalBenchmarkOptions>()
            .Bind(configuration.GetSection(DeepEvalBenchmarkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<Bitcraft.Application.Features.Benchmarking.StandardBenchmarkFeatures.StandardBenchmarkProcessingOptions>()
            .Bind(configuration.GetSection(Bitcraft.Application.Features.Benchmarking.StandardBenchmarkFeatures.StandardBenchmarkProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Tutoring
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<ITutorMembershipReader, TutorMembershipReader>();
        services.AddScoped<IRevisionEffectiveSetReader, RevisionEffectiveSetReader>();
        services.AddScoped<IModelRepository, ModelRepository>();
        services.AddScoped<IRuntimeEngineRepository, RuntimeEngineRepository>();

        // Benchmarking
        services.AddScoped<IBenchmarkTemplateRepository, BenchmarkTemplateRepository>();
        services.AddScoped<IBenchmarkRunRepository, BenchmarkRunRepository>();
        services.AddScoped<IBenchmarkEngine, DeepEvalBenchmarkEngine>();
        services.AddScoped<ITutorBenchmarkOutputGenerator, TutorBenchmarkOutputGenerator>();
        services.AddSingleton<IBenchmarkRunQueue, InMemoryBenchmarkRunQueue>();
        services.AddHostedService<BenchmarkRunWorker>();
        services.AddScoped<IStandardBenchmarkRunRepository, StandardBenchmarkRunRepository>();
        services.AddSingleton<IStandardBenchmarkRunQueue, InMemoryStandardBenchmarkRunQueue>();
        services.AddHostedService<StandardBenchmarkRunWorker>();

        // External model providers
        var ollamaEmbeddingTimeoutSeconds = configuration.GetValue<int?>("ModelProviders:Ollama:EmbeddingTimeoutSeconds") ?? 300;
        services.AddHttpClient(nameof(OllamaEmbeddingProvider), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(ollamaEmbeddingTimeoutSeconds);
        });
        services.AddHttpClient(nameof(Bitcraft.Infrastructure.ExternalServices.OpenAI.OpenAiCompatibleEmbeddingProvider), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(ollamaEmbeddingTimeoutSeconds);
        });
        var deepEvalTimeoutSeconds = configuration.GetValue<int?>("Benchmarking:DeepEval:TimeoutSeconds") ?? 120;
        services.AddHttpClient(nameof(DeepEvalBenchmarkEngine), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(deepEvalTimeoutSeconds);
        });

        return services;
    }

    private static int PositiveIntOrDefault(int? value, int fallback) => value is > 0 ? value.Value : fallback;

    private static long PositiveLongOrDefault(long? value, long fallback) => value is > 0 ? value.Value : fallback;

    private static IServiceCollection AddKnowledgePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var backend = configuration["Knowledge:VectorBackend"]?.Trim();
        if (string.IsNullOrWhiteSpace(backend) || string.Equals(backend, "pgvector", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IVectorStore, PgvectorVectorStore>();
            services.AddScoped<IKnowledgeRetriever, PgvectorKnowledgeRetriever>();
            return services;
        }

        throw new NotSupportedException($"Knowledge vector backend '{backend}' is not implemented. Configure 'pgvector' to use the current backend.");
    }
}
