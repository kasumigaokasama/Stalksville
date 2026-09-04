using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

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
}
