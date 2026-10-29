namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Outcome of a single policy evaluation. Mirrors XACML-style effects so that
/// composite evaluators can distinguish "this policy did not apply" from an
/// explicit Permit/Deny decision.
/// </summary>
public enum PolicyEffect
{
    /// <summary>Explicit allow. The action MAY proceed (subject to any obligations).</summary>
    Permit,
    /// <summary>Explicit deny. The action MUST NOT proceed.</summary>
    Deny,
    /// <summary>
    /// No policy applied to this request. Callers MUST treat this as deny
    /// (deny-by-default); it is NOT a permit.
    /// </summary>
    NotApplicable
}
