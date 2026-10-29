namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Application-layer ABAC evaluator contract. Implementations live in
/// Infrastructure (or higher) and decide whether a <see cref="PolicyRequest"/>
/// is permitted, denied, or not covered by any active policy.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluate the supplied <paramref name="request"/> against the active
    /// policy set and return a single <see cref="PolicyDecision"/>.
    /// Implementations must not throw for ordinary "deny" outcomes; throw
    /// only for infrastructural failures.
    /// <para>
    /// Callers MUST treat <see cref="PolicyEffect.NotApplicable"/> as deny
    /// (deny-by-default) and gate authorization on
    /// <see cref="PolicyDecision.IsPermit"/> rather than comparing
    /// <see cref="PolicyDecision.Effect"/> directly.
    /// </para>
    /// </summary>
    Task<PolicyDecision> EvaluateAsync(PolicyRequest request, CancellationToken cancellationToken);
}
