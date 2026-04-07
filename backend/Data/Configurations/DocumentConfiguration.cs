using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.SourcePath)
            .HasMaxLength(500);

        entity.Property(x => x.CreatedAtUtc)
            .IsRequired();

        entity.HasMany(x => x.Chunks)
            .WithOne(x => x.Document)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name);
    }
}