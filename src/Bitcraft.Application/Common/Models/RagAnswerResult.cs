namespace Bitcraft.Application.Common.Models;

public sealed record RagAnswerResult(
    string Answer,
    string Model,
    IReadOnlyList<RagSourceChunkResult> Sources);

public sealed record RagSourceChunkResult(
    Guid DocumentId,
    string? DocumentName,
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    double Score);
