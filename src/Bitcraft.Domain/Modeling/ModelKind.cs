namespace Bitcraft.Domain.Modeling;

/// <summary>
/// Discriminator for the <see cref="Model"/> TPH hierarchy. Persisted as the EF string discriminator column
/// <c>ModelKind</c>. Values are part of the on-disk contract and must not be renamed.
/// </summary>
public enum ModelKind
{
    /// <summary>A model used to generate tutor responses (chat / completion).</summary>
    Interaction = 0,

    /// <summary>A model used to embed text for retrieval-augmented generation (RAG).</summary>
    Embedding = 1,

    /// <summary>Reserved: a judge / evaluator model used by benchmarking. Not surfaced for the feature-freeze release.</summary>
    Judge = 100,

    /// <summary>Reserved: a reranker model used after retrieval. Not surfaced for the feature-freeze release.</summary>
    Reranker = 101
}
