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
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    private const string Password = "P@ssw0rd123!";

    [Fact]
    public async Task StreamingAskEmitsOrderedDeltasAndCommitsOneAssistantMessage()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true);
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var conversation = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/runtime/tutors/{tutor.Slug}/ask/stream")
        {
            Content = JsonContent.Create(new AskTutorRuntimeRequest("Explain inverse kinematics", ConversationId: conversation.Id))
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var events = ParseSse(body);
        Assert.Equal(new[] { "conversation.started", "source.retrieved", "message.delta", "message.delta", "message.completed" },
            events.Select(value => value.Type));
        Assert.Equal(events.Select(value => value.Sequence).Order(), events.Select(value => value.Sequence));
        Assert.Equal("Deterministic agent answer", string.Concat(events.Select(value => value.Delta)));
        Assert.NotNull(Assert.Single(events, value => value.Type == "source.retrieved").Source);
        var completed = Assert.Single(events, value => value.Type == "message.completed");
        Assert.Equal(conversation.Id, completed.ConversationId);
        Assert.Equal("Deterministic agent answer", completed.Message!.Answer);
        var stored = await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{conversation.Id}");
        Assert.Equal(new[] { "user", "assistant" }, stored!.Messages.Select(value => value.Role));

        using var followUp = new HttpRequestMessage(HttpMethod.Post,
            $"/api/runtime/tutors/{tutor.Slug}/ask/stream")
        {
            Content = JsonContent.Create(new AskTutorRuntimeRequest("Continue", ConversationId: conversation.Id))
        };
        using var followUpResponse = await client.SendAsync(followUp, HttpCompletionOption.ResponseHeadersRead);
        var followUpEvents = ParseSse(await followUpResponse.Content.ReadAsStringAsync());
        Assert.Equal(conversation.Id,
            Assert.Single(followUpEvents, value => value.Type == "message.completed").ConversationId);
        stored = await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{conversation.Id}");
        Assert.Equal(new[] { "user", "assistant", "user", "assistant" },
            stored!.Messages.Select(value => value.Role));
    }

    [Fact]
    public async Task StreamingClientAbortCancelsAgentAndDoesNotCommitPartialTurn()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var conversation = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!;
        var recorder = factory.Services.GetRequiredService<TestTutorAgentRecorder>();
        var serverCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.BeforeExecuteAsync = async token =>
        {
            using var registration = token.Register(() => serverCancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/runtime/tutors/{tutor.Slug}/ask/stream")
            {
                Content = JsonContent.Create(new AskTutorRuntimeRequest("cancel me", ConversationId: conversation.Id))
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            cancellation.Cancel();
            response.Dispose();
            await serverCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            recorder.BeforeExecuteAsync = null;
        }
        var stored = await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(
            $"/api/runtime/tutors/{tutor.Slug}/conversations/{conversation.Id}");
        Assert.Empty(stored!.Messages);
    }

    [Fact]
    public async Task StreamingFailureUsesStructuredSafeErrorEvent()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var agent = factory.Services.GetRequiredService<TestTutorAgentExecutor>();
        agent.Failure = new Error("TutorAgent.GenerationFailed", "private provider response");
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/runtime/tutors/{tutor.Slug}/ask/stream", new AskTutorRuntimeRequest("fail"));
            var body = await response.Content.ReadAsStringAsync();
            var events = ParseSse(body);
            Assert.Equal(new[] { "conversation.started", "error" }, events.Select(value => value.Type));
            Assert.Equal("Ollama.RequestFailed", events[^1].Error!.Code);
            Assert.DoesNotContain("private provider response", body);
        }
        finally { agent.Failure = null; }
    }

    [Fact]
    public async Task CanonicalAskSavesAndRetrievesOnlyOptInTutorMemory()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true);
        var memory = factory.Services.GetRequiredService<TestTutorMemoryStore>();
        while (memory.Memories.TryDequeue(out _)) { }
        while (memory.Requests.TryDequeue(out _)) { }
        var path = $"/api/runtime/tutors/{tutor.Slug}/ask";

        using var unsaved = await client.PostAsJsonAsync(path,
            new AskTutorRuntimeRequest("Do not remember", SaveToMemory: false));
        Assert.Equal(HttpStatusCode.OK, unsaved.StatusCode);
        Assert.Empty(memory.Memories);

        using var saved = await client.PostAsJsonAsync(path,
            new AskTutorRuntimeRequest("Remember encoders", SaveToMemory: true));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var saveResponse = (await saved.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.True(saveResponse.MemorySaved);
        Assert.False(saveResponse.MemoryUsed);
        var stored = Assert.Single(memory.Memories);
        Assert.Equal(owner, stored.UserId);
        Assert.Equal(tutor.TutorId, stored.TutorId);
        Assert.Equal(tutor.RevisionId, stored.TutorRevisionId);
        Assert.Equal(tutor.SnapshotId, stored.TutorConfigurationSnapshotId);
        Assert.NotNull(stored.ConversationId);

        using var used = await client.PostAsJsonAsync(path,
            new AskTutorRuntimeRequest("What did I save?", UseMemory: true));
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        var useResponse = (await used.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.True(useResponse.MemoryUsed);
        Assert.False(useResponse.MemorySaved);
        var search = Assert.Single(memory.Requests);
        Assert.Equal(owner, search.UserId);
        Assert.Equal(tutor.TutorId, search.TutorId);
        Assert.Equal(3, search.EmbeddingDimensions);
        Assert.Contains("Remember encoders", factory.Services.GetRequiredService<TestTutorAgentExecutor>()
            .LastResponse!.Memory!.Sources.Single().Content);
    }

    [Fact]
    public async Task FailedGenerationNeverSavesTutorMemory()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true);
        var memory = factory.Services.GetRequiredService<TestTutorMemoryStore>();
        while (memory.Memories.TryDequeue(out _)) { }
        var agent = factory.Services.GetRequiredService<TestTutorAgentExecutor>();
        agent.Failure = new Error("TutorAgent.GenerationFailed", "private");
        try
        {
            using var response = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask",
                new AskTutorRuntimeRequest("Remember this", SaveToMemory: true));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Empty(memory.Memories);
        }
        finally { agent.Failure = null; }
    }

    [Fact]
    public async Task MalformedPersistedSessionHasSafeSessionErrorAndLeavesDurableMessagesUnchanged()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var path = $"/api/runtime/tutors/{tutor.Slug}";
        using var first = await client.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest("hi"));
        var id = (await first.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!.ConversationId!.Value;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conversation = await db.Set<TutorConversation>().FindAsync(id);
            db.Entry(conversation!).Property(value => value.AgentSessionJson).CurrentValue = "{private-session-body";
            await db.SaveChangesAsync();
        }
        using var second = await client.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest("ok", ConversationId: id));
        Assert.Equal(HttpStatusCode.InternalServerError, second.StatusCode);
        Assert.Equal("TutorRuntime.SessionRestoreFailed", (await second.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.DoesNotContain("private-session-body", await second.Content.ReadAsStringAsync());
        var stored = await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(path + $"/conversations/{id}");
        Assert.Equal(2, stored!.Messages.Count);
    }

    [Fact]
    public async Task ConversationsPersistAcrossRequestScopesAndRemainPrivate()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var path = $"/api/runtime/tutors/{tutor.Slug}";
        using var created = await client.PostAsync(path + "/conversations", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!;
        Assert.Empty(conversation.Messages);
        Assert.Equal(tutor.SnapshotId, conversation.SnapshotId);
        using var first = await client.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest("Remember this", ConversationId: conversation.Id));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await client.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest("Continue", ConversationId: conversation.Id));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var stored = (await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>(path + $"/conversations/{conversation.Id}"))!;
        Assert.Equal(new long[] { 1, 2, 3, 4 }, stored.Messages.Select(message => message.Sequence));
        Assert.Equal(new[] { "user", "assistant", "user", "assistant" }, stored.Messages.Select(message => message.Role));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var durable = await db.Set<TutorConversation>().FindAsync(conversation.Id);
            Assert.Contains("Remember this", durable!.AgentSessionJson);
            Assert.Contains("Continue", durable.AgentSessionJson);
        }
        var list = await client.GetFromJsonAsync<List<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>>(path + "/conversations");
        Assert.Contains(list!, value => value.Id == conversation.Id);
        var otherTutor = await SeedActiveTutorAsync(owner);
        using var crossTutor = await client.PostAsJsonAsync($"/api/runtime/tutors/{otherTutor.Slug}/ask", new AskTutorRuntimeRequest("Probe", ConversationId: conversation.Id));
        Assert.Equal(HttpStatusCode.NotFound, crossTutor.StatusCode);
        using var other = factory.CreateClient();
        await AuthorizeAsRoleAsync(other, AppRoles.Editor);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + $"/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest("Probe", ConversationId: conversation.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + $"/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + $"/conversations/{conversation.Id}")).StatusCode);
    }

    [Fact]
    public async Task AutoCreatedConversationPinsSnapshotAcrossActivationChanges()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var path = $"/api/runtime/tutors/{tutor.Slug}/ask";
        using var first = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("First"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var response = (await first.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.NotNull(response.ConversationId);
        Guid newSnapshotId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var old = await db.TutorRevisions.FindAsync(tutor.RevisionId);
            var aggregate = await db.Tutors.FindAsync(tutor.TutorId);
            var prompt = await db.PromptVersions.FindAsync(old!.PromptVersionId);
            var model = await db.InteractionModels.FindAsync(old.InteractionModelId);
            old.Status = RevisionStatus.Archived;
            old.ActivationSlug = null;
            var next = new TutorRevision { TutorId = tutor.TutorId, VersionNumber = 4, Status = RevisionStatus.Active,
                ActivationSlug = tutor.Slug, PromptVersionId = prompt!.Id, InteractionModelId = model!.Id, InteractionModel = model };
            var snapshot = TutorConfigurationSnapshot.Capture(aggregate!, 4, prompt, null, "[]", "[]", null, owner,
                tutorRevision: next, effectiveDocumentIdsJson: "[]", effectiveKnowledgeBindingJson: "[]");
            aggregate!.ActiveRevisionId = next.Id;
            next.UpdateMetadata(owner); snapshot.UpdateMetadata(owner);
            db.Add(next); db.Add(snapshot);
            await db.SaveChangesAsync();
            newSnapshotId = snapshot.Id;
        }
        using var followup = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("Follow up", ConversationId: response.ConversationId));
        Assert.Equal(HttpStatusCode.OK, followup.StatusCode);
        var pinned = (await followup.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.Equal(tutor.SnapshotId, pinned.SnapshotId);
        Assert.Equal(tutor.RevisionId, pinned.Revision.Id);
        using var fresh = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("New conversation"));
        Assert.Equal(newSnapshotId, (await fresh.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!.SnapshotId);
    }

    [Fact]
    public async Task ConversationImageFollowupRestoresSessionAndDeletedImagesFailSafely()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, supportsImages: true);
        var image = await UploadImageAsync(client, "image/png");
        var path = $"/api/runtime/tutors/{tutor.Slug}/ask";
        using var first = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest(AttachmentIds: [image.Id]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var response = (await first.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        using var followup = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("Explain the image", ConversationId: response.ConversationId));
        Assert.Equal(HttpStatusCode.OK, followup.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var attachment = await db.ChatAttachments.FindAsync(image.Id);
            db.Entry(attachment!).Property(value => value.ExpiresUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using var expired = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("Explain again", ConversationId: response.ConversationId));
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        await client.DeleteAsync($"/api/runtime/attachments/{image.Id}");
        using var deleted = await client.PostAsJsonAsync(path, new AskTutorRuntimeRequest("Explain again", ConversationId: response.ConversationId));
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal("ChatAttachment.NotFound", (await deleted.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task ConcurrentConversationSavesRejectStaleSession()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var id = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!.Id;
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<ITutorConversationRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ITutorConversationRepository>();
        var first = (await repo1.GetAsync(id, owner, tutor.TutorId, default))!;
        var second = (await repo2.GetAsync(id, owner, tutor.TutorId, default))!;
        first.CompleteTurn("{}", "winner", null, "answer", "model", "Ollama");
        second.CompleteTurn("{}", "stale", null, "answer", "model", "Ollama");
        Assert.True(await repo1.SaveAsync(first, default));
        Assert.False(await repo2.SaveAsync(second, default));
        using var scope3 = factory.Services.CreateScope();
        var stored = await scope3.ServiceProvider.GetRequiredService<ITutorConversationRepository>().GetAsync(id, owner, tutor.TutorId, default);
        Assert.Equal(2, stored!.Messages.Count);
        Assert.DoesNotContain(stored.Messages, value => value.Content == "stale");
    }

    [Fact]
    public async Task SimultaneousAskRequestsReturnOneAnswerAndOneExplicitConflict()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var id = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!.Id;
        var recorder = factory.Services.GetRequiredService<TestTutorAgentRecorder>();
        var arrived = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.BeforeExecuteAsync = async token =>
        {
            if (Interlocked.Increment(ref arrived) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        };
        try
        {
            var responses = await Task.WhenAll(
                client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", new AskTutorRuntimeRequest("First writer", ConversationId: id)),
                client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", new AskTutorRuntimeRequest("Second writer", ConversationId: id)));
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("TutorConversation.Conflict", (await conflict.Content.ReadFromJsonAsync<ApiError>())!.Code);
            var stored = await client.GetFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>($"/api/runtime/tutors/{tutor.Slug}/conversations/{id}");
            Assert.Equal(2, stored!.Messages.Count);
            foreach (var response in responses) response.Dispose();
        }
        finally { recorder.BeforeExecuteAsync = null; }
    }

    [Fact]
    public async Task ConversationRequiresContinuingTutorMembershipAndRejectsSnapshotOverride()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        using var created = await client.PostAsync($"/api/runtime/tutors/{tutor.Slug}/conversations", null);
        var id = (await created.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse>())!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var runtime = scope.ServiceProvider.GetRequiredService<ITutorRuntime>();
            var forged = await runtime.ExecuteAsync(new Bitcraft.Application.Common.Models.TutorRuntimeRequest(
                tutor.TutorId, tutor.RevisionId, Guid.NewGuid(), "Probe", owner, ConversationId: id));
            Assert.Equal("TutorConversation.NotFound", forged.Error.Code);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TutorMembers.RemoveRange(db.TutorMembers.Where(member => member.TutorId == tutor.TutorId));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/runtime/tutors/{tutor.Slug}/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask", new AskTutorRuntimeRequest("Probe", ConversationId: id))).StatusCode);
    }

    [Theory]
    [InlineData("TutorAgent.ImageRejected", "TutorRuntime.ImageRejected", HttpStatusCode.BadGateway)]
    [InlineData("TutorAgent.Timeout", "TutorRuntime.Timeout", HttpStatusCode.GatewayTimeout)]
    [InlineData("TutorAgent.ModelNotFound", "Ollama.ModelNotFound", HttpStatusCode.BadGateway)]
    [InlineData("TutorAgent.SecretNotFound", "TutorRuntime.ModelConfigurationInvalid", HttpStatusCode.Conflict)]
    [InlineData("TutorAgent.RateLimited", "TutorRuntime.RateLimited", HttpStatusCode.TooManyRequests)]
    [InlineData("TutorAgent.ContextTooLarge", "TutorRuntime.ContextTooLarge", HttpStatusCode.RequestEntityTooLarge)]
    public async Task AgentFailuresPreserveSafeGatewayResponses(string agentCode, string expectedCode,
        HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();
        var userId = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var configured = await SeedActiveTutorAsync(userId);
        var agent = factory.Services.GetRequiredService<TestTutorAgentExecutor>();
        agent.Failure = new Bitcraft.Application.Common.Models.Error(agentCode, "private-provider-details");
        try
        {
            using var response = await client.PostAsJsonAsync($"/api/runtime/tutors/{configured.Slug}/ask", new AskTutorRuntimeRequest("Question"));
            Assert.Equal(expectedStatus, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal(expectedCode, error!.Code);
            Assert.DoesNotContain("private-provider-details", await response.Content.ReadAsStringAsync());
        }
        finally { agent.Failure = null; }
    }

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

        var generatedRequest = Assert.Single(factory.Services.GetRequiredService<TestTutorAgentExecutor>().Requests);
        Assert.Equal("runtime-chat-model", generatedRequest.Model.ProviderModelName);
        Assert.Equal(configured.TutorId, generatedRequest.TutorId);
        Assert.Equal(configured.RevisionId, generatedRequest.TutorRevisionId);
        Assert.Equal(configured.SnapshotId, generatedRequest.TutorConfigurationSnapshotId);
        Assert.Equal(0.25d, generatedRequest.Temperature);
        Assert.Equal(0.9d, generatedRequest.TopP);
        Assert.Equal(512, generatedRequest.MaxOutputTokens);
        Assert.Null(generatedRequest.Conversation);
        Assert.Contains("Bounded source content", factory.Services.GetRequiredService<TestTutorAgentExecutor>().LastResponse!.Knowledge!.ReferenceMaterial);
        Assert.DoesNotContain("Bounded source content", generatedRequest.Instructions);

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

    private void ClearProviderRequests()
    {
        var diagnostics = factory.Services.GetRequiredService<TestRuntimeModelDiagnostic>();
        while (diagnostics.Requests.TryDequeue(out _)) { }
        var agents = factory.Services.GetRequiredService<TestTutorAgentExecutor>();
        while (agents.Requests.TryDequeue(out _)) { }
        var embeddings = factory.Services.GetRequiredService<TestEmbeddingProvider>();
        while (embeddings.Requests.TryDequeue(out _)) { }
        var knowledge = factory.Services.GetRequiredService<TestKnowledgeRetriever>();
        while (knowledge.Requests.TryDequeue(out _)) { }
    }

    private async Task<ConfiguredTutor> SeedActiveTutorAsync(Guid ownerId, bool includeKnowledge = false, bool supportsImages = false, string? endpoint = null, string? providerModel = null, int maxTokens = 512)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var engine = new RuntimeEngine
        {
            Name = $"Runtime {suffix}",
            ProviderType = RuntimeEngineProviderType.Ollama,
            Endpoint = endpoint ?? "http://localhost:11434",
            IsEnabled = true
        };
        var interactionModel = new InteractionModel
        {
            Name = $"Chat model {suffix}",
            Capabilities = supportsImages ? ModelCapabilities.ImageInput : ModelCapabilities.None,
            ProviderModelName = providerModel ?? "runtime-chat-model",
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
            InteractionParametersJson = JsonSerializer.Serialize(new { temperature = 0.25, topP = 0.9, maxTokens }),
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
        if (db.Database.IsRelational())
        {
            // Insert the aggregate before its optional active pointers to avoid FK cycles.
            tutor.ActiveRevisionId = null; tutor.ActiveRevision = null;
            tutor.ActivePromptVersionId = null; tutor.ActivePromptVersion = null;
            await db.SaveChangesAsync();
            tutor.ActiveRevisionId = revision.Id; tutor.ActiveRevision = revision;
            tutor.ActivePromptVersionId = prompt.Id; tutor.ActivePromptVersion = prompt;
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
        var firstResponse = (await answer.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.Single(Assert.Single(factory.Services.GetRequiredService<TestTutorAgentExecutor>().Requests).Images!);
        Assert.Single(factory.Services.GetRequiredService<TestEmbeddingProvider>().Requests);
        ClearProviderRequests();
        using var followUp = await client.PostAsJsonAsync($"/api/runtime/tutors/{tutor.Slug}/ask",
            new AskTutorRuntimeRequest("What if Q3 fails?", ConversationId: firstResponse.ConversationId));
        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
        var sent = Assert.Single(factory.Services.GetRequiredService<TestTutorAgentExecutor>().Requests);
        Assert.Null(sent.Images);
        Assert.NotNull(sent.AgentSessionJson);
        Assert.Null(sent.Conversation);
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
        Assert.Contains("Please analyze the attached image(s).", Assert.Single(factory.Services.GetRequiredService<TestTutorAgentExecutor>().Requests).UserText);
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
        var sent = Assert.Single(factory.Services.GetRequiredService<TestRuntimeModelDiagnostic>().Requests);
        Assert.Equal("arbitrary-model", sent.ProviderModelName);
        Assert.Equal("http://runtime.test", sent.Endpoint);
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

    private static async Task<Bitcraft.Api.Contracts.Chat.ChatAttachmentResponse> UploadImageAsync(HttpClient client, string mimeType)
    {
        using var form = await ImageFormAsync(mimeType);
        using var response = await client.PostAsync("/api/runtime/attachments/images", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", json);
        Assert.DoesNotContain("/tmp/", json);
        return (await response.Content.ReadFromJsonAsync<Bitcraft.Api.Contracts.Chat.ChatAttachmentResponse>())!;
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

    private static List<TutorRuntimeStreamEventResponse> ParseSse(string body) => body
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
        .Select(block => block.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..])
        .Select(json => JsonSerializer.Deserialize<TutorRuntimeStreamEventResponse>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
        .ToList();

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
