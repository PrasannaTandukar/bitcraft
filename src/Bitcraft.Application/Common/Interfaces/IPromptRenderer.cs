namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Port for prompt template rendering. No implementation is provided yet;
/// storage of templates is sufficient for foundational scope.
/// Implementations (e.g. Liquid, Handlebars) belong in Infrastructure when added.
/// </summary>
public interface IPromptRenderer
{
    Task<string> RenderAsync(string template, IReadOnlyDictionary<string, object?> context, CancellationToken cancellationToken = default);
}
