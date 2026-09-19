using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Ollama;

public class OllamaGenerationProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<OllamaGenerationProvider>? logger = null) : ILlmGenerationProvider
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
                "Ollama.PromptRequired",
                "Prompt is required."));
        }

        if (generationRequest.Model is not null && string.IsNullOrWhiteSpace(generationRequest.Model))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.ModelInvalid",
                "Model cannot be empty when provided."));
        }

        // OD-4: the RuntimeEngine entity is the single source of truth for endpoint and model.
        // Process-level configuration fallbacks were removed to kill the parallel config path.
        var baseUrl = generationRequest.RuntimeEndpoint;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.BaseUrlInvalid",
                "Ollama base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = generationRequest.Model?.Trim();

        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.ModelInvalid",
                "Ollama chat model is not configured."));
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaGenerationProvider));
        var endpoint = new Uri(baseUri, "/api/generate");
        var request = new GenerateRequest(
            selectedModel.Trim(),
            generationRequest.Prompt.Trim(),
            Stream: false,
            Think: false,
            new GenerateOptions(
                generationRequest.Temperature,
                generationRequest.TopP,
                generationRequest.MaxTokens),
            Images: generationRequest.Images is { Count: > 0 }
                ? generationRequest.Images
                    .Select(image => Convert.ToBase64String(image.Data.Span))
                    .ToList()
                : null);

        try
        {
            using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (generationRequest.Images is { Count: > 0 } && (int)response.StatusCode is 400 or 415 or 422)
                {
                    logger?.LogWarning("Provider rejected image input. Model={Model}, ImageCount={ImageCount}, StatusCode={StatusCode}", selectedModel, generationRequest.Images.Count, (int)response.StatusCode);
                    return Result.Failure<LlmGenerationResult>(new Error("Ollama.ImageRejected", "The configured provider rejected the image request."));
                }
                logger?.LogWarning(
                    "Ollama generation failed. Operation={Operation}, Model={Model}, StatusCode={StatusCode}",
                    "generate",
                    selectedModel,
                    (int)response.StatusCode);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Result.Failure<LlmGenerationResult>(new Error(
                        "Ollama.ModelNotFound",
                        "The configured Ollama model was not found."));
                }
                return Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.RequestFailed",
                    "Ollama could not complete the generation request."));
            }

            var payload = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Model))
            {
                logger?.LogWarning(
                    "Ollama returned an invalid payload. Model={Model}",
                    selectedModel);

                return Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.InvalidResponse",
                    "Ollama returned an invalid generation response."));
            }

            if (string.IsNullOrWhiteSpace(payload.Response))
            {
                logger?.LogWarning(
                    "Ollama returned no final response. Model={Model}, Done={Done}, DoneReason={DoneReason}, EvalCount={EvalCount}, HasThinking={HasThinking}",
                    selectedModel,
                    payload.Done,
                    payload.DoneReason,
                    payload.EvalCount,
                    !string.IsNullOrWhiteSpace(payload.Thinking));

                return Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.InvalidResponse",
                    "Ollama completed generation but returned no final answer."));
            }

            return Result.Success(new LlmGenerationResult(
                payload.Model,
                payload.Response,
                payload.Done));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            logger?.LogWarning(
                "Ollama generation timed out. Operation={Operation}, Model={Model}",
                "generate",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.Unavailable",
                "The Ollama generation request timed out."));
        }
        catch (HttpRequestException)
        {
            logger?.LogWarning(
                "Ollama generation was unavailable. Operation={Operation}, Model={Model}",
                "generate",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.Unavailable",
                "Ollama is unavailable."));
        }
        catch (NotSupportedException)
        {
            logger?.LogWarning(
                "Ollama generation returned an unsupported response. Operation={Operation}, Model={Model}",
                "generate",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.InvalidResponse",
                "Ollama returned an unsupported response."));
        }
        catch (JsonException)
        {
            logger?.LogWarning(
                "Ollama generation returned invalid JSON. Operation={Operation}, Model={Model}",
                "generate",
                selectedModel);
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.InvalidResponse",
                "Ollama returned invalid JSON."));
        }
    }

    public async Task<IReadOnlyList<string>?> ProbeAvailableModelsAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaGenerationProvider));
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var tagUri = new Uri(baseUri, "/api/tags");
            using var response = await client.GetAsync(tagUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (json.TryGetProperty("models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in modelsElem.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
                        }
                    }
                    return list;
                }
            }
        }
        catch
        {
            // Ignore probe errors
        }

        return null;
    }

    private sealed record GenerateRequest(
        string Model,
        string Prompt,
        bool Stream,
        bool Think,
        GenerateOptions Options,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? Images = null);
    private sealed record GenerateOptions(
        double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("num_predict")] int? MaxTokens);
    private sealed record GenerateResponse(
        string? Model,
        string? Response,
        string? Thinking,
        bool Done,
        [property: JsonPropertyName("done_reason")]
        string? DoneReason,
        [property: JsonPropertyName("eval_count")]
        int? EvalCount);
}
