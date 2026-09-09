using Bitcraft.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Chat;

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> entity)
    {
        entity.ToTable("ChatAttachments");
        entity.ConfigureBaseModel();
        entity.Property(x => x.OriginalFileName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.MimeType).HasMaxLength(50).IsRequired();
        entity.Property(x => x.StorageKey).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.StorageKey).IsUnique();
        entity.HasIndex(x => x.ExpiresUtc);
        entity.HasIndex(x => new { x.CreatedById, x.Id });
    }
}
