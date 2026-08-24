namespace Stalksville.Application.Models;

/// <summary>Per-category cache TTLs (plan §29). Intentionally conservative for local dev.</summary>
public static class CachePolicy
{
    public static readonly TimeSpan PlayerTtl = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan ClanTtl = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ClanSearchTtl = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan RolesTtl = TimeSpan.FromHours(24);

    public const string PlayerByKeyPrefix = "wv:player:";
    public const string ClanInfoKeyPrefix = "wv:clan:";
    public const string ClanSearchKeyPrefix = "wv:clansearch:";
    public const string ClanMembersKeyPrefix = "wv:clanmembers:";
}
