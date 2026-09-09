namespace Bitcraft.Application.Common.Interfaces;

public interface IChatAttachmentStore
{
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
    Task CleanupExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
