using System.Text.Json.Serialization;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

internal sealed record SidecarRequest(
    [property: JsonPropertyName("template_key")] string TemplateKey,
    [property: JsonPropertyName("consistency_runs")] int ConsistencyRuns,
    [property: JsonPropertyName("guidance_path")] string? GuidancePath,
    [property: JsonPropertyName("metrics_json")] string? MetricsJson,
    [property: JsonPropertyName("cases")] IReadOnlyList<SidecarCaseRequest> Cases);

internal sealed record SidecarCaseRequest(
    [property: JsonPropertyName("case_id")] Guid CaseId,
    [property: JsonPropertyName("case_index")] int CaseIndex,
    [property: JsonPropertyName("input_text")] string InputText,
    [property: JsonPropertyName("actual_output")] string ActualOutput,
    [property: JsonPropertyName("expected_output")] string? ExpectedOutput,
    [property: JsonPropertyName("context_text")] string? ContextText,
    [property: JsonPropertyName("metadata_json")] string? MetadataJson);

internal sealed record SidecarEnvelope(
    [property: JsonPropertyName("tool_key")] string ToolKey,
    [property: JsonPropertyName("payload")] SidecarRequest Payload);

internal sealed record SidecarCaseResult(
    [property: JsonPropertyName("case_id")] Guid CaseId,
    [property: JsonPropertyName("case_index")] int CaseIndex,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("metrics_json")] string MetricsJson,
    [property: JsonPropertyName("performance_json")] string PerformanceJson,
    [property: JsonPropertyName("resource_json")] string ResourceJson);

internal sealed record SidecarSummary(
    [property: JsonPropertyName("average_score")] double AverageScore,
    [property: JsonPropertyName("pass_rate")] double PassRate,
    [property: JsonPropertyName("total_latency_ms")] double TotalLatencyMs,
    [property: JsonPropertyName("total_tokens")] double? TotalTokens,
    [property: JsonPropertyName("tokens_per_second")] double? TokensPerSecond,
    [property: JsonPropertyName("estimated_co2_grams")] double? EstimatedCo2Grams,
    [property: JsonPropertyName("summary_json")] string SummaryJson,
    [property: JsonPropertyName("system_metrics_json")] string? SystemMetricsJson);

internal sealed record SidecarResponse(
    [property: JsonPropertyName("engine_name")] string EngineName,
    [property: JsonPropertyName("engine_version")] string EngineVersion,
    [property: JsonPropertyName("summary")] SidecarSummary Summary,
    [property: JsonPropertyName("cases")] IReadOnlyList<SidecarCaseResult> Cases,
    [property: JsonPropertyName("raw_result_json")] string? RawResultJson);

internal sealed record SidecarError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("description")] string Description);

[JsonSerializable(typeof(SidecarEnvelope))]
[JsonSerializable(typeof(SidecarResponse))]
[JsonSerializable(typeof(SidecarError))]
internal partial class DeepEvalBenchmarkJsonContext : JsonSerializerContext
{
}
