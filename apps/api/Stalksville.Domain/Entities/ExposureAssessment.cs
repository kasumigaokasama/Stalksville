namespace Stalksville.Domain.Entities;

/// <summary>
/// One persisted exposure assessment (derived, evidence-linked): the explainable 0–100 score for
/// a player at a snapshot, kept so score movement over time can be alerted on. The category
/// breakdown is stored as JSON exactly as the analyzer produced it.
/// </summary>
public sealed class ExposureAssessment
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    /// <summary>The snapshot this assessment was computed from.</summary>
    public Guid SnapshotId { get; set; }

    public int Score { get; set; }

    /// <summary>Canonical JSON (jsonb): [{ name, score, factors: [{ label, points, evidence }] }].</summary>
    public required string CategoryScores { get; set; }

    public DateTimeOffset AssessedAt { get; set; }
}
