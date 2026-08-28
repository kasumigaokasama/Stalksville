namespace Stalksville.Domain.Entities;

/// <summary>
/// One row of a ranked leaderboard capture (observed, append-only). Rank is the array position
/// of the upstream board — the API does not return an explicit rank field.
/// </summary>
public sealed class RankedEntry
{
    public Guid Id { get; set; }

    public int SeasonNumber { get; set; }

    public int Rank { get; set; }

    public string WolvesvillePlayerId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string UsernameLower { get; set; } = string.Empty;

    public int Skill { get; set; }

    /// <summary>Resolved tracked player (by username), so the board can deep-link dossiers.</summary>
    public Guid? PlayerId { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
}

/// <summary>
/// One winner of a finished ranked season (observed, append-only per season). Position is the
/// array order of the upstream winners list — the API returns no explicit rank.
/// </summary>
public sealed class HallOfFameEntry
{
    public Guid Id { get; set; }

    public int SeasonNumber { get; set; }

    public int Position { get; set; }

    public string WolvesvillePlayerId { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public string PlayerNameLower { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    /// <summary>Resolved tracked player (by name), so winners can deep-link dossiers.</summary>
    public Guid? PlayerId { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
}
