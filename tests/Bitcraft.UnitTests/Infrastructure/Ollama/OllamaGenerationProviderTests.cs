using System.Net;
using System.Text.Json;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Ollama;

public class OllamaGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsync_WhenBaseUrlIsInvalid_ReturnsFailure()
    {
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"))));

        var result = await provider.GenerateAsync(new LlmGenerationRequest("Explain Docker", "llama3.2", RuntimeEndpoint: "not-a-url"));

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.BaseUrlInvalid", result.Error.Code);
    }

    [Fact]
    public async Task GenerateAsync_WhenOllamaReturnsNonSuccess_ReturnsFailure()
    {
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "failed"))));

        var result = await provider.GenerateAsync(new LlmGenerationRequest("Explain Docker", "llama3.2", RuntimeEndpoint: "http://localhost:11434"));

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.RequestFailed", result.Error.Code);
    }

    [Fact]
    public async Task GenerateAsync_TypedRequestUsesConfiguredModelEndpointAndOptions()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            "{\"model\":\"configured-model\",\"response\":\"Answer\",\"done\":true}");
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(handler)));

        var result = await provider.GenerateAsync(new LlmGenerationRequest(
            "Configured tutor prompt",
            "configured-model",
            Temperature: 0.3,
            TopP: 0.85,
            MaxTokens: 640,
            RuntimeEndpoint: "http://runtime.example:11434"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new Uri("http://runtime.example:11434/api/generate"), handler.RequestUri);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal("configured-model", body.RootElement.GetProperty("model").GetString());
        var options = body.RootElement.GetProperty("options");
        Assert.Equal(0.3, options.GetProperty("temperature").GetDouble());
        Assert.Equal(0.85, options.GetProperty("top_p").GetDouble());
        Assert.Equal(640, options.GetProperty("num_predict").GetInt32());
    }

    [Fact]
    public async Task GenerateAsync_WhenConfiguredModelIsMissing_ReturnsSafeModelNotFound()
    {
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.NotFound, "raw upstream body"))));

        var result = await provider.GenerateAsync(new LlmGenerationRequest("Prompt", "missing-model", RuntimeEndpoint: "http://localhost:11434"));

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.ModelNotFound", result.Error.Code);
        Assert.DoesNotContain("raw upstream body", result.Error.Description, StringComparison.Ordinal);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };
        }
    }
}
