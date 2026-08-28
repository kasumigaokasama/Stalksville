namespace Stalksville.Domain.Entities;

public static class AlertKinds
{
    public const string ClanChanged = "ClanChanged";
    public const string UsernameChanged = "UsernameChanged";
    public const string LevelJump = "LevelJump";
}

public enum AlertSeverity
{
    Info = 0,
    Notice = 1,
    Warning = 2
}

/// <summary>
/// A derived intelligence alert raised from observed change evidence. Alerts never contain data
/// that is not already backed by change records — <see cref="Evidence"/> always references them.
/// Read state is global (single-workbench semantics), not per user.
/// </summary>
public sealed class Alert
{
    public Guid Id { get; set; }

    public EntityType EntityType { get; set; }

    public Guid EntityId { get; set; }

    /// <summary>Display name of the entity at alert time (denormalized for the inbox).</summary>
    public required string EntityTitle { get; set; }

    public required string Kind { get; set; }

    public AlertSeverity Severity { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    /// <summary>Canonical JSON (jsonb) pointing at the backing change/snapshot ids.</summary>
    public required string Evidence { get; set; }

    /// <summary>kind:entityId:snapshotId — keyed to the backing change so ingestion stays idempotent.</summary>
    public required string DedupeKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
