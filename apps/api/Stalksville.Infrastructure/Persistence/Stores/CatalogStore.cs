using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class CatalogStore(StalksvilleDbContext db) : ICatalogStore
{
    public async Task<int> ReplaceAsync(string kind, IReadOnlyList<CatalogItem> items, DateTimeOffset refreshedAt, CancellationToken cancellationToken = default)
    {
        await db.CatalogItems.Where(i => i.Kind == kind).ExecuteDeleteAsync(cancellationToken);

        if (items.Count > 0)
        {
            db.CatalogItems.AddRange(items);
            await db.SaveChangesAsync(cancellationToken);
        }

        return items.Count;
    }

    public async Task<IReadOnlyList<CatalogItem>> ListAsync(string? kind = null, CancellationToken cancellationToken = default)
        => await db.CatalogItems.AsNoTracking()
            .Where(i => kind == null || i.Kind == kind)
            .OrderBy(i => i.Kind)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public async Task<CatalogItem?> FindAsync(string kind, string externalId, CancellationToken cancellationToken = default)
        => await db.CatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Kind == kind && i.ExternalId == externalId, cancellationToken);

    public async Task<int> CountAsync(string kind, CancellationToken cancellationToken = default)
        => await db.CatalogItems.CountAsync(i => i.Kind == kind, cancellationToken);

    public async Task<DateTimeOffset?> GetLastRefreshedAtAsync(CancellationToken cancellationToken = default)
        => await db.CatalogItems.AsNoTracking()
            .OrderByDescending(i => i.RefreshedAt)
            .Select(i => (DateTimeOffset?)i.RefreshedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
