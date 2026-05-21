using Bitcraft.Application.Common.Authorization;
using Xunit;

namespace Bitcraft.UnitTests.Application.Authorization;

/// <summary>
/// Sec Ops finding M-2 — locks the deny-by-default contract: callers MUST gate on
/// <see cref="PolicyDecision.IsPermit"/>, and <see cref="PolicyEffect.NotApplicable"/>
/// MUST be treated as deny.
/// </summary>
public class PolicyDecisionTests
{
    private static PolicyDecision Decision(PolicyEffect effect) => new(effect, [], []);

    [Fact]
    public void IsPermit_ReturnsTrue_OnlyForPermit()
    {
        Assert.True(Decision(PolicyEffect.Permit).IsPermit());
    }

    [Fact]
    public void IsPermit_ReturnsFalse_ForDeny()
    {
        Assert.False(Decision(PolicyEffect.Deny).IsPermit());
    }

    [Fact]
    public void IsPermit_ReturnsFalse_ForNotApplicable()
    {
        Assert.False(Decision(PolicyEffect.NotApplicable).IsPermit());
    }
}
