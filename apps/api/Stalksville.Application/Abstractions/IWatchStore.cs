using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>A watchlist row joined with the watched player.</summary>
public sealed record WatchedPlayer(WatchEntry Entry, Player Player);

/// <summary>Persistence port for per-user watchlists (starred players).</summary>
public interface IWatchStore
{
    /// <summary>Stars a player for a user; false when it was already starred.</summary>
    Task<bool> AddAsync(Guid userId, Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>Unstars; false when the pair was not starred.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>The user's starred players, newest star first.</summary>
    Task<IReadOnlyList<WatchedPlayer>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Player ids starred by any user — the worker refreshes these first.</summary>
    Task<IReadOnlyList<Guid>> GetWatchedPlayerIdsAsync(CancellationToken cancellationToken = default);
}
