using Stalksville.Application.Models;

namespace Stalksville.Application.Advanced;

// ---- Graph (phase 4) ----

public sealed record GraphNodeDto(
    string Id,
    string Type,
    string Label,
    int SnapshotCount,
    int ChangeCount,
    bool Imported);

public sealed record GraphEdgeDto(
    string Id,
    string Source,
    string Target,
    string Type,
    double Confidence,
    bool IsCurrent,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastObservedAt);

public sealed record GraphDto(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges);

public sealed record GraphPathNodeDto(string Id, string Type, string Label);

public sealed record GraphPathsDto(IReadOnlyList<IReadOnlyList<GraphPathNodeDto>> Paths);

// ---- Exposure (phase 5) ----

public sealed record ExposureFactorDto(string Label, int Points, string Evidence);

public sealed record ExposureCategoryDto(string Name, int Score, IReadOnlyList<ExposureFactorDto> Factors);

public sealed record ExposureResultDto(int Overall, IReadOnlyList<ExposureCategoryDto> Categories);

// ---- Insights (phase 5) ----

public sealed record InsightDto(
    string Classification,
    string Title,
    string Description,
    double Confidence,
    IReadOnlyList<Guid> EvidenceChangeIds);

// ---- Compare (phase 5) ----

public sealed record CompareFieldDto(string Field, string? A, string? B);

public sealed record CompareOverlapDto(string Kind, string Value, double Confidence, string Evidence);

public sealed record CompareResultDto(
    PlayerDossierDto A,
    PlayerDossierDto B,
    IReadOnlyList<CompareFieldDto> Fields,
    IReadOnlyList<CompareOverlapDto> Overlaps,
    string Disclaimer);

// ---- Analytics (phase 5) ----

public sealed record AnalyticsPointDto(string Date, int Value);

public sealed record AnalyticsSeriesDto(string Name, IReadOnlyList<AnalyticsPointDto> Points);

public sealed record AnalyticsSummaryDto(
    int PlayersTracked,
    int ClansTracked,
    int SnapshotsCollected,
    int ChangesDetected,
    int RelationshipsDiscovered,
    int ActiveInvestigations,
    IReadOnlyList<AnalyticsSeriesDto> Series);
