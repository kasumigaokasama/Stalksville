namespace Stalksville.Domain.Entities;

public enum RelationshipType
{
    MemberOf = 0,
    PreviouslyMemberOf = 1
}

public enum EntityType
{
    Player = 0,
    Clan = 1
}

/// <summary>
/// A derived relationship between two entities. Never presented as observed fact: every row is
/// backed by evidence (memberships / snapshots).
/// </summary>
public sealed class Relationship
{
    public Guid Id { get; set; }

    public EntityType SourceEntityType { get; set; }

    public Guid SourceEntityId { get; set; }

    public EntityType TargetEntityType { get; set; }

    public Guid TargetEntityId { get; set; }

    public RelationshipType Type { get; set; }

    /// <summary>0..1; how strongly the evidence supports this relationship.</summary>
    public double Confidence { get; set; }

    public DateTimeOffset FirstObservedAt { get; set; }

    public DateTimeOffset LastObservedAt { get; set; }

    public bool IsCurrent { get; set; }
}
