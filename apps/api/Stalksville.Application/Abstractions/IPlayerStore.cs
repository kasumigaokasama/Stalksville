using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;

namespace Stalksville.Application.Abstractions;

public sealed record ChangeWithPlayer(PlayerChange Change, Player Player);

/// <summary>Persistence port for the player aggregate (players, snapshots, changes).</summary>
public interface IPlayerStore
{
    Task<Player?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Player?> FindByWolvesvilleIdAsync(string wolvesvillePlayerId, CancellationToken cancellationToken = default);

    /// <summary>Creates the player on first observation or updates identity fields; returns the player and whether it was created.</summary>
    Task<(Player Player, bool Created)> UpsertPlayerAsync(NormalizedPlayerState state, DateTimeOffset observedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Player>> SearchLocalAsync(string query, int limit = 25, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Player>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task<PlayerSnapshot?> GetLatestSnapshotAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<PlayerSnapshot> AddSnapshotAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default);

    Task TouchSnapshotAsync(Guid snapshotId, DateTimeOffset observedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerSnapshot>> GetSnapshotsAsync(Guid playerId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Full snapshot history, oldest first — progression charts read the whole series.</summary>
    Task<IReadOnlyList<PlayerSnapshot>> GetSnapshotHistoryAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task AddChangesAsync(IReadOnlyList<PlayerChange> changes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerChange>> GetChangesAsync(Guid playerId, int limit = 100, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeWithPlayer>> GetRecentChangesAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Player>> GetPlayersByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task SetCurrentClanAsync(Guid playerId, Guid? clanId, CancellationToken cancellationToken = default);

    Task<int> CountPlayersAsync(CancellationToken cancellationToken = default);

    Task<int> CountSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<int> CountChangesAsync(CancellationToken cancellationToken = default);

    Task<int> CountChangesForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<int> CountSnapshotsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>Batch playerId → change count (graph annotations).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetChangeCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Batch playerId → snapshot count (graph annotations).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetSnapshotCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyCount>> GetChangeCountsPerDayAsync(int days, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyCount>> GetSnapshotCountsPerDayAsync(int days, CancellationToken cancellationToken = default);
}

public sealed record DailyCount(DateOnly Date, int Count);
