using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Modeling;

/// <summary>
/// Abstract base for the admin-managed Model TPH hierarchy (D11 / D25 step 4). Each <see cref="Model"/> is
/// served by a single <see cref="RuntimeEngine"/>; many models may share the same engine. Concrete leaves
/// (<see cref="InteractionModel"/>, <see cref="EmbeddingModel"/>) capture role-specific properties.
/// </summary>
public abstract class Model : BaseModel
{
    /// <summary>Human-readable name. Unique across all models regardless of kind.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>FK to the <see cref="RuntimeEngine"/> serving this model. Restrict-delete at the database level.</summary>
    public Guid RuntimeEngineId { get; set; }

    /// <summary>EF navigation to the runtime engine.</summary>
    public RuntimeEngine? RuntimeEngine { get; set; }

    /// <summary>The model identifier sent to the upstream runtime (e.g. <c>llama3:8b</c>, <c>gpt-4o-mini</c>).</summary>
    public string ProviderModelName { get; set; } = string.Empty;

    /// <summary>Per-model default settings (temperature, top_p, etc.) as opaque JSON; consumed by the runtime adapter.</summary>
    public string? SettingsJson { get; set; }

    /// <summary>Gates selection of this model anywhere in tutor / knowledge UI.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Discriminator surfaced to application code (matches the EF TPH column).</summary>
    public abstract ModelKind Kind { get; }
}
