using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class MembershipTrackerTests
{
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid ClanA = Guid.NewGuid();
    private static readonly Guid ClanB = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void NoMemberships_ObservingClan_StartsMembership()
    {
        var transitions = MembershipTracker.Track([], ClanA, Now);

        var transition = Assert.Single(transitions);
        Assert.Equal(ClanA, transition.ClanId);
        Assert.Equal(MembershipAction.Started, transition.Action);
    }

    [Fact]
    public void OpenMembershipInSameClan_NoTransition()
    {
        var open = new[] { TestData.Membership(Player, ClanA, Now.AddDays(-10)) };

        var transitions = MembershipTracker.Track(open, ClanA, Now);

        Assert.Empty(transitions);
    }

    [Fact]
    public void OpenMembershipInOtherClan_EndsOldAndStartsNew()
    {
        var open = new[] { TestData.Membership(Player, ClanA, Now.AddDays(-10)) };

        var transitions = MembershipTracker.Track(open, ClanB, Now);

        Assert.Equal(2, transitions.Count);
        Assert.Contains(transitions, t => t.ClanId == ClanA && t.Action == MembershipAction.Ended);
        Assert.Contains(transitions, t => t.ClanId == ClanB && t.Action == MembershipAction.Started);
    }

    [Fact]
    public void ObservingNoClan_EndsAllOpenMemberships()
    {
        var open = new[]
        {
            TestData.Membership(Player, ClanA, Now.AddDays(-10)),
            TestData.Membership(Player, ClanB, Now.AddDays(-5))
        };

        var transitions = MembershipTracker.Track(open, null, Now);

        Assert.Equal(2, transitions.Count);
        Assert.All(transitions, t => Assert.Equal(MembershipAction.Ended, t.Action));
    }

    [Fact]
    public void EndedMemberships_AreIgnored()
    {
        var history = new[] { TestData.Membership(Player, ClanA, Now.AddDays(-30), Now.AddDays(-10)) };

        var transitions = MembershipTracker.Track(history, ClanA, Now);

        var transition = Assert.Single(transitions);
        Assert.Equal(MembershipAction.Started, transition.Action);
    }
}
