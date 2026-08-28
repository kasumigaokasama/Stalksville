namespace Stalksville.Intelligence.Engine;

public sealed record NodeMetric(string NodeId, int Degree, double Betweenness);

public sealed record NodeCommunity(int Index, IReadOnlyList<string> Members);

public sealed record GraphAnalyticsResult(
    int NodeCount,
    int EdgeCount,
    IReadOnlyList<NodeMetric> TopConnectors,
    IReadOnlyList<NodeCommunity> Communities)
{
    public const string ConfidenceNote = "Deterministic graph measures over derived relationships; descriptive, not predictive.";
}

/// <summary>
/// Deterministic graph analytics over the relationship graph: degree, betweenness centrality
/// (Brandes, unweighted undirected projection) and communities (label propagation with a fixed
/// node order, so results are reproducible). Pure function — the caller supplies nodes/edges.
/// </summary>
public static class GraphMetrics
{
    public static GraphAnalyticsResult Analyze(
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<(string Source, string Target)> edges,
        int topConnectors = 5)
    {
        var nodes = nodeIds.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        var adjacency = nodes.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal));

        foreach (var (source, target) in edges)
        {
            if (adjacency.ContainsKey(source) && adjacency.ContainsKey(target) && source != target)
            {
                adjacency[source].Add(target);
                adjacency[target].Add(source);
            }
        }

        var degree = adjacency.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
        var betweenness = BrandesBetweenness(nodes, adjacency);
        var communities = LabelPropagation(nodes, adjacency);

        var topConnectorsList = nodes
            .OrderByDescending(n => betweenness[n])
            .ThenBy(n => degree[n], comparer: System.Collections.Generic.Comparer<int>.Create((a, b) => b.CompareTo(a)))
            .ThenBy(n => n, StringComparer.Ordinal)
            .Take(Math.Clamp(topConnectors, 1, 25))
            .Select(n => new NodeMetric(n, degree[n], Math.Round(betweenness[n], 4)))
            .ToList();

        return new GraphAnalyticsResult(nodes.Count, edges.Count, topConnectorsList, communities);
    }

    /// <summary>Brandes' algorithm for exact betweenness on an unweighted undirected graph.</summary>
    private static Dictionary<string, double> BrandesBetweenness(IReadOnlyList<string> nodes, Dictionary<string, HashSet<string>> adjacency)
    {
        var betweenness = nodes.ToDictionary(n => n, _ => 0.0);

        foreach (var source in nodes)
        {
            var stack = new Stack<string>();
            var predecessors = nodes.ToDictionary(n => n, _ => new List<string>());
            var shortestPaths = nodes.ToDictionary(n => n, _ => 0.0);
            var distance = nodes.ToDictionary(n => n, _ => -1);
            shortestPaths[source] = 1;
            distance[source] = 0;

            var queue = new Queue<string>();
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                stack.Push(current);

                foreach (var neighbor in adjacency[current])
                {
                    if (distance[neighbor] < 0)
                    {
                        distance[neighbor] = distance[current] + 1;
                        queue.Enqueue(neighbor);
                    }

                    if (distance[neighbor] == distance[current] + 1)
                    {
                        shortestPaths[neighbor] += shortestPaths[current];
                        predecessors[neighbor].Add(current);
                    }
                }
            }

            var dependency = nodes.ToDictionary(n => n, _ => 0.0);
            while (stack.Count > 0)
            {
                var target = stack.Pop();
                foreach (var predecessor in predecessors[target])
                {
                    dependency[predecessor] += shortestPaths[predecessor] / shortestPaths[target] * (1 + dependency[target]);
                }

                if (target != source)
                {
                    betweenness[target] += dependency[target];
                }
            }
        }

        // Undirected graphs count every path twice in Brandes' formulation.
        foreach (var node in nodes)
        {
            betweenness[node] /= 2;
        }

        return betweenness;
    }

    /// <summary>Label propagation over a fixed node order — deterministic community detection.</summary>
    private static List<NodeCommunity> LabelPropagation(IReadOnlyList<string> nodes, Dictionary<string, HashSet<string>> adjacency)
    {
        var labels = new Dictionary<string, int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            labels[nodes[i]] = i;
        }

        for (var round = 0; round < 10; round++)
        {
            var changed = false;
            foreach (var node in nodes)
            {
                if (adjacency[node].Count == 0)
                {
                    continue;
                }

                // Majority label among neighbors; ties resolve to the smallest label for stability.
                var best = adjacency[node]
                    .GroupBy(neighbor => labels[neighbor])
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .First()
                    .Key;

                if (labels[node] != best)
                {
                    labels[node] = best;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return labels
            .GroupBy(kv => kv.Value)
            .OrderBy(group => group.Key)
            .Select((group, index) => new NodeCommunity(index, [.. group.Select(kv => kv.Key).Order(StringComparer.Ordinal)]))
            .ToList();
    }
}
