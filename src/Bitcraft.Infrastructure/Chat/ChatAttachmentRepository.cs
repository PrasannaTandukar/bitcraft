using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Chat;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Chat;

public sealed class ChatAttachmentRepository(AppDbContext db) : IChatAttachmentRepository
{
    public async Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken = default)
    {
        db.ChatAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ChatAttachment?> GetOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        db.ChatAttachments.FirstOrDefaultAsync(value => value.Id == id && value.CreatedById == userId, cancellationToken);

    public async Task DeleteAsync(ChatAttachment attachment, CancellationToken cancellationToken = default)
    {
        db.ChatAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatAttachment>> ListExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        await db.ChatAttachments.Where(value => value.ExpiresUtc <= utcNow).OrderBy(value => value.ExpiresUtc).Take(batchSize).ToListAsync(cancellationToken);
}
