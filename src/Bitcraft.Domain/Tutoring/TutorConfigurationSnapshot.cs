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

    /// <summary>Sequential per-tutor revision number assigned at capture time. Forms the logical "tutor revision" identity (D25 step 7).</summary>
    public int VersionNumber { get; private set; }

    public string TutorName { get; private set; } = string.Empty;
    public Guid? PromptVersionId { get; private set; }
    public string? PromptContent { get; private set; }
    public Guid? TutorModelConnectionId { get; private set; }
    public Guid? InteractionModelId { get; private set; }
    public string? ModelName { get; private set; }
    public string? ModelSettingsJson { get; private set; }
    public string? ToolsJson { get; private set; }
    public string? KnowledgeSourcesJson { get; private set; }
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
        Guid capturedById)
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

        var interactionModel = tutorModelConnection?.InteractionModel;

        return new TutorConfigurationSnapshot
        {
            TutorId = tutor.Id,
            VersionNumber = versionNumber,
            TutorName = tutor.Name,
            PromptVersionId = promptVersion?.Id,
            PromptContent = promptVersion?.Content,
            TutorModelConnectionId = tutorModelConnection?.Id,
            InteractionModelId = interactionModel?.Id,
            ModelName = interactionModel?.ProviderModelName,
            ModelSettingsJson = tutorModelConnection?.OverrideSettingsJson ?? interactionModel?.SettingsJson,
            ToolsJson = toolsJson,
            KnowledgeSourcesJson = knowledgeSourcesJson,
            DeploymentProfileJson = deploymentProfile?.ConfigurationJson,
            CapturedUtc = DateTime.UtcNow,
            CapturedById = capturedById
        };
    }
}
