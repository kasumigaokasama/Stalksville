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

    [JsonPropertyName("wins")]
    public int? Wins { get; set; }

    [JsonPropertyName("losses")]
    public int? Losses { get; set; }

    [JsonPropertyName("gamesPlayed")]
    public int? GamesPlayed { get; set; }

    [JsonPropertyName("receivedRosesCount")]
    public int? ReceivedRosesCount { get; set; }

    [JsonPropertyName("sentRosesCount")]
    public int? SentRosesCount { get; set; }

    [JsonPropertyName("profileIcon")]
    public ProfileIconDto? ProfileIcon { get; set; }

    [JsonPropertyName("equippedAvatar")]
    public AvatarDto? EquippedAvatar { get; set; }

    [JsonPropertyName("badgeIds")]
    public List<string>? BadgeIds { get; set; }

    [JsonPropertyName("roleCards")]
    public List<RoleCardDto>? RoleCards { get; set; }

    [JsonPropertyName("rankedStats")]
    public RankedStatsDto? RankedStats { get; set; }

    [JsonPropertyName("gameStats")]
    public GameStatsDto? GameStats { get; set; }

    [JsonPropertyName("friendIds")]
    public List<string>? FriendIds { get; set; }
}

internal sealed class ProfileIconDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rarity")]
    public string? Rarity { get; set; }
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
    [JsonPropertyName("roleId")]
    public string RoleId { get; set; } = string.Empty;
}

internal sealed class RankedStatsDto
{
    [JsonPropertyName("seasonNumber")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("wins")]
    public int? Wins { get; set; }

    [JsonPropertyName("losses")]
    public int? Losses { get; set; }

    [JsonPropertyName("currentRating")]
    public int? CurrentRating { get; set; }

    [JsonPropertyName("placementRating")]
    public int? PlacementRating { get; set; }
}

internal sealed class GameStatsDto
{
    [JsonPropertyName("wins")]
    public int? Wins { get; set; }

    [JsonPropertyName("losses")]
    public int? Losses { get; set; }

    [JsonPropertyName("achievements")]
    public int? Achievements { get; set; }

    [JsonPropertyName("gamesPlayed")]
    public int? GamesPlayed { get; set; }
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
