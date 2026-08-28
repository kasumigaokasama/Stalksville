using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stalksville.Infrastructure.Wolvesville;

/// <summary>
/// Minimal typed view of the Wolvesville player profile. Unknown/extra fields are ignored so
/// upstream additions never break normalization.
/// </summary>
internal sealed class PlayerProfileDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Clan member payloads identify the player as "playerId" instead of "id".</summary>
    [JsonPropertyName("playerId")]
    public string? PlayerId { get; set; }

    /// <summary>Clan member presence field; full profiles call this "status".</summary>
    [JsonPropertyName("playerStatus")]
    public string? PlayerStatus { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("personalMessage")]
    public string? PersonalMessage { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("lastOnline")]
    public DateTimeOffset? LastOnline { get; set; }

    [JsonPropertyName("clanId")]
    public string? ClanId { get; set; }

    // Spec Player: ranked data is top-level (there is no rankedStats object).
    [JsonPropertyName("rankedSeasonSkill")]
    public int? RankedSeasonSkill { get; set; }

    [JsonPropertyName("rankedSeasonPlayedCount")]
    public int? RankedSeasonPlayedCount { get; set; }

    // Spec Player: the icon is a top-level id, not a nested object.
    [JsonPropertyName("profileIconId")]
    public string? ProfileIconId { get; set; }

    [JsonPropertyName("receivedRosesCount")]
    public int? ReceivedRosesCount { get; set; }

    [JsonPropertyName("sentRosesCount")]
    public int? SentRosesCount { get; set; }

    [JsonPropertyName("equippedAvatar")]
    public AvatarDto? EquippedAvatar { get; set; }

    [JsonPropertyName("badgeIds")]
    public List<string>? BadgeIds { get; set; }

    [JsonPropertyName("roleCards")]
    public List<RoleCardDto>? RoleCards { get; set; }

    [JsonPropertyName("gameStats")]
    public GameStatsDto? GameStats { get; set; }

    [JsonPropertyName("friendIds")]
    public List<string>? FriendIds { get; set; }
}

internal sealed class AvatarDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class RoleCardDto
{
    [JsonPropertyName("roleId1")]
    public string? RoleId1 { get; set; }
}

/// <summary>Spec PlayerGameStats: per-outcome win/lose counters plus a role-achievement array.</summary>
internal sealed class GameStatsDto
{
    [JsonPropertyName("totalWinCount")]
    public int? TotalWinCount { get; set; }

    [JsonPropertyName("totalLoseCount")]
    public int? TotalLoseCount { get; set; }

    [JsonPropertyName("totalTieCount")]
    public int? TotalTieCount { get; set; }

    [JsonPropertyName("achievements")]
    public List<JsonElement>? Achievements { get; set; }

    public int GamesPlayedTotal => (TotalWinCount ?? 0) + (TotalLoseCount ?? 0) + (TotalTieCount ?? 0);
}

/// <summary>Clan payload returned by /clans/search and /clans/{clanId}/info.</summary>
internal sealed class ClanDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("memberCount")]
    public int? MemberCount { get; set; }

    [JsonPropertyName("languageCode")]
    public string? LanguageCode { get; set; }

    [JsonPropertyName("joinType")]
    public string? JoinType { get; set; }

    [JsonPropertyName("xps")]
    public long? Xps { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("leaderId")]
    public string? LeaderId { get; set; }

    [JsonPropertyName("memberIds")]
    public List<string>? MemberIds { get; set; }
}

/// <summary>Spec schema PlayerRank — one row of a highscore board.</summary>
internal sealed class HighscoreRankDto
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("xp")]
    public long Xp { get; set; }
}

/// <summary>Spec schema HighScore — GET /players/highscores.</summary>
internal sealed class HighscoresDto
{
    [JsonPropertyName("allTime")]
    public List<HighscoreRankDto> AllTime { get; set; } = [];

    [JsonPropertyName("monthly")]
    public List<HighscoreRankDto> Monthly { get; set; } = [];

    [JsonPropertyName("weekly")]
    public List<HighscoreRankDto> Weekly { get; set; } = [];

    [JsonPropertyName("daily")]
    public List<HighscoreRankDto> Daily { get; set; } = [];
}

/// <summary>Spec schema RankedLeaderboardPlayer — one row of the ranked board (rank is array order).</summary>
internal sealed class RankedLeaderboardPlayerDto
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("skill")]
    public int Skill { get; set; }
}

/// <summary>Spec schema Leaderboard — GET /ranked/leaderboard. Only ranksTop is captured.</summary>
internal sealed class LeaderboardDto
{
    [JsonPropertyName("ranksTop")]
    public List<RankedLeaderboardPlayerDto> RanksTop { get; set; } = [];

    [JsonPropertyName("ranksPlayer")]
    public List<RankedLeaderboardPlayerDto> RanksPlayer { get; set; } = [];
}

/// <summary>Spec schema Season (subset) — nested in RankedSeasonInfo.</summary>
internal sealed class SeasonDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTimeOffset EndTime { get; set; }

    [JsonPropertyName("finished")]
    public bool Finished { get; set; }
}

/// <summary>Spec schema RankedSeasonInfo (subset) — GET /ranked/season.</summary>
internal sealed class RankedSeasonInfoDto
{
    [JsonPropertyName("season")]
    public SeasonDto Season { get; set; } = new();

    [JsonPropertyName("startSkillDefault")]
    public int? StartSkillDefault { get; set; }
}

/// <summary>Spec schema ProfileIcon — GET /items/profileIcons.</summary>
internal sealed class ProfileIconDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("costInGold")]
    public int? CostInGold { get; set; }

    [JsonPropertyName("costInRoses")]
    public int? CostInRoses { get; set; }
}

/// <summary>Spec schema Badge — GET /items/badges.</summary>
internal sealed class BadgeDto
{
    [JsonPropertyName("badgeId")]
    public string BadgeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
