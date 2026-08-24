using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Application.Players;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Clans;

public sealed class ClanService(
    IWolvesvilleClient wolvesville,
    IClanStore clans,
    IPlayerStore players,
    IDerivationStore derivations,
    PlayerService playerService,
    IAuditLog audit,
    ILogger<ClanService> logger)
{
    /// <summary>Live clan search against Wolvesville. Results are transient (not persisted until import).</summary>
    public async Task<IReadOnlyList<ClanSummaryDto>> SearchAsync(string name, bool exact = false, CancellationToken cancellationToken = default)
    {
        var results = await wolvesville.SearchClansAsync(name, exactName: exact, cancellationToken: cancellationToken);
        await audit.WriteAsync(AuditActions.ClanSearched, $"clan-search:{name}", new { exact, results = results.Count }, cancellationToken);
        return results.Select(ToSummary).ToList();
    }

    /// <summary>
    /// Imports a clan: clan info + full member list. Every member observation runs the full player
    /// pipeline (upsert → snapshot → changes → memberships). Members that previously had an open
    /// membership in this clan but are absent from the member list get their membership closed.
    /// </summary>
    public async Task<ClanDossierDto> ImportAsync(string wolvesvilleClanId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var info = await wolvesville.GetClanInfoAsync(wolvesvilleClanId, bypassCache: true, cancellationToken: cancellationToken);
        var (clan, created) = await clans.UpsertClanAsync(info, now, markImported: true, cancellationToken);

        var members = await wolvesville.GetClanMembersAsync(wolvesvilleClanId, bypassCache: true, cancellationToken: cancellationToken);
        var memberWolfIds = new HashSet<string>(members.Select(m => m.State.WolvesvillePlayerId), StringComparer.Ordinal);

        var memberPlayerIds = new List<Guid>();
        foreach (var observation in members)
        {
            var result = await playerService.ApplyObservationAsync(observation, auditAction: null, cancellationToken: cancellationToken);
            memberPlayerIds.Add(result.Dossier.Player.Id);
        }

        // Close open memberships of players that are no longer in the clan (only detectable for
        // players we already know about).
        var openMemberships = await clans.GetOpenMembershipsInClanAsync(clan.Id, cancellationToken);
        var openPlayerIds = openMemberships.Select(m => m.PlayerId).Distinct().ToList();
        var openPlayers = openPlayerIds.Count > 0
            ? (await players.GetPlayersByIdsAsync(openPlayerIds, cancellationToken)).ToDictionary(p => p.Id, p => p.WolvesvillePlayerId)
            : [];

        foreach (var membership in openMemberships)
        {
            var wolfId = openPlayers.GetValueOrDefault(membership.PlayerId);
            if (wolfId is not null && !memberWolfIds.Contains(wolfId))
            {
                await clans.CloseMembershipAsync(membership.PlayerId, clan.Id, now, cancellationToken);
                await derivations.SyncMembershipRelationshipAsync(membership.PlayerId, clan.Id, current: false, now, cancellationToken);
                await derivations.AddTimelineRangeAsync(
                [
                    new TimelineEvent
                    {
                        Id = Guid.NewGuid(),
                        EntityType = EntityType.Player,
                        EntityId = membership.PlayerId,
                        EventType = TimelineEventTypes.MembershipEnded,
                        Summary = $"Left clan {clan.Name ?? clan.WolvesvilleClanId}",
                        OccurredAt = now,
                        IsDerived = false,
                        Metadata = $$"""{"clanId":"{{clan.Id}}","source":"clanMemberImport"}"""
                    }
                ], cancellationToken);
            }
        }

        await derivations.AddTimelineRangeAsync(
        [
            new TimelineEvent
            {
                Id = Guid.NewGuid(),
                EntityType = EntityType.Clan,
                EntityId = clan.Id,
                EventType = TimelineEventTypes.ClanImported,
                Summary = created
                    ? $"Clan {clan.Name} imported with {members.Count} members"
                    : $"Clan {clan.Name} re-imported with {members.Count} members",
                OccurredAt = now,
                IsDerived = false,
                Metadata = $$"""{"wolvesvilleClanId":"{{clan.WolvesvilleClanId}}","memberCount":{{members.Count}}}"""
            }
        ], cancellationToken);

        await audit.WriteAsync(AuditActions.ClanImported, $"clan:{clan.WolvesvilleClanId}",
            new { clan.Name, members = members.Count, created }, cancellationToken);

        logger.LogInformation("Clan {WolvesvilleClanId} imported with {Members} members", clan.WolvesvilleClanId, members.Count);

        return await GetDossierAsync(clan.Id, cancellationToken);
    }

    public async Task<ClanDossierDto> GetDossierAsync(Guid clanId, CancellationToken cancellationToken = default)
    {
        var clan = await clans.FindByIdAsync(clanId, cancellationToken)
            ?? throw new EntityNotFoundException("clan", clanId);

        var open = await clans.GetOpenMembershipsInClanAsync(clan.Id, cancellationToken);
        var playerIds = open.Select(m => m.PlayerId).Distinct().ToList();
        var memberPlayers = playerIds.Count > 0
            ? await players.GetPlayersByIdsAsync(playerIds, cancellationToken)
            : [];

        var historyCount = await clans.CountMembershipsForClanAsync(clan.Id, cancellationToken);

        var memberDtos = memberPlayers
            .OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PlayerSummaryDto(p.Id, p.WolvesvillePlayerId, p.Username, p.FirstSeenAt, p.LastSeenAt, p.CurrentClanId, clan.Name))
            .ToList();

        return new ClanDossierDto(ToSummary(clan), memberDtos, open.Count, historyCount);
    }

    public async Task<IReadOnlyList<ClanSummaryDto>> ListLocalAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await clans.SearchLocalAsync(query, cancellationToken: cancellationToken);
        return results.Select(ToSummary).ToList();
    }

    private static ClanSummaryDto ToSummary(Clan clan) => new(
        clan.Id,
        clan.WolvesvilleClanId,
        clan.Name,
        clan.Description,
        clan.MemberCount,
        clan.LanguageCode,
        clan.JoinType,
        clan.Xps,
        clan.Level,
        clan.FirstSeenAt,
        clan.LastSeenAt,
        clan.LastImportedAt);

    private static ClanSummaryDto ToSummary(ObservedClan observed) => new(
        Guid.Empty,
        observed.WolvesvilleClanId,
        observed.Name,
        observed.Description,
        observed.MemberCount,
        observed.LanguageCode,
        observed.JoinType,
        observed.Xps,
        observed.Level,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        null);
}
