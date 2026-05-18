using Bitcraft.Domain.Common;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Tutoring;

public class Tutor : BaseModel
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TutorStatus Status { get; set; } = TutorStatus.Draft;
    public Guid? ActivePromptVersionId { get; set; }

    public AppUser? Owner { get; set; }
    public TutorModelConnection? TutorModelConnection { get; set; }
    public PromptVersion? ActivePromptVersion { get; set; }
    public DeploymentProfile? DeploymentProfile { get; set; }
    public ICollection<PromptVersion> PromptVersions { get; set; } = new List<PromptVersion>();
    public ICollection<KnowledgeSourceLink> KnowledgeSources { get; set; } = new List<KnowledgeSourceLink>();
    public ICollection<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    public ICollection<TutorConfigurationSnapshot> Snapshots { get; set; } = new List<TutorConfigurationSnapshot>();
    public ICollection<BenchmarkRun> BenchmarkRuns { get; set; } = new List<BenchmarkRun>();
}
