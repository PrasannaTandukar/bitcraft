using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> entity)
    {
        entity.ToTable("Tutors");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Description)
            .HasMaxLength(2000);

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        entity.HasIndex(x => x.OwnerId);
        entity.HasIndex(x => new { x.OwnerId, x.Name });

        entity.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ActivePromptVersion)
            .WithMany()
            .HasForeignKey(x => x.ActivePromptVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.TutorModelConnection)
            .WithOne(x => x.Tutor!)
            .HasForeignKey<TutorModelConnection>(x => x.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.DeploymentProfile)
            .WithOne(p => p.Tutor!)
            .HasForeignKey<DeploymentProfile>(p => p.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.PromptVersions)
            .WithOne(v => v.Tutor!)
            .HasForeignKey(v => v.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.KnowledgeSources)
            .WithOne(k => k.Tutor!)
            .HasForeignKey(k => k.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Tools)
            .WithOne(t => t.Tutor!)
            .HasForeignKey(t => t.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Snapshots)
            .WithOne()
            .HasForeignKey(s => s.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
