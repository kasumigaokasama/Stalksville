using System.Globalization;
using System.Text.Json;
using Stalksville.Domain.Entities;

namespace Stalksville.Intelligence.Engine;

/// <summary>An alert candidate before persistence; evidence ids reference PlayerChange rows.</summary>
public sealed record AlertCandidate(
    string Kind,
    AlertSeverity Severity,
    string Title,
    string Body,
    string EvidenceJson,
    string DedupeKey);

/// <summary>
/// Turns freshly detected player changes into derived alerts. Pure function of the change batch —
/// every alert carries the change ids that justify it, so the inbox can deep-link to evidence.
/// Rules are deliberately conservative: only identity-relevant movement alerts, ordinary drift
/// (wins ticking up, cosmetics) stays in the timeline where it belongs.
/// </summary>
public static class AlertEngine
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>|Δlevel| at or above this raises a LevelJump alert (from Alerts:LevelJumpThreshold).</summary>
    public const int DefaultLevelJumpThreshold = 10;

    public static IReadOnlyList<AlertCandidate> Evaluate(
        Guid playerId,
        string username,
        IReadOnlyList<PlayerChange> changes,
        DateTimeOffset at,
        int? levelJumpThreshold = null)
    {
        var threshold = levelJumpThreshold ?? DefaultLevelJumpThreshold;
        var candidates = new List<AlertCandidate>();

        foreach (var change in changes)
        {
            switch (change.Field)
            {
                case "clanId":
                    candidates.Add(new AlertCandidate(
                        AlertKinds.ClanChanged,
                        AlertSeverity.Notice,
                        $"{username} changed clan",
                        $"Clan {Describe(change.OldValue)} → {Describe(change.NewValue)}.",
                        Evidence(playerId, [change]),
                        Key(AlertKinds.ClanChanged, playerId, change)));
                    break;

                case "username":
                    candidates.Add(new AlertCandidate(
                        AlertKinds.UsernameChanged,
                        AlertSeverity.Warning,
                        $"{username} was renamed",
                        $"Username changed from {change.OldValue} to {change.NewValue} — earlier notes and snapshots keep the old name.",
                        Evidence(playerId, [change]),
                        Key(AlertKinds.UsernameChanged, playerId, change)));
                    break;

                case "level" when Math.Abs(ParseLevel(change.NewValue) - ParseLevel(change.OldValue)) >= threshold:
                    var delta = ParseLevel(change.NewValue) - ParseLevel(change.OldValue);
                    candidates.Add(new AlertCandidate(
                        AlertKinds.LevelJump,
                        AlertSeverity.Notice,
                        $"{username} jumped {delta} levels",
                        $"Level {change.OldValue} → {change.NewValue} ({delta:+0;-0} in one observation window).",
                        Evidence(playerId, [change]),
                        Key(AlertKinds.LevelJump, playerId, change)));
                    break;
            }
        }

        return candidates;
    }

    private static int ParseLevel(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? level : 0;

    private static string Describe(string? value) => string.IsNullOrEmpty(value) ? "none" : value;

    /// <summary>
    /// Dedupe is per backing change (its snapshot), not per day: smart snapshoting already makes
    /// identical re-observations produce no changes, and a same-day A→B→A oscillation is two real
    /// transitions that both belong in the inbox. The key keeps ingestion idempotent instead.
    /// </summary>
    private static string Key(string kind, Guid entityId, PlayerChange change) => $"{kind}:{entityId}:{change.ToSnapshotId}";

    private static string Evidence(Guid playerId, IReadOnlyList<PlayerChange> changes) =>
        JsonSerializer.Serialize(new
        {
            playerId,
            changes = changes.Select(c => new { c.Id, c.Field, c.OldValue, c.NewValue, c.FromSnapshotId, c.ToSnapshotId }).ToList()
        }, EvidenceJsonOptions);
}
