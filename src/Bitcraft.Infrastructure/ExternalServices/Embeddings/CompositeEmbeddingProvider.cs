using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Bitcraft.Infrastructure.ExternalServices.OpenAI;

namespace Bitcraft.Infrastructure.ExternalServices.Embeddings;

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
