using System.Net;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Agents;

public sealed class TutorToolProviderErrorTests
{
    [Fact]
    public void ToolRequestRejectionHasUsefulSafeMessage()
    {
        var error = TutorAgentErrorMapper.Map(new HttpRequestException("private upstream body", null, HttpStatusCode.BadRequest), "ExecuteAgent", false, true);
        Assert.Equal("TutorAgent.ToolRequestRejected", error.Code);
        Assert.Contains("function calling", error.Description);
        Assert.DoesNotContain("private", error.Description);
    }

    [Fact]
    public void TextOnlyBadRequestKeepsExistingMapping()
    {
        var error = TutorAgentErrorMapper.Map(new HttpRequestException("private", null, HttpStatusCode.BadRequest), "ExecuteAgent", false);
        Assert.Equal("TutorAgent.GenerationFailed", error.Code);
    }
}
