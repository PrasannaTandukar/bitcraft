namespace Bitcraft.Domain.Modeling;

/// <summary>
/// A <see cref="Model"/> used to generate tutor responses (chat / completion). Selectable in a tutor's
/// <c>TutorModelConnection</c>.
/// </summary>
public class InteractionModel : Model
{
    public ModelCapabilities Capabilities { get; set; } = ModelCapabilities.None;

    public override ModelKind Kind => ModelKind.Interaction;
}
