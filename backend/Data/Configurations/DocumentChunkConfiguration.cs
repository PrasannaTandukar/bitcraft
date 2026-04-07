using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Content)
            .IsRequired();
        
        entity.HasOne(x => x.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasIndex(x => new { x.DocumentId, x.ChunkIndex })
            .IsUnique();

        entity.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}