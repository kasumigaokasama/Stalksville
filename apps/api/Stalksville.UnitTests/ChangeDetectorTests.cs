using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class ChangeDetectorTests
{
    [Fact]
    public void IdenticalStates_ProduceNoChanges()
    {
        var previous = TestData.Player();
        var current = TestData.Player();

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Empty(changes);
    }

    [Fact]
    public void VolatileLastOnline_IsIgnored()
    {
        var previous = TestData.Player(lastOnline: DateTimeOffset.UtcNow.AddHours(-2));
        var current = TestData.Player(lastOnline: DateTimeOffset.UtcNow);

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Empty(changes);
    }

    [Fact]
    public void ClanChange_IsDetectedAsScalar()
    {
        var changes = ChangeDetector.Detect(TestData.Player(clanId: "2001"), TestData.Player(clanId: "2002"));

        var clanChange = Assert.Single(changes, c => c.Field == "clanId");
        Assert.Equal(PlayerChangeKind.Scalar, clanChange.Kind);
        Assert.Equal("2001", clanChange.OldValue);
        Assert.Equal("2002", clanChange.NewValue);
    }

    [Fact]
    public void LeavingClan_DetectsChangeToNull()
    {
        var changes = ChangeDetector.Detect(TestData.Player(clanId: "2001"), TestData.Player(clanId: null));

        var clanChange = Assert.Single(changes, c => c.Field == "clanId");
        Assert.Equal("2001", clanChange.OldValue);
        Assert.Null(clanChange.NewValue);
    }

    [Fact]
    public void LevelChange_IsDetected()
    {
        var changes = ChangeDetector.Detect(TestData.Player(level: 80), TestData.Player(level: 86));

        var levelChange = Assert.Single(changes, c => c.Field == "level");
        Assert.Equal("80", levelChange.OldValue);
        Assert.Equal("86", levelChange.NewValue);
    }

    [Fact]
    public void BadgeAdditionsAndRemovals_AreDetectedSeparately()
    {
        var previous = TestData.Player(badges: ["badge_a", "badge_b"]);
        var current = TestData.Player(badges: ["badge_b", "badge_c"]);

        var changes = ChangeDetector.Detect(previous, current);

        var added = Assert.Single(changes, c => c.Field == "badgeIds" && c.Kind == PlayerChangeKind.SetAddition);
        Assert.Equal("badge_c", added.NewValue);
        var removed = Assert.Single(changes, c => c.Field == "badgeIds" && c.Kind == PlayerChangeKind.SetRemoval);
        Assert.Equal("badge_a", removed.OldValue);
    }

    [Fact]
    public void PersonalMessageAppearing_IsDetected()
    {
        var changes = ChangeDetector.Detect(TestData.Player(personalMessage: null), TestData.Player(personalMessage: "hi"));

        var messageChange = Assert.Single(changes, c => c.Field == "personalMessage");
        Assert.Null(messageChange.OldValue);
        Assert.Equal("hi", messageChange.NewValue);
    }

    [Fact]
    public void Classification_MapsClanAndLevelChanges()
    {
        var previous = TestData.Player(clanId: "2001", level: 80);
        var current = TestData.Player(clanId: "2002", level: 81);

        var classifications = ChangeDetector.Classify(ChangeDetector.Detect(previous, current).ToList());

        Assert.Contains(TimelineEventTypes.ClanChanged, classifications);
        Assert.Contains(TimelineEventTypes.LevelChanged, classifications);
        Assert.DoesNotContain(TimelineEventTypes.CosmeticsChanged, classifications);
    }

    [Fact]
    public void CosmeticChanges_ClassifyAccordingly()
    {
        var previous = TestData.Player(badges: ["a"]);
        var current = TestData.Player(badges: ["a", "b"]);

        var classifications = ChangeDetector.Classify(ChangeDetector.Detect(previous, current).ToList());

        Assert.Contains(TimelineEventTypes.CosmeticsChanged, classifications);
        Assert.DoesNotContain(TimelineEventTypes.ClanChanged, classifications);
    }

    [Fact]
    public void FriendAdditionsAndRemovals_AreDetectedAsSetChanges()
    {
        var previous = TestData.Player() with { FriendWolvesvilleIds = ["2001", "2002"] };
        var current = TestData.Player() with { FriendWolvesvilleIds = ["2002", "2003"] };

        var changes = ChangeDetector.Detect(previous, current).ToList();

        var added = Assert.Single(changes, c => c.Field == "friendIds" && c.Kind == PlayerChangeKind.SetAddition);
        Assert.Null(added.OldValue);
        Assert.Equal("2003", added.NewValue);

        var removed = Assert.Single(changes, c => c.Field == "friendIds" && c.Kind == PlayerChangeKind.SetRemoval);
        Assert.Equal("2001", removed.OldValue);
        Assert.Null(removed.NewValue);

        var classifications = ChangeDetector.Classify(changes);
        Assert.Contains(TimelineEventTypes.FriendshipChanged, classifications);
        Assert.DoesNotContain(TimelineEventTypes.CosmeticsChanged, classifications);
    }
}
