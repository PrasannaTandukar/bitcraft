using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Models;

/// <summary>An immutable copy of the connectivity and model settings selected by the caller.</summary>
public sealed record TutorAgentModelConfiguration(
    Guid RuntimeEngineId,
    RuntimeEngineProviderType ProviderType,
    string Endpoint,
    string? ApiKeySecretRef,
    Guid ModelId,
    string ProviderModelName,
    ModelCapabilities Capabilities);

public enum TutorAgentMessageRole
{
    User,
    Assistant
}

public sealed record TutorAgentConversationMessage(
    TutorAgentMessageRole Role,
    string Content,
    IReadOnlyList<LlmImageInput>? Images = null);

public sealed record TutorAgentRequest(
    Guid TutorId,
    Guid TutorRevisionId,
    Guid TutorConfigurationSnapshotId,
    string Instructions,
    TutorAgentModelConfiguration Model,
    string UserText,
    IReadOnlyList<TutorAgentConversationMessage>? Conversation = null,
    IReadOnlyList<LlmImageInput>? Images = null,
    TutorKnowledgeConfiguration? Knowledge = null,
    TutorMemoryConfiguration? Memory = null,
    IReadOnlyList<TutorAgentToolConfiguration>? Tools = null,
    double? Temperature = null,
    double? TopP = null,
    int? MaxOutputTokens = null,
    string? AgentSessionJson = null, bool PersistSession = false, Guid? ConversationId = null);

/// <summary>Token counts are null when the provider does not report them; latency is measured locally.</summary>
public sealed record TutorAgentUsage(
    double GenerationLatencyMs,
    long? InputTokenCount,
    long? OutputTokenCount,
    long? TotalTokenCount);

public sealed record TutorAgentDiagnostics(bool UsesCompatibilityTransport);

public sealed record TutorAgentResponse(
    string Answer,
    RuntimeEngineProviderType ProviderType,
    string ProviderModelName,
    TutorAgentUsage Usage,
    TutorAgentDiagnostics Diagnostics,
    TutorKnowledgeResult? Knowledge = null,
    TutorMemoryResult? Memory = null,
    IReadOnlyList<TutorToolCallActivity>? ToolActivities = null,
    string? AgentSessionJson = null);

public sealed record TutorAgentToolConfiguration(Guid ToolDefinitionId, Guid TutorId, string Name,
    string? Description, string JsonSchema, TutorToolKind Kind, string ImplementationKey,
    TutorToolExecutionPolicy ExecutionPolicy);

public enum TutorToolCallStatus
{
    Running,
    Succeeded,
    Failed,
    TimedOut
}

public sealed record TutorToolCallActivity(string ToolName, DateTime StartedUtc, DateTime CompletedUtc,
    TutorToolCallStatus Status, double DurationMs, string? ResultSummary, string? ErrorCategory);

public enum TutorAgentStreamEventKind
{
    MessageDelta,
    SourceRetrieved,
    ToolStarted,
    ToolCompleted
}

/// <summary>Framework-neutral progress emitted while one agent turn is running.</summary>
public sealed record TutorAgentStreamEvent(TutorAgentStreamEventKind Kind, string? Delta = null,
    TutorRuntimeSource? Source = null, TutorToolCallActivity? ToolActivity = null);

/// <summary>Only the authorized snapshot's effective documents and embedding bindings may be used for retrieval.</summary>
public sealed record TutorKnowledgeConfiguration(
    bool UseKnowledge,
    string? UserQuery,
    Guid? QueryEmbeddingModelId,
    string? EffectiveDocumentIdsJson,
    string? EffectiveKnowledgeBindingJson,
    int TopK = 5);

public sealed record TutorKnowledgeResult(
    TutorRuntimeEmbeddingModelMetadata? EmbeddingModel,
    IReadOnlyList<TutorRuntimeSource> Sources,
    string? ReferenceMaterial)
{
    /// <summary>Only excerpts actually supplied within the context budget; never asserted model citations.</summary>
    public IReadOnlyList<TutorRuntimeSource> ContextSources { get; init; } = [];
    // Legacy runtime flag means context was supplied, not that the model relied on it.
    public bool KnowledgeUsed => ContextSources.Count > 0;
    public static TutorKnowledgeResult Empty { get; } = new(null, Array.Empty<TutorRuntimeSource>(), null);
}

public sealed record TutorMemoryConfiguration(bool UseMemory, bool SaveToMemory, Guid UserId,
    Guid TutorId, Guid TutorRevisionId, Guid TutorConfigurationSnapshotId, Guid? ConversationId,
    string? UserQuery, Guid? QueryEmbeddingModelId, int TopK = 5);

public sealed record TutorMemorySource(Guid MemoryId, Guid TutorId, Guid? ConversationId,
    Guid? TutorRevisionId, Guid? TutorConfigurationSnapshotId, double Score, string Content);

public sealed record TutorMemoryResult(bool MemorySaved,
    TutorRuntimeEmbeddingModelMetadata? EmbeddingModel, IReadOnlyList<TutorMemorySource> Sources)
{
    /// <summary>Retrieved memories with content actually supplied to the agent, excluding budgeted-out entries.</summary>
    public int ContextMemoryCount { get; init; }
    public bool MemoryUsed => ContextMemoryCount > 0;
    public static TutorMemoryResult Empty { get; } = new(false, null, []);
}
