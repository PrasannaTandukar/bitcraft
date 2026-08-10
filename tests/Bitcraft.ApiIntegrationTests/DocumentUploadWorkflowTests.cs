using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public class DocumentUploadWorkflowTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task EditorMultipartUpload_ReturnsExtractedDetailAndSummaryMetadata()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        using var form = CreateFileForm("course.txt", "Line one\r\nLine two");
        var response = await client.PostAsync("/api/documents/upload", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Line one\nLine two", detail.RawText);
        Assert.Null(detail.SourcePath);

        var summaries = await client.GetFromJsonAsync<List<DocumentSummaryResponse>>("/api/documents");
        var summary = Assert.Single(summaries!, item => item.Id == detail.Id);
        Assert.True(summary!.HasRawText);
        Assert.Equal(detail.RawText!.Length, summary.RawTextLength);
        Assert.Null(typeof(DocumentSummaryResponse).GetProperty("RawText"));
    }

    [Fact]
    public async Task Upload_RequiresAuthenticationAndEditorRole()
    {
        var anonymous = factory.CreateClient();
        using var anonymousForm = CreateFileForm("notes.txt", "content");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/documents/upload", anonymousForm)).StatusCode);

        var auditor = factory.CreateClient();
        await AuthorizeAsRoleAsync(auditor, AppRoles.Auditor);
        using var auditorForm = CreateFileForm("notes.txt", "content");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsync("/api/documents/upload", auditorForm)).StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedTypeReturns415_AndOversizedReturns413()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        using var unsupported = CreateFileForm("photo.png", "not really an image", "image/png");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            (await client.PostAsync("/api/documents/upload", unsupported)).StatusCode);

        using var oversized = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[(10 * 1024 * 1024) + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        oversized.Add(content, "file", "large.txt");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsync("/api/documents/upload", oversized)).StatusCode);
    }

    [Fact]
    public async Task OwnerCanReplaceContentAndStatusReportsNotVectorized()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        using var original = CreateFileForm("original.txt", "old text");
        var createdResponse = await client.PostAsync("/api/documents/upload", original);
        var created = await createdResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(created);

        using var replacement = CreateFileForm("replacement.md", "# New text", "text/markdown");
        var replaceResponse = await client.PutAsync($"/api/documents/{created.Id}/content", replacement);
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var replaced = await replaceResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(replaced);
        Assert.Equal("replacement.md", replaced.Name);
        Assert.Equal("# New text", replaced.RawText);
        Assert.Equal(created.FolderId, replaced.FolderId);

        var status = await client.GetFromJsonAsync<DocumentVectorizationStatusResponse>(
            $"/api/documents/{created.Id}/vectorization-status");
        Assert.NotNull(status);
        Assert.True(status.HasRawText);
        Assert.False(status.IsVectorized);
        Assert.Equal(0, status.ChunkCount);
    }

    [Fact]
    public async Task ReplaceContent_RejectsNonOwner()
    {
        var ownerClient = factory.CreateClient();
        await AuthorizeAsRoleAsync(ownerClient, AppRoles.Editor);
        using var upload = CreateFileForm("owned.txt", "owner content");
        var createdResponse = await ownerClient.PostAsync("/api/documents/upload", upload);
        var created = await createdResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(created);

        var otherClient = factory.CreateClient();
        await AuthorizeAsRoleAsync(otherClient, AppRoles.Editor);
        using var replacement = CreateFileForm("replacement.txt", "other content");
        var response = await otherClient.PutAsync($"/api/documents/{created.Id}/content", replacement);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TutorlessEmbeddingCatalog_ReturnsEnabledModelsToEditor()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var runtime = new RuntimeEngine
        {
            Name = $"Upload runtime {Guid.NewGuid():N}",
            Endpoint = "http://runtime.invalid",
            ProviderType = RuntimeEngineProviderType.Ollama,
            IsEnabled = true
        };
        var model = new EmbeddingModel
        {
            Name = $"Upload embedding {Guid.NewGuid():N}",
            ProviderModelName = "nomic-embed-text",
            RuntimeEngine = runtime,
            RuntimeEngineId = runtime.Id,
            IsEnabled = true,
            EmbeddingDimensions = 768
        };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var creatorId = await db.Users.Select(user => user.Id).FirstAsync();
            runtime.UpdateMetadata(creatorId);
            model.UpdateMetadata(creatorId);
            db.RuntimeEngines.Add(runtime);
            db.EmbeddingModels.Add(model);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/model-catalog/embedding-models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalog = await response.Content.ReadFromJsonAsync<List<TutorEmbeddingModelCatalogResponse>>();
        var item = Assert.Single(catalog!, value => value.Id == model.Id);
        Assert.Equal("nomic-embed-text", item.ProviderModelName);
        Assert.Equal(768, item.EmbeddingDimensions);
        Assert.True(item.IsRuntimeEngineEnabled);
    }

    [Fact]
    public async Task VectorizationStatus_ReturnsChunkAndModelMetadataWithoutRawContent()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        using var upload = CreateFileForm("status.txt", "source text");
        var createdResponse = await client.PostAsync("/api/documents/upload", upload);
        var created = await createdResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        Assert.NotNull(created);
        var modelId = Guid.NewGuid();
        var ingestedUtc = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<TestDocumentVectorizationReader>();
            reader.Metadata[created.Id] = new DocumentVectorizationMetadata(
                true,
                false,
                4,
                ingestedUtc,
                modelId,
                "Nomic Embed Text",
                "nomic-embed-text",
                "Ollama",
                768);
        }

        var response = await client.GetAsync($"/api/documents/{created.Id}/vectorization-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<DocumentVectorizationStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.IsVectorized);
        Assert.Equal(4, status.ChunkCount);
        Assert.Equal(modelId, status.EmbeddingModelId);
        Assert.Equal("Nomic Embed Text", status.EmbeddingModelName);
        Assert.Equal("nomic-embed-text", status.ProviderModelName);
        Assert.Equal(768, status.EmbeddingDimensions);
    }

    private static MultipartFormDataContent CreateFileForm(
        string fileName,
        string text,
        string contentType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        return form;
    }

    private async Task AuthorizeAsRoleAsync(HttpClient client, string role)
    {
        var email = $"upload-{Guid.NewGuid():N}@bitcraft.test";
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
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);

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
