using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.Domain.Knowledge;

public class KnowledgeChunk : BaseModel
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }

    /// <summary>Legacy free-text label of the embedding model. Preserved for back-compat; new rows should also populate <see cref="EmbeddingModelId"/>.</summary>
    public string EmbeddingModel { get; set; } = string.Empty;

    /// <summary>Legacy free-text label of the embedding provider (e.g. <c>Ollama</c>). Preserved for back-compat.</summary>
    public string EmbeddingProvider { get; set; } = string.Empty;

    /// <summary>FK to the admin-managed <see cref="Bitcraft.Domain.Modeling.EmbeddingModel"/> that produced this row (D25 step 6). Nullable for legacy rows ingested before the FK existed.</summary>
    public Guid? EmbeddingModelId { get; set; }

    /// <summary>EF navigation to the embedding model used to produce <see cref="Embedding"/>.</summary>
    public EmbeddingModel? EmbeddingModelEntity { get; set; }

    /// <summary>Dimension count of <see cref="Embedding"/>. Must equal <see cref="EmbeddingModel"/>'s declared dimensions when <see cref="EmbeddingModelId"/> is set.</summary>
    public int? EmbeddingDimensions { get; set; }

    public DateTime IngestedUtc { get; set; } = DateTime.UtcNow;
    public Document? Document { get; set; }
}