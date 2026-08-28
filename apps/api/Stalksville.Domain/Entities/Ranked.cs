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
