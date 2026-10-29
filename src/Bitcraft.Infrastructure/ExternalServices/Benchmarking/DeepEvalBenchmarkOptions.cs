using System.ComponentModel.DataAnnotations;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class DeepEvalBenchmarkOptions
{
    public const string SectionName = "Benchmarking:DeepEval";

    /// <summary>Transport used by the API to reach the sidecar. Defaults to Unix domain sockets for local Docker isolation.</summary>
    [Required]
    public string Transport { get; set; } = "uds";

    /// <summary>Unix domain socket path shared between the API and benchmark sidecar containers.</summary>
    [Required]
    public string SocketPath { get; set; } = "/var/run/bitcraft/sidecar.sock";

    /// <summary>
    /// HTTP fallback endpoint for non-UDS development or hosted sidecar deployments.
    /// Port 48317 is deliberately uncommon to avoid host/container port clashes.
    /// It must match the sidecar's BITCRAFT_SIDECAR_HTTP_PORT when that transport is used.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "http://benchmark-sidecar:48317";

    /// <summary>Maximum sidecar request duration before the benchmark engine call is canceled.</summary>
    [Range(1, 86_400)]
    public int TimeoutSeconds { get; set; } = 120;
}
