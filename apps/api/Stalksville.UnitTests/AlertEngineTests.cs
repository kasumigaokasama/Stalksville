using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class AlertEngineTests
{
    private static readonly Guid PlayerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly DateTimeOffset At = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static PlayerChange Change(string field, string? old, string? now) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = PlayerId,
        ToSnapshotId = Guid.NewGuid(),
        Field = field,
        Kind = PlayerChangeKind.Scalar,
        OldValue = old,
        NewValue = now,
        DetectedAt = At
    };

    [Fact]
    public void ClanChange_RaisesNoticeAlertWithEvidence()
    {
        var change = Change("clanId", "2001", "2002");

        var alerts = AlertEngine.Evaluate(PlayerId, "flex", [change], At);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKinds.ClanChanged, alert.Kind);
        Assert.Equal(AlertSeverity.Notice, alert.Severity);
        Assert.Contains("flex", alert.Title);
        Assert.Contains("2001", alert.Body);
        Assert.Contains("2002", alert.Body);
        Assert.Contains(change.Id.ToString(), alert.EvidenceJson);
        Assert.Equal($"ClanChanged:{PlayerId}:{change.ToSnapshotId}", alert.DedupeKey);
    }

    [Fact]
    public void UsernameChange_RaisesWarningAlert()
    {
        var alerts = AlertEngine.Evaluate(PlayerId, "newname", [Change("username", "oldname", "newname")], At);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKinds.UsernameChanged, alert.Kind);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void LevelJump_AtOrAboveThreshold_Alerts()
    {
        var alerts = AlertEngine.Evaluate(PlayerId, "flex", [Change("level", "40", "50")], At);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKinds.LevelJump, alert.Kind);
        Assert.Contains("+10", alert.Body);
    }

    [Fact]
    public void LevelDrift_BelowThreshold_DoesNotAlert()
    {
        var alerts = AlertEngine.Evaluate(PlayerId, "flex", [Change("level", "40", "45")], At);

        Assert.Empty(alerts);
    }

    [Fact]
    public void LevelDrop_BelowThresholdNegatively_AlertsWhenLarge()
    {
        var alerts = AlertEngine.Evaluate(PlayerId, "flex", [Change("level", "180", "37")], At);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKinds.LevelJump, alert.Kind);
        Assert.Contains("-143", alert.Body);
    }

    [Fact]
    public void OrdinaryDriftFields_NeverAlert()
    {
        var changes = new[]
        {
            Change("wins", "400", "410"),
            Change("personalMessage", null, "hi"),
            Change("badgeIds", "badge_a", null),
            Change("rankedCurrentRating", "1500", "1512")
        };

        var alerts = AlertEngine.Evaluate(PlayerId, "flex", changes, At);

        Assert.Empty(alerts);
    }

    [Fact]
    public void SameChangeBatch_SharesDedupeKey()
    {
        var change = Change("clanId", "2001", "2002");
        var first = AlertEngine.Evaluate(PlayerId, "flex", [change], At);
        var replay = AlertEngine.Evaluate(PlayerId, "flex", [change], At.AddHours(2));

        // Re-evaluating the same change (ingestion retry) maps to the same key — idempotent.
        Assert.Equal(first[0].DedupeKey, replay[0].DedupeKey);
    }

    [Fact]
    public void DistinctChanges_GetDifferentDedupeKeys()
    {
        var first = AlertEngine.Evaluate(PlayerId, "flex", [Change("clanId", "2001", "2002")], At);
        var second = AlertEngine.Evaluate(PlayerId, "flex", [Change("clanId", "2002", "2001")], At.AddHours(2));

        // A→B→A is two real transitions: both alert, each with its own key.
        Assert.NotEqual(first[0].DedupeKey, second[0].DedupeKey);
    }

    [Fact]
    public void CustomThreshold_IsRespected()
    {
        var alerts = AlertEngine.Evaluate(PlayerId, "flex", [Change("level", "40", "45")], At, levelJumpThreshold: 5);

        Assert.Single(alerts);
    }
}
