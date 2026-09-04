namespace Stalksville.Domain.Entities;

/// <summary>What a scheduled scan does when it fires.</summary>
public static class ScanKinds
{
    /// <summary>Re-observes the least-recently-seen tracked players (watched first) through the full pipeline.</summary>
    public const string PlayerRefresh = "player-refresh";

    /// <summary>Captures the current XP highscore boards and raises rank-shift alerts for tracked players.</summary>
    public const string HighscoreCapture = "highscore-capture";

    public static readonly IReadOnlyList<string> All = [PlayerRefresh, HighscoreCapture];
}

public static class ScanRunStatus
{
    public const string Ok = "ok";
    public const string Failed = "failed";
}

/// <summary>Which players a player-refresh schedule scans.</summary>
public static class ScanPlayerScopes
{
    /// <summary>Every tracked player (least recently seen first) — the default.</summary>
    public const string All = "all";

    /// <summary>Only players starred on any user's watchlist.</summary>
    public const string Watched = "watched";

    /// <summary>An explicit hand-picked selection stored per schedule.</summary>
    public const string Selected = "selected";

    public static readonly IReadOnlyList<string> AllScopes = [All, Watched, Selected];
}

public static class NotificationChannelKinds
{
    /// <summary>HTTP POST of a Discord-compatible JSON payload ({content, embeds}).</summary>
    public const string Webhook = "webhook";
}

/// <summary>
/// An admin-defined recurring scan (master plan §31). Schedules live in the database so they can
/// be managed at runtime; the worker picks up due schedules and executes them through the same
/// application services the manual UI buttons use.
/// </summary>
public sealed class ScanSchedule
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>One of <see cref="ScanKinds"/>.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Minimum spacing between runs, in minutes (>= 5).</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Upper bound of players observed per PlayerRefresh run (ignored by other kinds).</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>One of <see cref="ScanPlayerScopes"/> — which players this schedule scans.</summary>
    public string PlayerScope { get; set; } = ScanPlayerScopes.All;

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>When the worker may next fire this schedule; maintained by the scan service.</summary>
    public DateTimeOffset? NextRunAt { get; set; }
}

/// <summary>One execution of a scan schedule, with the outcome the notifications are based on.</summary>
public sealed class ScanRun
{
    public Guid Id { get; set; }

    public Guid ScheduleId { get; set; }

    /// <summary>Name snapshot at run time — runs outlive their schedule and keep history readable.</summary>
    public string ScheduleName { get; set; } = "";

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>One of <see cref="ScanRunStatus"/>.</summary>
    public string Status { get; set; } = ScanRunStatus.Ok;

    /// <summary>Players re-observed (player scans) or board entries stored (board captures).</summary>
    public int PlayersObserved { get; set; }

    /// <summary>Field-level changes detected by this run — the "anything changed" signal.</summary>
    public int ChangesDetected { get; set; }

    /// <summary>Alerts raised into the inbox by this run.</summary>
    public int AlertsRaised { get; set; }

    /// <summary>Failure detail when Status = failed.</summary>
    public string? Error { get; set; }

    /// <summary>Per-player change summary (jsonb) so runs are self-describing without joins.</summary>
    public string? DetailJson { get; set; }
}

/// <summary>One hand-picked player of a "selected"-scope schedule.</summary>
public sealed class ScanSchedulePlayer
{
    public Guid ScheduleId { get; set; }

    public Guid PlayerId { get; set; }
}

/// <summary>
/// An outbound notification target. Webhook URLs are credentials (Discord tokens) — they are
/// stored server-side only and masked in API responses.
/// </summary>
public sealed class NotificationChannel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>One of <see cref="NotificationChannelKinds"/>.</summary>
    public string Kind { get; set; } = NotificationChannelKinds.Webhook;

    public string TargetUrl { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastDeliveryAt { get; set; }

    /// <summary>Last delivery outcome, e.g. "200 OK" or "blocked: non-https host".</summary>
    public string? LastDeliveryStatus { get; set; }
}
