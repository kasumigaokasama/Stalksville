using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Players;

/// <summary>
/// Orchestrates the player vertical slice: lookup / refresh → normalize → persist → snapshot →
/// change detection → membership tracking → dossier assembly. Keeps observed and derived data
/// strictly separated in its results.
/// </summary>
public sealed class PlayerService(
    IWolvesvilleClient wolvesville,
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    IAlertStore alerts,
    IAuditLog audit,
    ILogger<PlayerService> logger)
{
    private static readonly JsonSerializerOptions MetaJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Exact-username lookup against Wolvesville, importing/refreshing the player locally.</summary>
    public async Task<PlayerLookupResultDto> LookupAsync(string username, CancellationToken cancellationToken = default)
    {
        var observation = await wolvesville.GetPlayerByUsernameAsync(username, cancellationToken: cancellationToken);
        var result = await ApplyObservationAsync(observation, AuditActions.PlayerImported, cancellationToken);
        return result;
    }

    /// <summary>Re-fetches a tracked player by its Wolvesville id, bypassing the cache.</summary>
    public async Task<PlayerLookupResultDto> RefreshAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var observation = await wolvesville.GetPlayerByIdAsync(player.WolvesvillePlayerId, bypassCache: true, cancellationToken: cancellationToken);
        var result = await ApplyObservationAsync(observation, AuditActions.PlayerRefreshed, cancellationToken);
        return result;
    }

    /// <summary>
    /// The single ingestion path for a player observation — also used by clan imports for each
    /// member. Produces smart snapshots, change records with evidence, membership transitions and
    /// timeline events.
    /// </summary>
    public async Task<PlayerLookupResultDto> ApplyObservationAsync(PlayerObservation observation, string? auditAction, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = observation.State;

        var (player, created) = await players.UpsertPlayerAsync(state, now, cancellationToken);

        // --- Resolve the observed clan into a Stalksville clan row (stub until imported) ---
        Guid? observedClanId = null;
        if (state.ClanWolvesvilleId is not null)
        {
            var clan = await clans.GetOrCreateClanStubAsync(state.ClanWolvesvilleId, now, cancellationToken);
            observedClanId = clan.Id;
        }

        if (player.CurrentClanId != observedClanId)
        {
            await players.SetCurrentClanAsync(player.Id, observedClanId, cancellationToken);
        }

        // --- Membership tracking ---
        var timelineEvents = new List<TimelineEvent>();
        var openMemberships = await clans.GetOpenMembershipsForPlayerAsync(player.Id, cancellationToken);
        var transitions = MembershipTracker.Track(openMemberships, observedClanId, now);
        var membershipEvidence = new List<Evidence>();

        foreach (var transition in transitions)
        {
            if (transition.Action == MembershipAction.Started)
            {
                var membership = await clans.OpenMembershipAsync(player.Id, transition.ClanId, observation.Source, now, cancellationToken);
                await derivations.SyncMembershipRelationshipAsync(player.Id, transition.ClanId, current: true, now, cancellationToken);
                timelineEvents.Add(Timeline(player.Id, TimelineEventTypes.MembershipStarted, isDerived: false, $"Joined clan {transition.ClanId}", now,
                    new { clanId = transition.ClanId, wolvesvilleClanId = state.ClanWolvesvilleId, source = observation.Source }));
                membershipEvidence.Add(ForMembership(membership.Id, observation, now));
            }
            else
            {
                await clans.CloseMembershipAsync(player.Id, transition.ClanId, now, cancellationToken);
                await derivations.SyncMembershipRelationshipAsync(player.Id, transition.ClanId, current: false, now, cancellationToken);
                timelineEvents.Add(Timeline(player.Id, TimelineEventTypes.MembershipEnded, isDerived: false, $"Left clan {transition.ClanId}", now,
                    new { clanId = transition.ClanId }));
            }
        }

        // --- Smart snapshot ---
        var canonicalPayload = SnapshotEngine.Canonicalize(state);
        var payloadHash = SnapshotEngine.ComputeHash(state);
        var latest = await players.GetLatestSnapshotAsync(player.Id, cancellationToken);

        PlayerSnapshot snapshot;
        bool reobserved;

        if (latest is not null && latest.PayloadHash == payloadHash)
        {
            await players.TouchSnapshotAsync(latest.Id, now, cancellationToken);
            snapshot = latest;
            reobserved = true;
        }
        else
        {
            snapshot = await players.AddSnapshotAsync(new PlayerSnapshot
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                CapturedAt = now,
                LastObservedAt = now,
                Payload = canonicalPayload,
                PayloadHash = payloadHash,
                Source = observation.Source
            }, cancellationToken);
            reobserved = false;
        }

        // --- Change detection (only when the state actually moved) ---
        var changeEntities = new List<PlayerChange>();
        if (!reobserved && latest is not null)
        {
            var previousState = SnapshotEngine.Parse(latest.Payload);
            if (previousState is not null)
            {
                var fieldChanges = ChangeDetector.Detect(previousState, state).ToList();
                foreach (var fieldChange in fieldChanges)
                {
                    changeEntities.Add(new PlayerChange
                    {
                        Id = Guid.NewGuid(),
                        PlayerId = player.Id,
                        FromSnapshotId = latest.Id,
                        ToSnapshotId = snapshot.Id,
                        Field = fieldChange.Field,
                        Kind = fieldChange.Kind,
                        OldValue = fieldChange.OldValue,
                        NewValue = fieldChange.NewValue,
                        DetectedAt = now
                    });
                }

                if (changeEntities.Count > 0)
                {
                    await players.AddChangesAsync(changeEntities, cancellationToken);
                    await derivations.AddEvidenceRangeAsync(changeEntities.SelectMany(c => EvidenceForChange(c, latest, snapshot)).ToList(), cancellationToken);
                    timelineEvents.AddRange(TimelineForChanges(player.Id, fieldChanges, latest, snapshot, now));

                    // Alerts are derived from the same change records, transactionally with them.
                    var candidates = AlertEngine.Evaluate(player.Id, state.Username, changeEntities, now);
                    if (candidates.Count > 0)
                    {
                        await alerts.AddIfNewAsync(candidates, EntityType.Player, player.Id, state.Username, now, cancellationToken);
                    }
                }
            }
        }

        if (created)
        {
            timelineEvents.Add(Timeline(player.Id, TimelineEventTypes.PlayerDiscovered, isDerived: false,
                $"Player {state.Username} discovered via {observation.Source}", now, new { wolvesvillePlayerId = state.WolvesvillePlayerId }));
        }

        if (membershipEvidence.Count > 0)
        {
            await derivations.AddEvidenceRangeAsync(membershipEvidence, cancellationToken);
        }

        if (timelineEvents.Count > 0)
        {
            await derivations.AddTimelineRangeAsync(timelineEvents, cancellationToken);
        }

        if (auditAction is not null)
        {
            await audit.WriteAsync(auditAction, $"player:{state.WolvesvillePlayerId}",
                new { username = state.Username, reobserved, changes = changeEntities.Count }, cancellationToken);
        }

        logger.LogInformation(
            "Player {WolvesvillePlayerId} observed: reobserved={Reobserved}, changes={Changes}, snapshot={SnapshotId}",
            state.WolvesvillePlayerId, reobserved, changeEntities.Count, snapshot.Id);

        var dossier = await GetDossierAsync(player.Id, cancellationToken);
        return new PlayerLookupResultDto(dossier, reobserved, changeEntities.Count);
    }

    public async Task<PlayerDossierDto> GetDossierAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var latest = await players.GetLatestSnapshotAsync(player.Id, cancellationToken);
        ObservedStateDto? observed = null;
        if (latest is not null && SnapshotEngine.Parse(latest.Payload) is { } state)
        {
            observed = ToObservedDto(state, latest);
        }

        var changes = await players.GetChangesAsync(player.Id, limit: 50, cancellationToken);
        var totalChanges = await players.CountChangesForPlayerAsync(player.Id, cancellationToken);
        var evidence = await derivations.GetEvidenceForAsync("playerChange", changes.Select(c => c.Id).ToList(), cancellationToken);
        var changeDtos = changes.Select(c => ToChangeDto(c, evidence.GetValueOrDefault(c.Id) ?? [])).ToList();

        var memberships = await clans.GetMembershipsForPlayerAsync(player.Id, cancellationToken);
        var clanIds = memberships.Select(m => m.ClanId).Distinct().ToList();
        var clanMap = await clans.GetClansByIdsAsync(clanIds, cancellationToken);
        var membershipDtos = memberships
            .OrderByDescending(m => m.IsCurrent)
            .ThenByDescending(m => m.EndedAt ?? m.StartedAt)
            .Select(m =>
            {
                var clan = clanMap.GetValueOrDefault(m.ClanId);
                return new MembershipDto(
                    m.ClanId,
                    clan?.WolvesvilleClanId ?? "?",
                    clan?.Name,
                    clan?.Name is not null,
                    m.StartedAt,
                    m.EndedAt,
                    m.IsCurrent);
            }).ToList();

        var relationships = await derivations.GetRelationshipsAsync(EntityType.Player, player.Id, cancellationToken);
        var relationshipClanIds = relationships.Where(r => r.TargetEntityType == EntityType.Clan).Select(r => r.TargetEntityId).Distinct().ToList();
        var relationshipClans = relationshipClanIds.Count > 0
            ? await clans.GetClansByIdsAsync(relationshipClanIds, cancellationToken)
            : new Dictionary<Guid, Clan>();
        var relationshipDtos = relationships.Select(r => new RelationshipDto(
            r.Type == RelationshipType.MemberOf ? "MEMBER_OF" : "PREVIOUSLY_MEMBER_OF",
            r.TargetEntityType.ToString().ToLowerInvariant(),
            r.TargetEntityId,
            r.TargetEntityType == EntityType.Clan ? relationshipClans.GetValueOrDefault(r.TargetEntityId)?.Name : null,
            r.Confidence,
            r.FirstObservedAt,
            r.LastObservedAt,
            r.IsCurrent)).ToList();

        var currentClanName = player.CurrentClanId is { } clanId ? clanMap.GetValueOrDefault(clanId)?.Name : null;

        return new PlayerDossierDto(
            new PlayerSummaryDto(player.Id, player.WolvesvillePlayerId, player.Username, player.FirstSeenAt, player.LastSeenAt, player.CurrentClanId, currentClanName),
            observed,
            new DerivedDto(totalChanges, changeDtos, membershipDtos, relationshipDtos));
    }

    public async Task<IReadOnlyList<PlayerSummaryDto>> ListLocalAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await players.SearchLocalAsync(query, cancellationToken: cancellationToken);
        return await ToSummariesAsync(results, cancellationToken);
    }

    public async Task<IReadOnlyList<SnapshotDto>> GetSnapshotsAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var snapshots = await players.GetSnapshotsAsync(player.Id, cancellationToken: cancellationToken);
        return snapshots.Select(s => new SnapshotDto(s.Id, s.CapturedAt, s.LastObservedAt, s.ObservationCount, s.PayloadHash, s.Source)).ToList();
    }

    /// <summary>Observed progression series (level, wins, games) projected from snapshot history.</summary>
    public async Task<ProgressionDto> GetProgressionAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var history = await players.GetSnapshotHistoryAsync(player.Id, cancellationToken);
        var points = new List<ProgressionPointDto>();

        foreach (var snapshot in history)
        {
            if (SnapshotEngine.Parse(snapshot.Payload) is not { } state)
            {
                continue;
            }

            points.Add(new ProgressionPointDto(
                snapshot.CapturedAt,
                snapshot.ObservationCount,
                state.Level,
                state.Wins,
                state.GamesPlayed,
                state.Achievements));
        }

        return new ProgressionDto(points);
    }

    /// <summary>Change history for a player, including the evidence rows backing each change.</summary>
    public async Task<(int Total, IReadOnlyList<ChangeDto> Changes)> GetChangesAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var total = await players.CountChangesForPlayerAsync(player.Id, cancellationToken);
        var changes = await players.GetChangesAsync(player.Id, limit: 100, cancellationToken);
        var evidence = await derivations.GetEvidenceForAsync("playerChange", changes.Select(c => c.Id).ToList(), cancellationToken);

        return (total, changes.Select(c => ToChangeDto(c, evidence.GetValueOrDefault(c.Id) ?? [])).ToList());
    }

    internal async Task<PlayerSummaryDto> ToSummaryAsync(Player player, CancellationToken cancellationToken = default)
    {
        string? clanName = null;
        if (player.CurrentClanId is { } clanId)
        {
            clanName = (await clans.GetClansByIdsAsync([clanId], cancellationToken)).GetValueOrDefault(clanId)?.Name;
        }

        return new PlayerSummaryDto(player.Id, player.WolvesvillePlayerId, player.Username, player.FirstSeenAt, player.LastSeenAt, player.CurrentClanId, clanName);
    }

    private async Task<IReadOnlyList<PlayerSummaryDto>> ToSummariesAsync(IReadOnlyList<Player> list, CancellationToken cancellationToken)
    {
        var clanIds = list.Where(p => p.CurrentClanId is not null).Select(p => p.CurrentClanId!.Value).Distinct().ToList();
        var clanMap = clanIds.Count > 0
            ? await clans.GetClansByIdsAsync(clanIds, cancellationToken)
            : new Dictionary<Guid, Clan>();

        return list
            .Select(p => new PlayerSummaryDto(p.Id, p.WolvesvillePlayerId, p.Username, p.FirstSeenAt, p.LastSeenAt, p.CurrentClanId,
                p.CurrentClanId is { } id ? clanMap.GetValueOrDefault(id)?.Name : null))
            .ToList();
    }

    private static ObservedStateDto ToObservedDto(NormalizedPlayerState state, PlayerSnapshot snapshot) => new(
        state.WolvesvillePlayerId,
        state.Username,
        state.PersonalMessage,
        state.Level,
        state.Status,
        state.LastOnline,
        state.ClanWolvesvilleId,
        state.Wins,
        state.Losses,
        state.GamesPlayed,
        state.ReceivedRosesCount,
        state.SentRosesCount,
        state.ProfileIconId,
        state.ProfileIconName,
        state.EquippedAvatarId,
        state.BadgeIds,
        state.RoleCardIds,
        state.RankedSeason,
        state.RankedWins,
        state.RankedLosses,
        state.RankedCurrentRating,
        state.RankedPlacementRating,
        state.Achievements,
        state.FriendCount,
        snapshot.CapturedAt,
        snapshot.LastObservedAt,
        snapshot.ObservationCount,
        snapshot.Source,
        snapshot.PayloadHash);

    private static ChangeDto ToChangeDto(PlayerChange change, IReadOnlyList<Evidence> evidence) => new(
        change.Id,
        change.Field,
        change.Kind.ToString(),
        change.OldValue,
        change.NewValue,
        change.DetectedAt,
        change.FromSnapshotId,
        change.ToSnapshotId,
        evidence.Select(e => new EvidenceDto(e.SourceType.ToString(), e.SourceReference, e.CapturedAt, e.PayloadHash)).ToList());

    private static IEnumerable<Evidence> EvidenceForChange(PlayerChange change, PlayerSnapshot from, PlayerSnapshot to)
    {
        yield return new Evidence
        {
            Id = Guid.NewGuid(),
            EntityType = "playerChange",
            EntityId = change.Id,
            SourceType = EvidenceSourceType.WolvesvilleApi,
            SourceReference = from.Source,
            CapturedAt = from.CapturedAt,
            PayloadHash = from.PayloadHash,
            Metadata = SerializeMeta(new { snapshotId = from.Id })
        };
        yield return new Evidence
        {
            Id = Guid.NewGuid(),
            EntityType = "playerChange",
            EntityId = change.Id,
            SourceType = EvidenceSourceType.WolvesvilleApi,
            SourceReference = to.Source,
            CapturedAt = to.CapturedAt,
            PayloadHash = to.PayloadHash,
            Metadata = SerializeMeta(new { snapshotId = to.Id })
        };
    }

    private static Evidence ForMembership(Guid membershipId, PlayerObservation observation, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = "membership",
        EntityId = membershipId,
        SourceType = EvidenceSourceType.WolvesvilleApi,
        SourceReference = observation.Source,
        CapturedAt = at,
        Metadata = SerializeMeta(new { raw = "observed clanId in player profile" })
    };

    private static IEnumerable<TimelineEvent> TimelineForChanges(
        Guid playerId, IReadOnlyList<FieldChange> fieldChanges, PlayerSnapshot from, PlayerSnapshot to, DateTimeOffset at)
    {
        foreach (var classification in ChangeDetector.Classify(fieldChanges))
        {
            var relevant = fieldChanges.Where(c => BelongsTo(c.Field, classification)).Select(c => new { c.Field, c.OldValue, c.NewValue }).ToList();
            var summary = classification switch
            {
                TimelineEventTypes.ClanChanged => $"Clan changed: {DescribeValue(fieldChanges.FirstOrDefault(c => c.Field == "clanId")?.OldValue)} → {DescribeValue(fieldChanges.FirstOrDefault(c => c.Field == "clanId")?.NewValue)}",
                TimelineEventTypes.LevelChanged => $"Level {fieldChanges.FirstOrDefault(c => c.Field == "level")?.OldValue} → {fieldChanges.FirstOrDefault(c => c.Field == "level")?.NewValue}",
                TimelineEventTypes.CosmeticsChanged => "Cosmetics changed (badges, avatar or profile icon)",
                TimelineEventTypes.ProfileChanged => "Profile data changed",
                TimelineEventTypes.RankStateChanged => "Ranked state changed",
                _ => classification
            };

            yield return Timeline(playerId, classification, isDerived: true, summary, at,
                new { changes = relevant, fromSnapshotId = from.Id, toSnapshotId = to.Id });
        }
    }

    private static bool BelongsTo(string field, string classification) => classification switch
    {
        TimelineEventTypes.ClanChanged => field == "clanId",
        TimelineEventTypes.LevelChanged => field == "level",
        TimelineEventTypes.ProfileChanged => field is "personalMessage" or "username" or "status",
        TimelineEventTypes.CosmeticsChanged => field is "badgeIds" or "equippedAvatarId" or "profileIconId" or "roleCardIds",
        TimelineEventTypes.RankStateChanged => field is "rankedSeason" or "rankedWins" or "rankedLosses" or "rankedCurrentRating",
        _ => false
    };

    private static string? DescribeValue(string? value) => string.IsNullOrEmpty(value) ? "none" : value;

    private static TimelineEvent Timeline(Guid playerId, string eventType, bool isDerived, string summary, DateTimeOffset at, object metadata) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = EntityType.Player,
        EntityId = playerId,
        EventType = eventType,
        Summary = summary,
        OccurredAt = at,
        IsDerived = isDerived,
        Confidence = isDerived ? 1.0 : null,
        Metadata = SerializeMeta(metadata)
    };

    private static string SerializeMeta(object metadata) => JsonSerializer.Serialize(metadata, MetaJson);
}
