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
    int TopK);

public sealed record GeneratedBenchmarkOutput(
    string ActualOutput,
    string ModelUsed,
    string? ContextText,
    double? GenerationLatencyMs);
