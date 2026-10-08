using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.OpenAI;

public class OpenAiCompatibleEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IRuntimeApiKeyResolver apiKeys,
    ILogger<OpenAiCompatibleEmbeddingProvider>? logger = null) : IEmbeddingProvider
{
    private const string DefaultBaseUrl = "http://localhost:8080/v1";
    private const string DefaultEmbeddingModel = "text-embedding-3-small";

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

        var model = string.IsNullOrWhiteSpace(embeddingRequest.Model)
            ? configuration["ModelProviders:OpenAI:EmbeddingModel"] ?? configuration["OPENAI_EMBED_MODEL"] ?? DefaultEmbeddingModel
            : embeddingRequest.Model.Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "An embedding model is not configured.");
        }

        var baseUrl = embeddingRequest.RuntimeEndpoint
            ?? configuration["ModelProviders:OpenAI:BaseUrl"]
            ?? configuration["OPENAI_BASE_URL"]
            ?? DefaultBaseUrl;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new EmbeddingProviderException(
                EmbeddingProviderFailureKind.RequestFailed,
                "The embedding endpoint is not configured correctly.");
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleEmbeddingProvider));
        var providerType = Enum.TryParse<RuntimeEngineProviderType>(embeddingRequest.ProviderType,
            ignoreCase: true, out var parsedProvider) ? parsedProvider : RuntimeEngineProviderType.OpenAI;
        var resolvedKey = apiKeys.Resolve(new TutorAgentModelConfiguration(Guid.Empty, providerType,
            baseUri.AbsoluteUri, embeddingRequest.ApiKey, Guid.Empty, model, ModelCapabilities.None));
        if (resolvedKey.IsFailure)
            throw new EmbeddingProviderException(EmbeddingProviderFailureKind.RequestFailed,
                "The embedding credential reference could not be resolved.");

        HttpRequestMessage CreateRequest(Uri targetUri)
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, targetUri);
            if (!string.IsNullOrWhiteSpace(resolvedKey.Value))
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedKey.Value);
            }
            msg.Content = JsonContent.Create(new OpenAiEmbeddingRequest(model, embeddingRequest.Text.Trim()));
            return msg;
        }

        var primaryUri = BuildEmbeddingsUri(baseUri);
        using var requestMessage = CreateRequest(primaryUri);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(requestMessage, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var fallbackUri = BuildEmbeddingsUri(baseUri, preferRoot: true);
                if (fallbackUri != primaryUri)
                {
                    using var fallbackRequest = CreateRequest(fallbackUri);
                    var fallbackResponse = await client.SendAsync(fallbackRequest, cancellationToken);
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        response.Dispose();
                        response = fallbackResponse;
                    }
                    else
                    {
                        fallbackResponse.Dispose();
                    }
                }
            }
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
                logger?.LogWarning(
                    "Embedding request failed. Operation={Operation}, StatusCode={StatusCode}, Provider={ProviderType}, Model={EmbeddingModel}",
                    "embed", (int)response.StatusCode, providerType, model);

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

    private static Uri BuildEmbeddingsUri(Uri baseUri, bool preferRoot = false)
    {
        var raw = baseUri.ToString().TrimEnd('/');
        if (raw.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            if (preferRoot && raw.EndsWith("/v1/embeddings", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = raw[..^"/v1/embeddings".Length].TrimEnd('/');
                return new Uri($"{stripped}/embeddings");
            }
            return new Uri(raw);
        }
        if (raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            if (preferRoot)
            {
                var stripped = raw[..^"/v1".Length].TrimEnd('/');
                return new Uri($"{stripped}/embeddings");
            }
            return new Uri($"{raw}/embeddings");
        }
        return preferRoot ? new Uri($"{raw}/embeddings") : new Uri($"{raw}/v1/embeddings");
    }

    private sealed record OpenAiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OpenAiEmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingDataPayload>? Data);

    private sealed record EmbeddingDataPayload(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
