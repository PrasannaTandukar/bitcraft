using System.Net;
using Bitcraft.Infrastructure.ExternalServices.Ollama;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Ollama;

public class OllamaGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsync_WhenBaseUrlIsInvalid_ReturnsFailure()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ModelProviders:Ollama:BaseUrl"] = "not-a-url"
        });
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"))),
            configuration);

        var result = await provider.GenerateAsync("Explain Docker", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.BaseUrlInvalid", result.Error.Code);
    }

    [Fact]
    public async Task GenerateAsync_WhenOllamaReturnsNonSuccess_ReturnsFailure()
    {
        var configuration = BuildConfiguration();
        var provider = new OllamaGenerationProvider(
            new StubHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "failed"))),
            configuration);

        var result = await provider.GenerateAsync("Explain Docker", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.RequestFailed", result.Error.Code);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>
            {
                ["ModelProviders:Ollama:BaseUrl"] = "http://localhost:11434",
                ["ModelProviders:Ollama:ChatModel"] = "llama3.2"
            })
            .Build();

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }
}
