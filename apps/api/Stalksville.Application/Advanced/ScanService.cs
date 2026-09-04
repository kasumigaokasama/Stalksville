using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Application.Players;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Advanced;

/// <summary>
/// Scheduled scans (master plan §31): admin-defined schedules stored in the database, executed by
/// the worker (or on demand) through the same application services the manual UI uses. When a run
/// detects changes or raises alerts, every enabled notification channel is called — the in-app
/// alert inbox remains the per-user record of the same events.
/// </summary>
public sealed class ScanService(
    IScanStore scans,
    INotificationStore notifications,
    INotificationDispatcher dispatcher,
    IPlayerStore players,
    IWatchStore watch,
    PlayerService playerService,
    HighscoreService highscores,
    ILogger<ScanService> logger)
{
    public const int MinimumIntervalMinutes = 5;
    public const int MaximumIntervalMinutes = 10_080; // one week
    public const int MinimumBatchSize = 1;
    public const int MaximumBatchSize = 100;
    public const int DefaultBatchSize = 10;

    /// <summary>The per-player refresh floor shared with the adaptive worker, so both never hammer a player.</summary>
    private const int MinPlayerIntervalMinutes = 60;

    public Task<IReadOnlyList<ScanSchedule>> ListSchedulesAsync(CancellationToken cancellationToken = default) =>
        scans.ListAsync(cancellationToken);

    public Task<IReadOnlyList<ScanRun>> ListRunsAsync(int limit, int offset, CancellationToken cancellationToken = default) =>
        scans.ListRunsAsync(limit, offset, cancellationToken);

    public async Task<ScanSchedule> CreateScheduleAsync(UpsertScanScheduleDto request, CancellationToken cancellationToken = default)
    {
        var kind = request.Kind ?? throw new ValidationException("Scan kind is required.");
        var interval = request.IntervalMinutes ?? throw new ValidationException("Interval is required.");
        ValidateSchedule(request.Name, kind, interval, request.BatchSize);

        var now = DateTimeOffset.UtcNow;
        var schedule = new ScanSchedule
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Kind = kind,
            IntervalMinutes = interval,
            BatchSize = request.BatchSize ?? DefaultBatchSize,
            Enabled = request.Enabled ?? true,
            CreatedAt = now,
            UpdatedAt = now,
            // A fresh schedule is due immediately — the next worker tick picks it up.
            NextRunAt = now,
        };

        await scans.AddAsync(schedule, cancellationToken);
        logger.LogInformation("Created scan schedule {Name} ({Kind}, every {Interval}m)", schedule.Name, schedule.Kind, schedule.IntervalMinutes);
        return schedule;
    }

    public async Task<ScanSchedule?> UpdateScheduleAsync(Guid id, UpsertScanScheduleDto request, CancellationToken cancellationToken = default)
    {
        var schedule = await scans.FindAsync(id, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var kind = request.Kind ?? schedule.Kind;
        var interval = request.IntervalMinutes ?? schedule.IntervalMinutes;
        ValidateSchedule(request.Name, kind, interval, request.BatchSize ?? schedule.BatchSize);

        schedule.Name = request.Name.Trim();
        if (request.Kind is not null && request.Kind != schedule.Kind)
        {
            schedule.Kind = request.Kind;
        }
        schedule.IntervalMinutes = request.IntervalMinutes ?? schedule.IntervalMinutes;
        schedule.BatchSize = request.BatchSize ?? schedule.BatchSize;
        var wasEnabled = schedule.Enabled;
        schedule.Enabled = request.Enabled ?? schedule.Enabled;
        schedule.UpdatedAt = DateTimeOffset.UtcNow;

        // Re-arming on edits keeps the cadence honest: a new interval starts counting from now,
        // and re-enabling a paused schedule makes it due again rather than sleeping forever.
        schedule.NextRunAt = schedule.Enabled ? DateTimeOffset.UtcNow + TimeSpan.FromMinutes(schedule.IntervalMinutes) : null;
        if (!wasEnabled && schedule.Enabled)
        {
            schedule.NextRunAt = DateTimeOffset.UtcNow;
        }

        await scans.UpdateAsync(schedule, cancellationToken);
        return schedule;
    }

    public Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        scans.RemoveAsync(id, cancellationToken);

    /// <summary>Runs one schedule right now regardless of NextRunAt, recording the run.</summary>
    /// <remarks>Manual runs bypass the per-player refresh floor: the admin asked for this batch
    /// explicitly, and it stays bounded by the schedule's batch size.</remarks>
    public async Task<ScanRun?> RunNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await scans.FindAsync(id, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var run = await ExecuteAsync(schedule, trigger: "manual", minPlayerIntervalMinutes: 0, cancellationToken);
        return run;
    }

    /// <summary>The worker heartbeat: executes every enabled schedule that is due.</summary>
    public async Task<int> RunDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var due = await scans.GetDueAsync(now, cancellationToken);
        foreach (var schedule in due)
        {
            try
            {
                await ExecuteAsync(schedule, trigger: "scheduled", minPlayerIntervalMinutes: MinPlayerIntervalMinutes, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled scan {Name} failed; it will not run again until due", schedule.Name);
            }
        }

        return due.Count;
    }

    private async Task<ScanRun> ExecuteAsync(ScanSchedule schedule, string trigger, int minPlayerIntervalMinutes, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var run = new ScanRun
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            ScheduleName = schedule.Name,
            StartedAt = startedAt,
        };

        try
        {
            switch (schedule.Kind)
            {
                case ScanKinds.PlayerRefresh:
                    await RunPlayerRefreshAsync(schedule, run, minPlayerIntervalMinutes, cancellationToken);
                    break;
                case ScanKinds.HighscoreCapture:
                    await RunHighscoreCaptureAsync(schedule, run, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown scan kind '{schedule.Kind}'.");
            }

            run.Status = ScanRunStatus.Ok;
        }
        catch (Exception ex)
        {
            run.Status = ScanRunStatus.Failed;
            run.Error = ex.Message;
            logger.LogWarning(ex, "Scan {Name} ({Kind}) failed", schedule.Name, schedule.Kind);
        }

        run.FinishedAt = DateTimeOffset.UtcNow;
        await scans.AddRunAsync(run, cancellationToken);

        schedule.LastRunAt = run.FinishedAt;
        schedule.NextRunAt = schedule.Enabled ? run.FinishedAt + TimeSpan.FromMinutes(schedule.IntervalMinutes) : null;
        schedule.UpdatedAt = DateTimeOffset.UtcNow;
        await scans.UpdateAsync(schedule, cancellationToken);

        logger.LogInformation("Scan {Name} ({Trigger}, {Kind}): observed={Observed}, changes={Changes}, alerts={Alerts}, status={Status}",
            schedule.Name, trigger, schedule.Kind, run.PlayersObserved, run.ChangesDetected, run.AlertsRaised, run.Status);

        // "Notify if anything changed": a run with zero changes and zero alerts stays silent.
        if (run is { Status: ScanRunStatus.Ok, ChangesDetected: > 0 } or { Status: ScanRunStatus.Ok, AlertsRaised: > 0 })
        {
            await NotifyAsync(schedule, run, cancellationToken);
        }

        return run;
    }

    private async Task RunPlayerRefreshAsync(ScanSchedule schedule, ScanRun run, int minPlayerIntervalMinutes, CancellationToken cancellationToken)
    {
        var recent = await players.GetRecentAsync(1000, cancellationToken);
        var watchedIds = await watch.GetWatchedPlayerIdsAsync(cancellationToken);

        var plan = RefreshScheduler.Plan(
            recent.Select(p => new RefreshCandidate(p.Id, p.WolvesvillePlayerId, p.Username, p.LastSeenAt)).ToList(),
            run.StartedAt,
            schedule.BatchSize,
            minPlayerIntervalMinutes,
            watchedIds);

        var lines = new List<ScanChangeLine>();
        foreach (var candidate in plan.Selected)
        {
            try
            {
                var result = await playerService.RefreshAsync(candidate.PlayerId, cancellationToken);
                run.PlayersObserved++;
                run.ChangesDetected += result.ChangesDetectedInThisObservation;
                if (result.ChangesDetectedInThisObservation > 0)
                {
                    lines.Add(new ScanChangeLine(candidate.Username, result.ChangesDetectedInThisObservation));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scan {Name}: failed to refresh {Username}; continuing", schedule.Name, candidate.Username);
            }
        }

        run.DetailJson = lines.Count > 0
            ? global::System.Text.Json.JsonSerializer.Serialize(lines)
            : null;
    }

    private async Task RunHighscoreCaptureAsync(ScanSchedule schedule, ScanRun run, CancellationToken cancellationToken)
    {
        var result = await highscores.CaptureAsync(bypassCache: true, cancellationToken: cancellationToken);
        run.PlayersObserved = result.EntriesStored;
        run.AlertsRaised = result.RankShiftAlerts;
        run.ChangesDetected = result.RankShiftAlerts; // board movement that crossed the alert threshold
    }

    private async Task NotifyAsync(ScanSchedule schedule, ScanRun run, CancellationToken cancellationToken)
    {
        var channels = await notifications.ListAsync(enabledOnly: true, cancellationToken);
        if (channels.Count == 0)
        {
            return;
        }

        var lines = DeserializeLines(run.DetailJson);
        var notification = new ScanNotification(
            schedule.Name,
            schedule.Kind,
            run.FinishedAt,
            run.PlayersObserved,
            run.ChangesDetected,
            run.AlertsRaised,
            lines);

        foreach (var channel in channels)
        {
            try
            {
                var (success, detail) = await dispatcher.SendAsync(channel, notification, cancellationToken);
                channel.LastDeliveryAt = DateTimeOffset.UtcNow;
                channel.LastDeliveryStatus = detail;
                await notifications.UpdateAsync(channel, cancellationToken);
                if (!success)
                {
                    logger.LogWarning("Notification channel {Name} delivery failed: {Detail}", channel.Name, detail);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification channel {Name} threw during delivery", channel.Name);
            }
        }
    }

    /// <summary>Sends a fixed test notification to one channel and records the delivery outcome.</summary>
    public async Task<(bool Success, string Detail)?> TestChannelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var channel = await notifications.FindAsync(id, cancellationToken);
        if (channel is null)
        {
            return null;
        }

        var (success, detail) = await dispatcher.SendAsync(channel, new ScanNotification(
            channel.Name,
            "test",
            DateTimeOffset.UtcNow,
            0,
            0,
            0,
            [],
            IsTest: true), cancellationToken);

        channel.LastDeliveryAt = DateTimeOffset.UtcNow;
        channel.LastDeliveryStatus = detail;
        await notifications.UpdateAsync(channel, cancellationToken);
        return (success, detail);
    }

    public async Task<NotificationChannel> CreateChannelAsync(UpsertNotificationChannelDto request, CancellationToken cancellationToken = default)
    {
        var targetUrl = request.TargetUrl?.Trim() ?? throw new ValidationException("Webhook URL is required.");
        ValidateChannel(request.Name, targetUrl);
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Kind = NotificationChannelKinds.Webhook,
            TargetUrl = targetUrl,
            Enabled = request.Enabled ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await notifications.AddAsync(channel, cancellationToken);
        logger.LogInformation("Created notification channel {Name}", channel.Name);
        return channel;
    }

    public async Task<NotificationChannel?> UpdateChannelAsync(Guid id, UpsertNotificationChannelDto request, CancellationToken cancellationToken = default)
    {
        var channel = await notifications.FindAsync(id, cancellationToken);
        if (channel is null)
        {
            return null;
        }

        var targetUrl = request.TargetUrl?.Trim() ?? channel.TargetUrl;
        ValidateChannel(request.Name, targetUrl);
        channel.Name = request.Name.Trim();
        if (targetUrl != channel.TargetUrl)
        {
            channel.TargetUrl = targetUrl;
        }
        channel.Enabled = request.Enabled ?? channel.Enabled;
        await notifications.UpdateAsync(channel, cancellationToken);
        return channel;
    }

    public Task<bool> DeleteChannelAsync(Guid id, CancellationToken cancellationToken = default) =>
        notifications.RemoveAsync(id, cancellationToken);

    public Task<IReadOnlyList<NotificationChannel>> ListChannelsAsync(CancellationToken cancellationToken = default) =>
        notifications.ListAsync(enabledOnly: false, cancellationToken);

    public static IReadOnlyList<ScanChangeLine> DeserializeLines(string? detailJson)
    {
        if (string.IsNullOrEmpty(detailJson))
        {
            return [];
        }

        try
        {
            return global::System.Text.Json.JsonSerializer.Deserialize<List<ScanChangeLine>>(detailJson) ?? [];
        }
        catch (global::System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static void ValidateSchedule(string name, string kind, int intervalMinutes, int? batchSize)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 64)
        {
            throw new ValidationException("Schedule name must be 3–64 characters.");
        }

        if (!ScanKinds.All.Contains(kind))
        {
            throw new ValidationException($"Unknown scan kind '{kind}'.");
        }

        if (intervalMinutes is < MinimumIntervalMinutes or > MaximumIntervalMinutes)
        {
            throw new ValidationException($"Interval must be {MinimumIntervalMinutes}–{MaximumIntervalMinutes} minutes.");
        }

        if (batchSize is < MinimumBatchSize or > MaximumBatchSize)
        {
            throw new ValidationException($"Batch size must be {MinimumBatchSize}–{MaximumBatchSize}.");
        }
    }

    private static void ValidateChannel(string name, string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 64)
        {
            throw new ValidationException("Channel name must be 3–64 characters.");
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ValidationException("Webhook URL must be a valid https:// URL.");
        }
    }
}

/// <summary>Human-readable validation failure; the API middleware maps it to a 400 ProblemDetails.</summary>
public sealed class ValidationException(string message) : ArgumentException(message);
