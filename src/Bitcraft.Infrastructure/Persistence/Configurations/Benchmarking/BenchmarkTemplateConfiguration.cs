using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Benchmarking;

public class BenchmarkTemplateConfiguration : IEntityTypeConfiguration<BenchmarkTemplate>
{
    public void Configure(EntityTypeBuilder<BenchmarkTemplate> entity)
    {
        entity.ToTable("BenchmarkTemplates");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(120);

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Description)
            .HasMaxLength(2000);

        entity.Property(x => x.GuidancePath)
            .HasMaxLength(500);

        entity.Property(x => x.MetricsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        entity.Property(x => x.IsEnabled)
            .IsRequired();

        entity.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.OwnerId, x.Key })
            .IsUnique();
    }
}
