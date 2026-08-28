using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class ExposureAnalyzerTests
{
    [Fact]
    public void MinimalProfile_YieldsLowScore_WithExplainingFactors()
    {
        var state = TestData.Player(clanId: null, badges: []);

        var result = ExposureAnalyzer.Analyze(state, [], [], null);

        Assert.InRange(result.Overall, 0, 30);
        Assert.Equal(5, result.Categories.Count);
        Assert.All(result.Categories, c => Assert.NotEmpty(c.Factors));
        Assert.Contains(result.Categories, c => c.Name == "Clan exposure" && c.Score == 0);
    }

    [Fact]
    public void RichProfile_ScoresHigherThanMinimal()
    {
        var minimal = ExposureAnalyzer.Analyze(TestData.Player(clanId: null, badges: []), [], [], null);

        var state = TestData.Player(clanId: "2001", badges: [.. Enumerable.Range(0, 20).Select(i => $"badge_{i}")]);
        var memberships = new[]
        {
            TestData.Membership(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-90), DateTimeOffset.UtcNow.AddDays(-10)),
            TestData.Membership(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-9))
        };
        var relationships = new[]
        {
            newRelationship(current: true),
            newRelationship(current: true),
            newRelationship(current: false)
        };

        var rich = ExposureAnalyzer.Analyze(state, memberships, relationships, 40);

        Assert.True(rich.Overall > minimal.Overall);
        var historical = Assert.Single(rich.Categories, c => c.Name == "Historical exposure");
        Assert.True(historical.Score >= 45);
        var clan = Assert.Single(rich.Categories, c => c.Name == "Clan exposure");
        Assert.True(clan.Score >= 60);
        var profile = Assert.Single(rich.Categories, c => c.Name == "Profile exposure");
        Assert.True(profile.Score >= 40);
        // Every factor carries evidence text — the score must always be explainable.
        Assert.All(rich.Categories.SelectMany(c => c.Factors), f => Assert.False(string.IsNullOrWhiteSpace(f.Evidence)));

        static Relationship newRelationship(bool current) => new()
        {
            Id = Guid.NewGuid(),
            SourceEntityType = EntityType.Player,
            SourceEntityId = Guid.NewGuid(),
            TargetEntityType = EntityType.Clan,
            TargetEntityId = Guid.NewGuid(),
            Type = current ? RelationshipType.MemberOf : RelationshipType.PreviouslyMemberOf,
            Confidence = 1.0,
            FirstObservedAt = DateTimeOffset.UtcNow,
            LastObservedAt = DateTimeOffset.UtcNow,
            IsCurrent = current
        };
    }

    [Fact]
    public void Scores_AreCappedAt100()
    {
        var state = TestData.Player(clanId: "2001", badges: [.. Enumerable.Range(0, 100).Select(i => $"b{i}")]);
        var memberships = Enumerable.Range(0, 20)
            .Select(_ => TestData.Membership(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))
            .ToList();
        var relationships = Enumerable.Range(0, 30)
            .Select(_ => new Relationship
            {
                Id = Guid.NewGuid(),
                SourceEntityType = EntityType.Player,
                SourceEntityId = Guid.NewGuid(),
                TargetEntityType = EntityType.Clan,
                TargetEntityId = Guid.NewGuid(),
                Type = RelationshipType.MemberOf,
                Confidence = 1,
                FirstObservedAt = DateTimeOffset.UtcNow,
                LastObservedAt = DateTimeOffset.UtcNow,
                IsCurrent = true
            })
            .ToList();

        var result = ExposureAnalyzer.Analyze(state, memberships, relationships, 50);

        Assert.All(result.Categories, c => Assert.InRange(c.Score, 0, 100));
        Assert.InRange(result.Overall, 0, 100);
    }
}

