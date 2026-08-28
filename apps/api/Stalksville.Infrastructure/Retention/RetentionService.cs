using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Retention;

/// <summary>
/// Snapshot retention (Worker:SnapshotRetentionDays, 0 = disabled): deletes snapshots older than
/// the retention window while always keeping each player's most recent snapshot. Snapshots are
/// observed data — pruning is a storage/privacy decision, never silently applied.
/// </summary>
public sealed class RetentionService(StalksvilleDbContext db, ILogger<RetentionService> logger)
{
    public async Task<int> PruneSnapshotsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        // Snapshots inside the window are untouched; among the stale ones, each player keeps
        // their newest stale snapshot (their overall latest, when everything they have is stale).
        var stale = await db.PlayerSnapshots.AsNoTracking()
            .Where(s => s.CapturedAt < cutoff)
            .Select(s => new { s.Id, s.PlayerId, s.CapturedAt })
            .ToListAsync(cancellationToken);

        var deletable = stale
            .GroupBy(s => s.PlayerId)
            .SelectMany(group => group
                .OrderByDescending(s => s.CapturedAt)
                .Skip(1))
            .Select(s => s.Id)
            .ToList();

        if (deletable.Count == 0)
        {
            return 0;
        }

        await db.PlayerSnapshots.Where(s => deletable.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Retention pruned {Count} snapshot(s) older than {Days} day(s)", deletable.Count, retentionDays);
        return deletable.Count;
    }
}
