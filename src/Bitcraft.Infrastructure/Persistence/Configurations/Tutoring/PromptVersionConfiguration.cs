using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> entity)
    {
        entity.ToTable("PromptVersions");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Content)
            .IsRequired();

        entity.Property(x => x.Notes)
            .HasMaxLength(2000);

        entity.Property(x => x.VersionNumber).IsRequired();

        entity.HasIndex(x => new { x.TutorId, x.VersionNumber })
            .IsUnique();
    }
}
