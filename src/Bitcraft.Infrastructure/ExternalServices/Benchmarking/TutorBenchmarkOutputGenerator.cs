using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

/// <summary>
/// Backward-compatible benchmark adapter. All tutor prompt assembly, revision/snapshot
/// resolution, retrieval, interaction parameters, and generation are delegated to ITutorRuntime.
/// </summary>
public sealed class TutorBenchmarkOutputGenerator(ITutorRuntime runtime) : ITutorBenchmarkOutputGenerator
{
    public async Task<Result<GeneratedBenchmarkOutput>> GenerateAsync(
        TutorBenchmarkGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await runtime.ExecuteAsync(
            new TutorRuntimeRequest(
                request.TutorId,
                request.TutorRevisionId,
                request.TutorConfigurationSnapshotId,
                request.InputText,
                request.UserId,
                UseKnowledge: true,
                request.TopK,
                TutorRuntimeExecutionMode.Benchmark,
                History: null),
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<GeneratedBenchmarkOutput>(result.Error);
        }

        return Result.Success(new GeneratedBenchmarkOutput(
            result.Value.Answer,
            result.Value.Model.ProviderModelName,
            string.IsNullOrWhiteSpace(request.ContextOverride)
                ? result.Value.ReferenceMaterial
                : request.ContextOverride.Trim(),
            result.Value.Usage.GenerationLatencyMs));
    }
}
