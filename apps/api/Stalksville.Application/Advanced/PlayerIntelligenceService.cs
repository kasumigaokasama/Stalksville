using Stalksville.Application.Abstractions;
using Stalksville.Application.Advanced;
using Stalksville.Application.Players;
using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Per-player advanced intelligence: explainable exposure (§15), anomaly insights (§14) and
/// two-player comparison with overlaps (§16). Everything returned is derived and evidence-backed.
/// </summary>
public sealed class PlayerIntelligenceService(
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    Players.PlayerService dossiers)
{
    public async Task<ExposureResultDto> GetExposureAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var (player, state, memberships, relationships) = await LoadContextAsync(playerId, cancellationToken);
        state ??= new NormalizedPlayerState { WolvesvillePlayerId = player.WolvesvillePlayerId, Username = player.Username };

        int? clanMemberCount = null;
        if (player.CurrentClanId is { } clanId)
        {
            clanMemberCount = (await clans.FindByIdAsync(clanId, cancellationToken))?.MemberCount;
        }

        var result = ExposureAnalyzer.Analyze(state, memberships, relationships, clanMemberCount);

        return new ExposureResultDto(
            result.Overall,
            result.Categories.Select(c => new ExposureCategoryDto(
                c.Name,
                c.Score,
                c.Factors.Select(f => new ExposureFactorDto(f.Label, f.Points, f.Evidence)).ToList())).ToList());
    }

    public async Task<IReadOnlyList<InsightDto>> GetInsightsAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var changes = await players.GetChangesAsync(player.Id, limit: 200, cancellationToken);
        var insights = InsightGenerator.Generate(changes);

        return insights.Select(i => new InsightDto(
            i.Classification,
            i.Title,
            i.Description,
            i.Confidence,
            i.EvidenceChangeIds)).ToList();
    }

    public async Task<CompareResultDto> CompareAsync(Guid playerIdA, Guid playerIdB, CancellationToken cancellationToken = default)
    {
        if (playerIdA == playerIdB)
        {
            throw new ArgumentException("Pick two different players to compare.");
        }

        var contextA = await LoadContextAsync(playerIdA, cancellationToken);
        var contextB = await LoadContextAsync(playerIdB, cancellationToken);

        var fields = BuildFields(contextA.State, contextB.State);

        var overlaps = BuildOverlaps(contextA, contextB);

        var dossierA = await dossiers.GetDossierAsync(playerIdA, cancellationToken);
        var dossierB = await dossiers.GetDossierAsync(playerIdB, cancellationToken);

        return new CompareResultDto(
            dossierA,
            dossierB,
            fields,
            overlaps,
            "Correlation is not proof. Overlaps are computed from observed snapshots and derived membership history; they never establish that two accounts belong to the same person.");
    }

    private async Task<PlayerContext> LoadContextAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("player", playerId);

        var latest = await players.GetLatestSnapshotAsync(player.Id, cancellationToken);
        var state = latest is null ? null : SnapshotEngine.Parse(latest.Payload);
        var memberships = await clans.GetMembershipsForPlayerAsync(player.Id, cancellationToken);
        var relationships = await derivations.GetRelationshipsAsync(EntityType.Player, player.Id, cancellationToken);

        return new PlayerContext(player, state, memberships, relationships);
    }

    private static List<CompareFieldDto> BuildFields(NormalizedPlayerState? a, NormalizedPlayerState? b)
    {
        string? Value(NormalizedPlayerState? s, Func<NormalizedPlayerState, object?> selector)
            => s is null ? null : selector(s)?.ToString();

        return
        [
            new("Username", Value(a, s => s.Username), Value(b, s => s.Username)),
            new("Level", Value(a, s => s.Level), Value(b, s => s.Level)),
            new("Clan", Value(a, s => s.ClanWolvesvilleId), Value(b, s => s.ClanWolvesvilleId)),
            new("Wins", Value(a, s => s.Wins), Value(b, s => s.Wins)),
            new("Losses", Value(a, s => s.Losses), Value(b, s => s.Losses)),
            new("Games played", Value(a, s => s.GamesPlayed), Value(b, s => s.GamesPlayed)),
            new("Badges", Value(a, s => s.BadgeIds.Count), Value(b, s => s.BadgeIds.Count)),
            new("Ranked rating", Value(a, s => s.RankedCurrentRating), Value(b, s => s.RankedCurrentRating)),
            new("Achievements", Value(a, s => s.Achievements), Value(b, s => s.Achievements)),
            new("Friends", Value(a, s => s.FriendCount), Value(b, s => s.FriendCount))
        ];
    }

    private static List<CompareOverlapDto> BuildOverlaps(PlayerContext a, PlayerContext b)
    {
        var overlaps = new List<CompareOverlapDto>();

        var currentClansA = a.Memberships.Where(m => m.IsCurrent).Select(m => m.ClanId).ToHashSet();
        var currentClansB = b.Memberships.Where(m => m.IsCurrent).Select(m => m.ClanId).ToHashSet();
        var historicalClansA = a.Memberships.Select(m => m.ClanId).ToHashSet();
        var historicalClansB = b.Memberships.Select(m => m.ClanId).ToHashSet();

        var sharedCurrent = currentClansA.Intersect(currentClansB).ToList();
        if (sharedCurrent.Count > 0)
        {
            overlaps.Add(new CompareOverlapDto(
                "Shared current clan", $"{sharedCurrent.Count} clan(s)",
                1.0, "both players have an open clan_memberships row for the same clan"));
        }

        var sharedHistorical = historicalClansA.Intersect(historicalClansB).ToList();
        if (sharedHistorical.Count > 0)
        {
            overlaps.Add(new CompareOverlapDto(
                "Shared clan history", $"{sharedHistorical.Count} clan(s)",
                0.9, "membership history rows overlap"));
        }

        if (a.State is not null && b.State is not null)
        {
            var sharedBadges = a.State.BadgeIds.Intersect(b.State.BadgeIds).ToList();
            if (sharedBadges.Count > 0)
            {
                overlaps.Add(new CompareOverlapDto(
                    "Shared badges", $"{sharedBadges.Count} badge(s)",
                    0.5, "badgeIds intersect in latest snapshots (weak signal — badges are earned by many players)"));
            }

            if (a.State.ClanWolvesvilleId is null && b.State.ClanWolvesvilleId is null)
            {
                overlaps.Add(new CompareOverlapDto(
                    "Both clanless", "no current clan",
                    0.3, "both latest snapshots show no clanId"));
            }
        }

        if (overlaps.Count == 0)
        {
            overlaps.Add(new CompareOverlapDto("No overlaps found", "none", 0.0, "no shared clans or badges in the available history"));
        }

        return overlaps;
    }

    private sealed record PlayerContext(
        Player Player,
        NormalizedPlayerState? State,
        IReadOnlyList<ClanMembership> Memberships,
        IReadOnlyList<Relationship> Relationships);
}
