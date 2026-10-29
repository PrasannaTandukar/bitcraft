using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Microsoft.Extensions.AI;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Agents;

public sealed class TutorAgentExecutorTests
{
    [Fact]
    public async Task StreamingUsesAgentFrameworkDeltasWithoutExposingReasoning()
    {
        var factory = new RecordingFactory("stream");
        var events = new List<TutorAgentStreamEvent>();

        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory)
            .ExecuteStreamingAsync(Request(), (value, _) =>
            {
                events.Add(value);
                return ValueTask.CompletedTask;
            });

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("Hello", result.Value.Answer);
        Assert.Equal(new[] { "Hel", "lo" }, events.Where(value =>
            value.Kind == TutorAgentStreamEventKind.MessageDelta).Select(value => value.Delta));
        Assert.DoesNotContain("private reasoning", string.Join("", events.Select(value => value.Delta)));
    }

    [Fact]
    public async Task StreamingCancellationReachesChatClientAndDisposesIt()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new RecordingFactory("stream-wait");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory).ExecuteStreamingAsync(
                Request(), (value, _) =>
                {
                    if (value.Delta == "started") cancellation.Cancel();
                    return ValueTask.CompletedTask;
                }, cancellation.Token));

        Assert.True(Assert.Single(factory.Clients).Disposed);
    }

    [Fact]
    public async Task StreamingReportsKnowledgeAndToolLifecycle()
    {
        var knowledge = new KnowledgeTestSetup("untrusted reference");
        var factory = new ToolCallingFactory();
        var registry = new TutorToolRegistry([new CalculatorTutorToolHandler()], TutorToolRuntimeOptions.Default);
        var events = new List<TutorAgentStreamEvent>();
        var request = TutorToolRegistryTests.Request() with
        {
            Tools = [TutorToolRegistryTests.Tool()],
            Knowledge = knowledge.Configuration
        };

        var result = await new TutorAgentExecutor(factory, knowledge.Factory, toolRegistry: registry)
            .ExecuteStreamingAsync(request, (value, _) =>
            {
                events.Add(value);
                return ValueTask.CompletedTask;
            });

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Single(events, value => value.Kind == TutorAgentStreamEventKind.SourceRetrieved);
        Assert.Single(events, value => value.Kind == TutorAgentStreamEventKind.ToolStarted);
        Assert.Single(events, value => value.Kind == TutorAgentStreamEventKind.ToolCompleted);
        Assert.Equal(TutorToolCallStatus.Running,
            events.Single(value => value.Kind == TutorAgentStreamEventKind.ToolStarted).ToolActivity!.Status);
        Assert.Equal(TutorToolCallStatus.Succeeded,
            events.Single(value => value.Kind == TutorAgentStreamEventKind.ToolCompleted).ToolActivity!.Status);
        var provenance = Bitcraft.Application.Features.Tutoring.TutorMessageProvenanceCapture.Capture(request, result.Value);
        Assert.Equal(Bitcraft.Domain.Tutoring.TutorToolExecutionStatus.Succeeded,
            Assert.Single(provenance.ToolEvidence).Status);
        Assert.Single(provenance.KnowledgeEvidence);
    }
    [Fact]
    public async Task RegisteredFunctionExecutesThroughMeaiLoopAndReturnsActivity()
    {
        var factory = new ToolCallingFactory();
        var registry = new TutorToolRegistry([new CalculatorTutorToolHandler()], TutorToolRuntimeOptions.Default);
        var request = TutorToolRegistryTests.Request() with { Tools = [TutorToolRegistryTests.Tool()] };

        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory,
            toolRegistry: registry).ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("The answer is 5.", result.Value.Answer);
        Assert.Equal(2, factory.Client.RequestCount);
        Assert.NotNull(factory.Client.FirstOptions);
        Assert.Single(factory.Client.FirstOptions!.Tools!);
        Assert.Contains(factory.Client.SecondMessages!, message =>
            message.Contents.OfType<FunctionResultContent>().Any());
        Assert.Equal(TutorToolCallStatus.Succeeded, Assert.Single(result.Value.ToolActivities!).Status);
    }

    [Fact]
    public async Task ToolsOnModelWithoutFunctionCallingFailBeforeProviderInvocation()
    {
        var factory = new RecordingFactory();
        var registry = new TutorToolRegistry([new CalculatorTutorToolHandler()], TutorToolRuntimeOptions.Default);
        var request = Request();
        request = request with { Model = request.Model with { Capabilities = ModelCapabilities.None },
            Tools = [TutorToolRegistryTests.Tool(request.TutorId)] };

        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory,
            toolRegistry: registry).ExecuteAsync(request);

        Assert.Equal("TutorAgent.FunctionCallingNotSupported", result.Error.Code);
        Assert.Empty(Assert.Single(factory.Clients).Messages);
    }

    [Fact]
    public async Task ApprovalRequiredToolStopsBeforeInvocationAndReturnsSafeError()
    {
        var factory = new ToolCallingFactory();
        var registry = new TutorToolRegistry([new CalculatorTutorToolHandler()], TutorToolRuntimeOptions.Default);
        var request = TutorToolRegistryTests.Request() with
        {
            Tools = [TutorToolRegistryTests.Tool() with
            {
                ExecutionPolicy = Bitcraft.Domain.Tutoring.TutorToolExecutionPolicy.RequiresApproval
            }]
        };

        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory,
            toolRegistry: registry).ExecuteAsync(request);

        Assert.Equal("TutorAgent.ToolApprovalRequired", result.Error.Code);
        Assert.Equal(1, factory.Client.RequestCount);
    }

    [Fact]
    public async Task HistoryKeepsCanonicalContentAndToolPayloadsWithoutTransientResponseState()
    {
        var result = await new TutorAgentExecutor(new RecordingFactory("metadata"), new KnowledgeTestSetup().Factory)
            .ExecuteAsync(Request() with { PersistSession = true });
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.DoesNotContain("private-transient", result.Value.AgentSessionJson);
        var provider = (Microsoft.Agents.AI.InMemoryChatHistoryProvider)TutorSessionHistory.Create();
        var agent = new Microsoft.Agents.AI.ChatClientAgent(new RecordingClient(null, null),
            new Microsoft.Agents.AI.ChatClientAgentOptions { ChatHistoryProvider = provider });
        var restored = await agent.DeserializeSessionAsync(System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result.Value.AgentSessionJson!));
        var answer = provider.GetMessages(restored)[1];
        Assert.Equal("Answer", answer.Text);
        Assert.Single(answer.Contents.OfType<FunctionCallContent>());
        Assert.Single(answer.Contents.OfType<FunctionResultContent>());
        Assert.Empty(answer.Contents.OfType<TextReasoningContent>());
        Assert.Null(answer.RawRepresentation);
        Assert.Null(answer.AdditionalProperties);
    }

    [Fact]
    public async Task MalformedStoredSessionIsASessionFailureBeforeAnyProviderRequest()
    {
        var factory = new RecordingFactory();
        var logger = new SafeLogger();
        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory, logger)
            .ExecuteAsync(Request() with { AgentSessionJson = "{private-session-body", PersistSession = true });
        Assert.Equal("TutorAgent.SessionRestoreFailed", result.Error.Code);
        Assert.Empty(Assert.Single(factory.Clients).Messages);
        Assert.Contains("RestoreSession", Assert.Single(logger.Messages));
        Assert.DoesNotContain("private-session-body", logger.Messages[0]);
    }

    [Theory]
    [InlineData(false, "TutorAgent.GenerationFailed")]
    [InlineData(true, "TutorAgent.InvalidResponse")]
    public async Task ProviderFailuresPreserveTheirCategoryAndLogOnlySafeMetadata(bool malformedJson, string code)
    {
        var logger = new SafeLogger();
        var factory = new RecordingFactory(onRequest: _ =>
        {
            if (malformedJson) throw new System.Text.Json.JsonException("private-provider-body");
            throw new InvalidOperationException("private-provider-body", new ArgumentException("private-inner"));
        });
        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory, logger).ExecuteAsync(Request());
        Assert.Equal(code, result.Error.Code);
        var log = Assert.Single(logger.Messages);
        Assert.Contains("ExecuteAgent", log);
        Assert.Contains(malformedJson ? "JsonException" : "InvalidOperationException", log);
        Assert.DoesNotContain("private-provider-body", log);
        Assert.DoesNotContain("private-inner", log);
        Assert.DoesNotContain("Tutor instructions", log);
        Assert.DoesNotContain("Question", log);
    }

    [Fact]
    public async Task SessionSerializationFailureIsNotAProviderResponseFailure()
    {
        var logger = new SafeLogger();
        var result = await new TutorAgentExecutor(new RecordingFactory("serialize"), new KnowledgeTestSetup().Factory, logger)
            .ExecuteAsync(Request() with { PersistSession = true });
        Assert.Equal("TutorAgent.SessionSerializationFailed", result.Error.Code);
        Assert.Contains("SerializeSession", Assert.Single(logger.Messages));
    }

    private sealed class SafeLogger : Microsoft.Extensions.Logging.ILogger<TutorAgentExecutor>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task SerializedSessionRestoresRolesAndImagesAcrossExecutorInstancesWithoutPersistingRag()
    {
        var knowledge = new KnowledgeTestSetup("ephemeral reference");
        var firstFactory = new RecordingFactory();
        var request = Request() with { PersistSession = true, Images = [Image()], Knowledge = knowledge.Configuration };
        var first = await new TutorAgentExecutor(firstFactory, knowledge.Factory).ExecuteAsync(request);
        Assert.True(first.IsSuccess, first.Error.Description);
        Assert.NotNull(first.Value.AgentSessionJson);
        Assert.DoesNotContain("ephemeral reference", first.Value.AgentSessionJson);
        var secondFactory = new RecordingFactory();
        var second = await new TutorAgentExecutor(secondFactory, new KnowledgeTestSetup().Factory).ExecuteAsync(request with
        { UserText = "Follow up", Images = null, Knowledge = null, AgentSessionJson = first.Value.AgentSessionJson });
        Assert.True(second.IsSuccess, second.Error.Description);
        var messages = Assert.Single(secondFactory.Clients).Messages;
        Assert.Equal(new[] { ChatRole.User, ChatRole.Assistant, ChatRole.User }, messages.Select(message => message.Role));
        Assert.Single(messages[0].Contents.OfType<DataContent>());
        Assert.Equal("Follow up", messages[2].Text);
    }

    [Fact]
    public async Task SessionBoundsModelContextWithoutDeletingDurableHistory()
    {
        var factory = new RecordingFactory();
        var executor = new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory);
        var request = Request() with { PersistSession = true };
        for (var turn = 0; turn < 15; turn++)
        {
            var result = await executor.ExecuteAsync(request with { UserText = $"Turn {turn}" });
            Assert.True(result.IsSuccess, result.Error.Description);
            request = request with { AgentSessionJson = result.Value.AgentSessionJson };
        }
        Assert.True(factory.Clients[^1].Messages.Count <= 21);
        Assert.DoesNotContain(factory.Clients[^1].Messages, message => message.Text == "Turn 0");
        Assert.Contains("Turn 0", request.AgentSessionJson);
    }

    [Fact]
    public async Task ExecutePreservesRolesImagesOptionsAndUsageWithoutRetainingHistory()
    {
        var factory = new RecordingFactory();
        var knowledge = new KnowledgeTestSetup("Untrusted reference");
        ITutorAgentExecutor executor = new TutorAgentExecutor(factory, knowledge.Factory);
        var request = Request() with
        {
            Conversation = [new(TutorAgentMessageRole.User, "Earlier", [Image()]), new(TutorAgentMessageRole.Assistant, "Reply")],
            Images = [Image()], Knowledge = knowledge.Configuration, Temperature = 0.25, TopP = 0.9, MaxOutputTokens = 123
        };
        var result = await executor.ExecuteAsync(request);
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("Answer", result.Value.Answer);
        Assert.Equal("reported-model", result.Value.ProviderModelName);
        Assert.Equal(10, result.Value.Usage.InputTokenCount);
        Assert.Equal(4, result.Value.Usage.OutputTokenCount);
        Assert.Equal(14, result.Value.Usage.TotalTokenCount);
        Assert.True(result.Value.Usage.GenerationLatencyMs >= 0);
        Assert.False(result.Value.Diagnostics.UsesCompatibilityTransport);
        var client = Assert.Single(factory.Clients);
        Assert.Equal(request.Model, factory.Model);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.User], client.Messages.Select(m => m.Role));
        Assert.Equal("Earlier", client.Messages[0].Text);
        Assert.Equal(Image().Data.ToArray(), Assert.Single(client.Messages[0].Contents.OfType<DataContent>()).Data.ToArray());
        Assert.Contains("Untrusted reference", client.Messages[3].Text);
        Assert.Equal("Question", client.Messages[2].Text);
        Assert.Empty(client.Messages[3].Contents.OfType<DataContent>());
        Assert.Single(client.Messages[2].Contents.OfType<DataContent>());
        Assert.EndsWith("Tutor instructions", client.Options?.Instructions);
        Assert.Contains("configured tutor instructions below are authoritative", client.Options?.Instructions);
        Assert.Contains("untrusted data", client.Options?.Instructions);
        Assert.DoesNotContain("Untrusted reference", client.Options?.Instructions);
        Assert.Equal(0.25f, client.Options?.Temperature);
        Assert.Equal(0.9f, client.Options?.TopP);
        Assert.Equal(123, client.Options?.MaxOutputTokens);
        Assert.Equal("configured-model", client.Options?.ModelId);
        Assert.True(client.Disposed);

        await executor.ExecuteAsync(Request());
        Assert.Single(factory.Clients[1].Messages);
    }

    [Fact]
    public async Task RejectsHistoryImagesWithoutCapabilityBeforeCreatingClient()
    {
        var factory = new RecordingFactory();
        var request = Request() with { Model = Request().Model with { Capabilities = ModelCapabilities.None },
            Conversation = [new(TutorAgentMessageRole.User, "Earlier", [Image()])] };
        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory).ExecuteAsync(request);
        Assert.Equal("TutorAgent.ImageInputNotSupported", result.Error.Code);
        Assert.Empty(factory.Clients);
    }

    [Theory]
    [InlineData("empty", "TutorAgent.InvalidResponse")]
    [InlineData("http", "TutorAgent.Unavailable")]
    [InlineData("timeout", "TutorAgent.Timeout")]
    public async Task FailuresAreSafeAndDisposeClient(string failure, string code)
    {
        var factory = new RecordingFactory(failure);
        var result = await new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory).ExecuteAsync(Request());
        Assert.Equal(code, result.Error.Code);
        Assert.DoesNotContain("upstream-private-data", result.Error.Description);
        Assert.True(Assert.Single(factory.Clients).Disposed);
    }

    [Fact]
    public async Task CallerCancellationPropagatesAndDisposesClient()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new RecordingFactory(onRequest: token => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TutorAgentExecutor(factory, new KnowledgeTestSetup().Factory).ExecuteAsync(Request(), cancellation.Token));
        Assert.True(Assert.Single(factory.Clients).Disposed);
    }

    [Fact]
    public async Task MissingUsageRemainsUnknown()
    {
        var result = await new TutorAgentExecutor(new RecordingFactory("no-usage"), new KnowledgeTestSetup().Factory).ExecuteAsync(Request());
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Usage.InputTokenCount);
        Assert.Null(result.Value.Usage.OutputTokenCount);
        Assert.Null(result.Value.Usage.TotalTokenCount);
    }

    internal static TutorAgentRequest Request(RuntimeEngineProviderType provider = RuntimeEngineProviderType.Ollama) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tutor instructions",
            new(Guid.NewGuid(), provider, "https://runtime.test/proxy", null, Guid.NewGuid(), "configured-model", ModelCapabilities.ImageInput), "Question");

    internal static LlmImageInput Image() => new("image/png", new byte[] { 1, 2, 3 });

    internal sealed class RecordingFactory(string? failure = null, Action<CancellationToken>? onRequest = null) : IRuntimeChatClientFactory
    {
        public List<RecordingClient> Clients { get; } = [];
        public TutorAgentModelConfiguration? Model { get; private set; }
        public Result<IChatClient> Create(TutorAgentModelConfiguration model)
        {
            Model = model;
            var client = new RecordingClient(failure, onRequest);
            Clients.Add(client);
            return Result.Success<IChatClient>(client);
        }
    }

    internal sealed class RecordingClient(string? failure, Action<CancellationToken>? onRequest) : IChatClient
    {
        public List<ChatMessage> Messages { get; private set; } = [];
        public ChatOptions? Options { get; private set; }
        public bool Disposed { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Messages = messages.ToList();
            Options = options;
            onRequest?.Invoke(cancellationToken);
            if (failure == "http") throw new HttpRequestException("upstream-private-data");
            if (failure == "timeout") throw new OperationCanceledException("upstream-private-data");
            if (failure == "metadata")
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new TextContent("Answer"), new TextReasoningContent("private-transient thinking"),
                     new FunctionCallContent("call", "futureTool"), new FunctionResultContent("call", "tool result")])
                { RawRepresentation = "private-transient raw", AdditionalProperties = new() { ["private-transient"] = true } }));
            if (failure == "serialize")
            {
                var cycle = new Dictionary<string, object>();
                cycle["cycle"] = cycle;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new TextContent("Answer"), new FunctionResultContent("call", cycle)])));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, failure == "empty" ? " " : " Answer "))
            {
                ModelId = "reported-model",
                Usage = failure == "no-usage" ? null : new UsageDetails { InputTokenCount = 10, OutputTokenCount = 4, TotalTokenCount = 14 }
            });
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Messages = messages.ToList();
            Options = options;
            cancellationToken.ThrowIfCancellationRequested();
            if (failure == "stream")
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new TextContent("Hel"), new TextReasoningContent("private reasoning")]);
                await Task.Yield();
                yield return new ChatResponseUpdate(ChatRole.Assistant, "lo");
                yield break;
            }
            if (failure == "stream-wait")
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "started");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                yield break;
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Answer");
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() => Disposed = true;
    }

    private sealed class ToolCallingFactory : IRuntimeChatClientFactory
    {
        public ToolCallingClient Client { get; } = new();
        public Result<IChatClient> Create(TutorAgentModelConfiguration model) => Result.Success<IChatClient>(Client);
    }

    private sealed class ToolCallingClient : IChatClient
    {
        public int RequestCount { get; private set; }
        public ChatOptions? FirstOptions { get; private set; }
        public List<ChatMessage>? SecondMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                FirstOptions = options;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("call-1", "calculator",
                        new Dictionary<string, object?> { ["expression"] = "2 + 3" })])));
            }
            SecondMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "The answer is 5.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            if (RequestCount == 1)
            {
                FirstOptions = options;
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("call-1", "calculator",
                        new Dictionary<string, object?> { ["expression"] = "2 + 3" })]);
                yield break;
            }
            SecondMessages = messages.ToList();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "The answer is 5.");
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
