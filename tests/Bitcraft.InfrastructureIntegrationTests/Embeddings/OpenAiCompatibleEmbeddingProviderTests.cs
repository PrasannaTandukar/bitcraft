using System.Net;
using System.Text;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Bitcraft.Infrastructure.ExternalServices.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Embeddings;

public sealed class OpenAiCompatibleEmbeddingProviderTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ResolvesSecretReferenceBeforeTransport()
    {
        string? credential = null;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ModelProviders:Secrets:embedding-reference"] = "resolved-ephemeral-value"
        }).Build();
        var provider = Create(configuration, request =>
        {
            credential = request.Headers.Authorization?.Parameter;
            return Json(HttpStatusCode.OK, "{\"data\":[{\"embedding\":[0.1,0.2]}]}");
        });

        var vector = await provider.GenerateEmbeddingAsync(new EmbeddingGenerationRequest(
            "query", "embed-model", "https://runtime.invalid/proxy", "ref:embedding-reference",
            "OpenAICompatible"));

        Assert.Equal([0.1f, 0.2f], vector);
        Assert.Equal("resolved-ephemeral-value", credential);
        Assert.NotEqual("ref:embedding-reference", credential);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_MissingSecretFailsBeforeNetwork()
    {
        var called = false;
        var configuration = new ConfigurationBuilder().Build();
        var provider = Create(configuration, _ =>
        {
            called = true;
            return Json(HttpStatusCode.OK, "{}");
        });

        var exception = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.GenerateEmbeddingAsync(new EmbeddingGenerationRequest("query", "embed-model",
                "https://runtime.invalid", "ref:missing-reference", "OpenAI")));

        Assert.False(called);
        Assert.DoesNotContain("missing-reference", exception.Message);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_UpstreamBodyIsNotReturned()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = Create(configuration, _ => Json(HttpStatusCode.BadRequest,
            "{\"error\":\"private document and credential material\"}"));

        var exception = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.GenerateEmbeddingAsync(new EmbeddingGenerationRequest("query", "embed-model",
                "https://runtime.invalid", ProviderType: "OpenAICompatible")));

        Assert.DoesNotContain("private document", exception.Message);
        Assert.DoesNotContain("credential material", exception.Message);
    }

    [Theory]
    [InlineData("http://localhost:7997", "/embeddings")]
    [InlineData("http://localhost:7997/v1", "/v1/embeddings")]
    [InlineData("http://localhost:7997/embeddings", "/embeddings")]
    public async Task GenerateEmbeddingAsync_SupportsBothRootAndPrefixedRoutes(string endpoint, string expectedPath)
    {
        Uri? calledUri = null;
        var configuration = new ConfigurationBuilder().Build();
        var provider = Create(configuration, request =>
        {
            calledUri = request.RequestUri;
            if (request.RequestUri?.AbsolutePath == "/v1/embeddings" && endpoint == "http://localhost:7997")
            {
                return Json(HttpStatusCode.NotFound, "{}");
            }
            return Json(HttpStatusCode.OK, "{\"data\":[{\"embedding\":[0.5,0.6]}]}");
        });

        var vector = await provider.GenerateEmbeddingAsync(new EmbeddingGenerationRequest(
            "text", "embed-model", endpoint, ProviderType: "OpenAICompatible"));

        Assert.Equal([0.5f, 0.6f], vector);
        Assert.Equal(expectedPath, calledUri?.AbsolutePath);
    }

    private static OpenAiCompatibleEmbeddingProvider Create(IConfiguration configuration,
        Func<HttpRequestMessage, HttpResponseMessage> response) => new(
        new StubHttpClientFactory(new StubHandler(response)), configuration,
        new ConfigurationRuntimeApiKeyResolver(configuration),
        NullLogger<OpenAiCompatibleEmbeddingProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string value) => new(status)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
