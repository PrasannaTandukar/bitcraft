using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.OpenAI;

public class OpenAiCompatibleGenerationProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiCompatibleGenerationProvider>? logger = null) : ILlmGenerationProvider
{

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

        // OD-4: the RuntimeEngine entity is the single source of truth for endpoint, model, and key.
        // Process-level configuration fallbacks were removed to kill the parallel config path.
        var baseUrl = generationRequest.RuntimeEndpoint;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.BaseUrlInvalid",
                "OpenAI compatible base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = generationRequest.Model?.Trim();

        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "OpenAI.ModelInvalid",
                "Chat model is not configured."));
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleGenerationProvider));

        var apiKey = generationRequest.ApiKey;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUri));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        object content = generationRequest.Prompt.Trim();
        if (generationRequest.Images is { Count: > 0 })
        {
            var parts = new List<object> { new { type = "text", text = generationRequest.Prompt.Trim() } };
            parts.AddRange(generationRequest.Images.Select(image => (object)new
            {
                type = "image_url",
                image_url = new { url = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Data.Span)}" }
            }));
            content = parts;
        }
        var messages = new List<ChatMessagePayload> { new("user", content) };

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
                if (generationRequest.Images is { Count: > 0 } && (int)response.StatusCode is 400 or 415 or 422)
                {
                    logger?.LogWarning("Provider rejected image input. Model={Model}, ImageCount={ImageCount}, StatusCode={StatusCode}", selectedModel, generationRequest.Images.Count, (int)response.StatusCode);
                    return Result.Failure<LlmGenerationResult>(new Error("OpenAI.ImageRejected", "The configured provider rejected the image request."));
                }
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
            var answer = payload?.Choices?.FirstOrDefault()?.Message?.Content;

            if (payload is null || string.IsNullOrWhiteSpace(answer))
            {
                return Result.Failure<LlmGenerationResult>(new Error(
                    "OpenAI.InvalidResponse",
                    "OpenAI compatible response did not contain message content."));
            }

            return Result.Success(new LlmGenerationResult(
                payload.Model ?? selectedModel,
                answer.Trim(),
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
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleGenerationProvider));
        client.Timeout = TimeSpan.FromSeconds(5);

        // Security: the probe endpoint accepts caller-chosen endpoints. Never attach a
        // server-configured credential here — a client could steer a process-wide key to a
        // host it controls. The probe uses only the caller-supplied or server-resolved stored
        // key passed in as apiKey (Sec Ops finding F1).

        var endpointsToTry = new[]
        {
            baseUri.ToString().TrimEnd('/') + "/models",
            baseUri.ToString().TrimEnd('/') + "/v1/models"
        };

        IReadOnlyList<string>? lastParsed = null;
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

                    // Remember an empty-but-valid response and keep trying the alternate URL.
                    // Some servers only serve one of /models or /v1/models.
                    lastParsed ??= Array.Empty<string>();
                }
            }
            catch
            {
                // Try next endpoint format
            }
        }

        return lastParsed;
    }

    // 1x1 transparent PNG used by the image-input probe.
    private const string ProbeImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    public async Task<Result<bool>> ProbeImageInputSupportAsync(
        string endpoint,
        string model,
        string? apiKey,
        string? providerType,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Success(false);
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return Result.Success(false);
        }

        var client = httpClientFactory.CreateClient(nameof(OpenAiCompatibleGenerationProvider));
        client.Timeout = TimeSpan.FromSeconds(15);

        var parts = new List<object>
        {
            new { type = "text", text = "Describe this image in one word." },
            new { type = "image_url", image_url = new { url = $"data:image/png;base64,{ProbeImageBase64}" } }
        };
        var messages = new List<ChatMessagePayload> { new("user", parts) };
        var requestBody = new ChatCompletionsRequest(model.Trim(), messages, null, null, 8, Stream: false);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUri));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
        requestMessage.Content = JsonContent.Create(requestBody);

        try
        {
            using var response = await client.SendAsync(requestMessage, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }

            // A text-only model rejects the image request with 400/415/422.
            if ((int)response.StatusCode is 400 or 415 or 422)
            {
                logger?.LogInformation("Image probe rejected by provider. Model={Model}, StatusCode={StatusCode}", model, (int)response.StatusCode);
                return Result.Success(false);
            }

            logger?.LogWarning("Image probe failed. Model={Model}, StatusCode={StatusCode}", model, (int)response.StatusCode);
            return Result.Success(false);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            logger?.LogWarning("Image probe could not reach the provider. Model={Model}", model);
            return Result.Success(false);
        }
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
        [property: JsonPropertyName("content")] object Content);

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

    private sealed record ChatResponseMessagePayload([property: JsonPropertyName("content")] string? Content);

    private sealed record ChoicePayload(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("message")] ChatResponseMessagePayload? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);
}
