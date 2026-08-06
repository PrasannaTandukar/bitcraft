namespace Bitcraft.Domain.Modeling;

/// <summary>
/// A <see cref="Model"/> used to embed text for retrieval-augmented generation. Carries the fixed
/// embedding dimension count used to validate <c>vector_dims</c> on persisted <c>KnowledgeChunk</c> rows
/// (D25 step 6).
/// </summary>
public class EmbeddingModel : Model
{
    public override ModelKind Kind => ModelKind.Embedding;

    /// <summary>
    /// Fixed dimension of vectors produced by this model. Required and immutable for a given row;
    /// changing dimensions requires creating a new <see cref="EmbeddingModel"/>.
    /// </summary>
    public int EmbeddingDimensions { get; set; }
}
