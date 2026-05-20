using Bitcraft.Domain.Common;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Domain.Benchmarking;

public class BenchmarkRun : BaseModel
{
    public Guid TutorId { get; set; }

    /// <summary>FK <see cref="TutorRevision"/> (D25 step 8, plan.erm.md §8). Restrict on delete. Nullable for legacy benchmark rows captured before revisions existed.</summary>
    public Guid? TutorRevisionId { get; set; }
    public TutorRevision? TutorRevision { get; set; }

    public Guid? TutorConfigurationSnapshotId { get; set; }
    public Guid BenchmarkTemplateId { get; set; }
    public Guid? InteractionModelId { get; set; }
    public string? ModelAlias { get; set; }
    public int ConsistencyRuns { get; set; } = 1;
    public BenchmarkRunStatus Status { get; set; } = BenchmarkRunStatus.Queued;
    public DateTime QueuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public int TotalCaseCount { get; set; }
    public int CompletedCaseCount { get; set; }
    public double ProgressPercent { get; set; }
    public string? ProgressStage { get; set; }
    public string? ProgressMessage { get; set; }
    public DateTime? LastProgressUtc { get; set; }
    public string? EngineName { get; set; }
    public string? EngineVersion { get; set; }
    public double? AverageScore { get; set; }
    public double? PassRate { get; set; }
    public double? TotalLatencyMs { get; set; }
    public double? TotalTokens { get; set; }
    public double? TokensPerSecond { get; set; }
    public double? EstimatedCo2Grams { get; set; }
    public string? SummaryJson { get; set; }
    public string? SystemMetricsJson { get; set; }
    public string? RawResultJson { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureDescription { get; set; }

    public Tutor? Tutor { get; set; }
    public TutorConfigurationSnapshot? TutorConfigurationSnapshot { get; set; }
    public BenchmarkTemplate? BenchmarkTemplate { get; set; }
    public InteractionModel? InteractionModel { get; set; }
    public ICollection<BenchmarkRunCase> Cases { get; set; } = new List<BenchmarkRunCase>();

    public void UpdateProgress(int completedCaseCount, int totalCaseCount, string stage, string? message)
    {
        TotalCaseCount = Math.Max(totalCaseCount, 0);
        CompletedCaseCount = Math.Clamp(completedCaseCount, 0, TotalCaseCount);
        ProgressPercent = TotalCaseCount == 0 ? 0 : Math.Round((double)CompletedCaseCount / TotalCaseCount * 100, 2);
        ProgressStage = stage;
        ProgressMessage = message;
        LastProgressUtc = DateTime.UtcNow;
    }
}

public enum BenchmarkRunStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
