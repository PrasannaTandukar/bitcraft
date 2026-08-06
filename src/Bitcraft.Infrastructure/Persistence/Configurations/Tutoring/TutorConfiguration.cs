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

        // D2/D3: ownership now lives in TutorMembers; see TutorMemberConfiguration.
        entity.HasMany(x => x.Members)
            .WithOne(m => m.Tutor!)
            .HasForeignKey(m => m.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

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

        entity.HasMany(x => x.BenchmarkRuns)
            .WithOne(r => r.Tutor!)
            .HasForeignKey(r => r.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.ActiveRevision)
            .WithMany()
            .HasForeignKey(x => x.ActiveRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(x => x.Revisions)
            .WithOne(r => r.Tutor!)
            .HasForeignKey(r => r.TutorId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(x => x.TutorDocuments)
            .WithOne(td => td.Tutor!)
            .HasForeignKey(td => td.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.TutorDocumentFolders)
            .WithOne(tdf => tdf.Tutor!)
            .HasForeignKey(tdf => tdf.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
