using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using OpenAI;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

public sealed class RuntimeChatClientFactory(
    IHttpClientFactory httpClientFactory,
    IRuntimeApiKeyResolver apiKeys,
    IConfiguration configuration) : IRuntimeChatClientFactory
{
    public const string HttpClientName = "TutorAgent";
    public const string OllamaHttpClientName = "TutorAgent.Ollama";

    public Result<IChatClient> Create(TutorAgentModelConfiguration model)
    {
        if (model.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI
            or RuntimeEngineProviderType.OpenAICompatible or RuntimeEngineProviderType.vLLM
            or RuntimeEngineProviderType.LlamaCpp or RuntimeEngineProviderType.Anthropic))
            return Failure("UnsupportedProvider", "The runtime provider is not supported for tutor agent execution.");
        if (string.IsNullOrWhiteSpace(model.ProviderModelName))
            return Failure("ModelRequired", "A provider model name is required.");
        if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment))
            return Failure("EndpointInvalid", "The runtime endpoint must be an absolute HTTP or HTTPS URL without credentials, query or fragment.");

        var key = apiKeys.Resolve(model);
        if (key.IsFailure) return Result.Failure<IChatClient>(key.Error);
        var timeoutSeconds = configuration.GetValue<int?>($"ModelProviders:{model.ProviderType}:GenerateTimeoutSeconds")
            ?? configuration.GetValue<int?>("ModelProviders:Ollama:GenerateTimeoutSeconds") ?? 300;
        if (timeoutSeconds <= 0)
            return Failure("TimeoutInvalid", "The generation timeout must be positive.");

        var httpClient = httpClientFactory.CreateClient(model.ProviderType == RuntimeEngineProviderType.Ollama
            ? OllamaHttpClientName : HttpClientName);
        try
        {
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            httpClient.Timeout = timeout;
            IChatClient chatClient;
            if (model.ProviderType == RuntimeEngineProviderType.Ollama)
            {
                httpClient.BaseAddress = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
                if (key.Value is { } ollamaKey)
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ollamaKey);
                chatClient = new OllamaApiClient(httpClient, model.ProviderModelName);
            }
            else if (model.ProviderType == RuntimeEngineProviderType.Anthropic)
            {
                chatClient = new AnthropicCompatibilityChatClient(httpClient, model, key.Value);
            }
            else
            {
                var options = new OpenAIClientOptions
                {
                    Endpoint = OpenAiEndpoint(endpoint),
                    Transport = new HttpClientPipelineTransport(httpClient),
                    NetworkTimeout = timeout,
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
                };
                // A credential-free compatible server must not inherit an SDK/environment credential.
                if (key.Value is null)
                    options.AddPolicy(new RemoveAuthenticationPolicy(), PipelinePosition.BeforeTransport);
                // The stable SDK requires a nonempty credential. This sentinel is removed before transport.
                var sdk = new OpenAIClient(new ApiKeyCredential(key.Value ?? "unused"), options);
                chatClient = sdk.GetChatClient(model.ProviderModelName).AsIChatClient();
            }
            return Result.Success<IChatClient>(new OwnedRuntimeChatClient(chatClient, httpClient, timeout));
        }
        catch
        {
            httpClient.Dispose();
            throw;
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ProbeAvailableModelsAsync(
        TutorAgentModelConfiguration model,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment))
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "RuntimeDiagnostic.EndpointInvalid", "The runtime endpoint is invalid."));

        if (model.ProviderType is RuntimeEngineProviderType.Anthropic)
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "RuntimeDiagnostic.CatalogUnsupported",
                "The configured provider does not expose a supported model catalog."));
        if (model.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI
            or RuntimeEngineProviderType.OpenAICompatible or RuntimeEngineProviderType.vLLM
            or RuntimeEngineProviderType.LlamaCpp))
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "RuntimeDiagnostic.ProviderUnsupported", "The runtime provider is not supported."));

        var key = apiKeys.Resolve(model);
        if (key.IsFailure) return Result.Failure<IReadOnlyList<string>>(key.Error);
        using var httpClient = httpClientFactory.CreateClient(model.ProviderType == RuntimeEngineProviderType.Ollama
            ? OllamaHttpClientName : HttpClientName);
        try
        {
            if (model.ProviderType == RuntimeEngineProviderType.Ollama)
            {
                httpClient.BaseAddress = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
                if (key.Value is { } ollamaKey)
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ollamaKey);
                var response = await new OllamaApiClient(httpClient).ListLocalModelsAsync(cancellationToken);
                return Result.Success<IReadOnlyList<string>>(response
                    .Select(entry => entry.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList());
            }

            var timeoutSeconds = configuration.GetValue<int?>($"ModelProviders:{model.ProviderType}:GenerateTimeoutSeconds")
                ?? configuration.GetValue<int?>("ModelProviders:Ollama:GenerateTimeoutSeconds") ?? 300;

            async Task<Result<IReadOnlyList<string>>> QueryOpenAiModelsAsync(Uri baseUri)
            {
                var options = new OpenAIClientOptions
                {
                    Endpoint = baseUri,
                    Transport = new HttpClientPipelineTransport(httpClient),
                    NetworkTimeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
                };
                if (key.Value is null)
                    options.AddPolicy(new RemoveAuthenticationPolicy(), PipelinePosition.BeforeTransport);
                var sdk = new OpenAIClient(new ApiKeyCredential(key.Value ?? "unused"), options);
                var responseModels = await sdk.GetOpenAIModelClient().GetModelsAsync(cancellationToken);
                return Result.Success<IReadOnlyList<string>>(responseModels.Value
                    .Select(entry => entry.Id)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList());
            }

            var primaryEndpoint = OpenAiEndpoint(endpoint);
            try
            {
                return await QueryOpenAiModelsAsync(primaryEndpoint);
            }
            catch (ClientResultException ex) when (ex.Status == 404)
            {
                // If primary /v1/models 404s and endpoint was bare root, retry against root base directly
                var rootEndpoint = OpenAiEndpoint(endpoint, preferRoot: true);
                if (rootEndpoint != primaryEndpoint)
                {
                    try
                    {
                        return await QueryOpenAiModelsAsync(rootEndpoint);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Fall through to return failure
                    }
                }
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "RuntimeDiagnostic.ConnectionFailed", "The runtime endpoint could not be reached."));
        }
    }

    private static Uri OpenAiEndpoint(Uri endpoint, bool preferRoot = false)
    {
        var raw = endpoint.AbsoluteUri.TrimEnd('/');
        if (raw.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            raw = raw[..^"/chat/completions".Length].TrimEnd('/');
        else if (raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            raw = preferRoot ? raw[..^"/v1".Length].TrimEnd('/') : raw;
        else if (!preferRoot)
            raw += "/v1";
        return new Uri(raw + "/");
    }

    private static Result<IChatClient> Failure(string code, string description) =>
        Result.Failure<IChatClient>(new Error($"TutorAgent.{code}", description));

    private sealed class RemoveAuthenticationPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Remove("Authorization");
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Remove("Authorization");
            return ProcessNextAsync(message, pipeline, currentIndex);
        }
    }

    private sealed class OwnedRuntimeChatClient(IChatClient inner, HttpClient httpClient, TimeSpan timeout)
        : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            return await base.GetResponseAsync(messages, options, deadline.Token);
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, deadline.Token)
                .WithCancellation(deadline.Token))
                yield return update;
        }

        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) httpClient.Dispose(); }
        }
    }
}
