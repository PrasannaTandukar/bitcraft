using System.Text.Json;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Agents;
using Microsoft.Extensions.AI;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Agents;

public sealed class TutorToolRegistryTests
{
    private static readonly TutorToolRuntimeOptions FastOptions = new(TimeSpan.FromMilliseconds(20), 100);

    [Fact]
    public void NoToolsExposesNothing()
    {
        var result = Registry().CreateTools(Request() with { Tools = null });
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Tools);
    }

    [Fact]
    public void EnabledBuiltInExposesCapturedNameDescriptionAndSchema()
    {
        var definition = Tool();
        var result = Registry().CreateTools(Request() with { Tools = [definition] });
        Assert.True(result.IsSuccess, result.Error.Description);
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Value.Tools));
        Assert.Equal(definition.Name, function.Name);
        Assert.Equal(definition.Description, function.Description);
        Assert.Equal(JsonDocument.Parse(definition.JsonSchema).RootElement.GetRawText(),
            function.JsonSchema.GetRawText());
    }

    [Fact]
    public void DisabledToolIsNotResolvedOrExposed()
    {
        var definition = Tool() with
        {
            ImplementationKey = "missing.implementation.v1",
            ExecutionPolicy = TutorToolExecutionPolicy.Disabled
        };
        var result = Registry().CreateTools(Request() with { Tools = [definition] });
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Tools);
    }

    [Fact]
    public void UnknownMalformedCrossTutorAndRemoteKindsFailSafely()
    {
        Assert.Equal("TutorAgent.ToolUnavailable", Registry().CreateTools(Request() with
        { Tools = [Tool() with { ImplementationKey = "missing.v1" }] }).Error.Code);
        Assert.Equal("TutorAgent.ToolSchemaInvalid", Registry().CreateTools(Request() with
        { Tools = [Tool() with { JsonSchema = "{broken" }] }).Error.Code);
        Assert.Equal("TutorAgent.ToolConfigurationInvalid", Registry().CreateTools(Request() with
        { Tools = [Tool() with { TutorId = Guid.NewGuid() }] }).Error.Code);
        Assert.Equal("TutorAgent.ToolKindUnavailable", Registry().CreateTools(Request() with
        { Tools = [Tool() with { Kind = TutorToolKind.ApprovedHttp, ImplementationKey = "https://127.0.0.1" }] }).Error.Code);
        Assert.Equal("TutorAgent.ToolKindUnavailable", Registry().CreateTools(Request() with
        { Tools = [Tool() with { Kind = TutorToolKind.Mcp, ImplementationKey = "shell-command" }] }).Error.Code);
    }

    [Fact]
    public async Task ToolExceptionProducesSafeBoundedResultAndActivity()
    {
        var result = Registry(new ThrowingHandler()).CreateTools(Request() with
        { Tools = [Tool() with { ImplementationKey = "test.throw.v1" }] });
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Value.Tools));
        var output = await function.InvokeAsync(new AIFunctionArguments());
        Assert.Equal("{\"error\":\"tool_execution_failed\"}", Assert.IsType<JsonElement>(output).GetString());
        var activity = Assert.Single(result.Value.Activities.Snapshot());
        Assert.Equal(TutorToolCallStatus.Failed, activity.Status);
        Assert.Equal("ExecutionFailed", activity.ErrorCategory);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(activity));
    }

    [Fact]
    public async Task ToolTimeoutProducesSafeResultAndActivity()
    {
        var result = Registry(new SlowHandler()).CreateTools(Request() with
        { Tools = [Tool() with { ImplementationKey = "test.slow.v1" }] });
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Value.Tools));
        var output = await function.InvokeAsync(new AIFunctionArguments());
        Assert.Equal("{\"error\":\"tool_timeout\"}", Assert.IsType<JsonElement>(output).GetString());
        Assert.Equal(TutorToolCallStatus.TimedOut,
            Assert.Single(result.Value.Activities.Snapshot()).Status);
    }

    [Fact]
    public async Task CalculatorExecutesWithoutIoAndBoundsActivitySummary()
    {
        var result = Registry().CreateTools(Request() with { Tools = [Tool()] });
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Value.Tools));
        var output = await function.InvokeAsync(new AIFunctionArguments { ["expression"] = "(2 + 3) * 4" });
        Assert.Contains("20", Assert.IsType<JsonElement>(output).GetString()!);
        Assert.Equal(TutorToolCallStatus.Succeeded,
            Assert.Single(result.Value.Activities.Snapshot()).Status);
        Assert.Contains("20", Assert.Single(result.Value.Activities.Snapshot()).ResultSummary);
    }

    [Fact]
    public async Task RawToolOutputCannotBecomeActivityMetadataImplicitly()
    {
        var result = Registry(new SensitiveOutputHandler()).CreateTools(Request() with
        { Tools = [Tool() with { ImplementationKey = "test.sensitive.v1" }] });
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Value.Tools));

        var output = await function.InvokeAsync(new AIFunctionArguments());

        Assert.Contains("private-tool-result", Assert.IsType<JsonElement>(output).GetString());
        Assert.Null(Assert.Single(result.Value.Activities.Snapshot()).ResultSummary);
    }

    [Fact]
    public void RequiresApprovalUsesMeaiApprovalFunction()
    {
        var result = Registry().CreateTools(Request() with
        { Tools = [Tool() with { ExecutionPolicy = TutorToolExecutionPolicy.RequiresApproval }] });
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(result.Value.Tools));
        Assert.Empty(result.Value.Activities.Snapshot());
    }

    private static TutorToolRegistry Registry(ITutorToolHandler? handler = null) =>
        new(handler is null ? [new CalculatorTutorToolHandler()] : [handler], FastOptions);

    internal static TutorAgentToolConfiguration Tool(Guid? tutorId = null) => new(Guid.NewGuid(),
        tutorId ?? TutorId, "calculator", "Evaluate bounded arithmetic",
        "{\"type\":\"object\",\"properties\":{\"expression\":{\"type\":\"string\"}},\"required\":[\"expression\"],\"additionalProperties\":false}",
        TutorToolKind.BuiltIn, "builtin.calculator.v1", TutorToolExecutionPolicy.ReadOnlyAutomatic);

    internal static TutorAgentRequest Request() => TutorAgentExecutorTests.Request() with
    {
        TutorId = TutorId,
        Model = TutorAgentExecutorTests.Request().Model with
        { Capabilities = ModelCapabilities.FunctionCalling }
    };

    private static readonly Guid TutorId = Guid.NewGuid();

    private sealed class ThrowingHandler : ITutorToolHandler
    {
        public string ImplementationKey => "test.throw.v1";
        public bool IsReadOnly => true;
        public ValueTask<TutorToolHandlerResult> InvokeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private exception and secret");
    }

    private sealed class SlowHandler : ITutorToolHandler
    {
        public string ImplementationKey => "test.slow.v1";
        public bool IsReadOnly => true;
        public async ValueTask<TutorToolHandlerResult> InvokeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new("never");
        }
    }

    private sealed class SensitiveOutputHandler : ITutorToolHandler
    {
        public string ImplementationKey => "test.sensitive.v1";
        public bool IsReadOnly => true;
        public ValueTask<TutorToolHandlerResult> InvokeAsync(AIFunctionArguments arguments,
            CancellationToken cancellationToken) => ValueTask.FromResult(new TutorToolHandlerResult(
                "private-tool-result"));
    }
}
