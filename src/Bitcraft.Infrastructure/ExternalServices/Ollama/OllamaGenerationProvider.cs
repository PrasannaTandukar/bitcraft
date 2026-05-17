using System.Net.Http.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Configuration;

namespace Bitcraft.Infrastructure.ExternalServices.Ollama;

public class OllamaGenerationProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ILlmGenerationProvider
{
    private const string DefaultBaseUrl = "http://ollama:11434";
    private const string DefaultChatModel = "llama3.2";

    public async Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.PromptRequired",
                "Prompt is required."));
        }

        if (model is not null && string.IsNullOrWhiteSpace(model))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.ModelInvalid",
                "Model cannot be empty when provided."));
        }

        var baseUrl = configuration["ModelProviders:Ollama:BaseUrl"]
            ?? configuration["OLLAMA_BASE_URL"]
            ?? DefaultBaseUrl;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.BaseUrlInvalid",
                "Ollama base URL must be an absolute HTTP or HTTPS URL."));
        }

        var selectedModel = string.IsNullOrWhiteSpace(model)
            ? configuration["ModelProviders:Ollama:ChatModel"] ?? configuration["OLLAMA_CHAT_MODEL"] ?? DefaultChatModel
            : model.Trim();

        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.ModelInvalid",
                "Ollama chat model is not configured."));
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaGenerationProvider));
        var endpoint = new Uri(baseUri, "/api/generate");
        var request = new GenerateRequest(selectedModel.Trim(), prompt.Trim(), Stream: false);

        try
        {
            using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.RequestFailed",
                    $"Ollama generation request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}."));
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
        catch (TaskCanceledException ex)
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.Unavailable",
                "Ollama generation request timed out: " + ex.Message));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.Unavailable",
                "Ollama is unavailable: " + ex.Message));
        }
        catch (NotSupportedException ex)
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.InvalidResponse",
                "Ollama returned an unsupported response: " + ex.Message));
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result.Failure<LlmGenerationResult>(new Error(
                "Ollama.InvalidResponse",
                "Ollama returned invalid JSON: " + ex.Message));
        }
    }

    private sealed record GenerateRequest(string Model, string Prompt, bool Stream);
    private sealed record GenerateResponse(string? Model, string? Response, bool Done);
}
