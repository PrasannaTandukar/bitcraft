namespace Bitcraft.Application.Common.Models;

public enum TutorRuntimeExecutionMode
{
    Interactive = 0,
    Benchmark = 1,
}

public sealed record TutorRuntimeConversationMessage(string Role, string Content, IReadOnlyList<Guid>? AttachmentIds = null);

public sealed record TutorRuntimeRequest(
    Guid TutorId,
    Guid? TutorRevisionId,
    Guid? TutorConfigurationSnapshotId,
    string UserQuestion,
    Guid UserId,
    bool UseKnowledge = true,
    int TopK = 5,
    TutorRuntimeExecutionMode ExecutionMode = TutorRuntimeExecutionMode.Interactive,
    IReadOnlyList<TutorRuntimeConversationMessage>? History = null,
    IReadOnlyList<Guid>? AttachmentIds = null);

public sealed record TutorRuntimeModelMetadata(
    Guid Id,
    string Name,
    string ProviderModelName,
    Guid RuntimeEngineId,
    string RuntimeEngineName,
    bool SupportsImageInput = false);

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

public sealed record TutorRuntimeUsageMetadata(double GenerationLatencyMs);

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
    string? ReferenceMaterial);

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

public sealed record LlmGenerationRequest(
    string Prompt,
    string? Model,
    double? Temperature = null,
    double? TopP = null,
    int? MaxTokens = null,
    string? RuntimeEndpoint = null,
    string? ApiKey = null,
    string? ProviderType = null,
    IReadOnlyList<LlmImageInput>? Images = null);

public sealed record EmbeddingGenerationRequest(
    string Text,
    string Model,
    string? RuntimeEndpoint = null,
    string? ApiKey = null,
    string? ProviderType = null);

public sealed record KnowledgeSearchRequest(
    IReadOnlyCollection<Guid> DocumentIds,
    Guid EmbeddingModelId,
    int EmbeddingDimensions,
    float[] QueryEmbedding,
    int TopK);

public sealed record TutorInteractionParameters(
    double? Temperature,
    double? TopP,
    int? MaxTokens)
{
    public const int MaximumMaxTokens = 131_072;
}
