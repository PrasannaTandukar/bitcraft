namespace Bitcraft.Domain.Modeling;

/// <summary>
/// A <see cref="Model"/> used to generate tutor responses (chat / completion). Selectable in a tutor's
/// <c>TutorModelConnection</c>.
/// </summary>
public class InteractionModel : Model
{
    public override ModelKind Kind => ModelKind.Interaction;
}
