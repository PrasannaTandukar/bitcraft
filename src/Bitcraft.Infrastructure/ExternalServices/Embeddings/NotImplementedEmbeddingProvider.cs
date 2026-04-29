using Bitcraft.Application.Common.Interfaces;

namespace Bitcraft.Infrastructure.ExternalServices.Embeddings;

public class NotImplementedEmbeddingProvider : IEmbeddingProvider
{
    public Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Filled in Phase B");
}
