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

    /// <summary>Replaces one season's winners (idempotent re-capture) and resolves tracked-player matches by name; returns the stored count.</summary>
    Task<int> ReplaceHallOfFameCaptureAsync(IReadOnlyList<HallOfFameEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>One season's winners ordered by position; empty when the season was never captured.</summary>
    Task<IReadOnlyList<HallOfFameEntry>> GetHallOfFameAsync(int seasonNumber, CancellationToken cancellationToken = default);

    /// <summary>All captured hall-of-fame seasons, newest first.</summary>
    Task<IReadOnlyList<int>> GetHallOfFameSeasonsAsync(CancellationToken cancellationToken = default);
}
