using Bitcraft.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations;

internal static class BaseModelConfigurationExtensions
{
    public static void ConfigureBaseModel<TEntity>(this EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseModel
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.CreatedUtc).IsRequired();
        entity.Property(x => x.CreatedById).IsRequired();
        entity.Property(x => x.ModifiedUtc);
        entity.Property(x => x.ModifiedById);

        entity.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ModifiedBy)
            .WithMany()
            .HasForeignKey(x => x.ModifiedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
