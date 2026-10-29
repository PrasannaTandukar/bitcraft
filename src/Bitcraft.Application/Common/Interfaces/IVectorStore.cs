using Bitcraft.Domain.Knowledge;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IVectorStore
{
    Task<IReadOnlyList<KnowledgeChunk>> ReplaceDocumentChunksAsync(
        Guid documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken cancellationToken = default);
}
