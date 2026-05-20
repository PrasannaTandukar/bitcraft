namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Side-effectful instruction that a permitting policy attaches to a decision
/// (e.g. audit-log, scope-down, require-step-up). The <see cref="Key"/>
/// vocabulary is locked separately; this record only fixes the shape.
/// </summary>
/// <param name="Key">Stable string identifier for the obligation kind.</param>
/// <param name="Parameters">Free-form string parameters interpreted by the obligation handler.</param>
public sealed record Obligation(
    string Key,
    IReadOnlyDictionary<string, string> Parameters);
