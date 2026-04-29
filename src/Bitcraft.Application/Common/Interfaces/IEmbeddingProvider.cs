namespace Bitcraft.Application.Common.Interfaces;

public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, string embeddingModel, CancellationToken cancellationToken = default);
}
