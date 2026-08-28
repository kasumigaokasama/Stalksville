using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Advanced;
using Stalksville.Application.Players;
using Stalksville.Infrastructure.Seeding;

namespace Stalksville.Worker;

/// <summary>
/// Background adaptive refresh (master plan §27): every interval, the least recently observed
/// tracked players are re-fetched through the full player pipeline (snapshot → changes →
/// memberships). In mock mode this makes the demo dataset evolve on its own.
/// </summary>
public sealed class AdaptiveRefreshWorker(
    IServiceProvider services,
    ILogger<AdaptiveRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, services.GetRequiredService<IConfiguration>().GetValue("Worker:IntervalMinutes", 15));
        var enabled = services.GetRequiredService<IConfiguration>().GetValue("Worker:Enabled", true);

        if (!enabled)
        {
            logger.LogInformation("Adaptive refresh worker disabled (Worker:Enabled=false)");
            return;
        }

        // Apply migrations + seed once so the worker can boot standalone.
        using (var bootScope = services.CreateScope())
        {
            await bootScope.ServiceProvider.GetRequiredService<StalksvilleSeeder>().SeedAsync(stoppingToken);
        }

        logger.LogInformation("Adaptive refresh worker started: interval {Interval}m, max {Max} player(s)/run",
            intervalMinutes,
            services.GetRequiredService<IConfiguration>().GetValue("Worker:MaxPerRun", 5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh cycle failed; retrying next interval");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Adaptive refresh worker stopped");
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var players = scope.ServiceProvider.GetRequiredService<IPlayerStore>();
        var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

        await RunRetentionAsync(scope.ServiceProvider, configuration, cancellationToken);
        await CaptureHighscoresIfDueAsync(scope.ServiceProvider, cancellationToken);
        await CaptureRankedIfDueAsync(scope.ServiceProvider, cancellationToken);
        await CaptureHallOfFameIfDueAsync(scope.ServiceProvider, cancellationToken);
        await RefreshCatalogsIfDueAsync(scope.ServiceProvider, cancellationToken);

        var maxPerRun = Math.Max(1, configuration.GetValue("Worker:MaxPerRun", 5));
        var minInterval = Math.Max(5, configuration.GetValue("Worker:MinPlayerIntervalMinutes", 60));

        var recent = await players.GetRecentAsync(1000, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // Watched players (starred by any user) jump the refresh queue; the min interval still applies.
        var watchedIds = await scope.ServiceProvider.GetRequiredService<IWatchStore>().GetWatchedPlayerIdsAsync(cancellationToken);

        var plan = RefreshScheduler.Plan(
            recent.Select(p => new RefreshCandidate(p.Id, p.WolvesvillePlayerId, p.Username, p.LastSeenAt)).ToList(),
            now,
            maxPerRun,
            minInterval,
            watchedIds);

        if (plan.Selected.Count == 0)
        {
            logger.LogDebug("No players due for refresh ({Skipped} too recent)", plan.SkippedTooRecent);
            return;
        }

        logger.LogInformation("Refreshing {Count} player(s): {Names} ({SkippedRecent} too recent, {SkippedLimit} over limit)",
            plan.Selected.Count,
            string.Join(", ", plan.Selected.Select(c => c.Username)),
            plan.SkippedTooRecent,
            plan.SkippedByLimit);

        foreach (var candidate in plan.Selected)
        {
            try
            {
                var result = await playerService.RefreshAsync(candidate.PlayerId, cancellationToken);
                logger.LogInformation("Refreshed {Username}: reobserved={Reobserved}, changes={Changes}",
                    candidate.Username, result.WasReobserved, result.ChangesDetectedInThisObservation);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh {Username}; continuing", candidate.Username);
            }
        }
    }

    /// <summary>Snapshot retention, off by default (Worker:SnapshotRetentionDays=0).</summary>
    private static async Task RunRetentionAsync(IServiceProvider scoped, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var retentionDays = configuration.GetValue("Worker:SnapshotRetentionDays", 0);
        if (retentionDays <= 0)
        {
            return;
        }

        try
        {
            await scoped.GetRequiredService<Infrastructure.Retention.RetentionService>()
                .PruneSnapshotsAsync(retentionDays, cancellationToken);
        }
        catch (Exception ex)
        {
            scoped.GetRequiredService<ILogger<AdaptiveRefreshWorker>>()
                .LogWarning(ex, "Snapshot retention failed; retrying next cycle");
        }
    }

    /// <summary>Highscore boards are captured at most once per capture interval (default 24h).</summary>
    private async Task CaptureHighscoresIfDueAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var highscores = scoped.GetRequiredService<Application.Advanced.HighscoreService>();
        var store = scoped.GetRequiredService<IHighscoreStore>();
        var intervalHours = Math.Max(1, services.GetRequiredService<IConfiguration>().GetValue("Worker:HighscoreCaptureIntervalHours", 24));

        var lastCapture = await store.GetLastCaptureAtAsync(cancellationToken);
        if (lastCapture is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromHours(intervalHours))
        {
            return;
        }

        try
        {
            var result = await highscores.CaptureAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Highscore capture: {Entries} entries, {Alerts} rank-shift alert(s)", result.EntriesStored, result.RankShiftAlerts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Highscore capture failed; retrying next cycle");
        }
    }

    /// <summary>The ranked board is captured at most once per interval (default 24h), separately from XP boards.</summary>
    private async Task CaptureRankedIfDueAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var ranked = scoped.GetRequiredService<Application.Advanced.RankedService>();
        var store = scoped.GetRequiredService<IRankedStore>();
        var intervalHours = Math.Max(1, services.GetRequiredService<IConfiguration>().GetValue("Worker:RankedCaptureIntervalHours", 24));

        var lastCapture = await store.GetLastCaptureAtAsync(cancellationToken);
        if (lastCapture is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromHours(intervalHours))
        {
            return;
        }

        try
        {
            var result = await ranked.CaptureAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Ranked capture: {Entries} entries (season {Season}), {Alerts} rank-shift alert(s)",
                result.EntriesStored, result.SeasonNumber, result.RankShiftAlerts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ranked capture failed; retrying next cycle");
        }
    }

    /// <summary>Finished seasons are immutable: each is captured once, when first seen.</summary>
    private async Task CaptureHallOfFameIfDueAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var ranked = scoped.GetRequiredService<Application.Advanced.RankedService>();
        var store = scoped.GetRequiredService<IRankedStore>();

        int previousSeason;
        try
        {
            previousSeason = (await ranked.GetSeasonAsync(cancellationToken)).Number - 1;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ranked season fetch failed; hall-of-fame capture skipped this cycle");
            return;
        }

        var captured = await store.GetHallOfFameAsync(previousSeason, cancellationToken);
        if (captured.Count > 0)
        {
            return; // already captured — finished seasons never change
        }

        try
        {
            var result = await ranked.CaptureHallOfFameAsync(previousSeason, cancellationToken: cancellationToken);
            logger.LogInformation("Hall of fame capture: season {Season}, {Winners} winners, {Alerts} tracked-winner alert(s)",
                result.SeasonNumber, result.EntriesStored, result.TrackedWinnerAlerts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hall of fame capture failed; retrying next cycle");
        }
    }

    /// <summary>Cosmetics catalogs refresh daily (default) so id → name resolution stays current.</summary>
    private async Task RefreshCatalogsIfDueAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var catalog = scoped.GetRequiredService<Application.Advanced.CatalogService>();
        var store = scoped.GetRequiredService<ICatalogStore>();
        var intervalHours = Math.Max(1, services.GetRequiredService<IConfiguration>().GetValue("Worker:CatalogRefreshIntervalHours", 24));

        var lastRefresh = await store.GetLastRefreshedAtAsync(cancellationToken);
        if (lastRefresh is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromHours(intervalHours))
        {
            return;
        }

        try
        {
            var (icons, badges) = await catalog.RefreshAsync(cancellationToken);
            logger.LogInformation("Catalog refresh: {Icons} profile icons, {Badges} badges", icons, badges);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Catalog refresh failed; retrying next cycle");
        }
    }
}
