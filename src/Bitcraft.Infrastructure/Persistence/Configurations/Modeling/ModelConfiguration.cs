using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Modeling;

/// <summary>
/// TPH configuration for the <see cref="Model"/> hierarchy. Single <c>Models</c> table with a string
/// discriminator column <c>ModelKind</c> (values <c>Interaction</c> / <c>Embedding</c>). Leaf-specific
/// columns (e.g. <c>EmbeddingDimensions</c>) are nullable at the database level.
/// </summary>
public class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> entity)
    {
        entity.ToTable("Models");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.ProviderModelName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.SettingsJson).HasColumnType("jsonb");

        entity.HasIndex(x => x.Name).IsUnique();

        entity.HasOne(x => x.RuntimeEngine)
            .WithMany()
            .HasForeignKey(x => x.RuntimeEngineId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.RuntimeEngineId);

        // TPH discriminator. The on-disk discriminator values are part of the contract; do not rename.
        entity.HasDiscriminator<string>("ModelKind")
            .HasValue<InteractionModel>("Interaction")
            .HasValue<EmbeddingModel>("Embedding");

        // Ignore the abstract Kind navigation: it is derived from the runtime CLR type via TPH.
        entity.Ignore(x => x.Kind);
    }
}
