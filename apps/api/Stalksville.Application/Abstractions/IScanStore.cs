using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>A schedule with its hand-picked selection resolved (empty unless scope = selected).</summary>
public sealed record ScanScheduleView(ScanSchedule Schedule, IReadOnlyList<Guid> SelectedPlayerIds);

/// <summary>Persistence port for admin-defined scan schedules and their runs.</summary>
public interface IScanStore
{
    Task<IReadOnlyList<ScanSchedule>> ListAsync(CancellationToken cancellationToken = default);

    Task<ScanSchedule?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Enabled schedules whose NextRunAt is due (or unset).</summary>
    Task<IReadOnlyList<ScanSchedule>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task AddAsync(ScanSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>Persists scheduling bookkeeping (enabled/next run/last run); false when unknown.</summary>
    Task<bool> UpdateAsync(ScanSchedule schedule, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default);

    /// <summary>Newest-first run history (schedule name is denormalized onto each run).</summary>
    Task<IReadOnlyList<ScanRun>> ListRunsAsync(int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>Schedules with their selections resolved (for list views and execution).</summary>
    Task<IReadOnlyList<ScanScheduleView>> ListWithSelectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>The hand-picked player ids of one "selected"-scope schedule.</summary>
    Task<IReadOnlyList<Guid>> GetSelectedPlayerIdsAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the hand-picked selection of a schedule.</summary>
    Task SetSelectedPlayersAsync(Guid scheduleId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken = default);
}
