using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Benchmarking;

public class BenchmarkRunConfiguration : IEntityTypeConfiguration<BenchmarkRun>
{
    public void Configure(EntityTypeBuilder<BenchmarkRun> entity)
    {
        entity.ToTable("BenchmarkRuns");

        entity.ConfigureBaseModel();

        entity.Property(x => x.ModelAlias)
            .HasMaxLength(200);

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.EngineName)
            .HasMaxLength(100);

        entity.Property(x => x.EngineVersion)
            .HasMaxLength(60);

        entity.Property(x => x.FailureCode)
            .HasMaxLength(200);

        entity.Property(x => x.FailureDescription)
            .HasMaxLength(2000);

        entity.Property(x => x.ProgressStage)
            .HasMaxLength(80);

        entity.Property(x => x.ProgressMessage)
            .HasMaxLength(500);

        entity.Property(x => x.SummaryJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.SystemMetricsJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.RawResultJson)
            .HasColumnType("jsonb");

        entity.HasOne(x => x.BenchmarkTemplate)
            .WithMany()
            .HasForeignKey(x => x.BenchmarkTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.TutorConfigurationSnapshot)
            .WithMany()
            .HasForeignKey(x => x.TutorConfigurationSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.InteractionModel)
            .WithMany()
            .HasForeignKey(x => x.InteractionModelId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.TutorId, x.QueuedUtc });
        entity.HasIndex(x => x.TutorConfigurationSnapshotId);
        entity.HasIndex(x => x.InteractionModelId);
        entity.HasIndex(x => x.Status);
    }
}
