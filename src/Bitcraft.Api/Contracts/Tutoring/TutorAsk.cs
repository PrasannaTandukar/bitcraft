namespace Bitcraft.Api.Contracts.Tutoring;

public sealed record AskTutorQuestionRequest(
    string Question,
    string? Model = null,
    bool UseMemory = false,
    bool SaveToMemory = false,
    int? TopK = null,
    Guid? TutorId = null,
    IReadOnlyList<TutorConversationMessageRequest>? History = null);

public sealed record TutorConversationMessageRequest(string Role, string Content);

public sealed record AskTutorQuestionResponse(
    string Answer,
    string Model,
    string Mode,
    bool ContextUsed,
    bool MemorySaved,
    IReadOnlyList<TutorSourceResponse> Sources,
    string Message);

public sealed record TutorSourceResponse(
    string SourceType,
    Guid SourceId,
    Guid? DocumentId,
    string? DocumentName,
    Guid? MemoryId,
    Guid? ChunkId,
    int? ChunkIndex,
    string Content,
    double Score);
