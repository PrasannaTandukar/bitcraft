namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to create a tutor benchmark run from collected model outputs.</summary>
public sealed record CreateTutorBenchmarkRunRequest(
    Guid BenchmarkTemplateId,
    Guid? TutorConfigurationSnapshotId,
    Guid? InteractionModelId,
    string? ModelAlias,
    int ConsistencyRuns,
    IReadOnlyList<TutorBenchmarkCaseInputRequest?>? Cases,
    Guid? TutorRevisionId = null,
    bool GenerateActualOutputs = false,
    bool UseTutorKnowledge = true,
    int? TopK = 5);

/// <summary>Single benchmark case with input and actual model output for evaluation.</summary>
public sealed record TutorBenchmarkCaseInputRequest(
    string? InputText,
    string? ActualOutput,
    string? ExpectedOutput,
    string? ContextText,
    string? MetadataJson);

/// <summary>Summary data for a queued or completed tutor benchmark run.</summary>
public sealed record TutorBenchmarkRunSummaryResponse(
    Guid Id,
    Guid TutorId,
    Guid BenchmarkTemplateId,
    string? BenchmarkTemplateName,
    Guid? TutorRevisionId,
    int? TutorRevisionNumber,
    Guid? TutorConfigurationSnapshotId,
    Guid? InteractionModelId,
    string? ModelAlias,
    bool GenerateActualOutputs,
    bool UseTutorKnowledge,
    int RetrievalTopK,
    int ConsistencyRuns,
    string Status,
    DateTime QueuedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    int TotalCaseCount,
    int CompletedCaseCount,
    double ProgressPercent,
    string? ProgressStage,
    string? ProgressMessage,
    DateTime? LastProgressUtc,
    string? EngineName,
    string? EngineVersion,
    double? AverageScore,
    double? PassRate,
    double? TotalLatencyMs,
    double? TotalTokens,
    double? TokensPerSecond,
    double? EstimatedCo2Grams,
    string? FailureCode,
    string? FailureDescription);

/// <summary>Lightweight benchmark run progress response for polling UI state.</summary>
public sealed record TutorBenchmarkRunProgressResponse(
    Guid Id,
    Guid TutorId,
    string Status,
    int TotalCaseCount,
    int CompletedCaseCount,
    double ProgressPercent,
    string? ProgressStage,
    string? ProgressMessage,
    DateTime QueuedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    DateTime? LastProgressUtc,
    string? FailureCode,
    string? FailureDescription);

/// <summary>Case-level benchmark result for auditing evaluation behavior.</summary>
public sealed record TutorBenchmarkRunCaseResponse(
    Guid Id,
    int CaseIndex,
    string InputText,
    string ActualOutput,
    string? ExpectedOutput,
    string? ContextText,
    string? MetadataJson,
    bool? Passed,
    double? Score,
    string? Reason,
    string? MetricsJson,
    string? PerformanceJson,
    string? ResourceJson);

/// <summary>Detailed benchmark run response including case-level results and raw summary metadata.</summary>
public sealed record TutorBenchmarkRunResponse(
    TutorBenchmarkRunSummaryResponse Summary,
    IReadOnlyList<TutorBenchmarkRunCaseResponse> Cases,
    string? SummaryJson,
    string? SystemMetricsJson,
    string? RawResultJson);

/// <summary>Request to execute a standardized LLM benchmark directly.</summary>
public sealed record ExecuteStandardBenchmarkRequest(
    string BenchmarkKey,
    string Provider,
    string ModelName,
    string? BaseUrl = null,
    string? ApiKeyEnv = null,
    IReadOnlyList<string>? Tasks = null,
    int NShots = 0,
    bool AllowCodeExecution = false);

/// <summary>Response from a standardized LLM benchmark run.</summary>
public sealed record StandardBenchmarkResponse(
    string BenchmarkKey,
    double? OverallScore,
    int TaskCount,
    string EngineVersion,
    string Notes,
    string? Error);
