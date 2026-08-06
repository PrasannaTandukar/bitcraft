using Bitcraft.Domain.Common;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Uniform tutor versioning unit (D25 step 7, plan.erm.md §7.2). A revision is the editable
/// composition of everything required to run a tutor and is the canonical addressable target
/// for activation, benchmarking, and audit.
/// </summary>
public class TutorRevision : BaseModel
{
    public Guid TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    /// <summary>Monotonically increasing per tutor. Unique <c>(TutorId, VersionNumber)</c>.</summary>
    public int VersionNumber { get; set; }

    public RevisionStatus Status { get; set; } = RevisionStatus.Draft;

    /// <summary>FK PromptVersion (Restrict). Required at publish-time.</summary>
    public Guid? PromptVersionId { get; set; }
    public PromptVersion? PromptVersion { get; set; }

    /// <summary>FK Model filtered to <see cref="InteractionModel"/> (Restrict). Required at publish-time.</summary>
    public Guid? InteractionModelId { get; set; }
    public InteractionModel? InteractionModel { get; set; }

    /// <summary>FK Model filtered to <see cref="EmbeddingModel"/> used to embed runtime queries (Restrict).</summary>
    public Guid? QueryEmbeddingModelId { get; set; }
    public EmbeddingModel? QueryEmbeddingModel { get; set; }

    /// <summary>JSON payload: temperature, top_p, max_tokens, stop sequences, etc.</summary>
    public string? InteractionParametersJson { get; set; }

    public DeploymentTarget DeploymentTarget { get; set; } = DeploymentTarget.Local;
    public string? DeploymentConfigJson { get; set; }

    public DateTime? PublishedUtc { get; set; }
    public Guid? PublishedById { get; set; }

    public DateTime? ActivatedUtc { get; set; }
    public Guid? ActivatedById { get; set; }

    public DateTime? ArchivedUtc { get; set; }
    public Guid? ArchivedById { get; set; }

    /// <summary>D22.b. Non-null only while <see cref="Status"/> is <see cref="RevisionStatus.Active"/>. Unique partial index.</summary>
    public string? ActivationSlug { get; set; }

    /// <summary>D22.b. One-revision redirect window for the prior active slug; cleared on the next activation.</summary>
    public string? PreviousActivationSlug { get; set; }

    public ICollection<TutorRevisionTool> RevisionTools { get; set; } = new List<TutorRevisionTool>();
    public ICollection<TutorConfigurationSnapshot> Snapshots { get; set; } = new List<TutorConfigurationSnapshot>();

    /// <summary>Transition Draft → Published. Enforces publish prerequisites.</summary>
    public void Publish(Guid publishedByUserId)
    {
        if (publishedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Publishing user id is required.", nameof(publishedByUserId));
        }
        if (Status != RevisionStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft revisions may be published (current: {Status}).");
        }
        if (PromptVersionId is null || InteractionModelId is null)
        {
            throw new InvalidOperationException("Cannot publish without PromptVersion and InteractionModel.");
        }

        Status = RevisionStatus.Published;
        PublishedUtc = DateTime.UtcNow;
        PublishedById = publishedByUserId;
    }

    /// <summary>Transition Published → Active. Caller must archive any prior active revision in the same transaction.</summary>
    public void Activate(Guid activatedByUserId, string activationSlug, string? previousActivationSlug)
    {
        if (activatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Activating user id is required.", nameof(activatedByUserId));
        }
        if (Status != RevisionStatus.Published)
        {
            throw new InvalidOperationException($"Only Published revisions may be activated (current: {Status}).");
        }
        if (string.IsNullOrWhiteSpace(activationSlug))
        {
            throw new ArgumentException("Activation slug is required.", nameof(activationSlug));
        }

        Status = RevisionStatus.Active;
        ActivatedUtc = DateTime.UtcNow;
        ActivatedById = activatedByUserId;
        ActivationSlug = activationSlug;
        PreviousActivationSlug = previousActivationSlug;
    }

    /// <summary>Transition Active or Published → Archived. Clears the active slug.</summary>
    public void Archive(Guid archivedByUserId)
    {
        if (archivedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Archiving user id is required.", nameof(archivedByUserId));
        }
        if (Status != RevisionStatus.Active && Status != RevisionStatus.Published)
        {
            throw new InvalidOperationException($"Only Published or Active revisions may be archived (current: {Status}).");
        }

        Status = RevisionStatus.Archived;
        ArchivedUtc = DateTime.UtcNow;
        ArchivedById = archivedByUserId;
        ActivationSlug = null;
    }
}

/// <summary>
/// M:N join between <see cref="TutorRevision"/> and <see cref="ToolDefinition"/>. Composes
/// multiple tools into a revision. Unique <c>(TutorRevisionId, ToolDefinitionId)</c>.
/// </summary>
public class TutorRevisionTool : BaseModel
{
    public Guid TutorRevisionId { get; set; }
    public TutorRevision? TutorRevision { get; set; }

    public Guid ToolDefinitionId { get; set; }
    public ToolDefinition? ToolDefinition { get; set; }
}
