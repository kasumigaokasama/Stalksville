using Stalksville.Application.Abstractions;
using Stalksville.Application.Advanced;
using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Builds the relationship graph (plan §12) from tracked entities and their relationships.
/// Edges are derived intelligence and always carry confidence. Investigation scope expands from
/// the case's targets one hop through relationships (clans and co-members).
/// </summary>
public sealed class GraphService(
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    IInvestigationStore investigations)
{
    public async Task<GraphDto> BuildAsync(Guid? investigationId, CancellationToken cancellationToken = default)
    {
        var (allPlayers, allClans, relationships) =
            (await players.GetRecentAsync(1000, cancellationToken),
             await clans.GetRecentAsync(1000, cancellationToken),
             await derivations.GetAllRelationshipsAsync(cancellationToken));

        var playerById = allPlayers.ToDictionary(p => p.Id);
        var clanById = allClans.ToDictionary(c => c.Id);

        // Node scope: whole graph, or the investigation's ego network (1 hop from targets).
        HashSet<Guid> playerScope;
        HashSet<Guid> clanScope;

        if (investigationId is null)
        {
            playerScope = [.. allPlayers.Select(p => p.Id)];
            clanScope = [.. allClans.Select(c => c.Id)];
        }
        else
        {
            (playerScope, clanScope) = await ExpandFromTargetsAsync(investigationId.Value, relationships, playerById, clanById, cancellationToken);
        }

        var changeCounts = await players.GetChangeCountsAsync(cancellationToken);
        var snapshotCounts = await players.GetSnapshotCountsAsync(cancellationToken);

        var nodes = new List<GraphNodeDto>();
        foreach (var player in allPlayers.Where(p => playerScope.Contains(p.Id)))
        {
            nodes.Add(new GraphNodeDto(
                player.Id.ToString(),
                "player",
                player.Username,
                snapshotCounts.GetValueOrDefault(player.Id),
                changeCounts.GetValueOrDefault(player.Id),
                Imported: true));
        }

        foreach (var clan in allClans.Where(c => clanScope.Contains(c.Id)))
        {
            nodes.Add(new GraphNodeDto(
                clan.Id.ToString(),
                "clan",
                clan.Name ?? $"clan {clan.WolvesvilleClanId[..Math.Min(8, clan.WolvesvilleClanId.Length)]}…",
                0,
                0,
                Imported: clan.Name is not null));
        }

        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var edges = relationships
            .Where(r => nodeIds.Contains(r.SourceEntityId.ToString()) && nodeIds.Contains(r.TargetEntityId.ToString()))
            .Select(r => new GraphEdgeDto(
                r.Id.ToString(),
                r.SourceEntityId.ToString(),
                r.TargetEntityId.ToString(),
                r.Type == RelationshipType.MemberOf ? "MEMBER_OF" : "PREVIOUSLY_MEMBER_OF",
                r.Confidence,
                r.IsCurrent,
                r.FirstObservedAt,
                r.LastObservedAt))
            .ToList();

        return new GraphDto(nodes, edges);
    }

    /// <summary>Shortest connection paths between two players through shared clans (plan §4).</summary>
    public async Task<GraphPathsDto> FindPathsAsync(Guid fromPlayerId, Guid toPlayerId, CancellationToken cancellationToken = default)
    {
        var graph = await BuildAsync(null, cancellationToken);
        var labelById = graph.Nodes.ToDictionary(n => n.Id, n => n);

        var paths = GraphPaths.FindPaths(
            graph.Edges.Select(e => new SimpleEdge(e.Source, e.Target)).ToList(),
            fromPlayerId.ToString(),
            toPlayerId.ToString());

        return new GraphPathsDto(
            paths.Select(path => (IReadOnlyList<GraphPathNodeDto>)path
                .Where(id => labelById.ContainsKey(id))
                .Select(id => new GraphPathNodeDto(labelById[id].Id, labelById[id].Type, labelById[id].Label))
                .ToList())
            .ToList());
    }

    private async Task<(HashSet<Guid> Players, HashSet<Guid> Clans)> ExpandFromTargetsAsync(
        Guid investigationId,
        IReadOnlyList<Relationship> relationships,
        Dictionary<Guid, Player> playerById,
        Dictionary<Guid, Clan> clanById,
        CancellationToken cancellationToken)
    {
        var targets = await investigations.GetTargetsAsync(investigationId, cancellationToken);
        var players = new HashSet<Guid>();
        var clans = new HashSet<Guid>();

        foreach (var target in targets)
        {
            switch (target.EntityType)
            {
                case EntityType.Player when playerById.ContainsKey(target.EntityId):
                    players.Add(target.EntityId);
                    break;
                case EntityType.Clan when clanById.ContainsKey(target.EntityId):
                    clans.Add(target.EntityId);
                    break;
            }
        }

        // One hop: players ↔ their clans ↔ co-members (relationships are player→clan edges).
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var relationship in relationships)
            {
                var sourceIsPlayer = relationship.SourceEntityType == EntityType.Player;
                var playerId = sourceIsPlayer ? relationship.SourceEntityId : relationship.TargetEntityId;
                var clanId = sourceIsPlayer ? relationship.TargetEntityId : relationship.SourceEntityId;

                if (players.Contains(playerId) && !clans.Contains(clanId) && clanById.ContainsKey(clanId))
                {
                    clans.Add(clanId);
                    changed = true;
                }
                else if (clans.Contains(clanId) && !players.Contains(playerId) && playerById.ContainsKey(playerId))
                {
                    players.Add(playerId);
                    changed = true;
                }
            }
        }

        return (players, clans);
    }
}
