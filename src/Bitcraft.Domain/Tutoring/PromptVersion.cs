using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class PromptVersion : BaseModel
{
    public Guid TutorId { get; set; }
    public int VersionNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Tutor? Tutor { get; set; }
}
