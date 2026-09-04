using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>One field-level change inside a scan change line.</summary>
public sealed record ScanFieldChange(string Field, string? OldValue, string? NewValue);

/// <summary>One player's changes in a scan run — with what actually moved.</summary>
public sealed record ScanChangeLine(
    string Player,
    int Changes,
    IReadOnlyList<ScanFieldChange>? Fields = null);

/// <summary>What a scan found — the payload notifications are built from.</summary>
public sealed record ScanNotification(
    string ScheduleName,
    string Kind,
    DateTimeOffset FinishedAt,
    int PlayersObserved,
    int ChangesDetected,
    int AlertsRaised,
    IReadOnlyList<ScanChangeLine> Changes,
    bool IsTest = false);

/// <summary>Persistence port for outbound notification channels (webhook URLs stay server-side).</summary>
public interface INotificationStore
{
    Task<IReadOnlyList<NotificationChannel>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);

    Task<NotificationChannel?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists enablement and the last delivery outcome.</summary>
    Task<bool> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default);
}

/// <summary>Sends one notification to one channel; returns the delivery outcome for bookkeeping.</summary>
public interface INotificationDispatcher
{
    Task<(bool Success, string Detail)> SendAsync(NotificationChannel channel, ScanNotification notification, CancellationToken cancellationToken = default);
}
