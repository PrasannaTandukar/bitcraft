namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// D25 step 7 (plan.erm.md §7.4): materializes the effective document set and knowledge
/// binding for a tutor at activation time. The result is captured into the
/// <c>TutorConfigurationSnapshot</c> so benchmarking and replay are reproducible.
/// </summary>
public interface IRevisionEffectiveSetReader
{
    Task<EffectiveDocumentSet> GetEffectiveSetAsync(Guid tutorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Effective document set captured at activation time.
/// </summary>
/// <param name="DocumentIds">Distinct document ids resolved from direct + folder attachments.</param>
/// <param name="KnowledgeBindings">Per-document embedding metadata required to reproduce retrieval.</param>
public sealed record EffectiveDocumentSet(
    IReadOnlyList<Guid> DocumentIds,
    IReadOnlyList<EffectiveKnowledgeBinding> KnowledgeBindings);

public sealed record EffectiveKnowledgeBinding(
    Guid DocumentId,
    Guid EmbeddingModelId,
    int Dimensions);
