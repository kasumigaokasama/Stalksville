using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class PlayerStore(StalksvilleDbContext db) : IPlayerStore
{
    public Task<Player?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Player?> FindByWolvesvilleIdAsync(string wolvesvillePlayerId, CancellationToken cancellationToken = default)
        => db.Players.FirstOrDefaultAsync(p => p.WolvesvillePlayerId == wolvesvillePlayerId, cancellationToken);

    public async Task<(Player Player, bool Created)> UpsertPlayerAsync(NormalizedPlayerState state, DateTimeOffset observedAt, CancellationToken cancellationToken = default)
    {
        // An empty Wolvesville id must never match or create a row: it would fuse every
        // unidentified observation into one player (observed with live clan-member imports).
        if (string.IsNullOrWhiteSpace(state.WolvesvillePlayerId))
        {
            throw new InvalidOperationException("Cannot upsert a player observation without a Wolvesville player id.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.WolvesvillePlayerId == state.WolvesvillePlayerId, cancellationToken);
        bool created;

        if (player is null)
        {
            player = new Player
            {
                Id = Guid.NewGuid(),
                WolvesvillePlayerId = state.WolvesvillePlayerId,
                Username = state.Username,
                UsernameLower = state.Username.ToLowerInvariant(),
                FirstSeenAt = observedAt,
                LastSeenAt = observedAt
            };
            db.Players.Add(player);
            created = true;
        }
        else
        {
            player.Username = state.Username;
            player.UsernameLower = state.Username.ToLowerInvariant();
            player.LastSeenAt = observedAt;
            created = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (player, created);
    }

    public async Task<IReadOnlyList<Player>> SearchLocalAsync(string query, int limit = 25, CancellationToken cancellationToken = default)
    {
        var lowered = query.Trim().ToLowerInvariant();
        return await db.Players
            .Where(p => p.UsernameLower.Contains(lowered))
            .OrderByDescending(p => p.LastSeenAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Player>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default)
        => await db.Players
            .OrderByDescending(p => p.LastSeenAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<PlayerSnapshot?> GetLatestSnapshotAsync(Guid playerId, CancellationToken cancellationToken = default)
        => await db.PlayerSnapshots
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PlayerSnapshot> AddSnapshotAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        db.PlayerSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task TouchSnapshotAsync(Guid snapshotId, DateTimeOffset observedAt, CancellationToken cancellationToken = default)
    {
        // Tracked update (not ExecuteUpdate) so the in-memory snapshot instance — often already
        // loaded in this scope — stays consistent with the database within the same request.
        var snapshot = await db.PlayerSnapshots.FirstAsync(s => s.Id == snapshotId, cancellationToken);
        snapshot.LastObservedAt = observedAt;
        snapshot.ObservationCount += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerSnapshot>> GetSnapshotsAsync(Guid playerId, int limit = 50, CancellationToken cancellationToken = default)
        => await db.PlayerSnapshots
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.CapturedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>Full snapshot history, oldest first — progression charts read the whole series.</summary>
    public async Task<IReadOnlyList<PlayerSnapshot>> GetSnapshotHistoryAsync(Guid playerId, CancellationToken cancellationToken = default)
        => await db.PlayerSnapshots
            .Where(s => s.PlayerId == playerId)
            .OrderBy(s => s.CapturedAt)
            .ToListAsync(cancellationToken);

    public async Task AddChangesAsync(IReadOnlyList<PlayerChange> changes, CancellationToken cancellationToken = default)
    {
        db.PlayerChanges.AddRange(changes);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerChange>> GetChangesAsync(Guid playerId, int limit = 100, CancellationToken cancellationToken = default)
        => await db.PlayerChanges
            .Where(c => c.PlayerId == playerId)
            .OrderByDescending(c => c.DetectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChangeWithPlayer>> GetRecentChangesAsync(int limit = 10, CancellationToken cancellationToken = default)
        => await db.PlayerChanges
            .Include(c => c.Player)
            .OrderByDescending(c => c.DetectedAt)
            .Take(limit)
            .Select(c => new ChangeWithPlayer(c, c.Player!))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Player>> GetPlayersByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => await db.Players.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public async Task SetCurrentClanAsync(Guid playerId, Guid? clanId, CancellationToken cancellationToken = default)
    {
        var player = await db.Players.FirstAsync(p => p.Id == playerId, cancellationToken);
        player.CurrentClanId = clanId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountPlayersAsync(CancellationToken cancellationToken = default)
        => db.Players.CountAsync(cancellationToken);

    public Task<int> CountSnapshotsAsync(CancellationToken cancellationToken = default)
        => db.PlayerSnapshots.CountAsync(cancellationToken);

    public Task<int> CountChangesAsync(CancellationToken cancellationToken = default)
        => db.PlayerChanges.CountAsync(cancellationToken);

    public Task<int> CountChangesForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
        => db.PlayerChanges.CountAsync(c => c.PlayerId == playerId, cancellationToken);

    public Task<int> CountSnapshotsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
        => db.PlayerSnapshots.CountAsync(s => s.PlayerId == playerId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetChangeCountsAsync(CancellationToken cancellationToken = default)
        => await db.PlayerChanges
            .GroupBy(c => c.PlayerId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetSnapshotCountsAsync(CancellationToken cancellationToken = default)
        => await db.PlayerSnapshots
            .GroupBy(s => s.PlayerId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<DailyCount>> GetChangeCountsPerDayAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var rows = await db.PlayerChanges
            .Where(c => c.DetectedAt >= since)
            .GroupBy(c => DateOnly.FromDateTime(c.DetectedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new DailyCount(x.Date, x.Count)).ToList();
    }

    public async Task<IReadOnlyList<DailyCount>> GetSnapshotCountsPerDayAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var rows = await db.PlayerSnapshots
            .Where(s => s.CapturedAt >= since)
            .GroupBy(s => DateOnly.FromDateTime(s.CapturedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new DailyCount(x.Date, x.Count)).ToList();
    }
}
