using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;

namespace Bitcraft.Domain.Knowledge;

public class KnowledgeChunk : BaseModel
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public string EmbeddingModel { get; set; } = string.Empty;
    public string EmbeddingProvider { get; set; } = string.Empty;
    public DateTime IngestedUtc { get; set; } = DateTime.UtcNow;
    public Document? Document { get; set; }
}