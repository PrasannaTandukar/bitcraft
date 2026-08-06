using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, AppRole, Guid>(options), IApplicationDbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<TutorMemory> TutorMemories => Set<TutorMemory>();

    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<TutorMember> TutorMembers => Set<TutorMember>();
    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    public DbSet<RuntimeEngine> RuntimeEngines => Set<RuntimeEngine>();
    public DbSet<Model> Models => Set<Model>();
    public DbSet<InteractionModel> InteractionModels => Set<InteractionModel>();
    public DbSet<EmbeddingModel> EmbeddingModels => Set<EmbeddingModel>();
    public DbSet<TutorModelConnection> TutorModelConnections => Set<TutorModelConnection>();
    public DbSet<KnowledgeSourceLink> TutorKnowledgeSources => Set<KnowledgeSourceLink>();
    public DbSet<TutorDocument> TutorDocuments => Set<TutorDocument>();
    public DbSet<TutorDocumentFolder> TutorDocumentFolders => Set<TutorDocumentFolder>();
    public DbSet<TutorRevision> TutorRevisions => Set<TutorRevision>();
    public DbSet<TutorRevisionTool> TutorRevisionTools => Set<TutorRevisionTool>();
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
