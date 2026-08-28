using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for ranked leaderboard captures (observed data, append-only).</summary>
public interface IRankedStore
{
    /// <summary>Stores one full board capture and resolves tracked-player matches by username.</summary>
    Task<int> AddCaptureAsync(IReadOnlyList<RankedEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>The most recent capture, ranked; empty before the first capture.</summary>
    Task<IReadOnlyList<RankedEntry>> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>The capture before the latest one (for rank diffing); empty when only one exists.</summary>
    Task<IReadOnlyList<RankedEntry>> GetPreviousAsync(CancellationToken cancellationToken = default);

    /// <summary>When the last capture happened; null when never captured.</summary>
    Task<DateTimeOffset?> GetLastCaptureAtAsync(CancellationToken cancellationToken = default);
}
