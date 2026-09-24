using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

public sealed class TutorToolCatalog(IEnumerable<ITutorToolHandler> handlers) : ITutorToolCatalog
{
    public IReadOnlyList<TutorToolCatalogEntry> Entries { get; } = handlers
        .OfType<CalculatorTutorToolHandler>()
        .Select(handler => new TutorToolCatalogEntry(handler.ImplementationKey, "Calculator",
            "Performs mathematical calculations.", TutorToolKind.BuiltIn,
            """{"type":"object","properties":{"expression":{"type":"string","description":"Arithmetic expression using numbers, parentheses and + - * / operators.","maxLength":200}},"required":["expression"],"additionalProperties":false}""",
            handler.IsReadOnly)).ToArray();
}
