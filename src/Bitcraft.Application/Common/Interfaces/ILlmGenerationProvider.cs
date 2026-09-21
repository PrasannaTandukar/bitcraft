using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface ILlmGenerationProvider
{
    Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default);

    Task<Result<LlmGenerationResult>> GenerateAsync(
        LlmGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        request.Images is { Count: > 0 }
            ? Task.FromResult(Result.Failure<LlmGenerationResult>(new Error("Llm.ImageInputNotSupported", "This generation provider does not support image input.")))
            : GenerateAsync(request.Prompt, request.Model, cancellationToken);

    Task<IReadOnlyList<string>?> ProbeAvailableModelsAsync(
        string endpoint,
        string? apiKey,
        string? providerType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>?>(null);

    /// <summary>
    /// Probes whether the model at the given endpoint accepts image input by sending a
    /// minimal one-pixel image request. Returns true when the provider accepts the image.
    /// The default fails closed (false) so providers without image support keep compiling.
    /// </summary>
    Task<Result<bool>> ProbeImageInputSupportAsync(
        string endpoint,
        string model,
        string? apiKey,
        string? providerType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(false));
}
