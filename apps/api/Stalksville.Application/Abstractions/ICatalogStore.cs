using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for Wolvesville cosmetics reference data (observed).</summary>
public interface ICatalogStore
{
    /// <summary>Replaces all items of one kind with the upstream catalog; returns the stored count.</summary>
    Task<int> ReplaceAsync(string kind, IReadOnlyList<CatalogItem> items, DateTimeOffset refreshedAt, CancellationToken cancellationToken = default);

    /// <summary>All catalog items, optionally filtered by kind.</summary>
    Task<IReadOnlyList<CatalogItem>> ListAsync(string? kind = null, CancellationToken cancellationToken = default);

    /// <summary>Resolves one id to its catalog item; null when unknown or not yet refreshed.</summary>
    Task<CatalogItem?> FindAsync(string kind, string externalId, CancellationToken cancellationToken = default);

    /// <summary>How many items of a kind are stored (0 = never refreshed).</summary>
    Task<int> CountAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>When any catalog kind was last refreshed; null when never.</summary>
    Task<DateTimeOffset?> GetLastRefreshedAtAsync(CancellationToken cancellationToken = default);
}
