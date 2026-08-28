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

/// <summary>Persistence port for derived intelligence alerts.</summary>
public interface IAlertStore
{
    /// <summary>Persists candidates whose dedupe key is not present yet; returns the stored alerts.</summary>
    Task<IReadOnlyList<Alert>> AddIfNewAsync(IReadOnlyList<AlertCandidate> candidates, EntityType entityType, Guid entityId, string entityTitle, DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> ListAsync(AlertFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Total alerts matching the filter (for pagination headers).</summary>
    Task<int> CountAsync(AlertFilter filter, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid alertId, DateTimeOffset readAt, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(DateTimeOffset readAt, CancellationToken cancellationToken = default);
}
