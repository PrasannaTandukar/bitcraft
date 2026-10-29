namespace Bitcraft.Application.Common.Models;

public sealed record ChatAttachmentLimits(
    long MaxFileBytes = 5 * 1024 * 1024,
    int MaxImagesPerMessage = 4,
    int MaxDimension = 2048,
    int RetentionHours = 24,
    int MaxImagesPerRequest = 20,
    long MaxDecodedPixels = 40_000_000);

public sealed record NormalizedChatImage(string MimeType, ReadOnlyMemory<byte> Data, int Width, int Height);

public sealed record ChatAttachmentResponse(
    Guid Id, string FileName, string MimeType, long ByteLength, int Width, int Height, DateTime ExpiresUtc);

public sealed record ChatAttachmentContent(ChatAttachmentResponse Metadata, ReadOnlyMemory<byte> Data);
