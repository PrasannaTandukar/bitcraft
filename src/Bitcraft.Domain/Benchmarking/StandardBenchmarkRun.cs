using Bitcraft.Domain.Common;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.Domain.Benchmarking;

/// <summary>
/// Model-scoped run of a standardized DeepEval benchmark suite (MMLU, GSM8K, etc.).
/// Deliberately separate from <see cref="BenchmarkRun"/> (tutor-scoped case scoring) to keep
/// continuous Model Evaluation distinct from systematic Tutor Benchmarking.
/// </summary>
public class StandardBenchmarkRun : BaseModel
{
    /// <summary>FK <see cref="InteractionModel"/>. The model under evaluation.</summary>
    public Guid InteractionModelId { get; set; }
    public InteractionModel? InteractionModel { get; set; }

    /// <summary>Canonical benchmark key, e.g. "mmlu" or "gsm8k". Validated against the suite registry.</summary>
    public string BenchmarkKey { get; set; } = string.Empty;

    /// <summary>JSON array of requested task names; null or empty runs the full default task set.</summary>
    public string? TasksJson { get; set; }

    /// <summary>Few-shot example count (0-5).</summary>
    public int NShots { get; set; }

    /// <summary>Problem cap. 0 means no cap (full default dataset). Positive values cap per task on task-based benchmarks.</summary>
    public int NProblems { get; set; } = 10;

    public BenchmarkRunStatus Status { get; set; } = BenchmarkRunStatus.Queued;
    public DateTime QueuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Coarse execution stage reported by the engine (loading-dataset, evaluating, scoring).</summary>
    public string? ProgressStage { get; set; }

    /// <summary>Number of problems completed so far, as reported by the engine. Null until the engine reports counts.</summary>
    public int? CompletedTasks { get; set; }

    /// <summary>Total number of problems to evaluate, as reported by the engine. Null until the engine reports counts.</summary>
    public int? TotalTasks { get; set; }

    /// <summary>Progress percentage (0-100) derived from completed/total. Null until the engine reports counts.</summary>
    public double? ProgressPercent { get; set; }

    /// <summary>Timestamp of the last progress update received from the engine.</summary>
    public DateTime? LastProgressUtc { get; set; }

    /// <summary>
    /// Engine-side run id returned when the run started. Required to cancel or read the
    /// log of a run that is already executing. Null while the run is queued.
    /// </summary>
    public string? EngineRunId { get; set; }

    /// <summary>Timestamp when the run was cancelled. Null unless the status is Cancelled.</summary>
    public DateTime? CancelledUtc { get; set; }

    /// <summary>Queue priority. Lower values run first (DR-5). Zero is normal.</summary>
    public int Priority { get; set; }

    public string? EngineName { get; set; }
    public string? EngineVersion { get; set; }

    /// <summary>Overall accuracy across all evaluated tasks/problems (0-1).</summary>
    public double? OverallScore { get; set; }

    /// <summary>Number of tasks evaluated as reported by the engine.</summary>
    public int TaskCount { get; set; }

    /// <summary>JSON array of per-task scores, when the engine reports them.</summary>
    public string? TaskScoresJson { get; set; }

    /// <summary>Engine notes, e.g. the effective problem cap applied.</summary>
    public string? Notes { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureDescription { get; set; }
}
