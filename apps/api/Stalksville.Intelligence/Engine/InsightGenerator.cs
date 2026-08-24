using Stalksville.Domain.Entities;

namespace Stalksville.Intelligence.Engine;

public sealed record Insight(
    string Classification,
    string Title,
    string Description,
    double Confidence,
    IReadOnlyList<Guid> EvidenceChangeIds);

/// <summary>
/// Deterministic anomaly heuristics over a player's change history (master plan §14/§39).
/// Insights are always derived intelligence: every one carries the change records that prove it.
/// </summary>
public static class InsightGenerator
{
    public const string ClassificationMembershipVolatility = "Membership volatility";
    public const string ClassificationIdentityChurn = "Identity churn";
    public const string ClassificationRapidProgression = "Rapid progression";
    public const string ClassificationCosmeticsMomentum = "Cosmetics momentum";

    public static IReadOnlyList<Insight> Generate(IReadOnlyList<PlayerChange> changes)
    {
        var insights = new List<Insight>();

        var clanChanges = changes
            .Where(c => c.Field == "clanId")
            .OrderBy(c => c.DetectedAt)
            .ToList();

        insights.AddIfNotNull(DetectMembershipVolatility(clanChanges));
        insights.AddIfNotNull(DetectIdentityChurn(changes));

        var levelInsight = DetectRapidProgression(changes);
        insights.AddIfNotNull(levelInsight);

        insights.AddIfNotNull(DetectCosmeticsMomentum(changes));

        return insights;
    }

    private static Insight? DetectMembershipVolatility(List<PlayerChange> clanChanges)
    {
        if (clanChanges.Count < 2)
        {
            return null;
        }

        // Any window of 14 days containing >= 2 clan changes qualifies.
        for (var i = 0; i < clanChanges.Count; i++)
        {
            var window = clanChanges.Where(c => c.DetectedAt >= clanChanges[i].DetectedAt
                                             && c.DetectedAt <= clanChanges[i].DetectedAt.AddDays(14)).ToList();

            if (window.Count < 2)
            {
                continue;
            }

            var confidence = window.Count >= 3 ? 0.9 : 0.7;
            return new Insight(
                ClassificationMembershipVolatility,
                $"Clan changed {window.Count} times within 14 days",
                $"Detected {window.Count} clanId changes between {window.Min(c => c.DetectedAt):yyyy-MM-dd} and {window.Max(c => c.DetectedAt):yyyy-MM-dd}. "
                    + "Repeated short-lived memberships may indicate clan shopping, eviction cycles or an account in transition.",
                confidence,
                window.Select(c => c.Id).ToList());
        }

        return null;
    }

    private static Insight? DetectIdentityChurn(IReadOnlyList<PlayerChange> changes)
    {
        var usernameChanges = changes.Where(c => c.Field == "username").ToList();

        if (usernameChanges.Count == 0)
        {
            return null;
        }

        var latest = usernameChanges.OrderByDescending(c => c.DetectedAt).First();
        return new Insight(
            ClassificationIdentityChurn,
            "Username changed",
            $"Latest rename: {latest.OldValue ?? "∅"} → {latest.NewValue ?? "∅"}. Renames complicate long-term tracking; historical snapshots keep the prior identity.",
            0.6,
            usernameChanges.Select(c => c.Id).ToList());
    }

    private static Insight? DetectRapidProgression(IReadOnlyList<PlayerChange> changes)
    {
        var levelChanges = changes.Where(c => c.Field == "level").OrderBy(c => c.DetectedAt).ToList();

        foreach (var change in levelChanges)
        {
            if (int.TryParse(change.OldValue, out var oldLevel) && int.TryParse(change.NewValue, out var newLevel)
                && newLevel - oldLevel >= 5
                && levelChanges.Any(other => other != change
                    && Math.Abs((other.DetectedAt - change.DetectedAt).TotalDays) <= 7))
            {
                return new Insight(
                    ClassificationRapidProgression,
                    $"Level rose {newLevel - oldLevel} within days",
                    $"Level moved {oldLevel} → {newLevel} with other level changes nearby, suggesting an unusually active period.",
                    0.75,
                    levelChanges.Select(c => c.Id).ToList());
            }
        }

        return null;
    }

    private static Insight? DetectCosmeticsMomentum(IReadOnlyList<PlayerChange> changes)
    {
        var badgeAdds = changes.Where(c => c.Field == "badgeIds" && c.Kind == PlayerChangeKind.SetAddition).ToList();

        if (badgeAdds.Count < 10)
        {
            return null;
        }

        return new Insight(
            ClassificationCosmeticsMomentum,
            $"{badgeAdds.Count} badges earned recently",
            "A burst of badge additions indicates heavy play activity; badge ids are public profile data.",
            0.6,
            badgeAdds.Select(c => c.Id).ToList());
    }
}

internal static class InsightListExtensions
{
    public static void AddIfNotNull(this List<Insight> list, Insight? insight)
    {
        if (insight is not null)
        {
            list.Add(insight);
        }
    }
}
