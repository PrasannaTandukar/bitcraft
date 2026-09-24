using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using Microsoft.Extensions.AI;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

public interface ITutorToolRegistry
{
    Result<TutorToolExecution> CreateTools(TutorAgentRequest request,
        Func<TutorToolCallActivity, CancellationToken, ValueTask>? observer = null);
}

public sealed record TutorToolExecution(IReadOnlyList<AITool> Tools, TutorToolActivityAccumulator Activities);

/// <summary>Resolves only server-registered implementations. Snapshot data can never supply executable code or a URL.</summary>
public sealed record TutorToolRuntimeOptions(TimeSpan ExecutionTimeout, int MaximumResultCharacters)
{
    public static TutorToolRuntimeOptions Default { get; } = new(TimeSpan.FromSeconds(5), 4_000);
}

public sealed class TutorToolRegistry(IEnumerable<ITutorToolHandler> handlers,
    TutorToolRuntimeOptions options) : ITutorToolRegistry
{
    internal const int MaximumTools = 10;
    private readonly IReadOnlyDictionary<string, ITutorToolHandler> handlers = handlers
        .ToDictionary(handler => handler.ImplementationKey, StringComparer.Ordinal);

    public Result<TutorToolExecution> CreateTools(TutorAgentRequest request,
        Func<TutorToolCallActivity, CancellationToken, ValueTask>? observer = null)
    {
        var configured = request.Tools ?? [];
        if (configured.Count > MaximumTools)
            return Failure("TooManyTools", $"A tutor may expose at most {MaximumTools} tools.");
        var activities = new TutorToolActivityAccumulator();
        var tools = new List<AITool>();
        foreach (var definition in configured)
        {
            if (definition.ExecutionPolicy == TutorToolExecutionPolicy.Disabled) continue;
            if (definition.TutorId != request.TutorId)
                return Failure("ConfigurationInvalid", "A snapshot tool does not belong to this tutor.");
            if (definition.Kind != TutorToolKind.BuiltIn)
                return Failure("KindUnavailable", "This snapshot uses a tool kind that is not enabled.");
            if (!TryParseSchema(definition.JsonSchema, out var schema))
                return Failure("SchemaInvalid", "A snapshot tool has an invalid JSON schema.");
            if (!handlers.TryGetValue(definition.ImplementationKey, out var handler))
                return Failure("Unavailable", "A snapshot tool implementation is not registered.");
            if (definition.ExecutionPolicy == TutorToolExecutionPolicy.ReadOnlyAutomatic && !handler.IsReadOnly)
                return Failure("ApprovalRequired", "A side-effecting tool cannot run without approval.");

            var invoker = new BoundedToolInvoker(definition.Name, handler, activities, options, observer);
            var generated = AIFunctionFactory.Create(
                (AIFunctionArguments arguments, CancellationToken cancellationToken) =>
                    invoker.InvokeAsync(arguments, cancellationToken),
                definition.Name, definition.Description);
            AIFunction function = new SnapshotSchemaAIFunction(generated, schema);
            if (definition.ExecutionPolicy == TutorToolExecutionPolicy.RequiresApproval)
                function = new ApprovalRequiredAIFunction(function);
            tools.Add(function);
        }
        return Result.Success(new TutorToolExecution(tools, activities));
    }

    private static bool TryParseSchema(string json, out JsonElement schema)
    {
        schema = default;
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16_384) return false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("type", out var type) || type.GetString() != "object" ||
                ContainsReference(document.RootElement)) return false;
            schema = document.RootElement.Clone();
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool ContainsReference(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("$ref") || ContainsReference(property.Value)) return true;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                if (ContainsReference(child)) return true;
        return false;
    }

    private static Result<TutorToolExecution> Failure(string code, string description) =>
        Result.Failure<TutorToolExecution>(new Error($"TutorAgent.Tool{code}", description));

    private sealed class SnapshotSchemaAIFunction(AIFunction inner, JsonElement schema)
        : DelegatingAIFunction(inner)
    {
        public override JsonElement JsonSchema => schema;
    }

    private sealed class BoundedToolInvoker(string name, ITutorToolHandler handler,
        TutorToolActivityAccumulator activities, TutorToolRuntimeOptions options,
        Func<TutorToolCallActivity, CancellationToken, ValueTask>? observer)
    {
        public async ValueTask<string> InvokeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ExecutionTimeout);
            if (observer is not null)
                await observer(new TutorToolCallActivity(name, started, started,
                    TutorToolCallStatus.Running, 0, null, null), cancellationToken);
            try
            {
                var handlerResult = await handler.InvokeAsync(arguments, timeout.Token);
                var result = Bound(handlerResult.Content, options.MaximumResultCharacters);
                var activity = new TutorToolCallActivity(name, started, DateTime.UtcNow,
                    TutorToolCallStatus.Succeeded, stopwatch.Elapsed.TotalMilliseconds,
                    handlerResult.SafeSummary is null ? null : Bound(handlerResult.SafeSummary, 500), null);
                activities.Add(activity);
                if (observer is not null) await observer(activity, cancellationToken);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var activity = new TutorToolCallActivity(name, started, DateTime.UtcNow,
                    TutorToolCallStatus.TimedOut, stopwatch.Elapsed.TotalMilliseconds,
                    null, "Timeout");
                activities.Add(activity);
                if (observer is not null) await observer(activity, cancellationToken);
                return "{\"error\":\"tool_timeout\"}";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                var activity = new TutorToolCallActivity(name, started, DateTime.UtcNow,
                    TutorToolCallStatus.Failed, stopwatch.Elapsed.TotalMilliseconds,
                    null, "ExecutionFailed");
                activities.Add(activity);
                if (observer is not null) await observer(activity, cancellationToken);
                return "{\"error\":\"tool_execution_failed\"}";
            }
        }

        private static string Bound(string value, int maximum) =>
            value.Length <= maximum ? value : value[..(maximum - 1)] + "…";
    }
}

