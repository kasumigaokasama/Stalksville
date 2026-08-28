using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Ranked leaderboard ingestion (plan round 2 §"more Wolvesville data"): captures the ranksTop
/// board (observed data, rank = array order) and derives rank-shift intelligence for tracked
/// players by diffing consecutive captures — every alert carries the two captures as evidence.
/// </summary>
public sealed class RankedService(
    IWolvesvilleClient wolvesville,
    IRankedStore ranked,
    IAlertStore alerts,
    IDerivationStore derivations,
    Microsoft.Extensions.Options.IOptions<AlertsOptions> alertOptions,
    ILogger<RankedService> logger)
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private int RankShiftThreshold => alertOptions.Value.RankShiftThreshold;

    public async Task<RankedCaptureResultDto> CaptureAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Captures must be strictly ordered in time, or "previous capture" becomes ambiguous
        // when two captures land on the same clock tick.
        var lastCapture = await ranked.GetLastCaptureAtAsync(cancellationToken);
        if (lastCapture is { } last && now <= last)
        {
            now = last.AddSeconds(1);
        }

        var observed = await wolvesville.GetRankedLeaderboardAsync(bypassCache, cancellationToken);

        // Season label is context, not a hard dependency: a failing season call must not block
        // the board capture.
        var seasonNumber = 0;
        try
        {
            seasonNumber = (await wolvesville.GetRankedSeasonAsync(cancellationToken: cancellationToken)).Number;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ranked season fetch failed; capturing with season 0");
        }

        var entries = new List<RankedEntry>();
        for (var index = 0; index < observed.Top.Count; index++)
        {
            var row = observed.Top[index];
            entries.Add(new RankedEntry
            {
                Id = Guid.NewGuid(),
                SeasonNumber = seasonNumber,
                Rank = index + 1,
                WolvesvillePlayerId = row.WolvesvillePlayerId,
                Username = row.Username,
                UsernameLower = row.Username.ToLowerInvariant(),
                Skill = row.Skill,
                CapturedAt = now
            });
        }

        var stored = await ranked.AddCaptureAsync(entries, cancellationToken);
        var raised = await DeriveRankShiftsAsync(seasonNumber, cancellationToken);

        logger.LogInformation("Ranked capture: {Stored} entries (season {Season}), {Alerts} rank-shift alert(s)",
            stored, seasonNumber, raised);

        return new RankedCaptureResultDto(now, seasonNumber, stored, raised);
    }

    public async Task<RankedBoardDto> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        var entries = await ranked.GetLatestAsync(cancellationToken);

        var rows = entries.Select(e => new RankedRowDto(
            e.Rank,
            e.Username,
            e.WolvesvillePlayerId,
            e.Skill,
            e.PlayerId,
            e.PlayerId is not null)).ToList();

        return new RankedBoardDto(
            entries.Count > 0 ? entries[0].SeasonNumber : null,
            entries.Count > 0 ? entries[0].CapturedAt : null,
            rows);
    }

    public async Task<RankedSeasonDto> GetSeasonAsync(CancellationToken cancellationToken = default)
    {
        var season = await wolvesville.GetRankedSeasonAsync(cancellationToken: cancellationToken);
        return new RankedSeasonDto(
            season.Number,
            season.StartTime,
            season.EndTime,
            season.Finished,
            season.StartSkillDefault,
            season.Source);
    }

    /// <summary>
    /// Captures one finished season's winners (default: the season before the current one).
    /// Tracked winners raise a one-time HallOfFameEntry alert — every alert cites the capture.
    /// </summary>
    public async Task<HallOfFameCaptureResultDto> CaptureHallOfFameAsync(int? seasonNumber = null, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var season = seasonNumber ?? (await wolvesville.GetRankedSeasonAsync(cancellationToken: cancellationToken)).Number - 1;        var observed = await wolvesville.GetHallOfFameAsync(season, bypassCache, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var entries = new List<HallOfFameEntry>();
        for (var index = 0; index < observed.Winners.Count; index++)
        {
            var winner = observed.Winners[index];
            entries.Add(new HallOfFameEntry
            {
                Id = Guid.NewGuid(),
                SeasonNumber = observed.SeasonNumber,
                Position = index + 1,
                WolvesvillePlayerId = winner.WolvesvillePlayerId,
                PlayerName = winner.PlayerName,
                PlayerNameLower = winner.PlayerName.ToLowerInvariant(),
                AvatarUrl = winner.AvatarUrl,
                CapturedAt = now
            });
        }

        var stored = await ranked.ReplaceHallOfFameCaptureAsync(entries, cancellationToken);
        var raised = 0;
        foreach (var entry in entries.Where(e => e.PlayerId is not null))
        {
            await alerts.AddIfNewAsync(
            [
                new AlertCandidate(
                    AlertKinds.HallOfFameEntry,
                    AlertSeverity.Info,
                    $"{entry.PlayerName} is a season {entry.SeasonNumber} ranked winner",
                    $"Finished in the top of ranked season {entry.SeasonNumber} (observed in the hall of fame, position {entry.Position}).",
                    JsonSerializer.Serialize(new
                    {
                        playerId = entry.PlayerId,
                        season = entry.SeasonNumber,
                        position = entry.Position,
                        wolvesvillePlayerId = entry.WolvesvillePlayerId,
                        capturedAt = now
                    }, EvidenceJson),
                    $"{AlertKinds.HallOfFameEntry}:{entry.PlayerId}:{entry.SeasonNumber}")
            ], EntityType.Player, entry.PlayerId!.Value, entry.PlayerName, now, cancellationToken);
            raised++;
        }

        logger.LogInformation("Hall of fame capture: season {Season}, {Stored} winners, {Alerts} tracked-winner alert(s)",
            observed.SeasonNumber, stored, raised);

        return new HallOfFameCaptureResultDto(now, observed.SeasonNumber, stored, raised);
    }

    public async Task<HallOfFameBoardDto> GetHallOfFameAsync(int? seasonNumber = null, CancellationToken cancellationToken = default)
    {
        var seasons = await ranked.GetHallOfFameSeasonsAsync(cancellationToken);
        var season = seasonNumber ?? (seasons.Count > 0 ? seasons[0] : 0);

        var entries = await ranked.GetHallOfFameAsync(season, cancellationToken);
        var rows = entries.Select(e => new HallOfFameRowDto(
            e.Position,
            e.PlayerName,
            e.WolvesvillePlayerId,
            e.AvatarUrl,
            e.PlayerId,
            e.PlayerId is not null)).ToList();

        return new HallOfFameBoardDto(
            season,
            entries.Count > 0 ? entries[0].CapturedAt : null,
            rows,
            seasons);
    }

    private async Task<int> DeriveRankShiftsAsync(int seasonNumber, CancellationToken cancellationToken)
    {
        var latest = await ranked.GetLatestAsync(cancellationToken);
        if (latest.Count == 0)
        {
            return 0;
        }

        var previous = await ranked.GetPreviousAsync(cancellationToken);
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

            var board = seasonNumber > 0 ? $"ranked S{seasonNumber}" : "ranked";
            var direction = shift > 0 ? "climbed" : "dropped";
            candidates.Add((entry.PlayerId!.Value, entry.Username, new AlertCandidate(
                AlertKinds.RankShift,
                shift > 0 ? AlertSeverity.Info : AlertSeverity.Notice,
                $"{entry.Username} {direction} {Math.Abs(shift)} rank(s) ({board})",
                $"Ranked leaderboard rank {before.Rank} → {entry.Rank} (skill {entry.Skill:N0}).",
                JsonSerializer.Serialize(new
                {
                    playerId = entry.PlayerId,
                    board = "ranked",
                    season = entry.SeasonNumber,
                    before = new { before.Rank, before.Skill, before.CapturedAt },
                    after = new { entry.Rank, entry.Skill, entry.CapturedAt }
                }, EvidenceJson),
                $"{AlertKinds.RankShift}:{entry.PlayerId}:ranked:{entry.CapturedAt:O}")));

            events.Add(new TimelineEvent
            {
                Id = Guid.NewGuid(),
                EntityType = EntityType.Player,
                EntityId = entry.PlayerId!.Value,
                EventType = TimelineEventTypes.RankedRankChanged,
                Summary = $"Ranked rank: {before.Rank} → {entry.Rank} (skill {entry.Skill:N0})",
                OccurredAt = entry.CapturedAt,
                IsDerived = true,
                Confidence = 1.0,
                Metadata = JsonSerializer.Serialize(
                    new { season = entry.SeasonNumber, before = before.Rank, after = entry.Rank, entry.Skill }, EvidenceJson)
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
}
