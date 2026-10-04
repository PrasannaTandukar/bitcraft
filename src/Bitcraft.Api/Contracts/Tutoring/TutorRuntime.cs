namespace Bitcraft.Api.Contracts.Tutoring;

public sealed record AskTutorRuntimeRequest(
    string Question = "",
    int? TopK = 5,
    Guid? ConversationId = null,
    IReadOnlyList<Guid>? AttachmentIds = null,
    bool UseMemory = false,
    bool SaveToMemory = false);

public sealed record TutorRuntimeTutorResponse(Guid Id, string Name);

public sealed record TutorRuntimeRevisionResponse(Guid Id, int Number);

public sealed record TutorRuntimeModelResponse(
    Guid Id,
    string Name,
    string ProviderModelName,
    string RuntimeEngineName,
    bool SupportsImageInput = false,
    bool SupportsFunctionCalling = false,
    bool SupportsStructuredOutput = false);

public sealed record TutorToolCallActivityResponse(string ToolName, DateTime StartedUtc,
    DateTime CompletedUtc, string Status, double DurationMs, string? ResultSummary,
    string? ErrorCategory);

public sealed record TutorRuntimeEmbeddingModelResponse(
    Guid Id,
    string Name,
    string ProviderModelName,
    int Dimensions,
    string RuntimeEngineName);

public sealed record TutorRuntimeSourceResponse(
    Guid DocumentId,
    string? DocumentName,
    Guid ChunkId,
    int ChunkIndex,
    double Score);

public sealed record TutorRuntimeAskResponse(
    string Answer,
    TutorRuntimeTutorResponse Tutor,
    TutorRuntimeRevisionResponse Revision,
    Guid SnapshotId,
    TutorRuntimeModelResponse Model,
    TutorRuntimeEmbeddingModelResponse? EmbeddingModel,
    bool KnowledgeUsed,
    IReadOnlyList<TutorRuntimeSourceResponse> Sources,
    Guid? ConversationId,
    bool MemoryUsed,
    bool MemorySaved,
    IReadOnlyList<TutorToolCallActivityResponse> ToolActivities);

public sealed record TutorRuntimeStreamEventResponse(
    long Sequence,
    string Type,
    Guid? ConversationId = null,
    string? Delta = null,
    TutorRuntimeSourceResponse? Source = null,
    TutorToolCallActivityResponse? Tool = null,
    TutorRuntimeAskResponse? Message = null,
    ApiError? Error = null);

public sealed record TutorRuntimeDescriptorResponse(
    Guid TutorId,
    string TutorName,
    string? Description,
    Guid ActiveRevisionId,
    int RevisionNumber,
    string ActivationSlug,
    Guid SnapshotId,
    TutorRuntimeModelResponse Model,
    TutorRuntimeEmbeddingModelResponse? EmbeddingModel,
    int KnowledgeDocumentCount);

public sealed record TutorRuntimeReadinessReasonResponse(string Code, string Message);

public sealed record TutorRuntimeReadinessResponse(
    Guid TutorId,
    string TutorName,
    Guid? ActiveRevisionId,
    int? ActiveRevisionNumber,
    bool IsChatReady,
    IReadOnlyList<TutorRuntimeReadinessReasonResponse> Reasons);
