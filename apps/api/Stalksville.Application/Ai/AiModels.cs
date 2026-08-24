using Stalksville.Application.Investigations;

namespace Stalksville.Application.Ai;

/// <summary>
/// AI narrative with the mandatory guardrail structure (master plan §41): every explanation must
/// separate Observed / Derived / Hypothesis / Unknown. Providers must fill these sections from
/// the supplied facts only — never invent data.
/// </summary>
public sealed record AiNarrative(
    string Provider,
    string? Model,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> Observed,
    IReadOnlyList<string> Derived,
    IReadOnlyList<string> Hypothesis,
    IReadOnlyList<string> Unknown,
    string Disclaimer);

/// <summary>
/// Deterministic composition of case facts into the four guardrail sections. Pure — used directly
/// as the no-AI narrator and as the prompt/fallback basis for LLM narrators.
/// </summary>
public static class InvestigationFacts
{
    public const string Disclaimer =
        "Observed items come directly from Wolvesville. Derived items were calculated by Stalksville and are evidence-backed. "
        + "Hypotheses are unproven possibilities. Unknown items are explicit data gaps. No claim about real-world identity is made.";

    public static (IReadOnlyList<string> Observed, IReadOnlyList<string> Derived) Sections(InvestigationWorkspaceDto workspace)
    {
        var observed = new List<string>();
        var derived = new List<string>();

        foreach (var target in workspace.Targets)
        {
            observed.Add($"{target.DisplayName} ({target.EntityType}) is tracked with {target.SnapshotCount} snapshot(s) and {target.CurrentRelationships} current relationship(s).");
        }

        foreach (var timelineEvent in workspace.Timeline)
        {
            var line = $"{timelineEvent.OccurredAt:yyyy-MM-dd} · {timelineEvent.EventType}: {timelineEvent.Summary ?? timelineEvent.EventType} ({timelineEvent.EntityType})";
            (timelineEvent.IsDerived ? derived : observed).Add(line);
        }

        derived.Add($"Case complexity: {workspace.Stats.Targets} targets, {workspace.Stats.TimelineEvents} timeline events, {workspace.Stats.SnapshotsCollected} snapshots, {workspace.Stats.HighConfidenceRelationships} high-confidence relationship(s).");

        return (observed, derived);
    }

    public static IReadOnlyList<string> Hypotheses(InvestigationWorkspaceDto workspace)
    {
        var hypotheses = new List<string>();

        var endedPerTarget = workspace.Targets
            .Select(t => (t.DisplayName, Ended: CountHistorical(workspace, t.EntityId)))
            .Where(x => x.Ended >= 2)
            .ToList();

        foreach (var (name, ended) in endedPerTarget)
        {
            hypotheses.Add($"{name} has {ended} ended membership period(s); repeated short-lived memberships may indicate clan shopping, eviction cycles or an account in transition — more observations would refine this.");
        }

        if (workspace.Stats.HighConfidenceRelationships > 0)
        {
            hypotheses.Add("Shared-clan overlaps may indicate persistent association between targets; co-membership alone does not prove coordination.");
        }

        hypotheses.Add("Suggested next queries: refresh the most volatile target and re-check in a few days, compare the two most connected targets, and import a target's clan for member-level snapshots.");

        return hypotheses;
    }

    public static IReadOnlyList<string> Unknowns(InvestigationWorkspaceDto workspace)
    {
        var unknowns = new List<string>
        {
            "Whether any tracked accounts belong to the same real-world person.",
            "What happened between snapshots — Stalksville only sees the observed moments.",
        };

        if (workspace.Targets.Any(t => t.EntityType == "clan"))
        {
            unknowns.Add("Full clan member lists require the bot to be a clan bot of that clan; foreign clans return no members.");
        }

        if (workspace.Targets.All(t => t.SnapshotCount <= 1))
        {
            unknowns.Add("Change behavior — every target has a single snapshot so far; no baseline exists yet.");
        }

        return unknowns;
    }

    private static int CountHistorical(InvestigationWorkspaceDto workspace, Guid entityId) =>
        workspace.Timeline.Count(e => e.EntityId == entityId && e.EventType == "MembershipEnded");
}
