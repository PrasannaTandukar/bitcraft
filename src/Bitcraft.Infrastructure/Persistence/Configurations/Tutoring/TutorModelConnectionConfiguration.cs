using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class TutorModelConnectionConfiguration : IEntityTypeConfiguration<TutorModelConnection>
{
    public void Configure(EntityTypeBuilder<TutorModelConnection> entity)
    {
        entity.ToTable("TutorModelConnections");

        entity.ConfigureBaseModel();

        entity.Property(x => x.OverrideSettingsJson).HasColumnType("jsonb");

        entity.HasOne(x => x.ModelCatalogEntry)
            .WithMany()
            .HasForeignKey(x => x.ModelCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.TutorId).IsUnique();
    }
}