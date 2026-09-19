using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Embeddings;

public class OllamaEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<OllamaEmbeddingProvider> logger) : IEmbeddingProvider
{
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        string embeddingModel,
        CancellationToken cancellationToken = default) =>
        await GenerateEmbeddingAsync(
            new EmbeddingGenerationRequest(text, embeddingModel),
            cancellationToken);

    public async Task<float[]> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest embeddingRequest,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(embeddingRequest.Text))
        {
            throw new ArgumentException("Embedding text is required.", nameof(embeddingRequest));
        }

        // OD-4: the RuntimeEngine entity is the single source of truth for endpoint and model.
        var model = embeddingRequest.Model?.Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "An Ollama embedding model is not configured.");
        }

        var baseUrl = embeddingRequest.RuntimeEndpoint;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "The Ollama endpoint is not configured correctly.");
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaEmbeddingProvider));
        var request = new EmbeddingRequest(model, embeddingRequest.Text);
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
                logger.LogWarning(
                    "Ollama embedding request failed. Operation={Operation}, StatusCode={StatusCode}, Model={EmbeddingModel}",
                    "embed",
                    (int)response.StatusCode,
                    model);
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

    private sealed record EmbeddingRequest(string Model, string Prompt);
    private sealed record EmbeddingResponse(float[]? Embedding);
}
