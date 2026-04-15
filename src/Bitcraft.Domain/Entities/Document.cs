namespace Bitcraft.Domain.Entities;

public class Document
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
