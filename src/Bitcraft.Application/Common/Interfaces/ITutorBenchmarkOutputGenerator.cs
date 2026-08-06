using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorBenchmarkOutputGenerator
{
    Task<Result<GeneratedBenchmarkOutput>> GenerateAsync(
        TutorBenchmarkGenerationRequest request,
        CancellationToken cancellationToken = default);
}
