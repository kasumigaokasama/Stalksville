namespace Stalksville.Application.Investigations;

public sealed record CreateInvestigationRequest(string Title, string? Description);

public sealed record AddTargetRequest(string EntityType, Guid EntityId);

public sealed record AddNoteRequest(string Content);

public sealed record InvestigationSummaryDto(
    Guid Id,
    int CaseNumber,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int TargetCount,
    int NoteCount);

public sealed record InvestigationTargetDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string DisplayName,
    DateTimeOffset AddedAt,
    string AddedBy,
    int SnapshotCount,
    int CurrentRelationships);

public sealed record InvestigationNoteDto(
    Guid Id,
    string Content,
    string Author,
    DateTimeOffset CreatedAt);

/// <summary>Investigation complexity metrics (master plan §38).</summary>
public sealed record InvestigationStatsDto(
    int Targets,
    int TimelineEvents,
    int SnapshotsCollected,
    int HighConfidenceRelationships);

public sealed record InvestigationWorkspaceDto(
    InvestigationSummaryDto Investigation,
    InvestigationStatsDto Stats,
    IReadOnlyList<InvestigationTargetDto> Targets,
    IReadOnlyList<InvestigationNoteDto> Notes,
    IReadOnlyList<Models.TimelineEventDto> Timeline);
