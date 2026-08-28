using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class WatchStore(StalksvilleDbContext db) : IWatchStore
{
    public async Task<bool> AddAsync(Guid userId, Guid playerId, CancellationToken cancellationToken = default)
    {
        if (await db.Watchlist.AnyAsync(w => w.UserId == userId && w.PlayerId == playerId, cancellationToken))
        {
            return false;
        }

        db.Watchlist.Add(new WatchEntry { Id = Guid.NewGuid(), UserId = userId, PlayerId = playerId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid playerId, CancellationToken cancellationToken = default)
    {
        var removed = await db.Watchlist
            .Where(w => w.UserId == userId && w.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }

    public async Task<IReadOnlyList<WatchedPlayer>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.Watchlist.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Join(db.Players, w => w.PlayerId, p => p.Id, (w, p) => new WatchedPlayer(w, p))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetWatchedPlayerIdsAsync(CancellationToken cancellationToken = default)
        => await db.Watchlist.AsNoTracking()
            .Select(w => w.PlayerId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
