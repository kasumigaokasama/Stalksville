namespace Stalksville.Application.Models;

/// <summary>Per-category cache TTLs (plan §29). Intentionally conservative for local dev.</summary>
public static class CachePolicy
{
    public static readonly TimeSpan PlayerTtl = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan ClanTtl = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ClanSearchTtl = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan RolesTtl = TimeSpan.FromHours(24);

    /// <summary>Boards reset daily at most; a short TTL keeps manual captures fresh.</summary>
    public static readonly TimeSpan HighscoresTtl = TimeSpan.FromMinutes(15);

    /// <summary>Ranked skill moves continuously; align with the highscore board TTL.</summary>
    public static readonly TimeSpan RankedLeaderboardTtl = TimeSpan.FromMinutes(15);

    /// <summary>Seasons last weeks; the season label hardly ever changes.</summary>
    public static readonly TimeSpan RankedSeasonTtl = TimeSpan.FromHours(24);

    /// <summary>Catalogs gain items slowly; names for existing ids are stable upstream.</summary>
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(24);

    public const string PlayerByKeyPrefix = "wv:player:";
    public const string ClanInfoKeyPrefix = "wv:clan:";
    public const string ClanSearchKeyPrefix = "wv:clansearch:";
    public const string ClanMembersKeyPrefix = "wv:clanmembers:";
}
