using System.Net;
using System.Net.Http.Json;
using Bitcraft.Api.Controllers;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public class HealthEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    [Fact]
    public async Task GetHealth_Anonymous_ReturnsOkWithStatusAndVersion()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload!.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
    }
}
