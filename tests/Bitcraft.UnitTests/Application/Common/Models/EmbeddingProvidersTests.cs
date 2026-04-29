using Bitcraft.Application.Common.Models;
using Xunit;

namespace Bitcraft.UnitTests.Application.Common.Models;

public class EmbeddingProvidersTests
{
    [Theory]
    [InlineData("ollama", EmbeddingProviders.Ollama)]
    [InlineData(" OLLAMA ", EmbeddingProviders.Ollama)]
    [InlineData("Ollama", EmbeddingProviders.Ollama)]
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
    [InlineData("OpenAI")]
    public void Normalize_WithUnsupportedProvider_ReturnsNull(string? provider)
    {
        // Arrange / Act
        var normalized = EmbeddingProviders.Normalize(provider);

        // Assert
        Assert.Null(normalized);
    }

    [Fact]
    public void All_WithImplementedProviders_ContainsOnlyOllama()
    {
        // Arrange / Act
        var providers = EmbeddingProviders.All;

        // Assert
        var provider = Assert.Single(providers);
        Assert.Equal(EmbeddingProviders.Ollama, provider);
    }
}
