using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for clans and clan memberships.</summary>
public interface IClanStore
{
    Task<Clan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Clan?> FindByWolvesvilleIdAsync(string wolvesvilleClanId, CancellationToken cancellationToken = default);

    /// <summary>Ensures a clan row exists for a Wolvesville clan id seen on a player profile (name unknown until imported).</summary>
    Task<Clan> GetOrCreateClanStubAsync(string wolvesvilleClanId, DateTimeOffset seenAt, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the clan from an observed clan payload (search result or clan info).</summary>
    Task<(Clan Clan, bool Created)> UpsertClanAsync(ObservedClan observed, DateTimeOffset observedAt, bool markImported, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Clan>> SearchLocalAsync(string query, int limit = 25, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Clan>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClanMembership>> GetOpenMembershipsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClanMembership>> GetMembershipsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClanMembership>> GetOpenMembershipsInClanAsync(Guid clanId, CancellationToken cancellationToken = default);

    Task<ClanMembership> OpenMembershipAsync(Guid playerId, Guid clanId, string source, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task CloseMembershipAsync(Guid playerId, Guid clanId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<int> CountClansAsync(CancellationToken cancellationToken = default);

    Task<int> CountMembershipsAsync(CancellationToken cancellationToken = default);

    Task<int> CountMembershipsForClanAsync(Guid clanId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Clan>> GetClansByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Membership joins (StartedAt) and leaves (EndedAt) aggregated per day.</summary>
    Task<IReadOnlyList<DailyFlow>> GetMembershipFlowPerDayAsync(int days, CancellationToken cancellationToken = default);
}

public sealed record DailyFlow(DateOnly Date, int Joins, int Leaves);
