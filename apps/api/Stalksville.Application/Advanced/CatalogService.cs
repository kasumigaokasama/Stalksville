using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Wolvesville cosmetics catalogs (observed reference data): GET /items/profileIcons and
/// GET /items/badges resolved into short-id → display-name rows so evidence and dossiers can
/// show names instead of opaque 3-character codes.
/// </summary>
public sealed class CatalogService(
    IWolvesvilleClient wolvesville,
    ICatalogStore catalog,
    ILogger<CatalogService> logger)
{
    public async Task<(int Icons, int Badges)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var at = DateTimeOffset.UtcNow;

        var icons = await wolvesville.GetProfileIconsAsync(cancellationToken: cancellationToken);
        var storedIcons = await catalog.ReplaceAsync(
            CatalogKinds.ProfileIcon,
            [.. icons.Select(i => ToItem(CatalogKinds.ProfileIcon, i, at))],
            at,
            cancellationToken);

        var badges = await wolvesville.GetBadgesAsync(cancellationToken: cancellationToken);
        var storedBadges = await catalog.ReplaceAsync(
            CatalogKinds.Badge,
            [.. badges.Select(b => ToItem(CatalogKinds.Badge, b, at))],
            at,
            cancellationToken);

        logger.LogInformation("Catalog refresh: {Icons} profile icons, {Badges} badges", storedIcons, storedBadges);
        return (storedIcons, storedBadges);
    }

    /// <summary>The catalog for UI consumption — lazily loads on first call after a fresh install.</summary>
    public async Task<IReadOnlyList<CatalogItemDto>> GetItemsAsync(string? kind, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrEmpty(kind) ? null : CatalogKinds.Normalize(kind);

        if (await IsEmptyAsync(normalized, cancellationToken))
        {
            try
            {
                await RefreshAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // An unreachable upstream must not 500 the catalog endpoint — ids render raw.
                logger.LogWarning(ex, "Catalog lazy-refresh failed; returning what is stored");
            }
        }

        return (await catalog.ListAsync(normalized, cancellationToken))
            .Select(i => new CatalogItemDto(i.Kind, i.ExternalId, i.Name, i.Rarity, i.Description, i.ImageUrl))
            .ToList();
    }

    /// <summary>Resolves one cosmetics id to its display name; null when unknown.</summary>
    public async Task<string?> ResolveNameAsync(string kind, string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        return (await catalog.FindAsync(kind, externalId, cancellationToken))?.Name;
    }

    private async Task<bool> IsEmptyAsync(string? kind, CancellationToken cancellationToken)
        => kind is null
            ? await catalog.CountAsync(CatalogKinds.ProfileIcon, cancellationToken) == 0
              && await catalog.CountAsync(CatalogKinds.Badge, cancellationToken) == 0
            : await catalog.CountAsync(kind, cancellationToken) == 0;

    private static CatalogItem ToItem(string kind, ObservedCatalogItem item, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        ExternalId = item.ExternalId,
        Name = item.Name,
        Rarity = item.Rarity,
        Description = item.Description,
        ImageUrl = item.ImageUrl,
        RefreshedAt = at
    };
}
