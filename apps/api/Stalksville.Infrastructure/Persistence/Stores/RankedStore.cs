using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class RankedStore(StalksvilleDbContext db) : IRankedStore
{
    public async Task<int> AddCaptureAsync(IReadOnlyList<RankedEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        // Resolve tracked players by username so the leaderboard can deep-link dossiers.
        var usernames = entries.Select(e => e.UsernameLower).Distinct().ToList();
        var trackedPlayers = await db.Players
            .Where(p => usernames.Contains(p.UsernameLower))
            .ToDictionaryAsync(p => p.UsernameLower, p => p.Id, cancellationToken);

        foreach (var entry in entries)
        {
            // TryGetValue, not GetValueOrDefault: a missing match must stay null, not Guid.Empty.
            entry.PlayerId = trackedPlayers.TryGetValue(entry.UsernameLower, out var playerId) ? playerId : null;
        }

        db.RankedEntries.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public Task<IReadOnlyList<RankedEntry>> GetLatestAsync(CancellationToken cancellationToken = default)
        => GetCaptureAsync(offset: 0, cancellationToken);

    public Task<IReadOnlyList<RankedEntry>> GetPreviousAsync(CancellationToken cancellationToken = default)
        => GetCaptureAsync(offset: 1, cancellationToken);

    public async Task<DateTimeOffset?> GetLastCaptureAtAsync(CancellationToken cancellationToken = default)
        => await db.RankedEntries.AsNoTracking()
            .OrderByDescending(e => e.CapturedAt)
            .Select(e => (DateTimeOffset?)e.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<RankedEntry>> GetCaptureAsync(int offset, CancellationToken cancellationToken)
    {
        // Distinct before ordering: EF otherwise drops the ORDER BY while composing Distinct+Skip,
        // which makes "latest capture" an arbitrary row.
        var capturedAt = await db.RankedEntries.AsNoTracking()
            .Select(e => e.CapturedAt)
            .Distinct()
            .OrderByDescending(at => at)
            .Skip(offset)
            .FirstOrDefaultAsync(cancellationToken);

        if (capturedAt == default)
        {
            return [];
        }

        return await db.RankedEntries.AsNoTracking()
            .Where(e => e.CapturedAt == capturedAt)
            .OrderBy(e => e.Rank)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ReplaceHallOfFameCaptureAsync(IReadOnlyList<HallOfFameEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        // Finished seasons are immutable upstream; re-captures exist to re-resolve tracked flags.
        await db.HallOfFameEntries.Where(e => e.SeasonNumber == entries[0].SeasonNumber).ExecuteDeleteAsync(cancellationToken);

        var names = entries.Select(e => e.PlayerNameLower).Distinct().ToList();
        var trackedPlayers = await db.Players
            .Where(p => names.Contains(p.UsernameLower))
            .ToDictionaryAsync(p => p.UsernameLower, p => p.Id, cancellationToken);

        foreach (var entry in entries)
        {
            entry.PlayerId = trackedPlayers.TryGetValue(entry.PlayerNameLower, out var playerId) ? playerId : null;
        }

        db.HallOfFameEntries.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public async Task<IReadOnlyList<HallOfFameEntry>> GetHallOfFameAsync(int seasonNumber, CancellationToken cancellationToken = default)
    {
        var entries = await db.HallOfFameEntries.AsNoTracking()
            .Where(e => e.SeasonNumber == seasonNumber)
            .OrderBy(e => e.Position)
            .ToListAsync(cancellationToken);
        return entries;
    }

    public async Task<IReadOnlyList<int>> GetHallOfFameSeasonsAsync(CancellationToken cancellationToken = default)
        => await db.HallOfFameEntries.AsNoTracking()
            .Select(e => e.SeasonNumber)
            .Distinct()
            .OrderByDescending(season => season)
            .ToListAsync(cancellationToken);
}
