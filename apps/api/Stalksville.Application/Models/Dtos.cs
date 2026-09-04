using Stalksville.Domain.Entities;

namespace Stalksville.Application.Models;

// ---- Auth ----

public sealed record LoginRequest(string Username, string Password);

public sealed record UserDto(Guid Id, string Username, string Role);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

// ---- Observed state ----

public sealed record ObservedStateDto(
    string WolvesvillePlayerId,
    string Username,
    string? PersonalMessage,
    int? Level,
    string? Status,
    DateTimeOffset? LastOnline,
    string? ClanWolvesvilleId,
    int Wins,
    int Losses,
    int GamesPlayed,
    int? ReceivedRosesCount,
    int? SentRosesCount,
    string? ProfileIconId,
    string? ProfileIconName,
    string? EquippedAvatarId,
    IReadOnlyList<string> BadgeIds,
    IReadOnlyList<string> RoleCardIds,
    int? RankedSeason,
    int? RankedWins,
    int? RankedLosses,
    int? RankedCurrentRating,
    int? RankedPlacementRating,
    int? Achievements,
    int FriendCount,
    DateTimeOffset CapturedAt,
    DateTimeOffset LastObservedAt,
    int ObservationCount,
    string Source,
    string PayloadHash);

// ---- Derived intelligence ----

public sealed record EvidenceDto(string SourceType, string SourceReference, DateTimeOffset CapturedAt, string? PayloadHash);

public sealed record ChangeDto(
    Guid Id,
    string Field,
    string Kind,
    string? OldValue,
    string? NewValue,
    DateTimeOffset DetectedAt,
    Guid? FromSnapshotId,
    Guid ToSnapshotId,
    IReadOnlyList<EvidenceDto> Evidence);

public sealed record MembershipDto(
    Guid ClanId,
    string WolvesvilleClanId,
    string? ClanName,
    bool ClanImported,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    bool IsCurrent);

public sealed record RelationshipDto(
    string Type,
    string TargetEntityType,
    Guid TargetEntityId,
    string? TargetName,
    double Confidence,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastObservedAt,
    bool IsCurrent);

public sealed record TimelineEventDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string EventType,
    string? Summary,
    DateTimeOffset OccurredAt,
    bool IsDerived,
    double? Confidence);

// ---- Alerts (derived) ----

