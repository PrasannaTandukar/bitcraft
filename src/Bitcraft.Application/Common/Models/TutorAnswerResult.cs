namespace Bitcraft.Application.Common.Models;

public sealed record TutorAnswerResult(
    string Answer,
    string Model,
    string Mode,
    bool ContextUsed,
    bool MemorySaved,
    IReadOnlyList<TutorSourceResult> Sources,
    string Message);

public sealed record TutorSourceResult(
    string SourceType,
    Guid SourceId,
    Guid? DocumentId,
    string? DocumentName,
    Guid? MemoryId,
    Guid? ChunkId,
    int? ChunkIndex,
    string Content,
    double Score);
