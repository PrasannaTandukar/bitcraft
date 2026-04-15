using Bitcraft.Domain.Entities;

namespace Bitcraft.Application.Common.Interfaces;

public interface IChunkRepository
{
    Task<DocumentChunk> AddAsync(int documentId, int chunkIndex, string content, float[] embedding, CancellationToken cancellationToken = default);
    Task<List<DocumentChunk>> SearchNearestAsync(float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default);
}
