using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IChatImageNormalizer
{
    Task<Result<NormalizedChatImage>> NormalizeAsync(ReadOnlyMemory<byte> data, string mimeType, CancellationToken cancellationToken = default);
}
