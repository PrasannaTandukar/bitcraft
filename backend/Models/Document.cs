using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Document
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SourcePath { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}