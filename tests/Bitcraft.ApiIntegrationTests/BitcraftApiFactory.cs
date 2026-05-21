using Bitcraft.Domain.Knowledge;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            services.AddScoped<AppDbContext>(serviceProvider =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(databaseName, databaseRoot)
                    .AddInterceptors(serviceProvider.GetRequiredService<EntitySaveChangesInterceptor>());

                return new TestAppDbContext(options.Options);
            });
        });
    }
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
