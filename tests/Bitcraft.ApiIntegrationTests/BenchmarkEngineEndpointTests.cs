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

/// <summary>
/// HD-9: the sidecar engine status endpoint is AuditorOrAbove; the clear-slot
/// recovery endpoint is AdminOnly. The sidecar itself is absent in the test
/// environment, so the status surface reports unreachable (200) and the
/// clear action reports a failed result (502) — both proving the policy passed.
/// </summary>
public class BenchmarkEngineEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task GetEngineStatus_WithAuditorRole_ReturnsStatusDocument()
    {
        // Arrange: auditors can read the engine status. The sidecar is absent in
        // the test environment, so the engine reports unreachable through a 200
        // status document (reachability is data, not an error).
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);

        // Act
        var response = await client.GetAsync("/api/standard-benchmark-runs/engine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<EngineStatusPayload>();
        Assert.NotNull(payload);
        Assert.False(payload.IsReachable);
    }

    [Fact]
    public async Task GetEngineStatus_WithRolelessUser_ReturnsForbidden()
    {
        // Arrange: a registered user without any role is below AuditorOrAbove.
        var client = factory.CreateClient();
        await AuthorizeAsRolelessAsync(client);

        // Act
        var response = await client.GetAsync("/api/standard-benchmark-runs/engine");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClearEngineSlot_WithAdminRole_ReportsSidecarFailure()
    {
        // Arrange: admins pass the policy. With no sidecar in the test
        // environment the clear call fails and maps to 502.
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Admin);

        // Act
        var response = await client.PostAsync("/api/standard-benchmark-runs/engine/clear-slot", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ClearEngineSlot_WithEditorRole_ReturnsForbidden()
    {
        // Arrange: editors are below AdminOnly for the recovery action.
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        // Act
        var response = await client.PostAsync("/api/standard-benchmark-runs/engine/clear-slot", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record EngineStatusPayload(bool IsReachable, string Transport, bool SlotHeld, string? CurrentRunId, string? EngineVersion, string? LastError);

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
