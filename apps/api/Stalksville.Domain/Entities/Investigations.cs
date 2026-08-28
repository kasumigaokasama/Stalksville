namespace Stalksville.Domain.Entities;

public enum InvestigationStatus
{
    Active = 0,
    Archived = 1
}

/// <summary>
/// An investigation case: a curated set of player/clan targets with notes, an aggregated
/// timeline and intelligence stats. Cases are archived, never hard-deleted.
/// </summary>
public sealed class Investigation
{
    public Guid Id { get; set; }

    /// <summary>Human-friendly sequential number, displayed as CASE #0042.</summary>
    public int CaseNumber { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public InvestigationStatus Status { get; set; } = InvestigationStatus.Active;

    /// <summary>Analyst the case is assigned to (users.Id), null = unassigned.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>Free-form case tags (jsonb array of short strings).</summary>
    public List<string> Tags { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<InvestigationTarget> Targets { get; set; } = [];

    public List<InvestigationNote> Notes { get; set; } = [];
}

/// <summary>A player or clan attached to an investigation.</summary>
public sealed class InvestigationTarget
{
    public Guid Id { get; set; }

    public Guid InvestigationId { get; set; }

    public Investigation? Investigation { get; set; }

    public EntityType EntityType { get; set; }

    public Guid EntityId { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public string AddedBy { get; set; } = "unknown";
}

/// <summary>Analyst note attached to an investigation.</summary>
public sealed class InvestigationNote
{
    public Guid Id { get; set; }

    public Guid InvestigationId { get; set; }

    public Investigation? Investigation { get; set; }

    public required string Content { get; set; }

    public string Author { get; set; } = "unknown";

    public DateTimeOffset CreatedAt { get; set; }
}
