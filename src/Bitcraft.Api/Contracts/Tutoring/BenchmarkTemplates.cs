namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to create a benchmark template for reusable evaluation workflows.</summary>
public sealed record CreateBenchmarkTemplateRequest(
    string Key,
    string Name,
    string? Description,
    string? GuidancePath,
    string? MetricsJson,
    bool IsEnabled = true);

/// <summary>Benchmark template metadata visible to editors and auditors.</summary>
public sealed record BenchmarkTemplateResponse(
    Guid Id,
    Guid OwnerId,
    string Key,
    string Name,
    string? Description,
    string? GuidancePath,
    string MetricsJson,
    bool IsEnabled,
    DateTime CreatedUtc,
    DateTime? ModifiedUtc);
