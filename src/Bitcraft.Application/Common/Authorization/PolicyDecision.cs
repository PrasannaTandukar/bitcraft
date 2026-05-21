namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Result of evaluating a <see cref="PolicyRequest"/> against the active policy set.
/// </summary>
/// <param name="Effect">Permit, Deny, or NotApplicable.</param>
/// <param name="Obligations">Obligations the caller must honor when acting on a Permit.</param>
/// <param name="Reasons">Human-readable rationale strings suitable for audit logs.</param>
public sealed record PolicyDecision(
    PolicyEffect Effect,
    IReadOnlyList<Obligation> Obligations,
    IReadOnlyList<string> Reasons)
{
    /// <summary>
    /// Returns <see langword="true"/> only when <see cref="Effect"/> is
    /// <see cref="PolicyEffect.Permit"/>. <see cref="PolicyEffect.NotApplicable"/>
    /// and <see cref="PolicyEffect.Deny"/> are both treated as deny
    /// (deny-by-default) so callers should always gate on this helper rather
    /// than comparing <see cref="Effect"/> directly.
    /// </summary>
    public bool IsPermit() => Effect == PolicyEffect.Permit;
}
