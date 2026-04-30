namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class DeepEvalBenchmarkOptions
{
    public const string SectionName = "Benchmarking:DeepEval";

    public string Transport { get; set; } = "uds";
    public string SocketPath { get; set; } = "/var/run/bitcraft/sidecar.sock";
    public string BaseUrl { get; set; } = "http://benchmark-sidecar:8080";
    public int TimeoutSeconds { get; set; } = 120;
}
