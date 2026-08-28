namespace Stalksville.Domain.Entities;

public static class CatalogKinds
{
    public const string ProfileIcon = "ProfileIcon";
    public const string Badge = "Badge";

    public static readonly IReadOnlyList<string> All = [ProfileIcon, Badge];

    public static string Normalize(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "profileicon" or "profileicons" or "icon" => ProfileIcon,
        "badge" or "badges" => Badge,
        null or "" => "",
        _ => throw new ArgumentException($"catalog kind must be 'profileIcon' or 'badge', got '{kind}'.")
    };
}

/// <summary>
/// Wolvesville cosmetics reference data (observed): short catalog ids resolved to display names.
/// Refreshed from GET /items/profileIcons and GET /items/badges; ids never change meaning upstream.
/// </summary>
public sealed class CatalogItem
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>Upstream short id (3 characters) as it appears in player payloads.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Rarity { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }
}
