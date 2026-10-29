using Bitcraft.Domain.Documents;

namespace Bitcraft.Application.Common.Interfaces;

public interface IDocumentContentStore
{
    Task UpdateContentAndClearChunksAsync(
        Document document,
        CancellationToken cancellationToken = default);
}
