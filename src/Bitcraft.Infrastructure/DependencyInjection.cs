using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Bitcraft.Infrastructure.ExternalServices;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
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
        services.AddScoped<IDocumentStore, DocumentStore>();
        services.AddScoped<IDocumentFolderStore, DocumentFolderStore>();
        services.AddScoped<ITutorMemoryStore, PgvectorTutorMemoryStore>();
        services.AddScoped<IEmbeddingProvider, OllamaEmbeddingProvider>();
        services.AddScoped<ILlmGenerationProvider, OllamaGenerationProvider>();
        services.AddKnowledgePersistence(configuration);
        services.Configure<DeepEvalBenchmarkOptions>(configuration.GetSection(DeepEvalBenchmarkOptions.SectionName));

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
        services.AddSingleton<IBenchmarkRunQueue, InMemoryBenchmarkRunQueue>();
        services.AddHostedService<BenchmarkRunWorker>();

        // External model providers
        var ollamaEmbeddingTimeoutSeconds = configuration.GetValue<int?>("ModelProviders:Ollama:EmbeddingTimeoutSeconds") ?? 30;
        services.AddHttpClient(nameof(OllamaEmbeddingProvider), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(ollamaEmbeddingTimeoutSeconds);
        });
        var ollamaGenerateTimeoutSeconds = configuration.GetValue<int?>("ModelProviders:Ollama:GenerateTimeoutSeconds") ?? 120;
        services.AddHttpClient(nameof(OllamaGenerationProvider), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(ollamaGenerateTimeoutSeconds);
        });
        var deepEvalTimeoutSeconds = configuration.GetValue<int?>("Benchmarking:DeepEval:TimeoutSeconds") ?? 120;
        services.AddHttpClient(nameof(DeepEvalBenchmarkEngine), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(deepEvalTimeoutSeconds);
        });

        return services;
    }

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
