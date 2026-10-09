using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

/// <summary>AC13: the standardized benchmark progress endpoint is AuditorOrAbove.</summary>
public class StandardBenchmarkProgressEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task GetProgress_WithAuditorRole_ReturnsNotFoundForUnknownRun()
    {
        // Arrange: the endpoint must be reachable for auditors; an unknown run id
        // yields 404 (not 403), proving the policy passed.
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);

        // Act
        var response = await client.GetAsync($"/api/standard-benchmark-runs/{Guid.NewGuid()}/progress");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProgress_WithRolelessUser_ReturnsForbidden()
    {
        // Arrange: a registered user without any role is below AuditorOrAbove.
        var client = factory.CreateClient();
        await AuthorizeAsRolelessAsync(client);

        // Act
        var response = await client.GetAsync($"/api/standard-benchmark-runs/{Guid.NewGuid()}/progress");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AuthorizeAsRoleAsync(HttpClient client, string role)
    {
        var email = $"test-{Guid.NewGuid():N}@bitcraft.test";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded, string.Join(" ", roleResult.Errors.Select(error => error.Description)));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }

    private async Task AuthorizeAsRolelessAsync(HttpClient client)
    {
        var email = $"test-{Guid.NewGuid():N}@bitcraft.test";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }
}
