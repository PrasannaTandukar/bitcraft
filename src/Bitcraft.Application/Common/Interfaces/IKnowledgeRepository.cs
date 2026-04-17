using Bitcraft.Domain.Knowledge;

namespace Bitcraft.Application.Common.Interfaces;

public interface IKnowledgeRepository
{
    Task<DocumentChunk> AddChunkAsync(int documentId, int chunkIndex, string content, float[] embedding, CancellationToken cancellationToken = default);
    Task<List<DocumentChunk>> SearchNearestChunksAsync(float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default);
}
