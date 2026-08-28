using Stalksville.Intelligence.Engine;
using Xunit;

namespace Stalksville.UnitTests;

public sealed class GraphMetricsTests
{
    [Fact]
    public void EmptyGraph_ReturnsZeroes()
    {
        var result = GraphMetrics.Analyze([], []);

        Assert.Equal(0, result.NodeCount);
        Assert.Equal(0, result.EdgeCount);
        Assert.Empty(result.TopConnectors);
        Assert.Empty(result.Communities);
    }

    [Fact]
    public void IsolatedNodes_FormSingletonCommunities()
    {
        var result = GraphMetrics.Analyze(["a", "b", "c"], []);

        Assert.Equal(3, result.Communities.Count);
        Assert.All(result.Communities, c => Assert.Single(c.Members));
    }

    [Fact]
    public void StarGraph_CenterHasHighestBetweenness()
    {
        // hub connected to 4 leaves; leaves also form one ring edge a-b.
        var edges = new List<(string, string)>
        {
            ("hub", "a"), ("hub", "b"), ("hub", "c"), ("hub", "d")
        };

        var result = GraphMetrics.Analyze(["hub", "a", "b", "c", "d"], edges);

        var top = result.TopConnectors[0];
        Assert.Equal("hub", top.NodeId);
        Assert.Equal(4, top.Degree);

        // Every leaf-to-leaf path (6 pairs) passes through the hub: betweenness = 6.
        Assert.Equal(6.0, top.Betweenness);

        Assert.Single(result.Communities);
    }

    [Fact]
    public void BridgeNode_SeparatesTwoCommunities_AndCarriesTheTraffic()
    {
        // Two triangles joined by a bridge node x: a-b, b-c, c-a, c-x, x-d, d-e, e-x.
        var edges = new List<(string, string)>
        {
            ("a", "b"), ("b", "c"), ("a", "c"),
            ("c", "x"), ("x", "d"), ("d", "e"), ("e", "x")
        };

        var result = GraphMetrics.Analyze(["a", "b", "c", "d", "e", "x"], edges);

        // Betweenness: paths between {a,b} and {d,e} all pass through x and c.
        var betweenness = result.TopConnectors.ToDictionary(m => m.NodeId, m => m.Betweenness);
        Assert.True(betweenness["x"] > betweenness["a"]);
        Assert.True(betweenness["c"] > betweenness["a"]);

        // The two triangles form separate communities, joined only via the bridge.
        Assert.Equal(2, result.Communities.Count);

        var sizes = result.Communities.Select(c => c.Members.Count).OrderBy(size => size).ToList();
        Assert.Equal([3, 3], sizes);
    }

    [Fact]
    public void Results_AreDeterministic()
    {
        var edges = new List<(string, string)> { ("p1", "clan1"), ("p2", "clan1"), ("p2", "clan2"), ("p3", "clan2") };
        var nodes = new List<string> { "p1", "p2", "p3", "clan1", "clan2" };

        var first = GraphMetrics.Analyze(nodes, edges);
        var second = GraphMetrics.Analyze(nodes.OrderBy(n => Guid.NewGuid()).ToList(), edges.OrderBy(e => Guid.NewGuid()).ToList());

        Assert.Equal(
            first.TopConnectors.Select(m => (m.NodeId, m.Degree, m.Betweenness)),
            second.TopConnectors.Select(m => (m.NodeId, m.Degree, m.Betweenness)));
        Assert.Equal(
            first.Communities.Select(c => c.Members.OrderBy(m => m)),
            second.Communities.Select(c => c.Members.OrderBy(m => m)));
    }

    [Fact]
    public void SelfLoops_AndUnknownEndpoints_AreIgnored()
    {
        var result = GraphMetrics.Analyze(["a", "b"], [("a", "a"), ("a", "ghost"), ("a", "b")]);

        Assert.Equal(2, result.NodeCount);
        Assert.Single(result.Communities);
        Assert.All(result.TopConnectors, m => Assert.True(m.Degree <= 1));
    }
}
