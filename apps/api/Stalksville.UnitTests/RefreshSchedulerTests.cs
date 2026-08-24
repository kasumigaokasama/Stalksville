using Stalksville.Application.Advanced;
using Stalksville.Application.Ai;
using Stalksville.Application.Investigations;
using Stalksville.Application.Models;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class RefreshSchedulerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static RefreshCandidate Player(string name, DateTimeOffset lastSeen) =>
        new(Guid.NewGuid(), $"id-{name}", name, lastSeen);

    [Fact]
    public void SelectsOldestFirst_UpToLimit()
    {
        var candidates = new[]
        {
            Player("recent", Now.AddMinutes(-10)),
            Player("old", Now.AddHours(-5)),
            Player("oldest", Now.AddHours(-10)),
            Player("middling", Now.AddHours(-2))
        };

        var plan = RefreshScheduler.Plan(candidates, Now, maxPerRun: 2, minIntervalMinutes: 60);

        Assert.Equal(2, plan.Selected.Count);
        Assert.Equal("oldest", plan.Selected[0].Username);
        Assert.Equal("old", plan.Selected[1].Username);
        Assert.Equal(1, plan.SkippedTooRecent);
        Assert.Equal(1, plan.SkippedByLimit);
    }

    [Fact]
    public void SkipsPlayersInsideMinInterval()
    {
        var candidates = new[] { Player("fresh", Now.AddMinutes(-5)) };

        var plan = RefreshScheduler.Plan(candidates, Now, maxPerRun: 5, minIntervalMinutes: 60);

        Assert.Empty(plan.Selected);
        Assert.Equal(1, plan.SkippedTooRecent);
    }

    [Fact]
    public void EmptyInput_ProducesEmptyPlan()
    {
        var plan = RefreshScheduler.Plan([], Now);
        Assert.Empty(plan.Selected);
    }
}

public sealed class InvestigationFactsTests
{
    private static InvestigationWorkspaceDto Workspace(int endedEvents = 0, int snapshotsPerTarget = 3)
    {
        var entityId = Guid.NewGuid();
        return new InvestigationWorkspaceDto(
            new InvestigationSummaryDto(Guid.NewGuid(), 42, "Test case", null, "active",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, 0),
            new InvestigationStatsDto(1, 1, snapshotsPerTarget, 1),
            [new InvestigationTargetDto(Guid.NewGuid(), "player", entityId, "flex",
                DateTimeOffset.UtcNow, "admin", snapshotsPerTarget, 1)],
            [],
            [
                new TimelineEventDto(Guid.NewGuid(), "player", entityId, "PlayerDiscovered", "flex discovered",
                    DateTimeOffset.UtcNow, false, null),
                new TimelineEventDto(Guid.NewGuid(), "player", entityId, "ClanChanged", "clan a → b",
                    DateTimeOffset.UtcNow, true, 1.0),
                .. Enumerable.Range(0, endedEvents).Select(_ => new TimelineEventDto(
                    Guid.NewGuid(), "player", entityId, "MembershipEnded", "left",
                    DateTimeOffset.UtcNow, false, null))
            ]);
    }

    [Fact]
    public void Sections_SplitObservedAndDerived()
    {
        var (observed, derived) = InvestigationFacts.Sections(Workspace());

        Assert.Contains(observed, o => o.Contains("flex"));
        Assert.Contains(observed, o => o.Contains("PlayerDiscovered"));
        Assert.Contains(derived, d => d.Contains("ClanChanged"));
        Assert.Contains(derived, d => d.Contains("Case complexity"));
    }

    [Fact]
    public void Hypotheses_AlwaysFramedAsUnproven_AndSuggestQueries()
    {
        var hypotheses = InvestigationFacts.Hypotheses(Workspace(endedEvents: 2));

        Assert.NotEmpty(hypotheses);
        Assert.Contains(hypotheses, h => h.Contains("more observations", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hypotheses, h => h.Contains("Suggested next queries"));
    }

    [Fact]
    public void Unknowns_AlwaysIncludeIdentityGap_AndDisclaimerExists()
    {
        var unknowns = InvestigationFacts.Unknowns(Workspace());

        Assert.Contains(unknowns, u => u.Contains("real-world person"));
        Assert.Contains(unknowns, u => u.Contains("between snapshots"));
        Assert.False(string.IsNullOrWhiteSpace(InvestigationFacts.Disclaimer));
    }

    [Fact]
    public void Unknowns_MentionSingleSnapshotGap_WithoutBaseline()
    {
        var unknowns = InvestigationFacts.Unknowns(Workspace(snapshotsPerTarget: 1));
        Assert.Contains(unknowns, u => u.Contains("single snapshot"));
    }
}
