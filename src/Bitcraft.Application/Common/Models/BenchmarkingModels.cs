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
    double? GenerationLatencyMs);

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

public sealed record StandardLlmBenchmarkResult(
    string BenchmarkKey,
    double? OverallScore,
    int TaskCount,
    string EngineVersion,
    string Notes,
    string? Error);

public sealed record ArduinoBenchmarkCaseInput(
    string CaseId,
    string Category,
    string InputPrompt,
    string? ImageUrl,
    string? SourceCode,
    string? ExpectedCircuitFlaw,
    string? ExpectedGuidanceConcept,
    double DetailDetectionWeight = 0.20,
    double CircuitUnderstandingWeight = 0.30,
    double SolutionReasoningWeight = 0.30,
    double GuidancePedagogyWeight = 0.20);

public sealed record ArduinoBenchmarkCaseResult(
    string CaseId,
    double OverallScore,
    double DetailDetectionScore,
    double CircuitUnderstandingScore,
    double SolutionReasoningScore,
    double GuidancePedagogyScore,
    string FeedbackSummary);
