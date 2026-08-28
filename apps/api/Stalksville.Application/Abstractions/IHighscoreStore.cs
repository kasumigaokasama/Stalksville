using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for highscore board captures (observed data, append-only).</summary>
public interface IHighscoreStore
{
    /// <summary>Stores one full capture (4 boards) and resolves tracked-player matches by username.</summary>
    Task<int> AddCaptureAsync(IReadOnlyList<HighscoreEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>The most recent capture of a period, ranked; empty before the first capture.</summary>
    Task<IReadOnlyList<HighscoreEntry>> GetLatestAsync(string period, CancellationToken cancellationToken = default);

    /// <summary>The capture before the latest one of a period (for rank diffing); empty when only one exists.</summary>
    Task<IReadOnlyList<HighscoreEntry>> GetPreviousAsync(string period, CancellationToken cancellationToken = default);

    /// <summary>When the last capture of any period happened; null when never captured.</summary>
    Task<DateTimeOffset?> GetLastCaptureAtAsync(CancellationToken cancellationToken = default);
}
