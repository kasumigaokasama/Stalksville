using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;

namespace Stalksville.Application.Models;

/// <summary>A single observation fetched from Wolvesville: raw payload, normalized state, provenance.</summary>
public sealed record PlayerObservation(
    string RawJson,
    NormalizedPlayerState State,
    string Source);

/// <summary>Clan data as observed from Wolvesville (search results or clan info).</summary>
public sealed record ObservedClan(
    string WolvesvilleClanId,
    string Name,
    string? Description,
    int? MemberCount,
    string? LanguageCode,
    string? JoinType,
    long? Xps,
    int? Level,
    string? LeaderWolvesvillePlayerId,
    IReadOnlyList<string> MemberWolvesvillePlayerIds,
    string Source);

/// <summary>One entry of a highscore board (spec schema PlayerRank).</summary>
public sealed record ObservedHighscoreRank(
    string WolvesvillePlayerId,
    string Username,
    long Xp);

/// <summary>A full highscore capture: spec schema HighScore with its four period boards.</summary>
public sealed record ObservedHighscores(
    IReadOnlyList<ObservedHighscoreRank> AllTime,
    IReadOnlyList<ObservedHighscoreRank> Monthly,
    IReadOnlyList<ObservedHighscoreRank> Weekly,
    IReadOnlyList<ObservedHighscoreRank> Daily,
    string Source)
{
    public IReadOnlyList<ObservedHighscoreRank> ForPeriod(string period) => period switch
    {
        HighscorePeriods.AllTime => AllTime,
        HighscorePeriods.Monthly => Monthly,
        HighscorePeriods.Weekly => Weekly,
        HighscorePeriods.Daily => Daily,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "unknown highscore period"),
    };
}

/// <summary>One row of the ranked leaderboard (spec RankedLeaderboardPlayer); rank is array order.</summary>
public sealed record ObservedRankedEntry(
    string WolvesvillePlayerId,
    string Username,
    int Skill);

/// <summary>Spec Leaderboard: the ranksTop board (ranksPlayer around the caller is not captured).</summary>
public sealed record ObservedRankedLeaderboard(
    IReadOnlyList<ObservedRankedEntry> Top,
    string Source);

/// <summary>One winner of a finished season (spec SeasonWinner); position is array order.</summary>
public sealed record ObservedHallOfFameWinner(
    string WolvesvillePlayerId,
    string PlayerName,
    string? AvatarUrl);

/// <summary>Spec schema SeasonWinners — GET /ranked/hallOfFame/{seasonNumber}.</summary>
public sealed record ObservedHallOfFame(
    int SeasonNumber,
    IReadOnlyList<ObservedHallOfFameWinner> Winners,
    string Source);

/// <summary>Season subset of spec RankedSeasonInfo — enough to label captures and show context.</summary>
public sealed record ObservedRankedSeason(
    int Number,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool Finished,
    int StartSkillDefault,
    string Source);

/// <summary>Spec ProfileIcon / Badge flattened into one catalog item shape.</summary>
public sealed record ObservedCatalogItem(
    string ExternalId,
    string Name,
    string Rarity,
    string? Description,
    string? ImageUrl);
