namespace Stalksville.Domain.Entities;

/// <summary>
/// One user's starred player. Watching is personal bookkeeping — every role may star/unstar —
/// and is purely a Stalksville-side priority marker (never communicated to Wolvesville).
/// </summary>
public sealed class WatchEntry
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PlayerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
