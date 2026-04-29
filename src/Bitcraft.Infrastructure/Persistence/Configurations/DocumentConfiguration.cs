using Bitcraft.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> entity)
    {
        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.SourcePath)
            .HasMaxLength(500);

        entity.Property(x => x.MimeType)
            .HasMaxLength(200);

        entity.Property(x => x.RawText);

        entity.HasIndex(x => x.Name);
    }
}
