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

/// <summary>Request to launch a standardized LLM benchmark run for an interaction model.</summary>
public sealed record ExecuteStandardBenchmarkRequest(
    Guid InteractionModelId,
    string BenchmarkKey,
    IReadOnlyList<string>? Tasks = null,
    int NShots = 0,
    int NProblems = 10);

/// <summary>Admin-visible projection of a standardized benchmark run. Never exposes engine credentials.</summary>
public sealed record StandardBenchmarkRunResponse(
    Guid Id,
    Guid InteractionModelId,
    string? InteractionModelName,
    string BenchmarkKey,
    string? TasksJson,
    int NShots,
    int NProblems,
    string Status,
    DateTime QueuedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    string? EngineName,
    string? EngineVersion,
    double? OverallScore,
    int TaskCount,
    string? TaskScoresJson,
    string? Notes,
    string? ProgressStage,
    int? CompletedTasks,
    int? TotalTasks,
    double? ProgressPercent,
    DateTime? LastProgressUtc,
    string? FailureCode,
    string? FailureDescription,
    int Priority,
    DateTime? CancelledUtc,
    bool IsArchived,
    DateTime? ArchivedUtc);

/// <summary>Lightweight standardized benchmark run progress response for polling UI state.</summary>
public sealed record StandardBenchmarkRunProgressResponse(
    Guid Id,
    string Status,
    string? ProgressStage,
    double? OverallScore,
    int TaskCount,
    int? CompletedTasks,
    int? TotalTasks,
    double? ProgressPercent,
    DateTime QueuedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    DateTime? LastProgressUtc,
    string? FailureCode,
    string? FailureDescription);

/// <summary>Metadata for one standardized benchmark suite in the catalog.</summary>
public sealed record StandardBenchmarkSuiteResponse(
    string Key,
    string DisplayName,
    string Description,
    bool SupportsTasks,
    bool SupportsShots,
    bool SupportsProblemCap,
    int MaxShots,
    bool Enabled,
    string Modality);

/// <summary>Sets the queue priority of a standardized benchmark run. Lower values run first.</summary>
public sealed record SetStandardBenchmarkRunPriorityRequest(int Priority);

/// <summary>Scrubbed, capped runner log for one standardized benchmark run.</summary>
public sealed record StandardBenchmarkRunLogResponse(Guid RunId, string Log);

/// <summary>Sidecar engine status for the Admin control surface (HD-9).</summary>
public sealed record BenchmarkEngineStatusResponse(
    bool IsReachable,
    string Transport,
    bool SlotHeld,
    string? CurrentRunId,
    string? EngineVersion,
    string? LastError);

/// <summary>Result of clearing the sidecar runner slot. True when the slot is now free.</summary>
public sealed record BenchmarkEngineSlotClearedResponse(bool SlotCleared);

/// <summary>Sets the global benchmark queue pause state (HD-8).</summary>
public sealed record SetBenchmarkQueuePausedRequest(bool IsPaused);

/// <summary>The global benchmark queue pause state.</summary>
public sealed record BenchmarkQueuePausedResponse(bool IsPaused);
