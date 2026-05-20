using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Tutoring;

/// <summary>
/// Locks the persisted int ordering of <see cref="ResourceRole"/>. The EF mapping uses
/// <c>HasConversion&lt;int&gt;()</c>, so reordering members would silently corrupt existing rows.
/// These tests are intentionally brittle — they MUST fail on any reorder so the migration
/// implications surface during review (plan.erm.md §3.2, D2/D3).
/// </summary>
public class ResourceRoleTests
{
    [Fact]
    public void Values_Are_AuditorEditorOwner_InAscendingOrder()
    {
        Assert.Equal(0, (int)ResourceRole.Auditor);
        Assert.Equal(1, (int)ResourceRole.Editor);
        Assert.Equal(2, (int)ResourceRole.Owner);
    }

    [Fact]
    public void Values_Comparison_FollowsPrivilegeOrder()
    {
        // why: handler code that escalates by comparing roles relies on Owner > Editor > Auditor.
        Assert.True(ResourceRole.Owner > ResourceRole.Editor);
        Assert.True(ResourceRole.Editor > ResourceRole.Auditor);
    }
}
