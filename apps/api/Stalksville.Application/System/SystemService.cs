using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.System;

public sealed class SystemService(
    IWolvesvilleClient wolvesville,
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    IAiNarrator narrator,
    IConfiguration configuration,
    ILogger<SystemService> logger)
{
    /// <summary>
    /// Connectivity + capability report. Write operations are not implemented by design; clan-bot
    /// gated endpoints are listed as requiring a clan bot and are never called by this version.
    /// </summary>
    public async Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        string status;
        string? message = null;

        if (wolvesville.Mode == "Mock")
        {
            status = "MockMode";
            message = "Running against the built-in demo dataset (no Wolvesville API key required).";
        }
        else if (!wolvesville.IsConfigured)
        {
            status = "NotConfigured";
            message = "No Wolvesville API key configured. Set Wolvesville:ApiKey via user-secrets or environment.";
        }
        else
        {
            try
            {
                await wolvesville.PingAsync(cancellationToken);
                status = "Connected";
            }
            catch (WolvesvilleApiException ex) when (ex.IsUnauthorized)
            {
                status = "InvalidKey";
                message = "Wolvesville rejected the configured API key.";
            }
            catch (Exception ex)
            {
                status = "Error";
                message = ex.Message;
                logger.LogWarning(ex, "Wolvesville connectivity check failed");
            }
        }

        var writeEnabled = bool.TryParse(configuration["Wolvesville:EnableWriteOperations"], out var flag) && flag;

        var capabilities = new List<CapabilityDto>
        {
            new("Players", "Available"),
            new("Player search by username", "Available"),
            new("Clans", "Available"),
            new("Clan members", "Available"),
            new("Highscores", "Available", "All four XP boards captured by the worker; leaderboards feed rank-shift alerts."),
            new("Ranked leaderboards", "Available", "Ranked board captured by the worker; season context and rank-shift alerts included."),
            new("Cosmetics catalogs", "Available", "Profile icon and badge names refreshed daily from the items endpoints."),
            new("Clan chat", "RequiresClanBot", "Only readable when the bot is a clan bot of the clan"),
            new("Clan announcements / ledger / logs", "RequiresClanBot", "Only readable when the bot is a clan bot of the clan"),
            new("Clan quests", "RequiresClanBot", "Only readable when the bot is a clan bot of the clan"),
            new("Write operations", writeEnabled ? "Flagged" : "Disabled",
                writeEnabled
                    ? "Wolvesville:EnableWriteOperations is on, but no write endpoints are implemented in this build — enabling them is a deliberate future product decision."
                    : "Not implemented by design (kick, block, chat, announcements, quest actions); Wolvesville:EnableWriteOperations=false"),
            new($"AI narration ({narrator.ProviderName})", narrator.Model is null ? "Available" : "Available",
                narrator.Model is null
                    ? "Deterministic template narrator — no LLM configured. Set Ai:Provider=OpenAiCompatible + Ai:ApiKey to upgrade."
                    : $"LLM: {narrator.Model}. Falls back to the deterministic narrator on any failure.")
        };

        return new ConnectionStatusDto(wolvesville.Mode, status, message, WriteOperationsEnabled: false, capabilities);
    }

    public async Task<IReadOnlyList<TimelineEventDto>> GetRecentTimelineAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        var events = await derivations.GetRecentTimelineAsync(limit, cancellationToken);
        return events.Select(e => new TimelineEventDto(
            e.Id,
            e.EntityType.ToString().ToLowerInvariant(),
            e.EntityId,
            e.EventType,
            e.Summary,
            e.OccurredAt,
            e.IsDerived,
            e.Confidence)).ToList();
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var playerCount = await players.CountPlayersAsync(cancellationToken);
        var clanCount = await clans.CountClansAsync(cancellationToken);
        var snapshotCount = await players.CountSnapshotsAsync(cancellationToken);
        var changeCount = await players.CountChangesAsync(cancellationToken);
        var relationshipCount = await derivations.CountRelationshipsAsync(cancellationToken);

        var recentChanges = await players.GetRecentChangesAsync(10, cancellationToken);
        var recentPlayers = await players.GetRecentAsync(8, cancellationToken);

        var clanIds = recentPlayers.Where(p => p.CurrentClanId is not null).Select(p => p.CurrentClanId!.Value).Distinct().ToList();
        var clanMap = clanIds.Count > 0
            ? await clans.GetClansByIdsAsync(clanIds, cancellationToken)
            : new Dictionary<Guid, Clan>();

        return new DashboardStatsDto(
            playerCount,
            clanCount,
            snapshotCount,
            changeCount,
            relationshipCount,
            recentChanges.Select(r => new ChangeWithPlayerDto(
                r.Change.Id,
                r.Player.Id,
                r.Player.Username,
                r.Change.Field,
                r.Change.OldValue,
                r.Change.NewValue,
                r.Change.DetectedAt)).ToList(),
            recentPlayers.Select(p => new PlayerSummaryDto(
                p.Id,
                p.WolvesvillePlayerId,
                p.Username,
                p.FirstSeenAt,
                p.LastSeenAt,
                p.CurrentClanId,
                p.CurrentClanId is { } id ? clanMap.GetValueOrDefault(id)?.Name : null)).ToList());
    }
}
