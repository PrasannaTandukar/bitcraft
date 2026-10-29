using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class DeploymentProfile : BaseModel
{
    public Guid TutorId { get; set; }
    public DeploymentTarget Target { get; set; } = DeploymentTarget.Local;
    public string? ConfigurationJson { get; set; }
    public Tutor? Tutor { get; set; }
}
