using Bitcraft.Domain.Common;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Immutable point-in-time capture of a tutor's full configuration. Used as a
/// reproducible reference for benchmarking runs and historical comparison.
/// All properties have private setters; instances are constructed via <see cref="Capture"/>.
/// </summary>
public class TutorConfigurationSnapshot : BaseModel
{
    public Guid TutorId { get; private set; }

    /// <summary>FK <see cref="TutorRevision"/> (D25 step 7, plan.erm.md §7.4). Restrict on delete. Nullable for legacy snapshots captured before revisions existed.</summary>
    public Guid? TutorRevisionId { get; private set; }
    public TutorRevision? TutorRevision { get; private set; }

    /// <summary>Sequential per-tutor revision number assigned at capture time. Mirrors <see cref="TutorRevision.VersionNumber"/> when the snapshot was captured against a revision.</summary>
    public int VersionNumber { get; private set; }

    public string TutorName { get; private set; } = string.Empty;
    public Guid? PromptVersionId { get; private set; }
    public string? PromptContent { get; private set; }
    public Guid? TutorModelConnectionId { get; private set; }
    public Guid? InteractionModelId { get; private set; }
    public string? ModelName { get; private set; }
    public ModelCapabilities ModelCapabilities { get; private set; }
    public string? ModelSettingsJson { get; private set; }
    public string? ToolsJson { get; private set; }
    public string? KnowledgeSourcesJson { get; private set; }

    /// <summary>Materialized effective document set (plan.erm.md §7.4). JSON array of document ids resolved from direct attachments + attached folders at activation time.</summary>
    public string? EffectiveDocumentIdsJson { get; private set; }

    /// <summary>Materialized <c>(DocumentId, EmbeddingModelId)</c> pairs the runtime depends on (plan.erm.md §7.4).</summary>
    public string? EffectiveKnowledgeBindingJson { get; private set; }

    public string? DeploymentProfileJson { get; private set; }
    public DateTime CapturedUtc { get; private set; } = DateTime.UtcNow;
    public Guid CapturedById { get; private set; }
    public AppUser? CapturedBy { get; private set; }

    private TutorConfigurationSnapshot() { }

    public static TutorConfigurationSnapshot Capture(
        Tutor tutor,
        int versionNumber,
        PromptVersion? promptVersion,
        TutorModelConnection? tutorModelConnection,
        string? toolsJson,
        string? knowledgeSourcesJson,
        DeploymentProfile? deploymentProfile,
        Guid capturedById,
        TutorRevision? tutorRevision = null,
        string? effectiveDocumentIdsJson = null,
        string? effectiveKnowledgeBindingJson = null,
        string? revisionDeploymentProfileJson = null)
    {
        ArgumentNullException.ThrowIfNull(tutor);
        if (capturedById == Guid.Empty)
        {
            throw new ArgumentException("Captured user id is required.", nameof(capturedById));
        }
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), versionNumber, "Version number must be positive.");
        }

        var interactionModel = tutorRevision?.InteractionModel ?? tutorModelConnection?.InteractionModel;

        return new TutorConfigurationSnapshot
        {
            TutorId = tutor.Id,
            TutorRevisionId = tutorRevision?.Id,
            TutorRevision = tutorRevision,
            VersionNumber = versionNumber,
            TutorName = tutor.Name,
            PromptVersionId = promptVersion?.Id,
            PromptContent = promptVersion?.Content,
            TutorModelConnectionId = tutorRevision is null ? tutorModelConnection?.Id : null,
            InteractionModelId = interactionModel?.Id,
            ModelName = interactionModel?.ProviderModelName,
            ModelCapabilities = interactionModel?.Capabilities ?? ModelCapabilities.None,
            ModelSettingsJson = tutorRevision is not null
                ? tutorRevision.InteractionParametersJson ?? interactionModel?.SettingsJson
                : tutorModelConnection?.OverrideSettingsJson ?? interactionModel?.SettingsJson,
            ToolsJson = toolsJson,
            KnowledgeSourcesJson = knowledgeSourcesJson,
            EffectiveDocumentIdsJson = effectiveDocumentIdsJson,
            EffectiveKnowledgeBindingJson = effectiveKnowledgeBindingJson,
            DeploymentProfileJson = revisionDeploymentProfileJson ?? deploymentProfile?.ConfigurationJson,
            CapturedUtc = DateTime.UtcNow,
            CapturedById = capturedById
        };
    }
}
