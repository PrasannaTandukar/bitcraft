using Bitcraft.Domain.Modeling;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Modeling;

public class RuntimeEngineProviderTypeDialectTests
{
    [Theory]
    [InlineData(RuntimeEngineProviderType.Ollama, LlmDialect.Ollama)]
    [InlineData(RuntimeEngineProviderType.OpenAI, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.OpenAICompatible, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.vLLM, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.LlamaCpp, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.Anthropic, LlmDialect.Anthropic)]
    [InlineData(RuntimeEngineProviderType.AzureOpenAI, LlmDialect.Unsupported)]
    [InlineData(RuntimeEngineProviderType.Bedrock, LlmDialect.Unsupported)]
    public void ToLlmDialect_MapsProviderTypesToDialects(RuntimeEngineProviderType providerType, LlmDialect expected)
    {
        Assert.Equal(expected, providerType.ToLlmDialect());
    }

    [Theory]
    [InlineData(RuntimeEngineProviderType.Ollama, LlmDialect.Ollama)]
    [InlineData(RuntimeEngineProviderType.OpenAI, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.OpenAICompatible, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.vLLM, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.LlamaCpp, LlmDialect.OpenAI)]
    [InlineData(RuntimeEngineProviderType.Anthropic, LlmDialect.Unsupported)]
    [InlineData(RuntimeEngineProviderType.AzureOpenAI, LlmDialect.Unsupported)]
    [InlineData(RuntimeEngineProviderType.Bedrock, LlmDialect.Unsupported)]
    public void ToEmbeddingDialect_MapsProviderTypesToDialects(RuntimeEngineProviderType providerType, LlmDialect expected)
    {
        Assert.Equal(expected, providerType.ToEmbeddingDialect());
    }
}
