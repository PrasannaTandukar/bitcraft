using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Modeling;

/// <summary>
/// Admin-managed connectivity to an upstream model runtime (D11). Many <c>Model</c> rows may share one engine
/// (e.g. a single Ollama instance serving several models). Health checks and richer engine lifecycle are deferred.
/// </summary>
public class RuntimeEngine : BaseModel
{
    /// <summary>Human-readable name. Unique across all runtime engines.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Locked provider vocabulary.</summary>
    public RuntimeEngineProviderType ProviderType { get; set; }

    /// <summary>Base URL or socket reference for the upstream runtime.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key credential for the upstream runtime. Stored as the raw key value at rest today;
    /// a deployment secret store is future work. Never returned by API responses.
    /// </summary>
    public string? ApiKeySecretRef { get; set; }

    /// <summary>
    /// Gates selection of any child <c>Model</c> in tutor / knowledge UI. Disabling does not delete dependent models.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
