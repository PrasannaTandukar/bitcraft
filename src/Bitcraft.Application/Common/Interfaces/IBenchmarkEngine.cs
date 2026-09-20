using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IBenchmarkEngine
{
    Task<Result<BenchmarkEngineResult>> EvaluateAsync(BenchmarkEngineRequest request, CancellationToken cancellationToken = default);

    /// <summary>Starts a standardized LLM benchmark run and returns a pollable handle.</summary>
    Task<Result<StandardBenchmarkHandle>> StartStandardLlmBenchmarkAsync(StandardLlmBenchmarkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the current status of a started standardized benchmark run.</summary>
    Task<Result<StandardBenchmarkStatus>> GetStandardLlmBenchmarkStatusAsync(StandardBenchmarkHandle handle, CancellationToken cancellationToken = default);
}
