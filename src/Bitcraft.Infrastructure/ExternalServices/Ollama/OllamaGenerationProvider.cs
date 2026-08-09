using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Ollama;

public class OllamaGenerationProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaGenerationProvider>? logger = null) : ILlmGenerationProvider
{
    private const string DefaultBaseUrl = "http://ollama:11434";
    private const string DefaultChatModel = "llama3.2";

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

        var baseUrl = generationRequest.RuntimeEndpoint
            ?? configuration["ModelProviders:Ollama:BaseUrl"]
            ?? configuration["OLLAMA_BASE_URL"]
            ?? DefaultBaseUrl;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.BaseUrlInvalid",
                "Ollama base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = string.IsNullOrWhiteSpace(generationRequest.Model)
            ? configuration["ModelProviders:Ollama:ChatModel"] ?? configuration["OLLAMA_CHAT_MODEL"] ?? DefaultChatModel
            : generationRequest.Model.Trim();

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
            new GenerateOptions(
                generationRequest.Temperature,
                generationRequest.TopP,
                generationRequest.MaxTokens));

        try
        {
            using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
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
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.Model) ||
                string.IsNullOrWhiteSpace(payload.Response))
            {
                return Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.InvalidResponse",
                    "Ollama generation response did not contain generated text."));
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

    private sealed record GenerateRequest(string Model, string Prompt, bool Stream, GenerateOptions Options);
    private sealed record GenerateOptions(
        double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("num_predict")] int? MaxTokens);
    private sealed record GenerateResponse(string? Model, string? Response, bool Done);
}
