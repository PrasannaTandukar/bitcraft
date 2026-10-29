using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class TutorConfigurationSnapshotConfiguration : IEntityTypeConfiguration<TutorConfigurationSnapshot>
{
    public void Configure(EntityTypeBuilder<TutorConfigurationSnapshot> entity)
    {
        entity.ToTable("TutorConfigurationSnapshots");

        entity.ConfigureBaseModel();

        entity.Property(x => x.VersionNumber).IsRequired();

        entity.Property(x => x.TutorName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.PromptContent);

        entity.Property(x => x.TutorModelConnectionId);
        entity.Property(x => x.InteractionModelId);

        entity.Property(x => x.ModelName)
            .HasMaxLength(200);

        entity.Property(x => x.ModelCapabilities).HasConversion<int>().HasDefaultValue(Bitcraft.Domain.Modeling.ModelCapabilities.None);

        entity.Property(x => x.ModelSettingsJson).HasColumnType("jsonb");
        entity.Property(x => x.ToolsJson).HasColumnType("jsonb");
        entity.Property(x => x.KnowledgeSourcesJson).HasColumnType("jsonb");
        entity.Property(x => x.EffectiveDocumentIdsJson).HasColumnType("jsonb");
        entity.Property(x => x.EffectiveKnowledgeBindingJson).HasColumnType("jsonb");
        entity.Property(x => x.DeploymentProfileJson).HasColumnType("jsonb");

        entity.Property(x => x.CapturedUtc).IsRequired();
        entity.Property(x => x.CapturedById).IsRequired();

        entity.HasOne(x => x.CapturedBy)
            .WithMany()
            .HasForeignKey(x => x.CapturedById)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.TutorRevision)
            .WithMany(r => r.Snapshots)
            .HasForeignKey(x => x.TutorRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.TutorId);
        entity.HasIndex(x => x.TutorRevisionId);
        entity.HasIndex(x => x.CapturedById);
        entity.HasIndex(x => x.CapturedUtc);
        entity.HasIndex(x => new { x.TutorId, x.VersionNumber }).IsUnique();
    }
}
