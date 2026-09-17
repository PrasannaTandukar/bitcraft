using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Chat;

/// <summary>Temporary, user-owned image metadata. Image bytes live outside the database.</summary>
public sealed class ChatAttachment : BaseModel
{
    public string OriginalFileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTime ExpiresUtc { get; private set; }

    private ChatAttachment() { }

    public static ChatAttachment Create(Guid userId, string fileName, string mimeType,
        long byteLength, int width, int height, DateTime expiresUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(userId));
        var attachment = new ChatAttachment
        {
            OriginalFileName = fileName,
            MimeType = mimeType,
            ByteLength = byteLength,
            Width = width,
            Height = height,
            ExpiresUtc = expiresUtc
        };
        // Expiry in the private key lets storage cleanup also recover orphan files after a crash.
        attachment.StorageKey = $"{new DateTimeOffset(expiresUtc).AddSeconds(1).ToUnixTimeSeconds()}.{attachment.Id:N}";
        attachment.UpdateMetadata(userId);
        return attachment;
    }
}
