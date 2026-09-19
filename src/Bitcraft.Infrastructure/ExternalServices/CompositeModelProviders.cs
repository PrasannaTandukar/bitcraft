using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.ExternalServices.Anthropic;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Bitcraft.Infrastructure.ExternalServices.OpenAI;

namespace Bitcraft.Infrastructure.ExternalServices;

/// <summary>
/// Shared provider-type parsing for the composites. A blank value is the legacy Ollama path
/// (pre-RuntimeEngine callers). An unparseable value returns null. Callers fail closed on null.
/// </summary>
internal static class CompositeProviderTypeParser
{
    public static RuntimeEngineProviderType? Parse(string? providerType) =>
        string.IsNullOrWhiteSpace(providerType)
            ? RuntimeEngineProviderType.Ollama
            : Enum.TryParse<RuntimeEngineProviderType>(providerType, ignoreCase: true, out var parsed)
                ? parsed
                : null;
}

public class CompositeLlmGenerationProvider(
    OllamaGenerationProvider ollamaProvider,
    OpenAiCompatibleGenerationProvider openAiProvider,
    AnthropicGenerationProvider anthropicProvider) : ILlmGenerationProvider
{
    public Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(new LlmGenerationRequest(prompt, model), cancellationToken);

    public Task<Result<LlmGenerationResult>> GenerateAsync(
        LlmGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        // A blank provider type is the legacy Ollama path (pre-RuntimeEngine callers).
        var dialect = CompositeProviderTypeParser.Parse(request.ProviderType)?.ToLlmDialect();

        return dialect switch
        {
            LlmDialect.OpenAI => openAiProvider.GenerateAsync(request, cancellationToken),
            LlmDialect.Anthropic => anthropicProvider.GenerateAsync(request, cancellationToken),
            LlmDialect.Ollama => ollamaProvider.GenerateAsync(request, cancellationToken),
            _ => Task.FromResult(Result.Failure<LlmGenerationResult>(
                new Error("LlmGeneration.ProviderTypeUnsupported", $"The provider type '{request.ProviderType}' is not supported.")))
        };
    }

    public Task<IReadOnlyList<string>?> ProbeAvailableModelsAsync(
        string endpoint,
        string? apiKey,
        string? providerType,
        CancellationToken cancellationToken = default)
    {
        // Fail closed: an unparseable provider type probes nothing (OD-8).
        var dialect = CompositeProviderTypeParser.Parse(providerType)?.ToLlmDialect();

        return dialect switch
        {
            LlmDialect.OpenAI => openAiProvider.ProbeAvailableModelsAsync(endpoint, apiKey, cancellationToken),
            LlmDialect.Ollama => ollamaProvider.ProbeAvailableModelsAsync(endpoint, cancellationToken),
            _ => Task.FromResult<IReadOnlyList<string>?>(null)
        };
    }
}

public class CompositeEmbeddingProvider(
    OllamaEmbeddingProvider ollamaProvider,
    OpenAiCompatibleEmbeddingProvider openAiProvider) : IEmbeddingProvider
{
    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        string embeddingModel,
        CancellationToken cancellationToken = default) =>
        GenerateEmbeddingAsync(new EmbeddingGenerationRequest(text, embeddingModel), cancellationToken);

    public Task<float[]> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        // A blank provider type is the legacy Ollama path (pre-RuntimeEngine callers).
        var dialect = CompositeProviderTypeParser.Parse(request.ProviderType)?.ToEmbeddingDialect();

        return dialect switch
        {
            LlmDialect.OpenAI => openAiProvider.GenerateEmbeddingAsync(request, cancellationToken),
            LlmDialect.Ollama => ollamaProvider.GenerateEmbeddingAsync(request, cancellationToken),
            _ => throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                $"The provider type '{request.ProviderType}' is not supported.")
        };
    }
}
