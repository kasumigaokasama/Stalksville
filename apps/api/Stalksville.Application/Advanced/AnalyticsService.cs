using Stalksville.Application.Abstractions;
using Stalksville.Application.Advanced;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Advanced;

/// <summary>Activity analytics over the observed history (plan §37). All series are derived aggregates.</summary>
public sealed class AnalyticsService(
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    IInvestigationStore investigations)
{
    public async Task<AnalyticsSummaryDto> GetSummaryAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 365);

        var playerCount = await players.CountPlayersAsync(cancellationToken);
        var clanCount = await clans.CountClansAsync(cancellationToken);
        var snapshotCount = await players.CountSnapshotsAsync(cancellationToken);
        var changeCount = await players.CountChangesAsync(cancellationToken);
        var relationshipCount = await derivations.CountRelationshipsAsync(cancellationToken);

        var investigationCount = await investigations.CountAsync(includeArchived: false, cancellationToken);

        var changesPerDay = await players.GetChangeCountsPerDayAsync(days, cancellationToken);
        var snapshotsPerDay = await players.GetSnapshotCountsPerDayAsync(days, cancellationToken);
        var membershipFlow = await clans.GetMembershipFlowPerDayAsync(days, cancellationToken);

        var series = new List<AnalyticsSeriesDto>
        {
            new("Changes detected", ToPoints(changesPerDay.Select(d => (d.Date, d.Count)))),
            new("Snapshots captured", ToPoints(snapshotsPerDay.Select(d => (d.Date, d.Count)))),
            new("Membership joins", ToPoints(membershipFlow.Select(d => (d.Date, d.Joins)))),
            new("Membership leaves", ToPoints(membershipFlow.Select(d => (d.Date, d.Leaves))))
        };

        return new AnalyticsSummaryDto(
            playerCount,
            clanCount,
            snapshotCount,
            changeCount,
            relationshipCount,
            investigationCount,
            series);
    }

    private static IReadOnlyList<AnalyticsPointDto> ToPoints(IEnumerable<(DateOnly Date, int Value)> rows) =>
        rows.Select(r => new AnalyticsPointDto(r.Date.ToString("yyyy-MM-dd"), r.Value)).ToList();
}