/// <summary>Evidence block of an alert: the change ids and snapshots that justify it.</summary>
public sealed record AlertEvidenceDto(
    Guid? PlayerId,
    IReadOnlyList<AlertEvidenceChangeDto> Changes)
{
    /// <summary>Rule-specific evidence fields (before/after, board, friend ids…) pass through raw.</summary>
    [global::System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, global::System.Text.Json.JsonElement>? Extras { get; set; }
}

public sealed record AlertEvidenceChangeDto(
    Guid Id,
    string Field,
    string? OldValue,
    string? NewValue,
    Guid? FromSnapshotId,
    Guid ToSnapshotId);

public sealed record AlertDto(
    Guid Id,
    string Kind,
    string Severity,
    string EntityType,
    Guid EntityId,
    string EntityTitle,
    string Title,
    string Body,
    AlertEvidenceDto? Evidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UnreadCountDto(int Unread);

// ---- Progression (observed) ----

public sealed record ProgressionPointDto(
    DateTimeOffset CapturedAt,
    int ObservationCount,
    int? Level,
    int Wins,
    int GamesPlayed,
    int? Achievements);

public sealed record ProgressionDto(IReadOnlyList<ProgressionPointDto> Points);

// ---- Graph analytics (derived) ----

public sealed record ConnectorDto(
    string NodeId,
    string Type,
    string Label,
    int Degree,
    double Betweenness);

public sealed record CommunityDto(int Index, int Size, IReadOnlyList<string> MemberIds);

public sealed record GraphAnalyticsDto(
    int NodeCount,
    int EdgeCount,
    IReadOnlyList<ConnectorDto> TopConnectors,
    IReadOnlyList<CommunityDto> Communities,
    string Note);

// ---- Highscores (observed boards + derived rank shifts) ----

public sealed record HighscoreRowDto(
    int Rank,
    string Username,
    string WolvesvillePlayerId,
    long Xp,
    Guid? PlayerId,
    bool Tracked);

public sealed record HighscoreBoardDto(
    string Period,
    DateTimeOffset? CapturedAt,
    IReadOnlyList<HighscoreRowDto> Rows);

public sealed record HighscoreCaptureResultDto(
    DateTimeOffset CapturedAt,
    int EntriesStored,
    int RankShiftAlerts);

// ---- Ranked (observed board + season context + derived rank shifts) ----

public sealed record RankedRowDto(
    int Rank,
    string Username,
    string WolvesvillePlayerId,
    int Skill,
    Guid? PlayerId,
    bool Tracked);

public sealed record RankedBoardDto(
    int? SeasonNumber,
    DateTimeOffset? CapturedAt,
    IReadOnlyList<RankedRowDto> Rows);

public sealed record RankedSeasonDto(
    int Number,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool Finished,
    int StartSkillDefault,
    string Source);

public sealed record RankedCaptureResultDto(
    DateTimeOffset CapturedAt,
    int SeasonNumber,
    int EntriesStored,
    int RankShiftAlerts);

public sealed record HallOfFameRowDto(
    int Position,
    string PlayerName,
    string WolvesvillePlayerId,
    string? AvatarUrl,
    Guid? PlayerId,
    bool Tracked);

public sealed record HallOfFameBoardDto(
    int SeasonNumber,
    DateTimeOffset? CapturedAt,
    IReadOnlyList<HallOfFameRowDto> Rows,
    IReadOnlyList<int> AvailableSeasons);

public sealed record HallOfFameCaptureResultDto(
    DateTimeOffset CapturedAt,
    int SeasonNumber,
    int EntriesStored,
    int TrackedWinnerAlerts);

// ---- Cosmetics catalog (observed reference data: ids → display names) ----

public sealed record CatalogItemDto(
    string Kind,
    string ExternalId,
    string Name,
    string Rarity,
    string? Description,
    string? ImageUrl);

// ---- Workspace search ----

public sealed record SearchHitDto(
    string Type,
    string Id,
    string Title,
    string? Subtitle);

// ---- Watchlist (per-user starred players) ----

public sealed record WatchedPlayerDto(
    Guid Id,
    string WolvesvillePlayerId,
    string Username,
    DateTimeOffset StarredAt);

public sealed record DerivedDto(
    int TotalChanges,
    IReadOnlyList<ChangeDto> RecentChanges,
    IReadOnlyList<MembershipDto> Memberships,
    IReadOnlyList<RelationshipDto> Relationships,
    IReadOnlyList<FriendDto> Friends);

/// <summary>A tracked player that appears in this player's observed friend list.</summary>
public sealed record FriendDto(
    Guid PlayerId,
    string Username,
    bool Current);

// ---- Dossiers ----

public sealed record PlayerSummaryDto(
    Guid Id,
    string WolvesvillePlayerId,
    string Username,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    Guid? CurrentClanId,
    string? CurrentClanName);

public sealed record PlayerDossierDto(PlayerSummaryDto Player, ObservedStateDto? Observed, DerivedDto Derived);

public sealed record SnapshotDto(
    Guid Id,
    DateTimeOffset CapturedAt,
    DateTimeOffset LastObservedAt,
    int ObservationCount,
    string PayloadHash,
    string Source);

public sealed record ClanSummaryDto(
    Guid Id,
    string WolvesvilleClanId,
    string? Name,
    string? Description,
    int? MemberCount,
    string? LanguageCode,
    string? JoinType,
    long? Xps,
    int? Level,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastImportedAt);

public sealed record ClanDossierDto(
    ClanSummaryDto Clan,
    IReadOnlyList<PlayerSummaryDto> KnownMembers,
    int OpenMembershipCount,
    int MembershipHistoryCount);

// ---- Dashboard / system ----

public sealed record DashboardStatsDto(
    int PlayersTracked,
    int ClansTracked,
    int SnapshotsCollected,
    int ChangesDetected,
    int RelationshipsDiscovered,
    IReadOnlyList<ChangeWithPlayerDto> RecentChanges,
    IReadOnlyList<PlayerSummaryDto> RecentlySeenPlayers);

public sealed record ChangeWithPlayerDto(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    string Field,
    string? OldValue,
    string? NewValue,
    DateTimeOffset DetectedAt);

public sealed record CapabilityDto(string Name, string Status, string? Note = null);

public sealed record ConnectionStatusDto(
    string Mode,
    string Status,
    string? Message,
    bool WriteOperationsEnabled,
    IReadOnlyList<CapabilityDto> Capabilities);

// ---- Lookup result for search ----

/// <summary>One field-level change observed during a single player observation.</summary>
public sealed record ObservedChangeDto(string Field, string? OldValue, string? NewValue);

public sealed record PlayerLookupResultDto(
    PlayerDossierDto Dossier,
    bool WasReobserved,
    int ChangesDetectedInThisObservation,
    IReadOnlyList<ObservedChangeDto>? ChangesInThisObservation = null);

// ---- Scheduled scans & change notifications ----

public sealed record ScanScheduleDto(
    Guid Id,
    string Name,
    string Kind,
    int IntervalMinutes,
    int BatchSize,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

/// <summary>Kind/Interval are required on create; on update, null fields keep their current values.</summary>
public sealed record UpsertScanScheduleDto(
    string Name,
    string? Kind = null,
    int? IntervalMinutes = null,
    int? BatchSize = null,
    bool? Enabled = null);

public sealed record ScanFieldChangeDto(string Field, string? OldValue, string? NewValue);

public sealed record ScanChangeLineDto(
    string Player,
    int Changes,
    IReadOnlyList<ScanFieldChangeDto> Fields);

public sealed record ScanRunDto(
    Guid Id,
    Guid ScheduleId,
    string ScheduleName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int PlayersObserved,
    int ChangesDetected,
    int AlertsRaised,
    string? Error,
    IReadOnlyList<ScanChangeLineDto> Changes);

public sealed record NotificationChannelDto(
    Guid Id,
    string Name,
    string Kind,
    string TargetUrlMasked,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastDeliveryAt,
    string? LastDeliveryStatus);

/// <summary>TargetUrl is required on create; on update, null fields keep their current values.</summary>
public sealed record UpsertNotificationChannelDto(
    string Name,
    string? TargetUrl = null,
    bool? Enabled = null);
