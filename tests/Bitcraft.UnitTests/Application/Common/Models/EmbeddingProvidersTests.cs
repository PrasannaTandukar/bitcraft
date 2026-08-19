using Bitcraft.Application.Common.Models;
using Xunit;

namespace Bitcraft.UnitTests.Application.Common.Models;

public class EmbeddingProvidersTests
{
    [Theory]
    [InlineData("ollama", EmbeddingProviders.Ollama)]
    [InlineData(" OLLAMA ", EmbeddingProviders.Ollama)]
    [InlineData("Ollama", EmbeddingProviders.Ollama)]
    [InlineData("openai", EmbeddingProviders.OpenAI)]
    [InlineData("OpenAICompatible", EmbeddingProviders.OpenAICompatible)]
    [InlineData("llamacpp", EmbeddingProviders.LlamaCpp)]
    public void Normalize_WithSupportedProvider_ReturnsCanonicalProvider(string provider, string expected)
    {
        // Arrange / Act
        var normalized = EmbeddingProviders.Normalize(provider);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UnsupportedProvider")]
    [InlineData("CustomBackend")]
    public void Normalize_WithUnsupportedProvider_ReturnsNull(string? provider)
    {
        // Arrange / Act
        var normalized = EmbeddingProviders.Normalize(provider);

        // Assert
        Assert.Null(normalized);
    }

    [Fact]
    public void All_WithImplementedProviders_ContainsConfiguredProviders()
    {
        // Arrange / Act
        var providers = EmbeddingProviders.All;

        // Assert
        Assert.Contains(EmbeddingProviders.Ollama, providers);
        Assert.Contains(EmbeddingProviders.OpenAI, providers);
        Assert.Contains(EmbeddingProviders.OpenAICompatible, providers);
        Assert.Contains(EmbeddingProviders.LlamaCpp, providers);
    }
}
