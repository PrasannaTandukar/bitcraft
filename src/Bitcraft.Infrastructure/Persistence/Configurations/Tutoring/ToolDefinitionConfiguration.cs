using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> entity)
    {
        entity.ToTable("ToolDefinitions");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Description)
            .HasMaxLength(2000);

        entity.Property(x => x.JsonSchema)
            .IsRequired()
            .HasColumnType("jsonb");

        entity.Property(x => x.ExecutionUrl)
            .HasMaxLength(500);

        entity.HasIndex(x => new { x.TutorId, x.Name })
            .IsUnique();
    }
}
