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
    Guid PlayerId,
    IReadOnlyList<AlertEvidenceChangeDto> Changes);

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

public sealed record DerivedDto(
    int TotalChanges,
    IReadOnlyList<ChangeDto> RecentChanges,
    IReadOnlyList<MembershipDto> Memberships,
    IReadOnlyList<RelationshipDto> Relationships);

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

public sealed record PlayerLookupResultDto(PlayerDossierDto Dossier, bool WasReobserved, int ChangesDetectedInThisObservation);
