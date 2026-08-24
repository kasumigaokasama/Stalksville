using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class ClanStore(StalksvilleDbContext db) : IClanStore
{
    public Task<Clan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Clans.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Clan?> FindByWolvesvilleIdAsync(string wolvesvilleClanId, CancellationToken cancellationToken = default)
        => db.Clans.FirstOrDefaultAsync(c => c.WolvesvilleClanId == wolvesvilleClanId, cancellationToken);

    public async Task<Clan> GetOrCreateClanStubAsync(string wolvesvilleClanId, DateTimeOffset seenAt, CancellationToken cancellationToken = default)
    {
        var clan = await db.Clans.FirstOrDefaultAsync(c => c.WolvesvilleClanId == wolvesvilleClanId, cancellationToken);
        if (clan is not null)
        {
            clan.LastSeenAt = seenAt;
            await db.SaveChangesAsync(cancellationToken);
            return clan;
        }

        clan = new Clan
        {
            Id = Guid.NewGuid(),
            WolvesvilleClanId = wolvesvilleClanId,
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt
        };
        db.Clans.Add(clan);
        await db.SaveChangesAsync(cancellationToken);
        return clan;
    }

    public async Task<(Clan Clan, bool Created)> UpsertClanAsync(ObservedClan observed, DateTimeOffset observedAt, bool markImported, CancellationToken cancellationToken = default)
    {
        var clan = await db.Clans.FirstOrDefaultAsync(c => c.WolvesvilleClanId == observed.WolvesvilleClanId, cancellationToken);
        bool created;

        if (clan is null)
        {
            clan = new Clan
            {
                Id = Guid.NewGuid(),
                WolvesvilleClanId = observed.WolvesvilleClanId,
                FirstSeenAt = observedAt
            };
            db.Clans.Add(clan);
            created = true;
        }
        else
        {
            created = false;
        }

        clan.Name = observed.Name;
        clan.Description = observed.Description;
        clan.MemberCount = observed.MemberCount;
        clan.LanguageCode = observed.LanguageCode;
        clan.JoinType = observed.JoinType;
        clan.Xps = observed.Xps;
        clan.Level = observed.Level;
        clan.LeaderWolvesvillePlayerId = observed.LeaderWolvesvillePlayerId;
        clan.LastSeenAt = observedAt;

        if (markImported)
        {
            clan.LastImportedAt = observedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (clan, created);
    }

    public async Task<IReadOnlyList<Clan>> SearchLocalAsync(string query, int limit = 25, CancellationToken cancellationToken = default)
    {
        var lowered = query.Trim().ToLowerInvariant();
        return await db.Clans
            .Where(c => c.Name != null && c.Name.ToLower().Contains(lowered))
            .OrderByDescending(c => c.LastImportedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clan>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default)
        => await db.Clans
            .OrderByDescending(c => c.LastSeenAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClanMembership>> GetOpenMembershipsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
        => await db.ClanMemberships
            .Where(m => m.PlayerId == playerId && m.EndedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClanMembership>> GetMembershipsForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
        => await db.ClanMemberships
            .Where(m => m.PlayerId == playerId)
            .OrderByDescending(m => m.StartedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClanMembership>> GetOpenMembershipsInClanAsync(Guid clanId, CancellationToken cancellationToken = default)
        => await db.ClanMemberships
            .Where(m => m.ClanId == clanId && m.EndedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<ClanMembership> OpenMembershipAsync(Guid playerId, Guid clanId, string source, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var membership = new ClanMembership
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            ClanId = clanId,
            StartedAt = at,
            Source = source
        };
        db.ClanMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task CloseMembershipAsync(Guid playerId, Guid clanId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var memberships = await db.ClanMemberships
            .Where(m => m.PlayerId == playerId && m.ClanId == clanId && m.EndedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            membership.EndedAt = at;
        }

        if (memberships.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<int> CountClansAsync(CancellationToken cancellationToken = default)
        => db.Clans.CountAsync(cancellationToken);

    public Task<int> CountMembershipsAsync(CancellationToken cancellationToken = default)
        => db.ClanMemberships.CountAsync(cancellationToken);

    public Task<int> CountMembershipsForClanAsync(Guid clanId, CancellationToken cancellationToken = default)
        => db.ClanMemberships.CountAsync(m => m.ClanId == clanId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Clan>> GetClansByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => await db.Clans.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

    public async Task<IReadOnlyList<DailyFlow>> GetMembershipFlowPerDayAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var joins = await db.ClanMemberships
            .Where(m => m.StartedAt >= since)
            .GroupBy(m => DateOnly.FromDateTime(m.StartedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken);

        var leaves = await db.ClanMemberships
            .Where(m => m.EndedAt != null && m.EndedAt >= since)
            .GroupBy(m => DateOnly.FromDateTime(m.EndedAt!.Value.UtcDateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken);

        return joins.Keys.Concat(leaves.Keys)
            .Distinct()
            .OrderBy(date => date)
            .Select(date => new DailyFlow(date, joins.GetValueOrDefault(date), leaves.GetValueOrDefault(date)))
            .ToList();
    }
}
