using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Knowledge;

/// <summary>A saved tutor conversation that can be retrieved as future context.</summary>
public class TutorMemory : BaseModel
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
}
