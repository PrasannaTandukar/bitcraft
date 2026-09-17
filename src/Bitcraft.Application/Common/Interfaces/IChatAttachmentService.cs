using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IChatAttachmentService
{
    Task<Result<ChatAttachmentResponse>> UploadAsync(Guid userId, string fileName, string mimeType, Stream input, CancellationToken cancellationToken = default);
    Task<Result<ChatAttachmentContent>> ReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
