using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class KnowledgeSourceLinkConfiguration : IEntityTypeConfiguration<KnowledgeSourceLink>
{
    public void Configure(EntityTypeBuilder<KnowledgeSourceLink> entity)
    {
        entity.ToTable("TutorKnowledgeSources");

        entity.ConfigureBaseModel();

        entity.Property(x => x.LinkedUtc).IsRequired();

        entity.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.TutorId, x.DocumentId })
            .IsUnique();
    }
}
