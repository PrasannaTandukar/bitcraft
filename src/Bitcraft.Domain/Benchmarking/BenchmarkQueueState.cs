using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Benchmarking;

/// <summary>
/// Single-row global state for the standard benchmark run queue (HD-8).
/// When <see cref="IsPaused"/> is true the worker stops picking up queued
/// runs; a run already executing finishes to completion. Cancel is terminal;
/// pause only gates scheduling.
/// </summary>
public class BenchmarkQueueState : BaseModel
{
    /// <summary>True when the queue is paused. The worker stops dequeuing new runs.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Timestamp of the last pause or resume toggle.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
