using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

internal static class TutorAgentTelemetry
{
    public const string SourceName = "Bitcraft.TutorAgent";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Executions = Meter.CreateCounter<long>("bitcraft.tutor.executions");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("bitcraft.tutor.generation.duration", "ms");
    private static readonly Counter<long> InputTokens = Meter.CreateCounter<long>("bitcraft.tutor.input.tokens");
    private static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>("bitcraft.tutor.output.tokens");

    public static Activity? Start(TutorAgentRequest request, bool streaming)
    {
        var activity = ActivitySource.StartActivity("tutor.generate", ActivityKind.Client);
        activity?.SetTag("bitcraft.tutor.id", request.TutorId)
            .SetTag("bitcraft.revision.id", request.TutorRevisionId)
            .SetTag("bitcraft.snapshot.id", request.TutorConfigurationSnapshotId)
            .SetTag("bitcraft.conversation.id", request.ConversationId)
            .SetTag("bitcraft.provider.type", request.Model.ProviderType.ToString())
            .SetTag("gen_ai.request.model", request.Model.ProviderModelName)
            .SetTag("bitcraft.streaming", streaming);
        return activity;
    }

    public static void Success(Activity? activity, TutorAgentResponse response)
    {
        var tags = Tags(response, true, null);
        Executions.Add(1, tags);
        Latency.Record(response.Usage.GenerationLatencyMs, tags);
        if (response.Usage.InputTokenCount is { } input) InputTokens.Add(input, tags);
        if (response.Usage.OutputTokenCount is { } output) OutputTokens.Add(output, tags);
        activity?.SetTag("bitcraft.generation.success", true)
            .SetTag("bitcraft.rag.used", response.Knowledge?.KnowledgeUsed == true)
            .SetTag("bitcraft.rag.result.count", response.Knowledge?.Sources.Count ?? 0)
            .SetTag("bitcraft.tool.call.count", response.ToolActivities?.Count ?? 0)
            .SetTag("gen_ai.usage.input_tokens", response.Usage.InputTokenCount)
            .SetTag("gen_ai.usage.output_tokens", response.Usage.OutputTokenCount)
            .SetStatus(ActivityStatusCode.Ok);
    }

    public static void Failure(Activity? activity, TutorAgentRequest request, Error error, double latencyMs)
    {
        var tags = new TagList
        {
            { "bitcraft.provider.type", request.Model.ProviderType.ToString() },
            { "gen_ai.request.model", request.Model.ProviderModelName },
            { "bitcraft.generation.success", false },
            { "bitcraft.error.code", error.Code }
        };
        Executions.Add(1, tags);
        Latency.Record(latencyMs, tags);
        activity?.SetTag("bitcraft.generation.success", false)
            .SetTag("bitcraft.error.code", error.Code)
            .SetStatus(ActivityStatusCode.Error);
    }

    private static TagList Tags(TutorAgentResponse response, bool success, string? errorCode) => new()
    {
        { "bitcraft.provider.type", response.ProviderType.ToString() },
        { "gen_ai.request.model", response.ProviderModelName },
        { "bitcraft.generation.success", success },
        { "bitcraft.error.code", errorCode }
    };
}
