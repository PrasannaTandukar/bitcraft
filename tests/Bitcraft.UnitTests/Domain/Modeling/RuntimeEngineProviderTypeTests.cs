using Bitcraft.Domain.Modeling;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Modeling;

/// <summary>
/// Locks the persisted int values of <see cref="RuntimeEngineProviderType"/> (D11.a). Any change here would silently
/// corrupt existing rows because the column uses <c>HasConversion&lt;int&gt;()</c>.
/// </summary>
public class RuntimeEngineProviderTypeTests
{
    [Theory]
    [InlineData(RuntimeEngineProviderType.Ollama, 0)]
    [InlineData(RuntimeEngineProviderType.OpenAI, 1)]
    [InlineData(RuntimeEngineProviderType.OpenAICompatible, 2)]
    [InlineData(RuntimeEngineProviderType.vLLM, 3)]
    [InlineData(RuntimeEngineProviderType.LlamaCpp, 4)]
    [InlineData(RuntimeEngineProviderType.AzureOpenAI, 100)]
    [InlineData(RuntimeEngineProviderType.Anthropic, 101)]
    [InlineData(RuntimeEngineProviderType.Bedrock, 102)]
    public void EnumValues_AreLocked(RuntimeEngineProviderType value, int expectedInt)
    {
        Assert.Equal(expectedInt, (int)value);
    }

    [Fact]
    public void EnumMembers_CountMatchesLockedVocabulary()
    {
        // If a new member is added, this test must be updated alongside an EF data migration plan.
        var names = Enum.GetNames<RuntimeEngineProviderType>();
        Assert.Equal(8, names.Length);
    }
}
