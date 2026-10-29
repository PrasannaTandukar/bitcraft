using Bitcraft.Domain.Common;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.Domain.Tutoring;

public class TutorModelConnection : BaseModel
{
    public Guid TutorId { get; set; }
    public Guid InteractionModelId { get; set; }
    public string? OverrideSettingsJson { get; set; }
    public Tutor? Tutor { get; set; }
    public InteractionModel? InteractionModel { get; set; }
}