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

    [Fact]
    public async Task UploadBatch_RequiresAuthenticationAndEditorRole()
    {
        var anonymous = factory.CreateClient();
        using var anonymousForm = CreateBatchForm("notes.txt", "content");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/documents/upload-batch", anonymousForm)).StatusCode);

        var auditor = factory.CreateClient();
        await AuthorizeAsRoleAsync(auditor, AppRoles.Auditor);
        using var auditorForm = CreateBatchForm("notes.txt", "content");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsync("/api/documents/upload-batch", auditorForm)).StatusCode);
    }

    [Fact]
    public async Task UploadBatch_ReturnsPerFileResults()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        using var form = new MultipartFormDataContent();
        form.Add(CreateBatchFileContent("one.txt", "first body"), "files", "one.txt");
        form.Add(CreateBatchFileContent("two.md", "# second body", "text/markdown"), "files", "two.md");
        form.Add(CreateBatchFileContent("bad.png", "not text", "image/png"), "files", "bad.png");

        var response = await client.PostAsync("/api/documents/upload-batch", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<UploadDocumentBatchResponse>();
        Assert.NotNull(batch);
        Assert.Equal(3, batch.Results.Count);
        Assert.Equal(2, batch.Results.Count(x => x.DocumentId is not null));
        var failed = Assert.Single(batch.Results, x => x.DocumentId is null);
        Assert.Equal("bad.png", failed.FileName);
        Assert.Equal("Document.UnsupportedFileType", failed.ErrorCode);

        var summaries = await client.GetFromJsonAsync<List<DocumentSummaryResponse>>("/api/documents");
        Assert.NotNull(summaries);
        foreach (var entry in batch.Results.Where(x => x.DocumentId is not null))
        {
            Assert.Contains(summaries, summary => summary.Id == entry.DocumentId);
        }
    }

    [Fact]
    public async Task UploadBatch_RejectsEmptyBatch()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        using var form = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/documents/upload-batch", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VectorizeBatch_RequiresAuthenticationAndEditorRole()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/documents/vectorize-batch",
                new VectorizeDocumentBatchRequest())).StatusCode);

        var auditor = factory.CreateClient();
        await AuthorizeAsRoleAsync(auditor, AppRoles.Auditor);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync("/api/documents/vectorize-batch",
                new VectorizeDocumentBatchRequest())).StatusCode);
    }

    [Fact]
    public async Task VectorizeBatch_VectorizesAllDocumentsInFolderAndScopesToRequestedFolder()
    {
        var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Editor);

        var folderResponse = await client.PostAsJsonAsync("/api/document-folders", new CreateDocumentFolderRequest("Batch vectorization", null));
        Assert.Equal(HttpStatusCode.Created, folderResponse.StatusCode);
        var folder = await folderResponse.Content.ReadFromJsonAsync<DocumentFolderResponse>();
        Assert.NotNull(folder);

        using (var uploadForm = new MultipartFormDataContent())
        {
            uploadForm.Add(CreateBatchFileContent("one.txt", "first body"), "files", "one.txt");
            uploadForm.Add(CreateBatchFileContent("two.md", "# second body", "text/markdown"), "files", "two.md");
            uploadForm.Add(new StringContent(folder.Id.ToString()), "folderId");
            var uploadResponse = await client.PostAsync("/api/documents/upload-batch", uploadForm);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
            var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadDocumentBatchResponse>();
            Assert.NotNull(uploaded);
            Assert.Equal(2, uploaded.Results.Count(x => x.DocumentId is not null));
        }

        // A second editor uploads a document into their own root; it must not be vectorized by the first user's folder batch.
        var otherClient = factory.CreateClient();
        await AuthorizeAsRoleAsync(otherClient, AppRoles.Editor);
        using (var otherForm = CreateBatchForm("other.txt", "other body"))
        {
            var otherResponse = await otherClient.PostAsync("/api/documents/upload-batch", otherForm);
            Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        }

        var response = await client.PostAsJsonAsync("/api/documents/vectorize-batch", new VectorizeDocumentBatchRequest(
            FolderId: folder.Id,
            EmbeddingProvider: "Ollama",
            EmbeddingModel: "nomic-embed-text"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<VectorizeDocumentBatchResponse>();
        Assert.NotNull(batch);
        // Exactly the two documents in the requested folder; the other user's root document is excluded.
        Assert.Equal(2, batch.Results.Count);
        foreach (var item in batch.Results)
        {
            Assert.NotNull(item.ChunkCount);
            Assert.True(item.ChunkCount > 0);
            Assert.Null(item.ErrorCode);
            Assert.Null(item.ErrorDescription);
        }

        var vectorStore = factory.Services.GetRequiredService<TestVectorStore>();
        foreach (var item in batch.Results)
        {
            Assert.True(vectorStore.DocumentChunks.ContainsKey(item.DocumentId));
            Assert.Equal(item.ChunkCount, vectorStore.DocumentChunks[item.DocumentId].Count);
        }
    }

    private static ByteArrayContent CreateBatchFileContent(string fileName, string text, string contentType = "text/plain")
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    private static MultipartFormDataContent CreateBatchForm(
        string fileName,
        string text,
        string contentType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        form.Add(CreateBatchFileContent(fileName, text, contentType), "files", fileName);
        return form;
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
