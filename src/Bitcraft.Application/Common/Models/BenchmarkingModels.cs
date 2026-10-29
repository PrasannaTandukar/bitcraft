namespace Bitcraft.Application.Common.Models;

public sealed record BenchmarkEngineRequest(
    string TemplateKey,
    int ConsistencyRuns,
    IReadOnlyList<BenchmarkEngineCaseRequest> Cases,
    string? GuidancePath,
    string? MetricsJson);

public sealed record BenchmarkEngineCaseRequest(
    Guid CaseId,
    int CaseIndex,
    string InputText,
    string ActualOutput,
    string? ExpectedOutput,
    string? ContextText,
    string? MetadataJson);

public sealed record BenchmarkEngineCaseResult(
    Guid CaseId,
    int CaseIndex,
    bool Passed,
    double Score,
    string Reason,
    string MetricsJson,
    string PerformanceJson,
    string ResourceJson);

public sealed record BenchmarkEngineSummary(
    double AverageScore,
    double PassRate,
    double TotalLatencyMs,
    double? TotalTokens,
    double? TokensPerSecond,
    double? EstimatedCo2Grams,
    string SummaryJson,
    string? SystemMetricsJson);

public sealed record BenchmarkEngineResult(
    string EngineName,
    string EngineVersion,
    BenchmarkEngineSummary Summary,
    IReadOnlyList<BenchmarkEngineCaseResult> Cases,
    string? RawResultJson);

public sealed record TutorBenchmarkGenerationRequest(
    Guid TutorId,
    Guid TutorRevisionId,
    Guid? TutorConfigurationSnapshotId,
    string InputText,
    string? ContextOverride,
    bool UseTutorKnowledge,
    int TopK,
    Guid UserId = default);

public sealed record GeneratedBenchmarkOutput(
    string ActualOutput,
    string ModelUsed,
    string? ContextText,
    double? GenerationLatencyMs,
    Guid? TutorRevisionId = null,
    Guid? TutorConfigurationSnapshotId = null,
    bool KnowledgeUsed = false,
    int SourceCount = 0,
    int ToolCallCount = 0,
    long? InputTokenCount = null,
    long? OutputTokenCount = null,
    long? TotalTokenCount = null);

public sealed record StandardBenchmarkTaskScore(string Task, double Score);

public sealed record StandardBenchmarkHandle(string RunId);

/// <summary>Sidecar engine status for the Admin control surface (HD-9).</summary>
public sealed record SidecarEngineStatus(
    bool IsReachable,
    string Transport,
    bool SlotHeld,
    string? CurrentRunId,
    string? EngineVersion,
    string? LastError);

/// <summary>Terminal or in-flight status of a standardized benchmark run.</summary>
public sealed record StandardBenchmarkStatus(
    string State,
    string? Stage,
    double? OverallScore,
    IReadOnlyList<StandardBenchmarkTaskScore>? TaskScores,
    int TaskCount,
    int? CompletedTasks,
    int? TotalTasks,
    string EngineVersion,
    string Notes,
    string? Error)
{
    public const string StateRunning = "running";
    public const string StateCompleted = "completed";
    public const string StateFailed = "failed";
    public const string StateCancelled = "cancelled";
}

public sealed record StandardLlmBenchmarkRequest(
    string BenchmarkKey,
    string Provider,
    string ModelName,
    string? BaseUrl = null,
    string? ApiKey = null,
    IReadOnlyList<string>? Tasks = null,
    int NShots = 0,
    int NProblems = 10,
    bool AllowCodeExecution = false);
