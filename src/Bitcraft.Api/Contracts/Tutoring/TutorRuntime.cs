namespace Bitcraft.Api.Contracts.Tutoring;

public sealed record AskTutorRuntimeRequest(
    string Question = "",
    int? TopK = 5,
    IReadOnlyList<TutorConversationMessageRequest>? History = null,
    Guid? ConversationId = null,
    IReadOnlyList<Guid>? AttachmentIds = null);

public sealed record TutorRuntimeTutorResponse(Guid Id, string Name);

public sealed record TutorRuntimeRevisionResponse(Guid Id, int Number);

public sealed record TutorRuntimeModelResponse(
    Guid Id,
    string Name,
    string ProviderModelName,
    string RuntimeEngineName,
    bool SupportsImageInput = false);

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
    Guid? ConversationId);

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
