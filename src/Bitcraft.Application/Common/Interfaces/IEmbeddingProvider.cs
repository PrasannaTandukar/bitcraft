namespace Bitcraft.Application.Common.Interfaces;

public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, string indexName, CancellationToken cancellationToken = default);
}
