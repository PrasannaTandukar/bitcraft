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
    IReadOnlyList<string> Reasons);
