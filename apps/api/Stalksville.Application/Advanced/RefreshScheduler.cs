namespace Stalksville.Application.Advanced;

public sealed record RefreshCandidate(Guid PlayerId, string WolvesvillePlayerId, string Username, DateTimeOffset LastSeenAt);

public sealed record RefreshPlan(IReadOnlyList<RefreshCandidate> Selected, int SkippedTooRecent, int SkippedByLimit);

/// <summary>
/// Adaptive refresh selection (master plan §27): the players least recently observed are
/// refreshed first, players observed inside the minimum interval are skipped, and each run is
/// capped so the Wolvesville rate budget is respected.
/// </summary>
public static class RefreshScheduler
{
    public static RefreshPlan Plan(
        IReadOnlyList<RefreshCandidate> candidates,
        DateTimeOffset now,
        int maxPerRun = 5,
        int minIntervalMinutes = 60)
    {
        var due = candidates
            .Where(c => c.LastSeenAt <= now.AddMinutes(-minIntervalMinutes))
            .OrderBy(c => c.LastSeenAt)
            .ToList();

        var skippedByLimit = Math.Max(0, due.Count - maxPerRun);
        var selected = due.Take(Math.Max(0, maxPerRun)).ToList();

        return new RefreshPlan(selected, candidates.Count - due.Count, skippedByLimit);
    }
}
