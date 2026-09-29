using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Anthropic;

/// <summary>
/// Reserved provider (consolidation plan, OD-2): kept registered so existing Anthropic engine
/// rows keep functioning, but not surfaced in the UI. OD-4 removed the process-level
/// configuration fallbacks. The RuntimeEngine entity is the only endpoint/key source.
/// </summary>
public class AnthropicGenerationProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AnthropicGenerationProvider>? logger = null) : ILlmGenerationProvider
{
    private const string AnthropicVersion = "2023-06-01";

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
                "Anthropic.PromptRequired",
                "Prompt is required."));
        }

        var baseUrl = generationRequest.RuntimeEndpoint;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Anthropic.BaseUrlInvalid",
                "Anthropic base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = generationRequest.Model?.Trim();
        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Anthropic.ModelInvalid",
                "Chat model is not configured."));
        }

        var client = httpClientFactory.CreateClient(nameof(AnthropicGenerationProvider));
        var apiKey = generationRequest.ApiKey;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/v1/messages"));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Add("x-api-key", apiKey.Trim());
        }
        requestMessage.Headers.Add("anthropic-version", AnthropicVersion);

        object content = generationRequest.Prompt.Trim();
        if (generationRequest.Images is { Count: > 0 })
        {
            var blocks = generationRequest.Images.Select(image => (object)new
            {
                type = "image",
                source = new { type = "base64", media_type = image.MimeType, data = Convert.ToBase64String(image.Data.Span) }
            }).ToList();
            blocks.Add(new { type = "text", text = generationRequest.Prompt.Trim() });
            content = blocks;
        }
        var messages = new List<AnthropicMessagePayload> { new("user", content) };

        var maxTokens = generationRequest.MaxTokens ?? 4096;
        var requestBody = new AnthropicMessagesRequest(
            selectedModel.Trim(),
            messages,
            maxTokens,
            generationRequest.Temperature,
            generationRequest.TopP);

        requestMessage.Content = JsonContent.Create(requestBody);

        try
        {
            using var response = await client.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (generationRequest.Images is { Count: > 0 } && (int)response.StatusCode is 400 or 415 or 422)
                {
                    logger?.LogWarning("Provider rejected image input. Model={Model}, ImageCount={ImageCount}, StatusCode={StatusCode}", selectedModel, generationRequest.Images.Count, (int)response.StatusCode);
                    return Result.Failure<LlmGenerationResult>(new Error("Anthropic.ImageRejected", "The configured provider rejected the image request."));
                }
                logger?.LogWarning(
                    "Anthropic generation failed. Model={Model}, StatusCode={StatusCode}",
                    selectedModel,
                    (int)response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Result.Failure<LlmGenerationResult>(new Error(
                        "Anthropic.Unauthorized",
                        "Authentication failed for Anthropic API. Check API key."));
                }

                return Result.Failure<LlmGenerationResult>(new Error(
                    "Anthropic.RequestFailed",
                    $"Anthropic API returned HTTP {(int)response.StatusCode}."));
            }

            var payload = await response.Content.ReadFromJsonAsync<AnthropicMessagesResponse>(cancellationToken: cancellationToken);
            var textBlock = payload?.Content?.FirstOrDefault(b => b.Type == "text")?.Text;

            if (payload is null || string.IsNullOrWhiteSpace(textBlock))
            {
                return Result.Failure<LlmGenerationResult>(new Error(
                    "Anthropic.InvalidResponse",
                    "Anthropic response did not contain text content."));
            }

            return Result.Success(new LlmGenerationResult(
                payload.Model ?? selectedModel,
                textBlock.Trim(),
                Done: true));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            logger?.LogWarning(
                "Anthropic generation timed out. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Anthropic.Unavailable",
                "The Anthropic generation request timed out."));
        }
        catch (HttpRequestException)
        {
            logger?.LogWarning(
                "Anthropic service was unavailable. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Anthropic.Unavailable",
                "Anthropic API is unavailable."));
        }
        catch (JsonException)
        {
            logger?.LogWarning(
                "Anthropic returned invalid JSON. Model={Model}",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Anthropic.InvalidResponse",
                "Anthropic returned invalid JSON."));
        }
    }

    private sealed record AnthropicMessagePayload(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);

    private sealed record AnthropicMessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessagePayload> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP);

    private sealed record AnthropicMessagesResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("content")] IReadOnlyList<ContentBlockPayload>? Content);

    private sealed record ContentBlockPayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
