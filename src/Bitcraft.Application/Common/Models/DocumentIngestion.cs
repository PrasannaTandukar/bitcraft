namespace Bitcraft.Application.Common.Models;

public sealed record DocumentIngestionLimits(
    long MaxUploadBytes,
    int MaxExtractedCharacters,
    int MaxVectorizationChunks,
    int MaxBatchFiles)
{
    public const long DefaultMaxUploadBytes = 10 * 1024 * 1024;
    public const int DefaultMaxExtractedCharacters = 2_000_000;
    public const int DefaultMaxVectorizationChunks = 500;
    public const int DefaultMaxBatchFiles = 200;

    public static DocumentIngestionLimits Default { get; } = new(
        DefaultMaxUploadBytes,
        DefaultMaxExtractedCharacters,
        DefaultMaxVectorizationChunks,
        DefaultMaxBatchFiles);
}

public sealed record DocumentTextExtractionRequest(
    string FileName,
    string? MimeType,
    ReadOnlyMemory<byte> Content);

public sealed record DocumentTextExtractionResult(
    string Text,
    string DetectedContentType,
    string ExtractionMethod);

public static class DocumentFileTypes
{
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(
        [".txt", ".md", ".csv", ".json", ".pdf", ".docx"],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName));
}

public sealed record DocumentVectorizationMetadata(
    bool IsVectorized,
    bool IsStale,
    int ChunkCount,
    DateTime? LastVectorizedUtc,
    Guid? EmbeddingModelId,
    string? EmbeddingModelName,
    string? ProviderModelName,
    string? EmbeddingProvider,
    int? EmbeddingDimensions);

public sealed record DocumentVectorizationStatus(
    Guid DocumentId,
    bool HasRawText,
    int RawTextLength,
    bool IsVectorized,
    bool IsStale,
    int ChunkCount,
    DateTime? LastVectorizedUtc,
    Guid? EmbeddingModelId,
    string? EmbeddingModelName,
    string? ProviderModelName,
    string? EmbeddingProvider,
    int? EmbeddingDimensions);
