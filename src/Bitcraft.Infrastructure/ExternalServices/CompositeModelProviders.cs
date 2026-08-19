using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.ExternalServices.Anthropic;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Bitcraft.Infrastructure.ExternalServices.OpenAI;

namespace Bitcraft.Infrastructure.ExternalServices;

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
        var providerType = request.ProviderType;
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return ollamaProvider.GenerateAsync(request, cancellationToken);
        }

        if (string.Equals(providerType, nameof(RuntimeEngineProviderType.OpenAI), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerType, nameof(RuntimeEngineProviderType.OpenAICompatible), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerType, nameof(RuntimeEngineProviderType.vLLM), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerType, nameof(RuntimeEngineProviderType.LlamaCpp), StringComparison.OrdinalIgnoreCase))
        {
            return openAiProvider.GenerateAsync(request, cancellationToken);
        }

        if (string.Equals(providerType, nameof(RuntimeEngineProviderType.Anthropic), StringComparison.OrdinalIgnoreCase))
        {
            return anthropicProvider.GenerateAsync(request, cancellationToken);
        }

        return ollamaProvider.GenerateAsync(request, cancellationToken);
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
        var providerType = request.ProviderType;
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return ollamaProvider.GenerateEmbeddingAsync(request, cancellationToken);
        }

        if (string.Equals(providerType, nameof(RuntimeEngineProviderType.OpenAI), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerType, nameof(RuntimeEngineProviderType.OpenAICompatible), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerType, nameof(RuntimeEngineProviderType.LlamaCpp), StringComparison.OrdinalIgnoreCase))
        {
            return openAiProvider.GenerateEmbeddingAsync(request, cancellationToken);
        }

        return ollamaProvider.GenerateEmbeddingAsync(request, cancellationToken);
    }
}