public sealed class TutorToolActivityAccumulator
{
    private readonly object gate = new();
    private readonly List<TutorToolCallActivity> activities = [];

    public void Add(TutorToolCallActivity activity)
    {
        lock (gate) activities.Add(activity);
    }

    public IReadOnlyList<TutorToolCallActivity> Snapshot()
    {
        lock (gate) return activities.ToList();
    }
}

public interface ITutorToolHandler
{
    string ImplementationKey { get; }
    bool IsReadOnly { get; }
    ValueTask<TutorToolHandlerResult> InvokeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken);
}

/// <summary>Raw content is model-only. SafeSummary is the handler's explicitly sanitized activity text.</summary>
public sealed record TutorToolHandlerResult(string Content, string? SafeSummary = null);

/// <summary>A bounded arithmetic evaluator; it has no I/O and accepts only numbers and arithmetic operators.</summary>
public sealed class CalculatorTutorToolHandler : ITutorToolHandler
{
    public string ImplementationKey => "builtin.calculator.v1";
    public bool IsReadOnly => true;

    public ValueTask<TutorToolHandlerResult> InvokeAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!arguments.TryGetValue("expression", out var raw))
            throw new ArgumentException("An expression is required.");
        var expression = raw is JsonElement json && json.ValueKind == JsonValueKind.String
            ? json.GetString()
            : Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 200)
            throw new ArgumentException("The expression is invalid.");
        var parser = new ArithmeticParser(expression);
        var value = parser.Parse();
        if (!double.IsFinite(value)) throw new ArithmeticException("The result is not finite.");
        return ValueTask.FromResult(new TutorToolHandlerResult(
            JsonSerializer.Serialize(new { value }), $"Calculated result: {value.ToString(CultureInfo.InvariantCulture)}"));
    }

    private sealed class ArithmeticParser(string text)
    {
        private int index;

        public double Parse()
        {
            var value = Expression();
            Skip();
            if (index != text.Length) throw new FormatException("Unexpected input.");
            return value;
        }

        private double Expression()
        {
            var value = Term();
            while (true)
            {
                Skip();
                if (Take('+')) value += Term();
                else if (Take('-')) value -= Term();
                else return value;
            }
        }

        private double Term()
        {
            var value = Factor();
            while (true)
            {
                Skip();
                if (Take('*')) value *= Factor();
                else if (Take('/'))
                {
                    var divisor = Factor();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private double Factor()
        {
            Skip();
            if (Take('+')) return Factor();
            if (Take('-')) return -Factor();
            if (Take('('))
            {
                var value = Expression();
                Skip();
                if (!Take(')')) throw new FormatException("Missing parenthesis.");
                return value;
            }
            var start = index;
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] is '.' or 'e' or 'E' ||
                text[index] is '+' or '-' && index > start && text[index - 1] is 'e' or 'E')) index++;
            if (start == index || !double.TryParse(text[start..index], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var number)) throw new FormatException("Invalid number.");
            return number;
        }

        private void Skip() { while (index < text.Length && char.IsWhiteSpace(text[index])) index++; }
        private bool Take(char value)
        {
            if (index >= text.Length || text[index] != value) return false;
            index++;
            return true;
        }
    }
}
