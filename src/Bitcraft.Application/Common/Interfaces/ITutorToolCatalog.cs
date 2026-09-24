using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>Safe metadata for server-registered implementations; never executable configuration.</summary>
public interface ITutorToolCatalog
{
    IReadOnlyList<TutorToolCatalogEntry> Entries { get; }
}

public sealed record TutorToolCatalogEntry(string ImplementationKey, string Name, string Description,
    TutorToolKind Kind, string JsonSchema, bool SupportsAutomaticExecution);
