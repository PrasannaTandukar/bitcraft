using Bitcraft.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations;

public class DocumentFolderConfiguration : IEntityTypeConfiguration<DocumentFolder>
{
    public void Configure(EntityTypeBuilder<DocumentFolder> entity)
    {
        entity.ToTable("DocumentFolders");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.HasOne(x => x.ParentFolder)
            .WithMany()
            .HasForeignKey(x => x.ParentFolderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.ParentFolderId);
        entity.HasIndex(x => new { x.CreatedById, x.ParentFolderId, x.Name });
    }
}
