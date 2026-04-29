namespace Bitcraft.Application.Common.Models;

public sealed record ModelHealthStatus(
    bool IsReachable,
    string ProviderType,
    string Endpoint,
    string? ModelName,
    bool ModelAvailable,
    long LatencyMs,
    string? Detail);
