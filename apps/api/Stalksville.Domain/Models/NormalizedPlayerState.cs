namespace Stalksville.Domain.Models;

/// <summary>
/// Normalized observed state of a Wolvesville player — the single source for snapshots, diffs and
/// dossiers. Field names mirror the official API where applicable. Volatile fields such as
/// <see cref="LastOnline"/> are recorded but excluded from change detection.
/// </summary>
public sealed record NormalizedPlayerState
{
    public required string WolvesvillePlayerId { get; init; }

    public required string Username { get; init; }

    public string? PersonalMessage { get; init; }

    public int? Level { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? LastOnline { get; init; }

    /// <summary>Wolvesville clan id from the player profile, null when clanless.</summary>
    public string? ClanWolvesvilleId { get; init; }

    public int Wins { get; init; }

    public int Losses { get; init; }

    public int GamesPlayed { get; init; }

    public int? ReceivedRosesCount { get; init; }

    public int? SentRosesCount { get; init; }

    public string? ProfileIconId { get; init; }

    public string? ProfileIconName { get; init; }

    public string? EquippedAvatarId { get; init; }

    public IReadOnlyList<string> BadgeIds { get; init; } = [];

    public IReadOnlyList<string> RoleCardIds { get; init; } = [];

    public int? RankedSeason { get; init; }

    public int? RankedWins { get; init; }

    public int? RankedLosses { get; init; }

    public int? RankedCurrentRating { get; init; }

    public int? RankedPlacementRating { get; init; }

    public int? Achievements { get; init; }

    public int FriendCount { get; init; }

    /// <summary>
    /// Wolvesville player ids from the profile's friendIds payload (observed). Only connections to
    /// other tracked players are materialized into relationships; the raw ids stay in snapshots.
    /// </summary>
    public IReadOnlyList<string> FriendWolvesvilleIds { get; init; } = [];
}
