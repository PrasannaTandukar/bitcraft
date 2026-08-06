namespace Bitcraft.Api.Contracts.Rag;

public sealed record AskRagQuestionRequest(
    string Question,
    string? Model = null,
    int? TopK = null);

public sealed record AskRagQuestionResponse(
    string Answer,
    string Model,
    IReadOnlyList<RagSourceChunkResponse> Sources);

public sealed record RagSourceChunkResponse(
    Guid DocumentId,
    string? DocumentName,
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    double Score);
