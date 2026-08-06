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
            .HasColumnType("vector(768)")
            .HasConversion(
                value => value == null ? null : new Vector(value),
                value => value == null ? null : value.ToArray());

        embeddingProperty.Metadata.SetValueComparer(new ValueComparer<float[]?>(
            (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value == null ? null : value.ToArray()));

        entity.HasIndex(x => x.CreatedById);

        entity.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
