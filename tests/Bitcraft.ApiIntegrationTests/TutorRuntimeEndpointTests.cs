using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Contracts.Chat;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed class TutorRuntimeEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task Ask_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/runtime/tutors/not-a-real-tutor/ask",
            new AskTutorRuntimeRequest("Hello"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ask_ActiveTutor_ReturnsRevisionModelAndBoundedSourceMetadata()
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var configured = await SeedActiveTutorAsync(userId, includeKnowledge: true);
        ClearProviderRequests();

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            question = "What is inverse kinematics?",
            topK = 4,
            model = "attempted-override",
            history = new[]
            {
                new { role = "user", content = "We are discussing robot arms." },
                new { role = "assistant", content = "Understood." }
            }
        }));
        var response = await client.PostAsJsonAsync(
            $"/api/runtime/tutors/{configured.Slug}/ask",
            body.RootElement);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>();
        Assert.NotNull(result);
        Assert.Equal(configured.TutorId, result.Tutor.Id);
        Assert.Equal(configured.RevisionId, result.Revision.Id);
        Assert.Equal(configured.SnapshotId, result.SnapshotId);
        Assert.Equal("runtime-chat-model", result.Model.ProviderModelName);
        Assert.True(result.KnowledgeUsed);
        var source = Assert.Single(result.Sources);
        Assert.Equal(configured.DocumentId, source.DocumentId);
        Assert.Equal("robotics.pdf", source.DocumentName);
        Assert.Equal(1, source.ChunkIndex);
        Assert.InRange(source.Score, 0.99d, 1d);

        var generation = factory.Services.GetRequiredService<TestLlmGenerationProvider>();
        var generatedRequest = Assert.Single(generation.Requests);
        Assert.Equal("runtime-chat-model", generatedRequest.Model);
        Assert.Equal(0.25d, generatedRequest.Temperature);
        Assert.Equal(0.9d, generatedRequest.TopP);
        Assert.Equal(512, generatedRequest.MaxTokens);
        Assert.Contains("We are discussing robot arms.", generatedRequest.Prompt, StringComparison.Ordinal);

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("promptContent", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelSettings", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("effectiveDocumentIds", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("embedding\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bounded source content", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Ask_InvalidTopK_ReturnsBadRequest(int topK)
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var configured = await SeedActiveTutorAsync(userId);

        var response = await client.PostAsJsonAsync(
            $"/api/runtime/tutors/{configured.Slug}/ask",
            new AskTutorRuntimeRequest("Hello", topK));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_ExcessiveHistory_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var configured = await SeedActiveTutorAsync(userId);
        var history = Enumerable.Range(0, 21)
            .Select(index => new TutorConversationMessageRequest(
                index % 2 == 0 ? "user" : "assistant",
                $"Message {index}"))
            .ToList();

        var response = await client.PostAsJsonAsync(
            $"/api/runtime/tutors/{configured.Slug}/ask",
            new AskTutorRuntimeRequest("Continue", History: history));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_NullHistoryEntry_ReturnsBadRequestInsteadOfServerError()
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var configured = await SeedActiveTutorAsync(userId);
        using var payload = JsonDocument.Parse("{\"question\":\"Continue\",\"history\":[null]}");

        var response = await client.PostAsJsonAsync(
            $"/api/runtime/tutors/{configured.Slug}/ask",
            payload.RootElement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_UserWithoutTutorMembership_ReturnsOpaqueNotFound()
    {
        var ownerClient = factory.CreateClient();
        var ownerId = await AuthorizeAsRoleAsync(ownerClient, AppRoles.Editor);
        var configured = await SeedActiveTutorAsync(ownerId);
        var otherClient = factory.CreateClient();
        await AuthorizeAsRoleAsync(otherClient, AppRoles.Auditor);

        var response = await otherClient.PostAsJsonAsync(
            $"/api/runtime/tutors/{configured.Slug}/ask",
            new AskTutorRuntimeRequest("Hello"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LegacyAsk_TutorWithoutActiveRevision_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutorId = await SeedIncompleteTutorAsync(userId);

        var response = await client.PostAsJsonAsync(
            "/api/tutor/ask",
            new AskTutorQuestionRequest("Hello", TutorId: tutorId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LegacyAsk_ModelOverrideIsRejected()
    {
        var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var configured = await SeedActiveTutorAsync(userId);

        var response = await client.PostAsJsonAsync(
            "/api/tutor/ask",
            new AskTutorQuestionRequest("Hello", Model: "different-model", TutorId: configured.TutorId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void ClearProviderRequests()
    {
        var generation = factory.Services.GetRequiredService<TestLlmGenerationProvider>();
        while (generation.Requests.TryDequeue(out _)) { }
        var embeddings = factory.Services.GetRequiredService<TestEmbeddingProvider>();
        while (embeddings.Requests.TryDequeue(out _)) { }
        var knowledge = factory.Services.GetRequiredService<TestKnowledgeRetriever>();
        while (knowledge.Requests.TryDequeue(out _)) { }
    }

    private async Task<ConfiguredTutor> SeedActiveTutorAsync(Guid ownerId, bool includeKnowledge = false, bool supportsImages = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var engine = new RuntimeEngine
        {
            Name = $"Runtime {suffix}",
            ProviderType = RuntimeEngineProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            IsEnabled = true
        };
        var interactionModel = new InteractionModel
        {
            Name = $"Chat model {suffix}",
            Capabilities = supportsImages ? ModelCapabilities.ImageInput : ModelCapabilities.None,
            ProviderModelName = "runtime-chat-model",
            RuntimeEngineId = engine.Id,
            RuntimeEngine = engine,
            IsEnabled = true
        };
        var embeddingModel = new EmbeddingModel
        {
            Name = $"Embedding model {suffix}",
            ProviderModelName = "runtime-embed-model",
            RuntimeEngineId = engine.Id,
            RuntimeEngine = engine,
            EmbeddingDimensions = 3,
            IsEnabled = true
        };
        var tutor = Tutor.Create($"Robotics Tutor {suffix}", "Robotics help", ownerId);
        tutor.Status = TutorStatus.Active;
        var prompt = new PromptVersion
        {
            TutorId = tutor.Id,
            Tutor = tutor,
            VersionNumber = 1,
            Name = "Robotics instructions",
            Content = "Teach robotics clearly and use concise examples."
        };
        var slug = $"robotics-{suffix}";
        var revision = new TutorRevision
        {
            TutorId = tutor.Id,
            Tutor = tutor,
            VersionNumber = 3,
            Status = RevisionStatus.Active,
            PromptVersionId = prompt.Id,
            PromptVersion = prompt,
            InteractionModelId = interactionModel.Id,
            InteractionModel = interactionModel,
            QueryEmbeddingModelId = includeKnowledge ? embeddingModel.Id : null,
            QueryEmbeddingModel = includeKnowledge ? embeddingModel : null,
            InteractionParametersJson = "{\"temperature\":0.25,\"topP\":0.9,\"maxTokens\":512}",
            ActivationSlug = slug,
            ActivatedById = ownerId,
            ActivatedUtc = DateTime.UtcNow
        };

        Document? document = null;
        IReadOnlyList<Guid> documentIds = [];
        IReadOnlyList<EffectiveKnowledgeBinding> bindings = [];
        if (includeKnowledge)
        {
            document = new Document { Name = "robotics.pdf", MimeType = "application/pdf" };
            document.UpdateMetadata(ownerId);
            documentIds = [document.Id];
            bindings =
            [
                new EffectiveKnowledgeBinding(
                    document.Id,
                    embeddingModel.Id,
                    embeddingModel.EmbeddingDimensions,
                    embeddingModel.ProviderModelName,
                    engine.Id,
                    engine.Name)
            ];
        }

        var snapshot = TutorConfigurationSnapshot.Capture(
            tutor,
            revision.VersionNumber,
            prompt,
            null,
            toolsJson: "[]",
            knowledgeSourcesJson: "[]",
            deploymentProfile: null,
            capturedById: ownerId,
            tutorRevision: revision,
            effectiveDocumentIdsJson: JsonSerializer.Serialize(documentIds),
            effectiveKnowledgeBindingJson: JsonSerializer.Serialize(bindings),
            revisionDeploymentProfileJson: revision.DeploymentConfigJson);

        tutor.ActivePromptVersionId = prompt.Id;
        tutor.ActivePromptVersion = prompt;
        tutor.ActiveRevisionId = revision.Id;
        tutor.ActiveRevision = revision;
        tutor.PromptVersions.Add(prompt);
        tutor.Revisions.Add(revision);
        tutor.Snapshots.Add(snapshot);
        revision.Snapshots.Add(snapshot);

        db.RuntimeEngines.Add(engine);
        db.InteractionModels.Add(interactionModel);
        if (includeKnowledge)
        {
            db.EmbeddingModels.Add(embeddingModel);
            db.Documents.Add(document!);
        }
        db.Tutors.Add(tutor);
        foreach (var entry in db.ChangeTracker.Entries<BaseModel>())
        {
            entry.Entity.UpdateMetadata(ownerId);
        }
        await db.SaveChangesAsync();

        return new ConfiguredTutor(tutor.Id, revision.Id, snapshot.Id, slug, document?.Id);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task UploadPreviewAskFollowUpAndDeleteImage(string mimeType)
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true, supportsImages: true);
        var image = await UploadImageAsync(client, mimeType);
        Assert.Equal("image/png", image.MimeType);
        Assert.True(image.ExpiresUtc > DateTime.UtcNow);
        using var preview = await client.GetAsync($"/api/runtime/attachments/{image.Id}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        Assert.True(preview.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(preview.Headers.GetValues("X-Content-Type-Options")));
        using var decoded = Image.Load(await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(image.Width, decoded.Width);
        var descriptors = await client.GetFromJsonAsync<List<TutorRuntimeDescriptorResponse>>("/api/runtime/tutors");
        Assert.True(Assert.Single(descriptors!, value => value.TutorId == tutor.TutorId).Model.SupportsImageInput);
        ClearProviderRequests();
        var request = new AskTutorRuntimeRequest("Why does this circuit fail?", AttachmentIds: [image.Id]);
        using var answer = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", request);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Single(Assert.Single(factory.Services.GetRequiredService<TestLlmGenerationProvider>().Requests).Images!);
        Assert.Single(factory.Services.GetRequiredService<TestEmbeddingProvider>().Requests);
        ClearProviderRequests();
        var history = new[] { new TutorConversationMessageRequest("user", request.Question, [image.Id]), new TutorConversationMessageRequest("assistant", "Check Q3.") };
        using var followUp = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", new AskTutorRuntimeRequest("What if Q3 fails?", History: history));
        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
        var sent = Assert.Single(factory.Services.GetRequiredService<TestLlmGenerationProvider>().Requests);
        Assert.Single(sent.Images!);
        Assert.Contains("attachments: [Image 1]", sent.Prompt);
        Assert.DoesNotContain(image.Id.ToString(), sent.Prompt);
        using var deleted = await client.DeleteAsync($"/api/runtime/attachments/{image.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
    }

    [Fact]
    public async Task ImageOnlySkipsRagAndTextSnapshotRejectsImages()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        var vision = await SeedActiveTutorAsync(owner, includeKnowledge: true, supportsImages: true);
        var text = await SeedActiveTutorAsync(owner);
        var image = await UploadImageAsync(client, "image/png");
        ClearProviderRequests();
        using var answer = await client.PostAsJsonAsync($"/api/runtime/tutors/{vision.Slug}/ask", new { attachmentIds = new[] { image.Id } });
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<TestEmbeddingProvider>().Requests);
        Assert.Contains("Please analyze the attached image(s).", Assert.Single(factory.Services.GetRequiredService<TestLlmGenerationProvider>().Requests).Prompt);
        using var rejected = await client.PostAsJsonAsync($"/api/runtime/tutors/{text.Slug}/ask", new AskTutorRuntimeRequest("Explain", AttachmentIds: [image.Id]));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("TutorRuntime.ImageInputNotSupported", (await rejected.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task AttachmentsRequireAuthenticationOwnershipAndUnexpiredReferences()
    {
        using var ownerClient = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(ownerClient, AppRoles.Auditor);
        var image = await UploadImageAsync(ownerClient, "image/png");
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
        using var multipart = await ImageFormAsync("image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/runtime/attachments/images", multipart)).StatusCode);
        using var other = factory.CreateClient();
        var otherId = await AuthorizeAsRoleAsync(other, AppRoles.Auditor);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
        var tutor = await SeedActiveTutorAsync(otherId, supportsImages: true);
        using var forged = await other.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", new AskTutorRuntimeRequest("Explain", AttachmentIds: [image.Id]));
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var attachment = await db.ChatAttachments.FindAsync(image.Id);
            db.Entry(attachment!).Property(value => value.ExpiresUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Gone, (await ownerClient.GetAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
        var ownerTutor = await SeedActiveTutorAsync(owner, supportsImages: true);
        using var expired = await ownerClient.PostAsJsonAsync($"/api/runtime/tutors/{ownerTutor.Slug}/ask", new AskTutorRuntimeRequest("Explain", AttachmentIds: [image.Id]));
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Equal("ChatAttachment.Expired", (await expired.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.DeleteAsync($"/api/runtime/attachments/{image.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("image/svg+xml", 10, HttpStatusCode.UnsupportedMediaType)]
    [InlineData("image/gif", 10, HttpStatusCode.UnsupportedMediaType)]
    [InlineData("image/png", 10, HttpStatusCode.BadRequest)]
    [InlineData("image/png", 0, HttpStatusCode.BadRequest)]
    [InlineData("image/png", 5242881, HttpStatusCode.RequestEntityTooLarge)]
    public async Task InvalidUploadHasExpectedSafeStatus(string mimeType, int byteCount, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(new byte[byteCount]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(bytes, "file", "fake.png");
        using var response = await client.PostAsync("/api/runtime/attachments/images", form);
        Assert.Equal(expected, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.StartsWith("ChatAttachment.", error!.Code);
        Assert.DoesNotContain("/tmp/", error.Description);
    }

    [Fact]
    public async Task AdminModelApiRoundTripsCapabilitiesAndLeavesEmbeddingShapeUnchanged()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Admin);
        Guid engineId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var engine = new RuntimeEngine { Name = $"Vision engine {Guid.NewGuid():N}", Endpoint = "http://runtime.test", IsEnabled = true };
            engine.UpdateMetadata(owner);
            db.RuntimeEngines.Add(engine);
            await db.SaveChangesAsync();
            engineId = engine.Id;
        }
        var name = $"Custom vision {Guid.NewGuid():N}";
        using var created = await client.PostAsJsonAsync("/api/admin/interaction-models", new CreateInteractionModelRequest(name, engineId, "arbitrary-model", null, true, true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var model = await created.Content.ReadFromJsonAsync<InteractionModelResponse>();
        Assert.True(model!.SupportsImageInput);
        var read = await client.GetFromJsonAsync<InteractionModelResponse>($"/api/admin/interaction-models/{model.Id}");
        Assert.True(read!.SupportsImageInput);
        using var update = await client.PutAsJsonAsync($"/api/admin/interaction-models/{model.Id}", new UpdateInteractionModelRequest(name, engineId, "arbitrary-model", null, true, false));
        Assert.False((await update.Content.ReadFromJsonAsync<InteractionModelResponse>())!.SupportsImageInput);
        var image = await UploadImageAsync(client, "image/png");
        ClearProviderRequests();
        using var diagnostic = await client.PostAsJsonAsync($"/api/admin/interaction-models/{model.Id}/test-image", new TestInteractionModelImageRequest(image.Id));
        Assert.Equal(HttpStatusCode.OK, diagnostic.StatusCode);
        var sent = Assert.Single(factory.Services.GetRequiredService<TestLlmGenerationProvider>().Requests);
        Assert.Equal("arbitrary-model", sent.Model);
        Assert.Equal("http://runtime.test", sent.RuntimeEndpoint);
        Assert.Single(sent.Images!);
        Assert.False((await client.GetFromJsonAsync<InteractionModelResponse>($"/api/admin/interaction-models/{model.Id}"))!.SupportsImageInput);
        using var embedding = await client.PostAsJsonAsync("/api/admin/embedding-models", new CreateEmbeddingModelRequest($"Embedding {Guid.NewGuid():N}", engineId, "embed", 3, null, true));
        Assert.Equal(HttpStatusCode.Created, embedding.StatusCode);
        Assert.DoesNotContain("supportsImageInput", await embedding.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DiagnosticIsAdminOnlyAndMultiFileUploadIsRejected()
    {
        using var client = factory.CreateClient();
        await AuthorizeAsRoleAsync(client, AppRoles.Auditor);
        using var diagnostic = await client.PostAsJsonAsync($"/api/admin/interaction-models/{Guid.NewGuid()}/test-image", new TestInteractionModelImageRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, diagnostic.StatusCode);
        using var form = await ImageFormAsync("image/png");
        form.Add(new ByteArrayContent(new byte[] { 1 }), "file", "second.png");
        using var upload = await client.PostAsync("/api/runtime/attachments/images", form);
        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal("ChatAttachment.InvalidUpload", (await upload.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    private static async Task<ChatAttachmentResponse> UploadImageAsync(HttpClient client, string mimeType)
    {
        using var form = await ImageFormAsync(mimeType);
        using var response = await client.PostAsync("/api/runtime/attachments/images", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", json);
        Assert.DoesNotContain("/tmp/", json);
        return (await response.Content.ReadFromJsonAsync<ChatAttachmentResponse>())!;
    }

    private static async Task<MultipartFormDataContent> ImageFormAsync(string mimeType)
    {
        using var image = new Image<Rgba32>(16, 8);
        using var output = new MemoryStream();
        IImageEncoder encoder = mimeType switch { "image/jpeg" => new JpegEncoder(), "image/webp" => new WebpEncoder(), _ => new PngEncoder() };
        await image.SaveAsync(output, encoder);
        var content = new ByteArrayContent(output.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        var form = new MultipartFormDataContent();
        form.Add(content, "file", "circuit.png");
        return form;
    }

    private async Task<Guid> SeedIncompleteTutorAsync(Guid ownerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tutor = Tutor.Create($"Incomplete Tutor {Guid.NewGuid():N}", null, ownerId);
        tutor.UpdateMetadata(ownerId);
        db.Tutors.Add(tutor);
        await db.SaveChangesAsync();
        return tutor.Id;
    }

    private async Task<Guid> AuthorizeAsRoleAsync(HttpClient client, string role)
    {
        var email = $"runtime-{Guid.NewGuid():N}@bitcraft.test";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            userId = user.Id;
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return userId;
    }

    private sealed record ConfiguredTutor(
        Guid TutorId,
        Guid RevisionId,
        Guid SnapshotId,
        string Slug,
        Guid? DocumentId);
}
