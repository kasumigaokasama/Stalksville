namespace Stalksville.Domain.Entities;

/// <summary>
/// A player tracked by Stalksville. <see cref="WolvesvillePlayerId"/> is the id used by the
/// Wolvesville API and is kept separate from the Stalksville <see cref="Id"/> so further data
/// sources can be attached later.
/// </summary>
public sealed class Player
{
    public Guid Id { get; set; }

    public required string WolvesvillePlayerId { get; set; }

    public required string Username { get; set; }

    /// <summary>Lowercased username for case-insensitive local search.</summary>
    public required string UsernameLower { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Clan (by Stalksville id) the player was last observed in, if that clan is known.</summary>
    public Guid? CurrentClanId { get; set; }

    public Clan? CurrentClan { get; set; }

    public List<PlayerSnapshot> Snapshots { get; set; } = [];

    public List<PlayerChange> Changes { get; set; } = [];
}

/// <summary>
/// Normalized observed state of a player at a point in time. Smart-snapshotted: re-observing an
/// unchanged state updates <see cref="LastObservedAt"/>/<see cref="ObservationCount"/> instead of
/// inserting a duplicate row.
/// </summary>
public sealed class PlayerSnapshot
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    public DateTimeOffset LastObservedAt { get; set; }

    public int ObservationCount { get; set; } = 1;

    /// <summary>SHA-256 of the canonical serialization of <see cref="Payload"/>.</summary>
    public required string PayloadHash { get; set; }

    /// <summary>Canonical JSON (jsonb) of the normalized observed state.</summary>
    public required string Payload { get; set; }

    /// <summary>Provenance, e.g. "wolvesville:GET /players/{playerId}".</summary>
    public required string Source { get; set; }
}

public enum PlayerChangeKind
{
    Scalar = 0,
    SetAddition = 1,
    SetRemoval = 2
}

/// <summary>A field-level difference between two snapshots. Derived intelligence, always evidenced.</summary>
public sealed class PlayerChange
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public Guid? FromSnapshotId { get; set; }

    public required Guid ToSnapshotId { get; set; }

    /// <summary>Changed field, e.g. "clanId", "level", "badgeIds".</summary>
    public required string Field { get; set; }

    public PlayerChangeKind Kind { get; set; }

    /// <summary>JSON-encoded previous value (null when a value appeared).</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON-encoded new value (null when a value disappeared).</summary>
    public string? NewValue { get; set; }

    public DateTimeOffset DetectedAt { get; set; }
}
