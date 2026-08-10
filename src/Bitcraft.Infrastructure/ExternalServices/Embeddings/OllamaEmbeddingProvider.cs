using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Embeddings;

public class OllamaEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaEmbeddingProvider> logger) : IEmbeddingProvider
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
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "An Ollama embedding model is not configured.");
        }

        var baseUrl = configuration["ModelProviders:Ollama:BaseUrl"]
            ?? configuration["OLLAMA_BASE_URL"]
            ?? "http://localhost:11434";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "The Ollama endpoint is not configured correctly.");
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaEmbeddingProvider));
        var request = new EmbeddingRequest(model, text);
        var endpoint = new Uri(baseUri, "/api/embeddings");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.ServiceUnavailable,
                "The Ollama embedding request timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.ServiceUnavailable,
                "The Ollama embedding service is unavailable.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var responsePreview = await ReadBoundedResponseAsync(response.Content, cancellationToken);
                logger.LogWarning(
                    "Ollama embedding request failed. StatusCode={StatusCode}, Model={EmbeddingModel}, ResponsePreview={ResponsePreview}",
                    (int)response.StatusCode,
                    model,
                    responsePreview);
                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.RequestFailed,
                    "Ollama could not process the embedding request.");
            }

            EmbeddingResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.ResponseInvalid,
                    "Ollama returned an invalid embedding response.",
                    ex);
            }

            if (payload?.Embedding is null || payload.Embedding.Length == 0)
            {
                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.ResponseInvalid,
                    "Ollama returned an empty embedding response.");
            }

            return payload.Embedding;
        }
    }

    private static async Task<string> ReadBoundedResponseAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        const int maximumBytes = 2048;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maximumBytes];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
        var preview = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        return preview.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private sealed record EmbeddingRequest(string Model, string Prompt);
    private sealed record EmbeddingResponse(float[]? Embedding);
}
