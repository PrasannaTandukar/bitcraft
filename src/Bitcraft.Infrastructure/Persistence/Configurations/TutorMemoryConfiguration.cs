using Bitcraft.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Bitcraft.Infrastructure.Persistence.Configurations;

public class TutorMemoryConfiguration : IEntityTypeConfiguration<TutorMemory>
{
    public void Configure(EntityTypeBuilder<TutorMemory> entity)
    {
        entity.ToTable("TutorMemories");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Question)
            .IsRequired();

        entity.Property(x => x.Answer)
            .IsRequired();

        entity.Property(x => x.Content)
            .IsRequired();

        var embeddingProperty = entity.Property(x => x.Embedding)
            .HasColumnType("vector")
            .HasConversion(
                value => value == null ? null : new Vector(value),
                value => value == null ? null : value.ToArray());

        embeddingProperty.Metadata.SetValueComparer(new ValueComparer<float[]?>(
            (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value == null ? null : value.ToArray()));

        entity.HasIndex(x => new { x.UserId, x.TutorId, x.EmbeddingModelId, x.EmbeddingDimensions });
        entity.HasIndex(x => x.ConversationId);
        entity.HasOne<Bitcraft.Domain.Identity.AppUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Bitcraft.Domain.Tutoring.Tutor>().WithMany().HasForeignKey(x => x.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Bitcraft.Domain.Tutoring.TutorRevision>().WithMany().HasForeignKey(x => x.TutorRevisionId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Bitcraft.Domain.Tutoring.TutorConfigurationSnapshot>().WithMany()
            .HasForeignKey(x => x.TutorConfigurationSnapshotId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Bitcraft.Domain.Modeling.EmbeddingModel>().WithMany().HasForeignKey(x => x.EmbeddingModelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
