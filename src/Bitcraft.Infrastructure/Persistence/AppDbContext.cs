using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, AppRole, Guid>(options), IApplicationDbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    public DbSet<ModelCatalogEntry> ModelCatalogEntries => Set<ModelCatalogEntry>();
    public DbSet<TutorModelConnection> TutorModelConnections => Set<TutorModelConnection>();
    public DbSet<KnowledgeSourceLink> TutorKnowledgeSources => Set<KnowledgeSourceLink>();
    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();
    public DbSet<DeploymentProfile> DeploymentProfiles => Set<DeploymentProfile>();
    public DbSet<TutorConfigurationSnapshot> TutorConfigurationSnapshots => Set<TutorConfigurationSnapshot>();
    public DbSet<BenchmarkTemplate> BenchmarkTemplates => Set<BenchmarkTemplate>();
    public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
    public DbSet<BenchmarkRunCase> BenchmarkRunCases => Set<BenchmarkRunCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
