namespace Stalksville.Domain.Entities;

/// <summary>
/// Per-user alert read state. Read/unread in the inbox is personal to each account; the legacy
/// global column on <see cref="Alert"/> is no longer written (its values migrated to the seeding
/// admin on first migration).
/// </summary>
public sealed class AlertRead
{
    public Guid AlertId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset ReadAt { get; set; }
}
