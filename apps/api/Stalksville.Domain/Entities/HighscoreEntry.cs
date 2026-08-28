namespace Stalksville.Domain.Entities;

public static class HighscorePeriods
{
    public const string AllTime = "alltime";
    public const string Monthly = "monthly";
    public const string Weekly = "weekly";
    public const string Daily = "daily";

    public static readonly IReadOnlyList<string> All = [AllTime, Monthly, Weekly, Daily];
}

/// <summary>
/// One row of a Wolvesville highscore board capture (observed data). Captures are append-only;
/// rank-change intelligence is derived by diffing consecutive captures of the same period.
/// </summary>
public sealed class HighscoreEntry
{
    public Guid Id { get; set; }

    /// <summary>alltime / monthly / weekly / daily.</summary>
    public required string Period { get; set; }

    /// <summary>1-based rank inside the capture.</summary>
    public int Rank { get; set; }

    public required string WolvesvillePlayerId { get; set; }

    public required string Username { get; set; }

    public required string UsernameLower { get; set; }

    public long Xp { get; set; }

    /// <summary>Tracked player row when the username matches one (resolved at capture time).</summary>
    public Guid? PlayerId { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
}
