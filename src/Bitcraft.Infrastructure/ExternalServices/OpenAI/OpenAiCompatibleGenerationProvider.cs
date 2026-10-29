using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.OpenAI;

public class OpenAiCompatibleGenerationProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OpenAiCompatibleGenerationProvider>? logger = null) : ILlmGenerationProvider
{
    private const string DefaultBaseUrl = "http://localhost:8080/v1";
    private const string DefaultChatModel = "gpt-4o-mini";

    public async Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default) =>
        await GenerateAsync(new LlmGenerationRequest(prompt, model), cancellationToken);

    public async Task<Result<LlmGenerationResult>> GenerateAsync(
        LlmGenerationRequest generationRequest,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(generationRequest.Prompt))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.PromptRequired",
                "Prompt is required."));
        }

        if (generationRequest.Model is not null && string.IsNullOrWhiteSpace(generationRequest.Model))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.ModelInvalid",
                "Model cannot be empty when provided."));
        }

        var baseUrl = generationRequest.RuntimeEndpoint
            ?? configuration["ModelProviders:OpenAI:BaseUrl"]
            ?? configuration["OPENAI_BASE_URL"]
            ?? DefaultBaseUrl;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.BaseUrlInvalid",
                "OpenAI compatible base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = string.IsNullOrWhiteSpace(generationRequest.Model)
            ? configuration["ModelProviders:OpenAI:ChatModel"] ?? configuration["OPENAI_CHAT_MODEL"] ?? DefaultChatModel
            : generationRequest.Model.Trim();

        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.ModelInvalid",
                "Chat model is not configured."));
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleGenerationProvider));

        var apiKey = generationRequest.ApiKeySecretRef
            ?? configuration["ModelProviders:OpenAI:ApiKey"]
            ?? configuration["OPENAI_API_KEY"];

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUri));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        var messages = new List<ChatMessagePayload>
        {
            new("user", generationRequest.Prompt.Trim())
        };

        var requestBody = new ChatCompletionsRequest(
            selectedModel.Trim(),
            messages,
            generationRequest.Temperature,
            generationRequest.TopP,
            generationRequest.MaxTokens,
            Stream: false);

        requestMessage.Content = JsonContent.Create(requestBody);

        try
        {
            using var response = await client.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning(
                    "OpenAI compatible generation failed. Model={Model}, StatusCode={StatusCode}",
                    selectedModel,
                    (int)response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Result.Failure<LlmGenerationResult>(new Error(
                        "OpenAI.Unauthorized",
                        "Authentication failed for OpenAI compatible endpoint. Check API key."));
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Result.Failure<LlmGenerationResult>(new Error(
                        "OpenAI.ModelNotFound",
                        $"The configured model '{selectedModel}' was not found on the endpoint."));
                }

                return Result.Failure<LlmGenerationResult>(new Error(
                    "OpenAI.RequestFailed",
                    $"OpenAI compatible endpoint returned HTTP {(int)response.StatusCode}."));
            }

            var payload = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(cancellationToken: cancellationToken);
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;

            if (payload is null || string.IsNullOrWhiteSpace(content))
            {
                return Result.Failure<LlmGenerationResult>(new Error(
                    "OpenAI.InvalidResponse",
                    "OpenAI compatible response did not contain message content."));
            }

            return Result.Success(new LlmGenerationResult(
                payload.Model ?? selectedModel,
                content.Trim(),
                Done: true));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            logger?.LogWarning(
                "OpenAI compatible generation timed out. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.Unavailable",
                "The OpenAI compatible generation request timed out."));
        }
        catch (HttpRequestException)
        {
            logger?.LogWarning(
                "OpenAI compatible endpoint was unavailable. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.Unavailable",
                "OpenAI compatible runtime endpoint is unavailable."));
        }
        catch (JsonException)
        {
            logger?.LogWarning(
                "OpenAI compatible endpoint returned invalid JSON. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.InvalidResponse",
                "OpenAI compatible endpoint returned invalid JSON."));
        }
    }

    public async Task<IReadOnlyList<string>?> ProbeAvailableModelsAsync(
        string endpoint,
        string? apiKeySecretRef,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleGenerationProvider));
        client.Timeout = TimeSpan.FromSeconds(5);

        var apiKey = apiKeySecretRef
            ?? configuration["ModelProviders:OpenAI:ApiKey"]
            ?? configuration["OPENAI_API_KEY"];

        var endpointsToTry = new[]
        {
            baseUri.ToString().TrimEnd('/') + "/models",
            baseUri.ToString().TrimEnd('/') + "/v1/models"
        };

        foreach (var rawUrl in endpointsToTry.Distinct())
        {
            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                }

                using var response = await client.SendAsync(requestMessage, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    if (json.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in json.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                var str = item.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
                            }
                            else if (item.TryGetProperty("id", out var idProp) || item.TryGetProperty("name", out idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrWhiteSpace(id)) list.Add(id);
                            }
                        }
                        if (list.Count > 0) return list;
                    }
                    else if (json.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in dataElem.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idProp) || item.TryGetProperty("name", out idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrWhiteSpace(id)) list.Add(id);
                            }
                        }
                        if (list.Count > 0) return list;
                    }
                    else if (json.TryGetProperty("models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in modelsElem.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out var nameProp) || item.TryGetProperty("id", out nameProp))
                            {
                                var name = nameProp.GetString();
                                if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
                            }
                        }
                        if (list.Count > 0) return list;
                    }

                    return Array.Empty<string>();
                }
            }
            catch
            {
                // Try next endpoint format
            }
        }

        return null;
    }

    private static Uri BuildChatCompletionsUri(Uri baseUri)
    {
        var raw = baseUri.ToString().TrimEnd('/');
        if (raw.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(raw);
        }
        if (raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{raw}/chat/completions");
        }
        return new Uri($"{raw}/v1/chat/completions");
    }

    private sealed record ChatMessagePayload(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionsRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessagePayload> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ChatCompletionsResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChoicePayload>? Choices);

    private sealed record ChoicePayload(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("message")] ChatMessagePayload? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);
}
