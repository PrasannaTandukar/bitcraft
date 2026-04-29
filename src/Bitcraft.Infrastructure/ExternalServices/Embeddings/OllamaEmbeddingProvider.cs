using System.Net.Http.Json;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Bitcraft.Infrastructure.ExternalServices.Embeddings;

public class OllamaEmbeddingProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IEmbeddingProvider
{
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        string embeddingModel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding text is required.", nameof(text));
        }

        var model = string.IsNullOrWhiteSpace(embeddingModel)
            ? configuration["ModelProviders:Ollama:EmbeddingModel"] ?? configuration["OLLAMA_EMBED_MODEL"]
            : embeddingModel.Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Ollama embedding model is not configured.");
        }

        var baseUrl = configuration["ModelProviders:Ollama:BaseUrl"]
            ?? configuration["OLLAMA_BASE_URL"]
            ?? "http://localhost:11434";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Ollama base URL is not a valid absolute URI.");
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaEmbeddingProvider));
        var request = new EmbeddingRequest(model, text);
        var endpoint = new Uri(baseUri, "/api/embeddings");

        using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama embedding request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
        if (payload?.Embedding is null || payload.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama embedding response did not contain an embedding vector.");
        }

        return payload.Embedding;
    }

    private sealed record EmbeddingRequest(string Model, string Prompt);
    private sealed record EmbeddingResponse(float[]? Embedding);
}