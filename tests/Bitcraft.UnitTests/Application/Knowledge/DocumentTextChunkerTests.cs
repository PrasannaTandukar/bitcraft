using Bitcraft.Application.Features.Knowledge;
using Xunit;

namespace Bitcraft.UnitTests.Application.Knowledge;

public class DocumentTextChunkerTests
{
    [Fact]
    public void Split_PrefersParagraphBoundaries()
    {
        var result = DocumentTextChunker.Split(
            "First paragraph has useful text.\n\nSecond paragraph also has useful text.",
            42,
            0,
            10);

        Assert.True(result.IsSuccess);
        Assert.Equal(["First paragraph has useful text.", "Second paragraph also has useful text."], result.Value);
    }

    [Fact]
    public void Split_OversizedParagraphPrefersSentenceThenWhitespaceBoundaries()
    {
        var result = DocumentTextChunker.Split(
            "This is the first sentence. This second sentence contains several useful words for splitting.",
            35,
            0,
            10);

        Assert.True(result.IsSuccess);
        Assert.Equal("This is the first sentence.", result.Value[0]);
        Assert.All(result.Value, chunk => Assert.InRange(chunk.Length, 1, 35));
    }

    [Fact]
    public void Split_ExactBoundaryProducesOneChunk()
    {
        var result = DocumentTextChunker.Split(new string('x', 100), 100, 0, 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('x', 100), Assert.Single(result.Value));
    }

    [Fact]
    public void Split_OverlapRepeatsPreviousTailAndStillProgresses()
    {
        var result = DocumentTextChunker.Split(new string('a', 120) + new string('b', 120), 100, 20, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Count >= 3);
        Assert.Equal(result.Value[0][^20..], result.Value[1][..20]);
        Assert.All(result.Value, chunk => Assert.InRange(chunk.Length, 1, 100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n\t ")]
    public void Split_EmptyOrWhitespaceOnlyContentProducesNoChunks(string? text)
    {
        var result = DocumentTextChunker.Split(text, 100, 10, 10);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Split_PreservesUnicodeText()
    {
        const string text = "नेपालको राजधानी काठमाडौं हो। こんにちは世界。 مرحبا بالعالم.";
        var result = DocumentTextChunker.Split(text, 24, 0, 10);

        Assert.True(result.IsSuccess);
        Assert.Contains("काठमाडौं", string.Concat(result.Value));
        Assert.All(result.Value, chunk => Assert.InRange(chunk.Length, 1, 24));
    }

    [Fact]
    public void Split_VeryLongUnbrokenTextUsesHardBoundariesWithoutEmptyChunks()
    {
        var result = DocumentTextChunker.Split(new string('z', 251), 100, 0, 10);

        Assert.True(result.IsSuccess);
        Assert.Equal([100, 100, 51], result.Value.Select(chunk => chunk.Length));
        Assert.DoesNotContain(result.Value, string.IsNullOrEmpty);
    }

    [Fact]
    public void Split_WhenChunkLimitWouldBeExceededReturnsFailure()
    {
        var result = DocumentTextChunker.Split(new string('z', 201), 100, 0, 2);

        Assert.True(result.IsFailure);
        Assert.Equal("Knowledge.ChunkCountExceeded", result.Error.Code);
    }
}
