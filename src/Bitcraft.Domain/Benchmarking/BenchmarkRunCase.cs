using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Benchmarking;

public class BenchmarkRunCase : BaseModel
{
    public Guid BenchmarkRunId { get; set; }
    public int CaseIndex { get; set; }
    public string InputText { get; set; } = string.Empty;
    public string ActualOutput { get; set; } = string.Empty;
    public string? ExpectedOutput { get; set; }
    public string? ContextText { get; set; }
    public string? MetadataJson { get; set; }

    public bool? Passed { get; set; }
    public double? Score { get; set; }
    public string? Reason { get; set; }
    public string? MetricsJson { get; set; }
    public string? PerformanceJson { get; set; }
    public string? ResourceJson { get; set; }

    public BenchmarkRun? BenchmarkRun { get; set; }
}
