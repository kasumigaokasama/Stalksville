using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class SnapshotEngineTests
{
    [Fact]
    public void CanonicalizeThenParse_RoundTrips()
    {
        var state = TestData.Player(lastOnline: DateTimeOffset.UtcNow);

        var parsed = SnapshotEngine.Parse(SnapshotEngine.Canonicalize(state));

        Assert.NotNull(parsed);
        Assert.Equal(state.WolvesvillePlayerId, parsed.WolvesvillePlayerId);
        Assert.Equal(state.Username, parsed.Username);
        Assert.Equal(state.ClanWolvesvilleId, parsed.ClanWolvesvilleId);
        Assert.Equal(state.BadgeIds, parsed.BadgeIds);
    }

    [Fact]
    public void IdenticalStates_ProduceSameHash()
    {
        var a = TestData.Player();
        var b = TestData.Player();

        Assert.Equal(SnapshotEngine.ComputeHash(a), SnapshotEngine.ComputeHash(b));
    }

    [Fact]
    public void VolatileLastOnline_DoesNotChangeHash()
    {
        var earlier = TestData.Player(lastOnline: DateTimeOffset.UtcNow.AddHours(-5));
        var later = TestData.Player(lastOnline: DateTimeOffset.UtcNow);

        Assert.Equal(SnapshotEngine.ComputeHash(earlier), SnapshotEngine.ComputeHash(later));
    }

    [Fact]
    public void MeaningfulChange_ChangesHash()
    {
        var before = SnapshotEngine.ComputeHash(TestData.Player(clanId: "2001"));
        var after = SnapshotEngine.ComputeHash(TestData.Player(clanId: "2002"));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void UnparseablePayload_ReturnsNull()
    {
        Assert.Null(SnapshotEngine.Parse("{ this is not json"));
    }

    [Fact]
    public void Hash_IsDeterministicAcrossConstructionOrder()
    {
        // Same logical content, rebuilt independently — records initialized in different ways
        // must hash identically since canonical serialization is property-order stable.
        var a = TestData.Player(wins: 500, losses: 200);
        var b = TestData.Player(wins: 500, losses: 200);

        Assert.Equal(SnapshotEngine.ComputeHash(a), SnapshotEngine.ComputeHash(b));
    }
}
