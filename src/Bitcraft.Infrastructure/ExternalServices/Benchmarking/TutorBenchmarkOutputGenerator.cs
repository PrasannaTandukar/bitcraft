using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

/// <summary>
/// Backward-compatible benchmark adapter. Revision/snapshot resolution, retrieval,
/// interaction parameters and agent execution are delegated to the same ITutorRuntime used by chat.
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
                UseKnowledge: request.UseTutorKnowledge,
                request.TopK,
                TutorRuntimeExecutionMode.Benchmark),
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
            result.Value.Usage.GenerationLatencyMs,
            result.Value.TutorRevisionId,
            result.Value.TutorConfigurationSnapshotId,
            result.Value.KnowledgeUsed,
            result.Value.Sources.Count,
            result.Value.ToolActivities?.Count ?? 0,
            result.Value.Usage.InputTokenCount,
            result.Value.Usage.OutputTokenCount,
            result.Value.Usage.TotalTokenCount));
    }
}
