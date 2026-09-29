using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.OpenAI;

public class OpenAiCompatibleEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiCompatibleEmbeddingProvider>? logger = null) : IEmbeddingProvider
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

        // OD-4: the RuntimeEngine entity is the single source of truth for endpoint, model, and key.
        var model = embeddingRequest.Model?.Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "An embedding model is not configured.");
        }

        var baseUrl = embeddingRequest.RuntimeEndpoint;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "The embedding endpoint is not configured correctly.");
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleEmbeddingProvider));
        var apiKey = embeddingRequest.ApiKey;

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildEmbeddingsUri(baseUri));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        var requestBody = new OpenAiEmbeddingRequest(model, embeddingRequest.Text.Trim());
        requestMessage.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(requestMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.ServiceUnavailable,
                "The embedding request timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.ServiceUnavailable,
                "The embedding service is unavailable.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger?.LogWarning(
                    "Embedding request failed. StatusCode={StatusCode}, Body={Body}",
                    (int)response.StatusCode,
                    body);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new EmbeddingProviderException(
                        EmbeddingProviderFailureKind.RequestFailed,
                        "Authentication failed for embedding service. Check API key.");
                }

                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.RequestFailed,
                    $"Embedding service returned HTTP {(int)response.StatusCode}.");
            }

            OpenAiEmbeddingResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.ResponseInvalid,
                    "Embedding response was not valid JSON.",
                    ex);
            }

            var vector = payload?.Data?.FirstOrDefault()?.Embedding;
            if (vector is null || vector.Length == 0)
            {
                throw new EmbeddingProviderException(
                    EmbeddingProviderFailureKind.ResponseInvalid,
                    "Embedding response contained no vector data.");
            }

            return vector;
        }
    }

    private static Uri BuildEmbeddingsUri(Uri baseUri)
    {
        var raw = baseUri.ToString().TrimEnd('/');
        if (raw.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(raw);
        }
        if (raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{raw}/embeddings");
        }
        return new Uri($"{raw}/v1/embeddings");
    }

    private sealed record OpenAiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OpenAiEmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingDataPayload>? Data);

    private sealed record EmbeddingDataPayload(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
