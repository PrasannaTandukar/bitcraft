using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Modeling;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Bitcraft.Infrastructure.Persistence.Configurations;

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> entity)
    {
        entity.ToTable("KnowledgeChunks");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Content)
            .IsRequired();

        // D25 step 6: embeddings are now per-model variable dimension. The column is
        // declared as an untyped pgvector so rows from different EmbeddingModels can
        // coexist. ANN indexing (HNSW) requires a fixed-dim column; we defer ANN
        // until per-model partitioning is introduced.
        var embeddingProperty = entity.Property(x => x.Embedding)
            .HasColumnType("vector")
            .HasConversion(
                v => v == null ? null : new Vector(v),
                v => v == null ? null : v.ToArray());

        embeddingProperty.Metadata.SetValueComparer(new ValueComparer<float[]?>(
            (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value == null ? null : value.ToArray()));

        entity.Property(x => x.EmbeddingModel)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.EmbeddingProvider)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.EmbeddingDimensions);

        entity.Property(x => x.IngestedUtc).IsRequired();

        entity.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.EmbeddingModelEntity)
            .WithMany()
            .HasForeignKey(x => x.EmbeddingModelId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.DocumentId, x.ChunkIndex })
            .IsUnique();

        entity.HasIndex(x => x.EmbeddingModelId);
    }
}
