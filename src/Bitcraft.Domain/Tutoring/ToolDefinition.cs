using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class ToolDefinition : BaseModel
{
    public Guid TutorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JsonSchema { get; set; } = string.Empty;
    public TutorToolKind Kind { get; set; } = TutorToolKind.BuiltIn;
    public string ImplementationKey { get; set; } = string.Empty;
    public TutorToolExecutionPolicy ExecutionPolicy { get; set; } = TutorToolExecutionPolicy.Disabled;
    public Tutor? Tutor { get; set; }
}

public enum TutorToolKind
{
    BuiltIn = 0,
    Mcp = 1,
    ApprovedHttp = 2
}

public enum TutorToolExecutionPolicy
{
    Disabled = 0,
    ReadOnlyAutomatic = 1,
    RequiresApproval = 2
}
