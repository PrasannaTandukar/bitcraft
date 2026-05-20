using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public class AuthenticationPolicyTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task GetDocuments_WithoutBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/documents");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_WithAuditorRole_ReturnsOk()
    {
        // Arrange
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);

        // Act
        var response = await client.GetAsync("/api/documents");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documents = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>();
        Assert.NotNull(documents);
        Assert.Empty(documents);
    }

    [Fact]
    public async Task CreateDocument_WithAuditorRole_ReturnsForbidden()
    {
        // Arrange
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var request = new CreateDocumentRequest("Forbidden", null, "text/plain", "Auditor cannot create.");

        // Act
        var response = await client.PostAsJsonAsync("/api/documents", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateDocument_WithEditorRole_CreatesDocumentForAuthenticatedUser()
    {
        // Arrange
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var request = new CreateDocumentRequest("Editor document", null, "text/plain", "Created from integration test.");

        // Act
        var createResponse = await client.PostAsJsonAsync("/api/documents", request);
        var listResponse = await client.GetAsync("/api/documents");

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var documents = await listResponse.Content.ReadFromJsonAsync<List<DocumentResponse>>();
        Assert.NotNull(documents);
        Assert.Contains(documents, document => document.Name == "Editor document");
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
        Assert.Contains(role, auth.Roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }
}
