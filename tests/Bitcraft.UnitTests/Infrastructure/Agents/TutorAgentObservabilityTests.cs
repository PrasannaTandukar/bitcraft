using System.Diagnostics;
using System.Net;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Agents;

public sealed class TutorAgentObservabilityTests
{
    [Fact]
    public async Task TelemetryContainsSafeIdsAndMetricsWithoutPrivateContent()
    {
        var stoppedActivities = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TutorAgentTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var request = TutorAgentExecutorTests.Request() with
        {
            Instructions = "private-system-prompt",
            UserText = "private-user-question",
            Images = [TutorAgentExecutorTests.Image()],
            ConversationId = Guid.NewGuid()
        };

        var result = await new TutorAgentExecutor(new TutorAgentExecutorTests.RecordingFactory(),
            new KnowledgeTestSetup().Factory,
            loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        var stopped = Assert.Single(stoppedActivities,
            value => value.GetTagItem("bitcraft.conversation.id")?.ToString() == request.ConversationId.ToString());
        Assert.NotNull(stopped);
        Assert.Equal(request.TutorId.ToString(), stopped.GetTagItem("bitcraft.tutor.id")?.ToString());
        Assert.Equal(request.ConversationId.ToString(), stopped.GetTagItem("bitcraft.conversation.id")?.ToString());
        Assert.Equal("True", stopped.GetTagItem("bitcraft.generation.success")?.ToString());
        Assert.Equal("False", stopped.GetTagItem("bitcraft.rag.used")?.ToString());
        Assert.Equal("0", stopped.GetTagItem("bitcraft.tool.call.count")?.ToString());
        Assert.Equal("10", stopped.GetTagItem("gen_ai.usage.input_tokens")?.ToString());
        Assert.Equal("4", stopped.GetTagItem("gen_ai.usage.output_tokens")?.ToString());
        var telemetry = string.Join("|", stoppedActivities.SelectMany(value => value.TagObjects)
            .Select(value => $"{value.Key}={value.Value}")) + string.Join("|", stoppedActivities
            .SelectMany(value => value.Events).SelectMany(value => value.Tags)
            .Select(value => $"{value.Key}={value.Value}"));
        Assert.DoesNotContain("private-system-prompt", telemetry);
        Assert.DoesNotContain("private-user-question", telemetry);
        Assert.DoesNotContain(Convert.ToBase64String(request.Images![0].Data.ToArray()), telemetry);
        Assert.DoesNotContain(request.Model.Endpoint, telemetry);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "TutorAgent.RateLimited")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "TutorAgent.ContextTooLarge")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "TutorAgent.GenerationFailed")]
    public void CentralMapperClassifiesHttpFailuresSafely(HttpStatusCode status, string expected)
    {
        var exception = new HttpRequestException("private upstream response", null, status);
        var error = TutorAgentErrorMapper.Map(exception, "ExecuteAgent", false);
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("private", error.Description);
    }
}
