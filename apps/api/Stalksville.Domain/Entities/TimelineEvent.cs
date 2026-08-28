namespace Stalksville.Domain.Entities;

public static class TimelineEventTypes
{
    public const string PlayerDiscovered = "PlayerDiscovered";
    public const string PlayerReobserved = "PlayerReobserved";
    public const string ClanChanged = "ClanChanged";
    public const string LevelChanged = "LevelChanged";
    public const string ProfileChanged = "ProfileChanged";
    public const string CosmeticsChanged = "CosmeticsChanged";
    public const string RankStateChanged = "RankStateChanged";
    public const string ClanImported = "ClanImported";
    public const string MembershipStarted = "MembershipStarted";
    public const string MembershipEnded = "MembershipEnded";
    public const string HighscoreRankChanged = "HighscoreRankChanged";
    public const string RankedRankChanged = "RankedRankChanged";
    public const string FriendshipChanged = "FriendshipChanged";
    public const string ExposureShifted = "ExposureShifted";
}

/// <summary>
/// Every significant observation or derivation becomes a timeline event. Events that Stalksville
/// calculated (rather than observed directly) set <see cref="IsDerived"/>.
/// </summary>
public sealed class TimelineEvent
{
    public Guid Id { get; set; }

    public EntityType EntityType { get; set; }

    public Guid EntityId { get; set; }

    public required string EventType { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>JSON (jsonb) with event details.</summary>
    public string? Metadata { get; set; }

    /// <summary>0..1 for derived events; null for directly observed ones.</summary>
    public double? Confidence { get; set; }

    public bool IsDerived { get; set; }
}
