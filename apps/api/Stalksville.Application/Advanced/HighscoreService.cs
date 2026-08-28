using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Highscore board ingestion (plan §"more Wolvesville data"): captures the top-100 XP boards
/// (observed data) and derives rank-shift intelligence for tracked players by diffing
/// consecutive captures — every alert carries the two captures as evidence.
/// </summary>
public sealed class HighscoreService(
    IWolvesvilleClient wolvesville,
    IHighscoreStore highscores,
    IAlertStore alerts,
    IDerivationStore derivations,
    Microsoft.Extensions.Options.IOptions<AlertsOptions> alertOptions,
    ILogger<HighscoreService> logger)
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private int RankShiftThreshold => alertOptions.Value.RankShiftThreshold;

    public async Task<HighscoreCaptureResultDto> CaptureAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Captures must be strictly ordered in time, or "previous capture" becomes ambiguous
        // when two captures land on the same clock tick.
        var lastCapture = await highscores.GetLastCaptureAtAsync(cancellationToken);
        if (lastCapture is { } last && now <= last)
        {
            now = last.AddSeconds(1);
        }

        var observed = await wolvesville.GetHighscoresAsync(bypassCache, cancellationToken);

        var entries = new List<HighscoreEntry>();
        foreach (var period in HighscorePeriods.All)
        {
            var board = observed.ForPeriod(period);
            for (var index = 0; index < board.Count; index++)
            {
                var rank = board[index];
                entries.Add(new HighscoreEntry
                {
                    Id = Guid.NewGuid(),
                    Period = period,
                    Rank = index + 1,
                    WolvesvillePlayerId = rank.WolvesvillePlayerId,
                    Username = rank.Username,
                    UsernameLower = rank.Username.ToLowerInvariant(),
                    Xp = rank.Xp,
                    CapturedAt = now
                });
            }
        }

        var stored = await highscores.AddCaptureAsync(entries, cancellationToken);
        var raised = 0;

        foreach (var period in HighscorePeriods.All)
        {
            raised += await DeriveRankShiftsAsync(period, cancellationToken);
        }

        logger.LogInformation("Highscore capture: {Stored} entries, {Alerts} rank-shift alert(s)", stored, raised);

        return new HighscoreCaptureResultDto(now, stored, raised);
    }

    public async Task<HighscoreBoardDto> GetBoardAsync(string period, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePeriod(period);
        var entries = await highscores.GetLatestAsync(normalized, cancellationToken);

        var rows = entries.Select(e => new HighscoreRowDto(
            e.Rank,
            e.Username,
            e.WolvesvillePlayerId,
            e.Xp,
            e.PlayerId,
            e.PlayerId is not null)).ToList();

        return new HighscoreBoardDto(normalized, entries.Count > 0 ? entries[0].CapturedAt : null, rows);
    }

    private async Task<int> DeriveRankShiftsAsync(string period, CancellationToken cancellationToken)
    {
        var latest = await highscores.GetLatestAsync(period, cancellationToken);
        if (latest.Count == 0)
        {
            return 0;
        }

        var previous = await highscores.GetPreviousAsync(period, cancellationToken);
        if (previous.Count == 0)
        {
            return 0;
        }

        var previousByPlayerId = previous
            .Where(e => e.PlayerId is not null)
            .ToDictionary(e => e.PlayerId!.Value, e => e);

        var candidates = new List<(Guid PlayerId, string Username, AlertCandidate Candidate)>();
        var events = new List<TimelineEvent>();

        foreach (var entry in latest.Where(e => e.PlayerId is not null))
        {
            if (!previousByPlayerId.TryGetValue(entry.PlayerId!.Value, out var before))
            {
                continue; // newly tracked on the board: no baseline yet, next capture will diff
            }

            var shift = before.Rank - entry.Rank; // positive = climbed
            if (Math.Abs(shift) < RankShiftThreshold)
            {
                continue;
            }

            var direction = shift > 0 ? "climbed" : "dropped";
            candidates.Add((entry.PlayerId!.Value, entry.Username, new AlertCandidate(
                AlertKinds.RankShift,
                shift > 0 ? AlertSeverity.Info : AlertSeverity.Notice,
                $"{entry.Username} {direction} {Math.Abs(shift)} rank(s) ({period})",
                $"Highscore rank {before.Rank} → {entry.Rank} in the {period} board (XP {entry.Xp:N0}).",
                JsonSerializer.Serialize(new
                {
                    playerId = entry.PlayerId,
                    period,
                    before = new { before.Rank, before.Xp, before.CapturedAt },
                    after = new { entry.Rank, entry.Xp, entry.CapturedAt }
                }, EvidenceJson),
                $"{AlertKinds.RankShift}:{entry.PlayerId}:{period}:{entry.CapturedAt:O}")));

            events.Add(new TimelineEvent
            {
                Id = Guid.NewGuid(),
                EntityType = EntityType.Player,
                EntityId = entry.PlayerId!.Value,
                EventType = TimelineEventTypes.HighscoreRankChanged,
                Summary = $"Highscore rank ({period}): {before.Rank} → {entry.Rank}",
                OccurredAt = entry.CapturedAt,
                IsDerived = true,
                Confidence = 1.0,
                Metadata = JsonSerializer.Serialize(new { period, before.Rank, after = entry.Rank, entry.Xp }, EvidenceJson)
            });
        }

        foreach (var (playerId, username, candidate) in candidates)
        {
            await alerts.AddIfNewAsync([candidate], EntityType.Player, playerId, username, DateTimeOffset.UtcNow, cancellationToken);
        }

        if (events.Count > 0)
        {
            await derivations.AddTimelineRangeAsync(events, cancellationToken);
        }

        return candidates.Count;
    }

    private static string NormalizePeriod(string period) => period?.Trim().ToLowerInvariant() switch
    {
        HighscorePeriods.AllTime => HighscorePeriods.AllTime,
        HighscorePeriods.Monthly => HighscorePeriods.Monthly,
        HighscorePeriods.Weekly => HighscorePeriods.Weekly,
        HighscorePeriods.Daily => HighscorePeriods.Daily,
        null or "" => HighscorePeriods.AllTime,
        _ => throw new ArgumentException($"period must be one of alltime/monthly/weekly/daily, got '{period}'.")
    };
}
