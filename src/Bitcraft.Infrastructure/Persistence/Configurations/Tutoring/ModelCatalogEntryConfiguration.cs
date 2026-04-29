using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class ModelCatalogEntryConfiguration : IEntityTypeConfiguration<ModelCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ModelCatalogEntry> entity)
    {
        entity.ToTable("ModelCatalogEntries");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.ProviderType)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.Endpoint)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.ModelName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.ApiKeySecretRef)
            .HasMaxLength(200);

        entity.Property(x => x.SettingsJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.IsEnabled).IsRequired();

        entity.HasIndex(x => x.Name).IsUnique();
    }
}