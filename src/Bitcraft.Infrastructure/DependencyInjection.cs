using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Identity;
using Bitcraft.Infrastructure.ExternalServices;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Bitcraft.Infrastructure.Identity;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Bitcraft.Infrastructure.Persistence.Repositories.Documents;
using Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;
using Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Bitcraft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector())
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IDocumentStore, DocumentStore>();
        services.AddScoped<IEmbeddingProvider, NotImplementedEmbeddingProvider>();
        services.AddKnowledgePersistence(configuration);

        // Tutoring
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();

        // External model providers
        var ollamaTimeoutSeconds = configuration.GetValue<int?>("ModelProviders:Ollama:HealthCheckTimeoutSeconds") ?? 5;
        services.AddHttpClient(nameof(OllamaHealthCheck), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(ollamaTimeoutSeconds);
        });
        services.AddScoped<OllamaHealthCheck>();
        services.AddScoped<IModelConnectionHealthCheck, ModelConnectionHealthCheckDispatcher>();

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

        throw new NotImplementedException("Filled in Phase B");
    }
}
