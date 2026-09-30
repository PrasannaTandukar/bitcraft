using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed class LocalSessionPostgresFactAttribute : FactAttribute
{
    public LocalSessionPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES") is null)
            Skip = "Set BITCRAFT_SESSION_TEST_POSTGRES to a disposable PostgreSQL database; optional BITCRAFT_SESSION_TEST_OLLAMA_MODEL enables real Ollama.";
    }
}

public sealed partial class TutorRuntimeEndpointTests
{
    [LocalSessionPostgresFact]
    public async Task PromptSevenToolSchema_UpgradesWithoutLosingLegacyExecutionUrl()
    {
        var connection = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES")!;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, provider => provider.UseVector())
            .Options;
        await using var db = new AppDbContext(options);

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync("20260913081811_ScopeTutorLongTermMemory");

        var userId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string legacyUrl = "https://legacy-tools.example.test/calculator";

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AspNetUsers" (
                "Id", "UserName", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ({userId}, 'migration-test-user', FALSE, FALSE, FALSE, FALSE, 0);
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Tutors" ("Id", "Name", "Status", "CreatedUtc", "CreatedById")
            VALUES ({tutorId}, 'Migration test tutor', 0, {now}, {userId});
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ToolDefinitions" (
                "Id", "TutorId", "Name", "JsonSchema", "ExecutionUrl", "CreatedUtc", "CreatedById")
            VALUES ({toolId}, {tutorId}, 'legacy-calculator', jsonb_build_object(), {legacyUrl}, {now}, {userId});
            """);

        await db.Database.MigrateAsync();

        var tool = await db.ToolDefinitions.AsNoTracking().SingleAsync(value => value.Id == toolId);
        Assert.Equal(TutorToolKind.BuiltIn, tool.Kind);
        Assert.Equal(TutorToolExecutionPolicy.Disabled, tool.ExecutionPolicy);
        Assert.Empty(tool.ImplementationKey);

        var preservedUrl = await db.Database.SqlQueryRaw<string>(
                "SELECT \"LegacyExecutionUrl\" AS \"Value\" FROM \"ToolDefinitions\" WHERE \"Id\" = {0}", toolId)
            .SingleAsync();
        Assert.Equal(legacyUrl, preservedUrl);

        var oldColumnExists = await db.Database.SqlQueryRaw<string>("""
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_name = 'ToolDefinitions' AND column_name = 'ExecutionUrl'
            """).AnyAsync();
        Assert.False(oldColumnExists);

        var applied = await db.Database.SqlQueryRaw<string>(
                "SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"")
            .ToListAsync();
        Assert.Contains("20260913090240_AddSafeTutorAgentTools", applied);
        Assert.Contains("20260913183052_ConfigureSafeTutorAgentTools", applied);
    }

    [LocalSessionPostgresFact]
    public async Task PostgreSqlTutorMemoryPersistsBeforeAutoCreatedConversationAndRemainsTutorScoped()
    {
        var connection = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES")!;
        using var localFactory = new BitcraftApiFactory
        {
            SessionTestPostgres = connection,
            UseRealTutorMemoryStore = true
        };
        var harness = new TutorRuntimeEndpointTests(localFactory);
        using var client = localFactory.CreateClient();
        var owner = await harness.AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await harness.SeedActiveTutorAsync(owner, includeKnowledge: true);
        var path = $"/api/runtime/tutors/{tutor.Slug}/ask";

        using var saved = await client.PostAsJsonAsync(path,
            new AskTutorRuntimeRequest("Remember scoped encoders", SaveToMemory: true));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var saveResult = (await saved.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
        Assert.True(saveResult.MemorySaved);
        Assert.NotNull(saveResult.ConversationId);

        using (var scope = localFactory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TutorMemory>()
                .AsNoTracking().SingleAsync(memory => memory.ConversationId == saveResult.ConversationId);
            Assert.Equal(owner, stored.UserId);
            Assert.Equal(tutor.TutorId, stored.TutorId);
            Assert.Equal(tutor.RevisionId, stored.TutorRevisionId);
            Assert.Equal(tutor.SnapshotId, stored.TutorConfigurationSnapshotId);
            Assert.Equal(3, stored.EmbeddingDimensions);
        }

        using var used = await client.PostAsJsonAsync(path,
            new AskTutorRuntimeRequest("What did I save?", UseMemory: true));
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.True((await used.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!.MemoryUsed);

        var otherTutor = await harness.SeedActiveTutorAsync(owner, includeKnowledge: true);
        using var isolated = await client.PostAsJsonAsync($"/api/runtime/tutors/{otherTutor.Slug}/ask",
            new AskTutorRuntimeRequest("What did I save?", UseMemory: true));
        Assert.Equal(HttpStatusCode.OK, isolated.StatusCode);
        Assert.False((await isolated.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!.MemoryUsed);
    }

    [LocalSessionPostgresFact]
    public async Task PostgresAndOptionalOllamaResumeSameConversationThroughThreeRequests()
    {
        var connection = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_POSTGRES")!;
        var model = Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_OLLAMA_MODEL");
        using var localFactory = new BitcraftApiFactory { SessionTestPostgres = connection, UseRealOllama = model is not null };
        var harness = new TutorRuntimeEndpointTests(localFactory);
        using var client = localFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(4);
        using (var scope = localFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"TutorConversations\" ALTER COLUMN \"AgentSessionJson\" TYPE text USING \"AgentSessionJson\"::text");
        var owner = await harness.AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await harness.SeedActiveTutorAsync(owner,
            endpoint: Environment.GetEnvironmentVariable("BITCRAFT_SESSION_TEST_OLLAMA_ENDPOINT") ?? "http://127.0.0.1:11434",
            providerModel: model, maxTokens: 4096);
        var path = $"/api/runtime/tutors/{tutor.Slug}";
        using var created = await client.PostAsync(path + "/conversations", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<TutorConversationResponse>())!.Id;
        // Reproduce the old schema's actual jsonb normalization, on this isolated database only.
        using (var scope = localFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"TutorConversations\" ALTER COLUMN \"AgentSessionJson\" TYPE jsonb USING \"AgentSessionJson\"::jsonb");
        localFactory.UseLegacySessionJsonb = true;
        var questions = new[] { "hi", "ok", "what did I just say?" };
        for (var turn = 0; turn < questions.Length; turn++)
        {
            using var answer = await client.PostAsJsonAsync(path + "/ask", new AskTutorRuntimeRequest(questions[turn], ConversationId: id));
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            var result = (await answer.Content.ReadFromJsonAsync<TutorRuntimeAskResponse>())!;
            Assert.Equal(id, result.ConversationId);
            Assert.Equal(tutor.SnapshotId, result.SnapshotId);
            Assert.False(string.IsNullOrWhiteSpace(result.Answer));
            string persisted;
            using (var scope = localFactory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stored = await db.Set<TutorConversation>().Include(value => value.Messages).SingleAsync(value => value.Id == id);
                persisted = stored.AgentSessionJson!;
                Assert.Equal((turn + 1) * 2, stored.Messages.Count);
                Assert.Equal(questions.Take(turn + 1), stored.Messages.Where(value => value.Role == "user").OrderBy(value => value.Sequence).Select(value => value.Content));
            }
            // Explicitly inspect the lazy provider state after a real DB reload, before another model call.
            var history = (InMemoryChatHistoryProvider)TutorSessionHistory.Create();
            using var chatClient = new ProbeClient();
            var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions { ChatHistoryProvider = history });
            var session = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(persisted));
            var messages = history.GetMessages(session);
            Assert.Equal((turn + 1) * 2, messages.Count);
            Assert.Equal(questions.Take(turn + 1), messages.Where(value => value.Role == ChatRole.User).Select(value => value.Text));
            Assert.Equal(result.Answer, messages[^1].Text.Trim());
            if (turn == 0)
            {
                var originalReader = new InMemoryChatHistoryProvider(new() { StateKey = "bitcraft.tutor.history.v1" });
                var originalAgent = new ChatClientAgent(chatClient, new ChatClientAgentOptions { ChatHistoryProvider = originalReader });
                var originalSession = await originalAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(persisted));
                var failure = Assert.Throws<JsonException>(() => originalReader.GetMessages(originalSession));
                Assert.Contains("$type", failure.Path);
                // Apply the migration's column conversion, preserving the already-normalized old session.
                using var scope = localFactory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"TutorConversations\" ALTER COLUMN \"AgentSessionJson\" TYPE text USING \"AgentSessionJson\"::text");
                localFactory.UseLegacySessionJsonb = false;
            }
            else
            {
                // New sessions must survive byte-for-byte, not merely as equivalent JSON.
                Assert.Equal(localFactory.Services.GetRequiredService<TestTutorAgentExecutor>().LastResponse!.AgentSessionJson, persisted);
            }
        }
        // Exercise the same repository conflict/rollback assertion against PostgreSQL too.
        await harness.ConcurrentConversationSavesRejectStaleSession();
    }

    private sealed class ProbeClient : IChatClient
    {
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
