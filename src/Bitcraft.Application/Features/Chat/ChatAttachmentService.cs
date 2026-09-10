using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Chat;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Application.Features.Chat;

public sealed class ChatAttachmentService(
    IChatAttachmentRepository repository,
    IChatAttachmentStore store,
    IChatImageNormalizer normalizer,
    ChatAttachmentLimits limits,
    TimeProvider time,
    ILogger<ChatAttachmentService> logger) : IChatAttachmentService
{
    private static readonly Error NotFound = new("ChatAttachment.NotFound", "This image is unavailable. Please attach it again.");
    private static readonly Error StorageUnavailable = new("ChatAttachment.StorageUnavailable", "Image storage is unavailable. Please try again.");

    public async Task<Result<ChatAttachmentResponse>> UploadAsync(Guid userId, string fileName, string mimeType,
        Stream input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Result.Failure<ChatAttachmentResponse>(NotFound);
        if (mimeType is not ("image/png" or "image/jpeg" or "image/webp"))
            return Result.Failure<ChatAttachmentResponse>(new Error("ChatAttachment.UnsupportedType", "Choose a JPEG, PNG, or WebP image."));

        var data = await ReadBoundedAsync(input, cancellationToken);
        if (data.IsFailure) return Result.Failure<ChatAttachmentResponse>(data.Error);
        var normalized = await normalizer.NormalizeAsync(data.Value, mimeType, cancellationToken);
        if (normalized.IsFailure) return Result.Failure<ChatAttachmentResponse>(normalized.Error);
        var image = normalized.Value;
        var safeName = new string(Path.GetFileName(fileName.Replace('\\', '/'))
            .Where(character => !char.IsControl(character)).Take(200).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "image";
        var attachment = ChatAttachment.Create(userId, safeName, image.MimeType, image.Data.Length,
            image.Width, image.Height, time.GetUtcNow().UtcDateTime.AddHours(limits.RetentionHours));
        try
        {
            await store.SaveAsync(attachment.StorageKey, image.Data, cancellationToken);
            try
            {
                await repository.AddAsync(attachment, cancellationToken);
            }
            catch
            {
                // Best effort rollback; the expiry sweep also removes files orphaned by process failure.
                await store.DeleteAsync(attachment.StorageKey, CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Attachment upload failed. AttachmentId={AttachmentId}", attachment.Id);
            return Result.Failure<ChatAttachmentResponse>(StorageUnavailable);
        }
        logger.LogInformation("Chat image stored. AttachmentId={AttachmentId}, ImageByteCount={ImageByteCount}", attachment.Id, image.Data.Length);
        return Result.Success(ToResponse(attachment));
    }

    public async Task<Result<ChatAttachmentContent>> ReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var attachment = await repository.GetOwnedAsync(id, userId, cancellationToken);
            if (attachment is null || userId == Guid.Empty) return Result.Failure<ChatAttachmentContent>(NotFound);
            if (attachment.ExpiresUtc <= time.GetUtcNow().UtcDateTime)
                return Result.Failure<ChatAttachmentContent>(new Error("ChatAttachment.Expired", "This image has expired. Please attach it again."));
            await using var stream = await store.OpenReadAsync(attachment.StorageKey, cancellationToken);
            if (stream is null) return Result.Failure<ChatAttachmentContent>(NotFound);
            var data = await ReadBoundedAsync(stream, cancellationToken);
            if (data.IsFailure || data.Value.Length != attachment.ByteLength)
                return Result.Failure<ChatAttachmentContent>(NotFound);
            return Result.Success(new ChatAttachmentContent(ToResponse(attachment), data.Value));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Attachment read failed. AttachmentId={AttachmentId}", id);
            return Result.Failure<ChatAttachmentContent>(StorageUnavailable);
        }
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var attachment = await repository.GetOwnedAsync(id, userId, cancellationToken);
            if (attachment is null || userId == Guid.Empty) return Result.Failure(NotFound);
            await store.DeleteAsync(attachment.StorageKey, cancellationToken);
            await repository.DeleteAsync(attachment, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Attachment deletion failed. AttachmentId={AttachmentId}", id);
            return Result.Failure(StorageUnavailable);
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = time.GetUtcNow().UtcDateTime;
        // Sweep disk independently of the database, including files orphaned during uploads.
        await store.CleanupExpiredAsync(utcNow, cancellationToken);
        while (true)
        {
            var expired = await repository.ListExpiredAsync(utcNow, 200, cancellationToken);
            foreach (var attachment in expired)
            {
                await store.DeleteAsync(attachment.StorageKey, cancellationToken);
                await repository.DeleteAsync(attachment, cancellationToken);
            }
            if (expired.Count < 200) break;
        }
    }

    private async Task<Result<ReadOnlyMemory<byte>>> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > limits.MaxFileBytes)
                return Result.Failure<ReadOnlyMemory<byte>>(new Error("ChatAttachment.FileTooLarge", $"Images must be at most {limits.MaxFileBytes} bytes."));
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.Length == 0
            ? Result.Failure<ReadOnlyMemory<byte>>(new Error("ChatAttachment.InvalidImage", "The selected image is empty."))
            : Result.Success<ReadOnlyMemory<byte>>(output.ToArray());
    }

    private static ChatAttachmentResponse ToResponse(ChatAttachment attachment) => new(
        attachment.Id, attachment.OriginalFileName, attachment.MimeType, attachment.ByteLength,
        attachment.Width, attachment.Height, attachment.ExpiresUtc);
}
