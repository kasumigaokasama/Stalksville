namespace Stalksville.Domain.Entities;

public enum EvidenceSourceType
{
    WolvesvilleApi = 0
}

/// <summary>
/// Traceability record: links a derived result (change, relationship, …) to the observation that
/// proves it — the upstream endpoint called, the payload hash, and the capture time.
/// </summary>
public sealed class Evidence
{
    public Guid Id { get; set; }

    /// <summary>Type of the derived entity this evidence supports, e.g. "playerChange".</summary>
    public required string EntityType { get; set; }

    /// <summary>Id of the derived entity this evidence supports.</summary>
    public Guid EntityId { get; set; }

    public EvidenceSourceType SourceType { get; set; } = EvidenceSourceType.WolvesvilleApi;

    /// <summary>Upstream reference, e.g. "GET /players/{playerId}" or "snapshot:{id}".</summary>
    public required string SourceReference { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    /// <summary>Hash of the observed payload backing this evidence, when applicable.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>JSON (jsonb) with supporting metadata.</summary>
    public string? Metadata { get; set; }
}
