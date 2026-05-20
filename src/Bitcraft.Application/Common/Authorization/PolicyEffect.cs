namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Outcome of a single policy evaluation. Mirrors XACML-style effects so that
/// composite evaluators can distinguish "this policy did not apply" from an
/// explicit Permit/Deny decision.
/// </summary>
public enum PolicyEffect
{
    Permit,
    Deny,
    NotApplicable
}
