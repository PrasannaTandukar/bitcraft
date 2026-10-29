using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

/// <summary>Controls only provider responses; the real runtime, Agent Framework, context providers and tools execute.</summary>
internal sealed class ProvenanceTestChatClient(string scenario) : IChatClient
{
    public const string ReportedModel = "provenance-reported-model";
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
    public TaskCompletionSource PartialResponseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(messages.ToList());
        if (scenario == "provenance-generation-failure") throw new HttpRequestException("private-provider-body");
        return Task.FromResult(new ChatResponse(Response(options)) { ModelId = ReportedModel });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(messages.ToList());
        if (scenario == "provenance-stream-cancel")
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial answer") { ModelId = ReportedModel };
            PartialResponseStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { if (cancellationToken.IsCancellationRequested) Cancelled.TrySetResult(); }
            yield break;
        }
        if (scenario == "provenance-generation-failure")
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial answer") { ModelId = ReportedModel };
            throw new HttpRequestException("private-provider-body");
        }
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, Response(options).Contents) { ModelId = ReportedModel };
    }

    private ChatMessage Response(ChatOptions? options)
    {
        if (Requests.Count == 1 && scenario is "provenance-tool-success" or "provenance-tool-failure" or "provenance-review-calculator")
        {
            var tool = Assert.Single(options!.Tools!.OfType<AIFunction>());
            return new(ChatRole.Assistant, [new FunctionCallContent("actual-call", tool.Name,
                new Dictionary<string, object?> { ["expression"] = scenario switch {
                    "provenance-tool-success" => "2 + 3", "provenance-review-calculator" => "12 * 5 / 18", _ => "1 / 0" } })]);
        }
        if (scenario == "provenance-review-calculator")
            return new(ChatRole.Assistant, "The ideal runtime is 3.33 hours, or about 3 hours 20 minutes.");
        if (scenario == "provenance-review-rag")
            return new(ChatRole.Assistant, "Equal PWM does not guarantee equal wheel speed. Motor tolerances, friction, load and wheel diameter can differ. Use encoder feedback to correct the speed difference.");
        // Mentioning a tool in text is not an invocation.
        return new(ChatRole.Assistant, "Completed answer; Calculator is a configured tool.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
