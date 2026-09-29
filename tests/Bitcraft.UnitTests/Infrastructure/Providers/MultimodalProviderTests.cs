using System.Net;
using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices;
using Bitcraft.Infrastructure.ExternalServices.Anthropic;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Bitcraft.Infrastructure.ExternalServices.OpenAI;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Providers;

public sealed class MultimodalProviderTests
{
    [Theory]
    [InlineData("Ollama", false)]
    [InlineData("Ollama", true)]
    [InlineData("OpenAI", false)]
    [InlineData("OpenAI", true)]
    [InlineData("OpenAICompatible", true)]
    [InlineData("vLLM", true)]
    [InlineData("LlamaCpp", true)]
    [InlineData("Anthropic", false)]
    [InlineData("Anthropic", true)]
    public async Task ProviderPreservesTextOptionsAndSerializesOrderedImages(string providerType, bool withImages)
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var images = new[] { new LlmImageInput("image/png", new byte[] { 1, 2 }), new LlmImageInput("image/jpeg", new byte[] { 3, 4 }) };
        var result = await provider.GenerateAsync(new LlmGenerationRequest("Tutor prompt", "custom-model",
            Temperature: 0.25, TopP: 0.9, MaxTokens: 123, RuntimeEndpoint: "https://runtime.test",
            ApiKey: "test-key", ProviderType: providerType, Images: withImages ? images : null));
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("Answer", result.Value.Response);
        var body = handler.Body;
        Assert.Equal("custom-model", body.GetProperty("model").GetString());
        if (providerType == "Ollama")
        {
            Assert.EndsWith("/api/generate", handler.Path);
            Assert.Equal("Tutor prompt", body.GetProperty("prompt").GetString());
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.Equal(123, body.GetProperty("options").GetProperty("num_predict").GetInt32());
            Assert.Equal(0.25, body.GetProperty("options").GetProperty("temperature").GetDouble());
            Assert.Equal(0.9, body.GetProperty("options").GetProperty("top_p").GetDouble());
            if (withImages)
                Assert.Equal(images.Select(image => Convert.ToBase64String(image.Data.Span)), body.GetProperty("images").EnumerateArray().Select(image => image.GetString()));
            else Assert.False(body.TryGetProperty("images", out _));
            return;
        }
        Assert.Equal(123, body.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.25, body.GetProperty("temperature").GetDouble());
        Assert.Equal(0.9, body.GetProperty("top_p").GetDouble());
        Assert.Equal("user", body.GetProperty("messages")[0].GetProperty("role").GetString());
        var content = body.GetProperty("messages")[0].GetProperty("content");
        if (providerType == "Anthropic")
        {
            Assert.Equal("test-key", handler.ApiKey);
            Assert.Equal("2023-06-01", handler.Version);
            Assert.EndsWith("/v1/messages", handler.Path);
            if (withImages)
            {
                Assert.Equal(3, content.GetArrayLength());
                for (var index = 0; index < images.Length; index++)
                {
                    Assert.Equal("image", content[index].GetProperty("type").GetString());
                    var source = content[index].GetProperty("source");
                    Assert.Equal("base64", source.GetProperty("type").GetString());
                    Assert.Equal(images[index].MimeType, source.GetProperty("media_type").GetString());
                    Assert.Equal(images[index].Data.ToArray(), Convert.FromBase64String(source.GetProperty("data").GetString()!));
                }
                Assert.Equal("Tutor prompt", content[2].GetProperty("text").GetString());
            }
        }
        else
        {
            Assert.Equal("Bearer test-key", handler.Authorization);
            Assert.EndsWith("/v1/chat/completions", handler.Path);
            if (withImages)
            {
                Assert.Equal(3, content.GetArrayLength());
                Assert.Equal("text", content[0].GetProperty("type").GetString());
                Assert.Equal("Tutor prompt", content[0].GetProperty("text").GetString());
                for (var index = 0; index < images.Length; index++)
                {
                    Assert.Equal("image_url", content[index + 1].GetProperty("type").GetString());
                    Assert.Equal($"data:{images[index].MimeType};base64,{Convert.ToBase64String(images[index].Data.Span)}", content[index + 1].GetProperty("image_url").GetProperty("url").GetString());
                }
            }
        }
        if (!withImages) Assert.Equal("Tutor prompt", content.GetString());
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("OpenAI")]
    [InlineData("Anthropic")]
    public async Task ImageRejectionReturnsSafeError(string providerType)
    {
        using var client = new HttpClient(new RecordingHandler(HttpStatusCode.BadRequest));
        var result = await CreateProvider(client).GenerateAsync(new LlmGenerationRequest("Prompt", "custom",
            RuntimeEndpoint: "https://runtime.test", ProviderType: providerType, Images: [new("image/png", new byte[] { 1 })]));
        Assert.True(result.IsFailure);
        Assert.Equal($"{providerType}.ImageRejected", result.Error.Code);
        Assert.DoesNotContain("sensitive upstream body", result.Error.Description);
    }

    [Fact]
    public async Task LegacyInterfaceCannotSilentlyDiscardImages()
    {
        ILlmGenerationProvider provider = new TextOnlyProvider();
        Assert.True((await provider.GenerateAsync(new LlmGenerationRequest("Hello", "model"))).IsSuccess);
        var result = await provider.GenerateAsync(new LlmGenerationRequest("Hello", "model", Images: [new("image/png", new byte[] { 1 })]));
        Assert.Equal("Llm.ImageInputNotSupported", result.Error.Code);
    }

    [Fact]
    public async Task ProbeNeverAttachesServerConfiguredCredential()
    {
        // Regression for Sec Ops finding F1: the probe endpoint accepts caller-chosen
        // endpoints, so a null-key probe to an arbitrary endpoint must not attach the
        // process-wide OpenAI credential from configuration.
        var handler = new ProbeRecordingHandler();
        using var client = new HttpClient(handler);
        var factory = new ClientFactory(client);
        // OD-4 removed the process-level config fallbacks entirely, so the probe can only ever
        // see the caller-supplied key. The regression assertion (no server credential attached)
        // is preserved for the case a fallback is ever reintroduced.
        var provider = new OpenAiCompatibleGenerationProvider(factory);

        await provider.ProbeAvailableModelsAsync("https://attacker.example.com/v1", null, CancellationToken.None);

        // First URL succeeds and the probe returns early; the essential assertion is that no
        // request carried a server-configured credential.
        Assert.Equal(1, handler.RequestCount);
        Assert.All(handler.Authorizations, Assert.Null);
    }

    [Fact]
    public async Task ProbeUsesCallerSuppliedKeyOnly()
    {
        var handler = new ProbeRecordingHandler();
        using var client = new HttpClient(handler);
        var factory = new ClientFactory(client);
        var provider = new OpenAiCompatibleGenerationProvider(factory);

        var models = await provider.ProbeAvailableModelsAsync("https://provider.example.com/v1", "probe-key", CancellationToken.None);

        Assert.NotNull(models);
        Assert.All(handler.Authorizations, auth => Assert.Equal("Bearer probe-key", auth));
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("OpenAICompatible")]
    [InlineData("vLLM")]
    [InlineData("LlamaCpp")]
    public async Task CompositeProbeRoutesOpenAiDialectAliasesToOpenAiProvider(string providerType)
    {
        // OD-1: the aliases collapse to the OpenAI dialect through the shared map (review L4).
        var handler = new ProbeRecordingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var models = await provider.ProbeAvailableModelsAsync("https://runtime.test/v1", "alias-key", providerType, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "model-a", "model-b" }, models);
        Assert.All(handler.Authorizations, auth => Assert.Equal("Bearer alias-key", auth));
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CompositeProbeRoutesOllamaDialectToOllamaProvider(string? providerType)
    {
        // A blank provider type is the documented legacy Ollama path (OD-8).
        var handler = new ProbeRecordingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var models = await provider.ProbeAvailableModelsAsync("http://runtime.test:11434", null, providerType, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "model-a", "model-b" }, models);
        Assert.All(handler.Authorizations, Assert.Null);
    }

    [Theory]
    [InlineData("Anthropic")]
    [InlineData("AzureOpenAI")]
    [InlineData("Bedrock")]
    [InlineData("not-a-provider")]
    public async Task CompositeProbeWithUnsupportedProviderTypeReturnsNull(string providerType)
    {
        // OD-8: fail closed. An unparseable or non-probeable provider type probes nothing.
        var handler = new ProbeRecordingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var models = await provider.ProbeAvailableModelsAsync("https://runtime.test", null, providerType, CancellationToken.None);

        Assert.Null(models);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("AzureOpenAI")]
    [InlineData("Bedrock")]
    [InlineData("not-a-provider")]
    public async Task CompositeGenerationWithUnsupportedProviderTypeFailsClosed(string providerType)
    {
        // OD-8: generation with an unparseable or unsupported provider type returns a failure.
        // It never falls back to a different provider silently.
        var handler = new RequestCountingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(new LlmGenerationRequest("Prompt", "custom",
            RuntimeEndpoint: "https://runtime.test", ProviderType: providerType));

        Assert.True(result.IsFailure);
        Assert.Equal("LlmGeneration.ProviderTypeUnsupported", result.Error.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    private sealed class ProbeRecordingHandler : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = new();
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Authorizations.Add(request.Headers.Authorization?.ToString());
            // The Ollama probe reads /api/tags; the OpenAI probe reads /v1/models.
            var body = request.RequestUri!.AbsolutePath.EndsWith("/api/tags")
                ? "{\"models\":[{\"name\":\"model-a\"},{\"name\":\"model-b\"}]}"
                : "{\"data\":[{\"id\":\"model-a\"},{\"id\":\"model-b\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }

    private sealed class RequestCountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"model\":\"custom-model\",\"response\":\"Answer\",\"done\":true}"),
            });
        }
    }

    private static ILlmGenerationProvider CreateProvider(HttpClient client)
    {
        var factory = new ClientFactory(client);
        return new CompositeLlmGenerationProvider(new OllamaGenerationProvider(factory), new OpenAiCompatibleGenerationProvider(factory), new AnthropicGenerationProvider(factory));
    }

    private sealed class TextOnlyProvider : ILlmGenerationProvider
    {
        public Task<Result<LlmGenerationResult>> GenerateAsync(string prompt, string? model, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new LlmGenerationResult(model!, "Answer", true)));
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public JsonElement Body { get; private set; }
        public string Path { get; private set; } = "";
        public string? ApiKey { get; private set; }
        public string? Version { get; private set; }
        public string? Authorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            Path = request.RequestUri!.AbsolutePath;
            ApiKey = request.Headers.TryGetValues("x-api-key", out var keys) ? keys.Single() : null;
            Version = request.Headers.TryGetValues("anthropic-version", out var versions) ? versions.Single() : null;
            Authorization = request.Headers.Authorization?.ToString();
            var response = Path switch
            {
                "/api/generate" => "{\"model\":\"custom-model\",\"response\":\"Answer\",\"done\":true}",
                "/v1/messages" => "{\"model\":\"custom-model\",\"content\":[{\"type\":\"text\",\"text\":\"Answer\"}]}",
                _ => "{\"model\":\"custom-model\",\"choices\":[{\"message\":{\"content\":\"Answer\"}}]}"
            };
            return new HttpResponseMessage(statusCode) { Content = new StringContent(statusCode == HttpStatusCode.OK ? response : "sensitive upstream body") };
        }
    }
}
