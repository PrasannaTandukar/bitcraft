using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Modeling;

public class RuntimeEngineConfiguration : IEntityTypeConfiguration<RuntimeEngine>
{
    public void Configure(EntityTypeBuilder<RuntimeEngine> entity)
    {
        entity.ToTable("RuntimeEngines");

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

        entity.Property(x => x.ApiKeySecretRef)
            .HasMaxLength(200);

        entity.Property(x => x.IsEnabled).IsRequired();

        entity.HasIndex(x => x.Name).IsUnique();
    }
}
