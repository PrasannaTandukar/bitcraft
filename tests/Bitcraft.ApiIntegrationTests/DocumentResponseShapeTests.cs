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

/// <summary>
/// D33 — verifies the <see cref="DocumentSummaryResponse"/> / <see cref="DocumentDetailResponse"/>
/// split: the list endpoint omits <c>RawText</c>, the get-by-id endpoint includes it.
/// </summary>
public class DocumentResponseShapeTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task ListReturnsSummaryWithoutRawText_AndGetByIdReturnsDetailWithRawText()
    {
        // Arrange
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        const string rawText = "Body content that must NOT leak through list endpoints.";
        var request = new CreateDocumentRequest("Shape probe", null, "text/plain", rawText);

        // Act
        var createResponse = await client.PostAsJsonAsync("/api/documents", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(created);
        Assert.Equal(rawText, created.RawText);

        var detailResponse = await client.GetAsync($"/api/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal(rawText, detail.RawText);

        var listResponse = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var summaries = await listResponse.Content.ReadFromJsonAsync<List<DocumentSummaryResponse>>();
        Assert.NotNull(summaries);
        var summary = Assert.Single(summaries, x => x.Id == created.Id);

        // Assert: the summary contract has no RawText member by construction; verify by reflection.
        Assert.Null(typeof(DocumentSummaryResponse).GetProperty("RawText"));
        Assert.Equal("Shape probe", summary.Name);
        Assert.True(summary.HasRawText);
        Assert.Equal(rawText.Length, summary.RawTextLength);
        // And the raw JSON of the list response must not contain the body.
        var listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawText, listJson, StringComparison.Ordinal);
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
        Assert.True(roleResult.Succeeded, string.Join(" ", roleResult.Errors.Select(e => e.Description)));

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
