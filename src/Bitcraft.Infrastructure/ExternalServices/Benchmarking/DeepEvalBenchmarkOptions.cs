namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class DeepEvalBenchmarkOptions
{
    public const string SectionName = "Benchmarking:DeepEval";

    /// <summary>Transport used by the API to reach the sidecar. Defaults to Unix domain sockets for local Docker isolation.</summary>
    public string Transport { get; set; } = "uds";

    /// <summary>Unix domain socket path shared between the API and benchmark sidecar containers.</summary>
    public string SocketPath { get; set; } = "/var/run/bitcraft/sidecar.sock";

    /// <summary>HTTP fallback endpoint for non-UDS development or hosted sidecar deployments.</summary>
    public string BaseUrl { get; set; } = "http://benchmark-sidecar:8080";

    /// <summary>Maximum sidecar request duration before the benchmark engine call is canceled.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
