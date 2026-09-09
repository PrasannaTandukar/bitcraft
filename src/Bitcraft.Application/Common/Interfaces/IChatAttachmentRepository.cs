using Bitcraft.Domain.Chat;

namespace Bitcraft.Application.Common.Interfaces;

public interface IChatAttachmentRepository
{
    Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken = default);
    Task<ChatAttachment?> GetOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(ChatAttachment attachment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatAttachment>> ListExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);
}
