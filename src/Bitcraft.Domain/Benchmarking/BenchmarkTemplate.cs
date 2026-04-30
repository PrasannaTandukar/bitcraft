using Bitcraft.Domain.Common;
using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Benchmarking;

public class BenchmarkTemplate : BaseModel
{
    public Guid OwnerId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? GuidancePath { get; set; }
    public string MetricsJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;

    public AppUser? Owner { get; set; }
}