public sealed class InsightGeneratorTests
{
    private static PlayerChange Change(string field, DateTimeOffset at, string? old = null, string? @new = null, PlayerChangeKind kind = PlayerChangeKind.Scalar) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = Guid.NewGuid(),
        ToSnapshotId = Guid.NewGuid(),
        Field = field,
        Kind = kind,
        OldValue = old,
        NewValue = @new,
        DetectedAt = at
    };

    [Fact]
    public void QuietHistory_ProducesNoInsights()
    {
        var changes = new[]
        {
            Change("wins", DateTimeOffset.UtcNow.AddDays(-5), "10", "12")
        };

        Assert.Empty(InsightGenerator.Generate(changes));
    }

    [Fact]
    public void HighWinRateOverEnoughGames_ProducesTrendInsight()
    {
        var now = DateTimeOffset.UtcNow;
        var changes = new[]
        {
            Change("wins", now.AddDays(-10), "100", "110"),
            Change("gamesPlayed", now.AddDays(-10), "200", "215"),
            Change("wins", now, "110", "135"),
            Change("gamesPlayed", now, "215", "250")
        };

        var insights = InsightGenerator.Generate(changes);

        var trend = Assert.Single(insights, i => i.Classification == InsightGenerator.ClassificationWinRateTrend);
        // 35 wins out of 50 games = 70% → confidence 0.5 + 0.70/2.
        Assert.Equal(0.85, trend.Confidence, precision: 2);
        Assert.Equal(4, trend.EvidenceChangeIds.Count);
        Assert.Contains("70% of the last 50 games", trend.Title);
    }

    [Fact]
    public void LowWinRateOrThinWindow_DoNotTriggerTrend()
    {
        var now = DateTimeOffset.UtcNow;

        // Below the 65% rate.
        var lowRate = new[]
        {
            Change("wins", now.AddDays(-10), "100", "110"),
            Change("gamesPlayed", now.AddDays(-10), "200", "220"),
            Change("wins", now, "110", "118"),
            Change("gamesPlayed", now, "220", "240")
        };
        Assert.DoesNotContain(InsightGenerator.Generate(lowRate), i => i.Classification == InsightGenerator.ClassificationWinRateTrend);

        // High rate but only 8 games in the window.
        var thinWindow = new[]
        {
            Change("wins", now.AddDays(-1), "100", "106"),
            Change("gamesPlayed", now.AddDays(-1), "200", "208"),
            Change("wins", now, "106", "108"),
            Change("gamesPlayed", now, "208", "208")
        };
        Assert.DoesNotContain(InsightGenerator.Generate(thinWindow), i => i.Classification == InsightGenerator.ClassificationWinRateTrend);
    }

    [Fact]
    public void TwoClanChangesWithin14Days_ProduceVolatilityInsight()
    {
        var now = DateTimeOffset.UtcNow;
        var changes = new[]
        {
            Change("clanId", now.AddDays(-10), "2001", "2002"),
            Change("clanId", now.AddDays(-3), "2002", "2001"),
            Change("wins", now, "1", "2")
        };

        var insights = InsightGenerator.Generate(changes);

        var volatility = Assert.Single(insights, i => i.Classification == InsightGenerator.ClassificationMembershipVolatility);
        Assert.Equal(0.7, volatility.Confidence);
        Assert.Equal(2, volatility.EvidenceChangeIds.Count);
        Assert.All(volatility.EvidenceChangeIds, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public void ThreeClanChangesWithin14Days_RaiseConfidence()
    {
        var now = DateTimeOffset.UtcNow;
        var changes = new[]
        {
            Change("clanId", now.AddDays(-12), "a", "b"),
            Change("clanId", now.AddDays(-6), "b", "c"),
            Change("clanId", now, "c", "a")
        };

        var insights = InsightGenerator.Generate(changes);

        var volatility = Assert.Single(insights, i => i.Classification == InsightGenerator.ClassificationMembershipVolatility);
        Assert.Equal(0.9, volatility.Confidence);
    }

    [Fact]
    public void SpreadOutClanChanges_DoNotTrigger()
    {
        var now = DateTimeOffset.UtcNow;
        var changes = new[]
        {
            Change("clanId", now.AddDays(-60), "a", "b"),
            Change("clanId", now, "b", "c")
        };

        Assert.Empty(InsightGenerator.Generate(changes));
    }

    [Fact]
    public void UsernameChange_ProducesIdentityChurn()
    {
        var changes = new[] { Change("username", DateTimeOffset.UtcNow.AddDays(-2), "oldName", "newName") };

        var insights = InsightGenerator.Generate(changes);

        var churn = Assert.Single(insights, i => i.Classification == InsightGenerator.ClassificationIdentityChurn);
        Assert.Contains("oldName → newName", churn.Description);
    }
}

public sealed class GraphPathsTests
{
    private static readonly string A = "a", B = "b", C = "c", D = "d", E = "e";

    [Fact]
    public void DirectConnection_ReturnsSingleHop()
    {
        var edges = new[] { new SimpleEdge(A, B) };

        var paths = GraphPaths.FindPaths(edges, A, B);

        var path = Assert.Single(paths);
        Assert.Equal([A, B], path);
    }

    [Fact]
    public void TwoHopsThroughClan_Found()
    {
        // player A - clan C - player B (bipartite like the real graph)
        var edges = new[] { new SimpleEdge(A, C), new SimpleEdge(B, C) };

        var paths = GraphPaths.FindPaths(edges, A, B);

        var path = Assert.Single(paths);
        Assert.Equal([A, C, B], path);
    }

    [Fact]
    public void Disconnected_ReturnsEmpty()
    {
        var edges = new[] { new SimpleEdge(A, B), new SimpleEdge(C, D) };

        Assert.Empty(GraphPaths.FindPaths(edges, A, D));
    }

    [Fact]
    public void DepthLimit_Respected()
    {
        var edges = new[]
        {
            new SimpleEdge(A, B),
            new SimpleEdge(B, C),
            new SimpleEdge(C, D),
            new SimpleEdge(D, E)
        };

        Assert.Empty(GraphPaths.FindPaths(edges, A, E, maxDepth: 3));
        Assert.NotEmpty(GraphPaths.FindPaths(edges, A, E, maxDepth: 4));
    }

    [Fact]
    public void SameNode_ReturnsSelf()
    {
        var paths = GraphPaths.FindPaths([new SimpleEdge(A, B)], A, A);
        Assert.Equal([A], Assert.Single(paths));
    }
}
