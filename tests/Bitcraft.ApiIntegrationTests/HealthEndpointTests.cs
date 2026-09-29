using System.Net;
using System.Net.Http.Json;
using Bitcraft.Api.Controllers;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public class HealthEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    [Fact]
    public async Task GetHealth_AnonymousOutsideDevelopment_ReturnsOkWithoutVersion()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload!.Status);
<<<<<<< HEAD
        // Build metadata must not leak to anonymous callers outside Development.
        // The test host runs in the Testing environment, so Version is expected to be null.
        Assert.True(string.IsNullOrWhiteSpace(payload.Version));
=======
        Assert.Null(payload.Version);
>>>>>>> dev
    }
}
