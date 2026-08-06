using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class TutorDocumentConfiguration : IEntityTypeConfiguration<TutorDocument>
{
    public void Configure(EntityTypeBuilder<TutorDocument> entity)
    {
        entity.ToTable("TutorDocuments");

        entity.ConfigureBaseModel();

        entity.Property(x => x.AttachedVia)
            .HasConversion<int>()
            .IsRequired();

        entity.HasOne(x => x.Tutor)
            .WithMany(t => t.TutorDocuments)
            .HasForeignKey(x => x.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.TutorId, x.DocumentId }).IsUnique();
        entity.HasIndex(x => x.DocumentId);
    }
}

public class TutorDocumentFolderConfiguration : IEntityTypeConfiguration<TutorDocumentFolder>
{
    public void Configure(EntityTypeBuilder<TutorDocumentFolder> entity)
    {
        entity.ToTable("TutorDocumentFolders");

        entity.ConfigureBaseModel();

        entity.HasOne(x => x.Tutor)
            .WithMany(t => t.TutorDocumentFolders)
            .HasForeignKey(x => x.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.DocumentFolder)
            .WithMany()
            .HasForeignKey(x => x.DocumentFolderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.TutorId, x.DocumentFolderId }).IsUnique();
        entity.HasIndex(x => x.DocumentFolderId);
    }
}
