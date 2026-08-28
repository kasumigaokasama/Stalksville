using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Abstractions;

/// <summary>Filter for alert queries; nulls mean "no restriction".</summary>
public sealed record AlertFilter(
    bool? UnreadOnly = null,
    string? Kind = null,
    Guid? EntityId = null,
    int Limit = 100,
    int Offset = 0);

/// <summary>An alert with the calling user's read state resolved.</summary>
public sealed record AlertWithRead(Alert Alert, DateTimeOffset? ReadAt);

/// <summary>Persistence port for derived intelligence alerts (read state is per user).</summary>
public interface IAlertStore
{
    /// <summary>Persists candidates whose dedupe key is not present yet; returns the stored alerts.</summary>
    Task<IReadOnlyList<Alert>> AddIfNewAsync(IReadOnlyList<AlertCandidate> candidates, EntityType entityType, Guid entityId, string entityTitle, DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    /// <summary>Alerts matching the filter with the calling user's read state.</summary>
    Task<IReadOnlyList<AlertWithRead>> ListAsync(AlertFilter filter, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Total alerts matching the filter (for pagination headers).</summary>
    Task<int> CountAsync(AlertFilter filter, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Alerts the user has not read yet.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks one alert read for the user; false when it was already read or unknown.</summary>
    Task<bool> MarkReadAsync(Guid alertId, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default);

    /// <summary>Marks every unread-for-user alert read; returns how many rows were added.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default);
}
