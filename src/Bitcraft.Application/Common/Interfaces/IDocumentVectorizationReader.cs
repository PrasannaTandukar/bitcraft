using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IDocumentVectorizationReader
{
    Task<DocumentVectorizationMetadata> GetMetadataAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DocumentVectorizationMetadata>> GetMetadataAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default);
}
