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
        string? apiKeySecretRef,
        string? providerType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>?>(null);
}
