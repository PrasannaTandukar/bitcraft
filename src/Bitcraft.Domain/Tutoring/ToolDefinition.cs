using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class ToolDefinition : BaseModel
{
    public Guid TutorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JsonSchema { get; set; } = string.Empty;
    public string? ExecutionUrl { get; set; }
    public Tutor? Tutor { get; set; }
}
