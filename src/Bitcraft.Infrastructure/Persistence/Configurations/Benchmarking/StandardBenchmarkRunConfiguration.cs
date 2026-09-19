using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Benchmarking;

public class StandardBenchmarkRunConfiguration : IEntityTypeConfiguration<StandardBenchmarkRun>
{
    public void Configure(EntityTypeBuilder<StandardBenchmarkRun> entity)
    {
        entity.ToTable("StandardBenchmarkRuns");

        entity.ConfigureBaseModel();

        entity.Property(x => x.BenchmarkKey)
            .HasMaxLength(80)
            .IsRequired();

        entity.Property(x => x.TasksJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.EngineName)
            .HasMaxLength(100);

        entity.Property(x => x.EngineVersion)
            .HasMaxLength(60);

        entity.Property(x => x.TaskScoresJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.Notes)
            .HasMaxLength(1000);

        entity.Property(x => x.FailureCode)
            .HasMaxLength(200);

        entity.Property(x => x.FailureDescription)
            .HasMaxLength(2000);

        entity.HasOne(x => x.InteractionModel)
            .WithMany()
            .HasForeignKey(x => x.InteractionModelId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.InteractionModelId);
        entity.HasIndex(x => new { x.BenchmarkKey, x.QueuedUtc });
    }
}
