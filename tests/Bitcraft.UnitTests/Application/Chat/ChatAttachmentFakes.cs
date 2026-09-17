using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Chat;

namespace Bitcraft.UnitTests.Application.Chat;

internal sealed class FakeChatAttachmentRepository : IChatAttachmentRepository
{
    public List<ChatAttachment> Items { get; } = [];
    public Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken = default) { Items.Add(attachment); return Task.CompletedTask; }
    public Task<ChatAttachment?> GetOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(value => value.Id == id && value.CreatedById == userId));
    public Task DeleteAsync(ChatAttachment attachment, CancellationToken cancellationToken = default) { Items.Remove(attachment); return Task.CompletedTask; }
    public Task<IReadOnlyList<ChatAttachment>> ListExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatAttachment>>(Items.Where(value => value.ExpiresUtc <= utcNow).Take(batchSize).ToList());
}

internal sealed class FakeChatAttachmentStore : IChatAttachmentStore
{
    public Dictionary<string, byte[]> Data { get; } = [];
    public int ReadCount { get; private set; }
    public Task SaveAsync(string storageKey, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) { Data.Add(storageKey, data.ToArray()); return Task.CompletedTask; }
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult<Stream?>(Data.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes, false) : null);
    }
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { Data.Remove(storageKey); return Task.CompletedTask; }
    public Task CleanupExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeAttachmentTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
