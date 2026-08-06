using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class TutorRevisionConfiguration : IEntityTypeConfiguration<TutorRevision>
{
    public void Configure(EntityTypeBuilder<TutorRevision> entity)
    {
        entity.ToTable("TutorRevisions");

        entity.ConfigureBaseModel();

        entity.Property(x => x.VersionNumber).IsRequired();

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.InteractionParametersJson).HasColumnType("jsonb");
        entity.Property(x => x.DeploymentConfigJson).HasColumnType("jsonb");

        entity.Property(x => x.DeploymentTarget)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.ActivationSlug).HasMaxLength(120);
        entity.Property(x => x.PreviousActivationSlug).HasMaxLength(120);

        entity.HasOne(x => x.Tutor)
            .WithMany(t => t.Revisions)
            .HasForeignKey(x => x.TutorId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.PromptVersion)
            .WithMany()
            .HasForeignKey(x => x.PromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.InteractionModel)
            .WithMany()
            .HasForeignKey(x => x.InteractionModelId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.QueryEmbeddingModel)
            .WithMany()
            .HasForeignKey(x => x.QueryEmbeddingModelId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.TutorId, x.VersionNumber }).IsUnique();
        entity.HasIndex(x => x.Status);

        // D22.b: unique partial index on the active slug (only one Active revision per slug).
        entity.HasIndex(x => x.ActivationSlug)
            .IsUnique()
            .HasFilter("\"ActivationSlug\" IS NOT NULL");
    }
}

public class TutorRevisionToolConfiguration : IEntityTypeConfiguration<TutorRevisionTool>
{
    public void Configure(EntityTypeBuilder<TutorRevisionTool> entity)
    {
        entity.ToTable("TutorRevisionTools");

        entity.ConfigureBaseModel();

        entity.HasOne(x => x.TutorRevision)
            .WithMany(r => r.RevisionTools)
            .HasForeignKey(x => x.TutorRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.ToolDefinition)
            .WithMany()
            .HasForeignKey(x => x.ToolDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.TutorRevisionId, x.ToolDefinitionId }).IsUnique();
    }
}
