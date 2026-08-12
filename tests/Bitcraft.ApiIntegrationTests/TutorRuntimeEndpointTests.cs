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

    private async Task<ConfiguredTutor> SeedActiveTutorAsync(Guid ownerId, bool includeKnowledge = false)
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
