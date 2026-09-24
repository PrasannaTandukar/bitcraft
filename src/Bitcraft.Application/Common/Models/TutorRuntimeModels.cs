namespace Bitcraft.Application.Common.Models;

public enum TutorRuntimeExecutionMode
{
    Interactive = 0,
    Benchmark = 1,
}

public sealed record TutorRuntimeRequest(
    Guid TutorId,
    Guid? TutorRevisionId,
    Guid? TutorConfigurationSnapshotId,
    string UserQuestion,
    Guid UserId,
    bool UseKnowledge = true,
    int TopK = 5,
    TutorRuntimeExecutionMode ExecutionMode = TutorRuntimeExecutionMode.Interactive,
    IReadOnlyList<Guid>? AttachmentIds = null, Guid? ConversationId = null,
    bool PersistConversation = false, bool UseMemory = false, bool SaveToMemory = false);

public sealed record TutorRuntimeModelMetadata(
    Guid Id,
    string Name,
    string ProviderModelName,
    Guid RuntimeEngineId,
    string RuntimeEngineName,
    bool SupportsImageInput = false,
    bool SupportsFunctionCalling = false,
    bool SupportsStructuredOutput = false);

public sealed record TutorRuntimeEmbeddingModelMetadata(
    Guid Id,
    string Name,
    string ProviderModelName,
    int Dimensions,
    Guid RuntimeEngineId,
    string RuntimeEngineName);

public sealed record TutorRuntimeSource(
    Guid DocumentId,
    string? DocumentName,
    Guid ChunkId,
    int ChunkIndex,
    double Score,
    string Content);

public sealed record TutorRuntimeUsageMetadata(double GenerationLatencyMs,
    long? InputTokenCount = null, long? OutputTokenCount = null, long? TotalTokenCount = null);

public sealed record TutorRuntimeResult(
    Guid TutorId,
    string TutorName,
    Guid TutorRevisionId,
    int RevisionNumber,
    Guid TutorConfigurationSnapshotId,
    string Answer,
    TutorRuntimeModelMetadata Model,
    TutorRuntimeEmbeddingModelMetadata? EmbeddingModel,
    bool KnowledgeUsed,
    IReadOnlyList<TutorRuntimeSource> Sources,
    TutorRuntimeUsageMetadata Usage,
    string? ReferenceMaterial, Guid? ConversationId = null,
    bool MemoryUsed = false, bool MemorySaved = false,
    IReadOnlyList<TutorMemorySource>? MemorySources = null,
    IReadOnlyList<TutorToolCallActivity>? ToolActivities = null);

public enum TutorRuntimeStreamEventKind
{
    ConversationStarted,
    MessageDelta,
    ToolStarted,
    ToolCompleted,
    SourceRetrieved,
    MessageCompleted,
    Error
}

/// <summary>One ordered, reconnect-safe event from the canonical tutor runtime.</summary>
public sealed record TutorRuntimeStreamEvent(long Sequence, TutorRuntimeStreamEventKind Kind,
    Guid? ConversationId = null, string? Delta = null, TutorRuntimeSource? Source = null,
    TutorToolCallActivity? ToolActivity = null, TutorRuntimeResult? Result = null,
    Error? Error = null);

public sealed record TutorRuntimeDescriptor(
    Guid TutorId,
    string TutorName,
    string? Description,
    Guid TutorRevisionId,
    int RevisionNumber,
    string ActivationSlug,
    Guid TutorConfigurationSnapshotId,
    TutorRuntimeModelMetadata Model,
    TutorRuntimeEmbeddingModelMetadata? EmbeddingModel,
    int KnowledgeDocumentCount);

public sealed record LlmImageInput(string MimeType, ReadOnlyMemory<byte> Data);

public sealed record EmbeddingGenerationRequest(
    string Text,
    string Model,
    string? RuntimeEndpoint = null,
    string? ApiKeySecretRef = null,
    string? ProviderType = null);

public sealed record KnowledgeSearchRequest(
    IReadOnlyCollection<Guid> DocumentIds,
    Guid EmbeddingModelId,
    int EmbeddingDimensions,
    float[] QueryEmbedding,
    int TopK);

public sealed record TutorMemorySearchRequest(Guid UserId, Guid TutorId, Guid EmbeddingModelId,
    int EmbeddingDimensions, float[] QueryEmbedding, int TopK, Guid? ExcludedConversationId = null);

public sealed record TutorInteractionParameters(
    double? Temperature,
    double? TopP,
    int? MaxTokens)
{
    public const int MaximumMaxTokens = 131_072;
}
