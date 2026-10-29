namespace Bitcraft.Api.Contracts.Chat;

public sealed record UploadChatImageRequest(IFormFile File);

public sealed record ChatAttachmentResponse(
    Guid Id, string FileName, string MimeType, long ByteLength, int Width, int Height, DateTime ExpiresUtc);

public sealed record ChatAttachmentOptionsResponse(
    long MaxFileBytes, int MaxImagesPerMessage, int MaxImagesPerRequest, int MaxDimension,
    int RetentionHours, IReadOnlyList<string> AllowedMimeTypes);
