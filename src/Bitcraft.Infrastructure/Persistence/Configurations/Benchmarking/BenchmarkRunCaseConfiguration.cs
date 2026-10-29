using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Benchmarking;

public class BenchmarkRunCaseConfiguration : IEntityTypeConfiguration<BenchmarkRunCase>
{
    public void Configure(EntityTypeBuilder<BenchmarkRunCase> entity)
    {
        entity.ToTable("BenchmarkRunCases");

        entity.ConfigureBaseModel();

        entity.Property(x => x.InputText)
            .IsRequired();

        entity.Property(x => x.ActualOutput)
            .IsRequired();

        entity.Property(x => x.ExpectedOutput);
        entity.Property(x => x.ContextText);

        entity.Property(x => x.MetadataJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.MetricsJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.PerformanceJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.ResourceJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.Reason)
            .HasMaxLength(3000);

        entity.HasOne(x => x.BenchmarkRun)
            .WithMany(x => x.Cases)
            .HasForeignKey(x => x.BenchmarkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.BenchmarkRunId, x.CaseIndex })
            .IsUnique();
    }
}
