using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace backend.Models;

public class DocumentChunk
{
    public int Id { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    public int ChunkIndex { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }
    
    [Required]

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public Document? Document { get; set; }
}