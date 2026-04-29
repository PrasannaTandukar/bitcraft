using Bitcraft.Domain.Common;
using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Immutable point-in-time capture of a tutor's full configuration. Used as a
/// reproducible reference for benchmarking runs and historical comparison.
/// All properties have private setters; instances are constructed via <see cref="Capture"/>.
/// </summary>
public class TutorConfigurationSnapshot : BaseModel
{
    public Guid TutorId { get; private set; }
    public string TutorName { get; private set; } = string.Empty;
    public Guid? PromptVersionId { get; private set; }
    public string? PromptContent { get; private set; }
    public Guid? TutorModelConnectionId { get; private set; }
    public Guid? ModelCatalogEntryId { get; private set; }
    public ModelProviderType? ProviderType { get; private set; }
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

        var catalogEntry = tutorModelConnection?.ModelCatalogEntry;

        return new TutorConfigurationSnapshot
        {
            TutorId = tutor.Id,
            TutorName = tutor.Name,
            PromptVersionId = promptVersion?.Id,
            PromptContent = promptVersion?.Content,
            TutorModelConnectionId = tutorModelConnection?.Id,
            ModelCatalogEntryId = catalogEntry?.Id,
            ProviderType = catalogEntry?.ProviderType,
            ModelName = catalogEntry?.ModelName,
            ModelSettingsJson = tutorModelConnection?.OverrideSettingsJson ?? catalogEntry?.SettingsJson,
            ToolsJson = toolsJson,
            KnowledgeSourcesJson = knowledgeSourcesJson,
            DeploymentProfileJson = deploymentProfile?.ConfigurationJson,
            CapturedUtc = DateTime.UtcNow,
            CapturedById = capturedById
        };
    }
}
