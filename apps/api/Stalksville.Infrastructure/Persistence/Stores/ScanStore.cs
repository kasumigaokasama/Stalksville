using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class ScanStore(StalksvilleDbContext db) : IScanStore
{
    public async Task<IReadOnlyList<ScanSchedule>> ListAsync(CancellationToken cancellationToken = default)
        => await db.ScanSchedules.AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ScanSchedule?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ScanSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScanSchedule>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => await db.ScanSchedules
            .Where(s => s.Enabled && (s.NextRunAt == null || s.NextRunAt <= now))
            .OrderBy(s => s.NextRunAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ScanSchedule schedule, CancellationToken cancellationToken = default)
    {
        db.ScanSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(ScanSchedule schedule, CancellationToken cancellationToken = default)
    {
        var updated = await db.ScanSchedules
            .Where(s => s.Id == schedule.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Name, schedule.Name)
                .SetProperty(s => s.Kind, schedule.Kind)
                .SetProperty(s => s.IntervalMinutes, schedule.IntervalMinutes)
                .SetProperty(s => s.BatchSize, schedule.BatchSize)
                .SetProperty(s => s.Enabled, schedule.Enabled)
                .SetProperty(s => s.UpdatedAt, schedule.UpdatedAt)
                .SetProperty(s => s.LastRunAt, schedule.LastRunAt)
                .SetProperty(s => s.NextRunAt, schedule.NextRunAt), cancellationToken);
        return updated == 1;
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Runs are immutable history with the schedule name denormalized — deleting a schedule
        // keeps its runs readable in the history.
        var removed = await db.ScanSchedules
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }

    public async Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default)
    {
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScanRun>> ListRunsAsync(int limit, int offset, CancellationToken cancellationToken = default)
        => await db.ScanRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
