using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Advanced;
using Stalksville.Infrastructure.Seeding;

namespace Stalksville.Worker;

/// <summary>
/// Heartbeat for admin-defined scan schedules: every tick, due schedules execute through
/// <see cref="ScanService"/> (which also dispatches change notifications). Runs are serialized —
/// one scan at a time — so schedules never compete for the upstream rate limit with each other.
/// </summary>
public sealed class ScheduledScanWorker(
    IServiceProvider services,
    ILogger<ScheduledScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = services.GetRequiredService<IConfiguration>().GetValue("Worker:ScansEnabled", true);
        var tickSeconds = Math.Max(10, services.GetRequiredService<IConfiguration>().GetValue("Worker:ScanTickSeconds", 30));

        if (!enabled)
        {
            logger.LogInformation("Scheduled scan worker disabled (Worker:ScansEnabled=false)");
            return;
        }

        // The adaptive worker applies migrations + seeds; align with it in case this host races it.
        using (var bootScope = services.CreateScope())
        {
            await bootScope.ServiceProvider.GetRequiredService<StalksvilleSeeder>().SeedAsync(stoppingToken);
        }

        logger.LogInformation("Scheduled scan worker started: tick {Tick}s", tickSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var executed = await scope.ServiceProvider.GetRequiredService<ScanService>()
                    .RunDueAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (executed > 0)
                {
                    logger.LogInformation("Scheduled scan tick executed {Count} schedule(s)", executed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled scan tick failed; retrying next tick");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(tickSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Scheduled scan worker stopped");
    }
}
