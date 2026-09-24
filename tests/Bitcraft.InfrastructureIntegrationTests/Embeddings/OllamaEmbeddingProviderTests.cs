using System.Net;
using System.Text;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Embeddings;

public class OllamaEmbeddingProviderTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_WhenTransportFailsReturnsServiceUnavailableFailure()
    {
        var provider = CreateProvider(new StubHandler(_ => throw new HttpRequestException("connection refused")));

        var exception = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.GenerateEmbeddingAsync("chunk", "nomic-embed-text"));

        Assert.Equal(EmbeddingProviderFailureKind.ServiceUnavailable, exception.Kind);
        Assert.DoesNotContain("connection refused", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenOllamaRejectsRequestReturnsSafeRequestFailure()
    {
        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("internal runtime detail that must not reach the API")
        }));

        var exception = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.GenerateEmbeddingAsync("chunk", "missing-model"));

        Assert.Equal(EmbeddingProviderFailureKind.RequestFailed, exception.Kind);
        Assert.DoesNotContain("internal runtime detail", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenResponseIsMalformedReturnsResponseInvalidFailure()
    {
        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json")
        }));

        var exception = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.GenerateEmbeddingAsync("chunk", "nomic-embed-text"));

        Assert.Equal(EmbeddingProviderFailureKind.ResponseInvalid, exception.Kind);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidResponseReturnsVector()
    {
        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"embedding\":[0.1,0.2,0.3]}", Encoding.UTF8, "application/json")
        }));

        var embedding = await provider.GenerateEmbeddingAsync("chunk", "nomic-embed-text");

        Assert.Equal([0.1f, 0.2f, 0.3f], embedding);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PreservesConfiguredProxyPrefix()
    {
        Uri? requested = null;
        var configuration = new ConfigurationBuilder().Build();
        var provider = new OllamaEmbeddingProvider(new StubHttpClientFactory(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"embedding\":[1]}", Encoding.UTF8, "application/json")
            };
        })), configuration, NullLogger<OllamaEmbeddingProvider>.Instance);

        await provider.GenerateEmbeddingAsync(new EmbeddingGenerationRequest("chunk", "embed",
            RuntimeEndpoint: "https://runtime.invalid/proxy"));

        Assert.Equal("/proxy/api/embeddings", requested?.AbsolutePath);
    }

    private static OllamaEmbeddingProvider CreateProvider(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModelProviders:Ollama:BaseUrl"] = "http://ollama.invalid"
            })
            .Build();
        return new OllamaEmbeddingProvider(
            new StubHttpClientFactory(handler),
            configuration,
            NullLogger<OllamaEmbeddingProvider>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}
