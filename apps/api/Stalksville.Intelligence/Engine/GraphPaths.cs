namespace Stalksville.Intelligence.Engine;

public sealed record SimpleEdge(string From, string To);

/// <summary>
/// Undirected BFS path finding over the relationship graph (master plan §4). Pure and in-memory —
/// the graph is small; a database graph engine can replace it later without contract changes.
/// </summary>
public static class GraphPaths
{
    /// <summary>Returns up to <paramref name="maxResults"/> distinct shortest-ish paths.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> FindPaths(
        IReadOnlyList<SimpleEdge> edges,
        string from,
        string to,
        int maxDepth = 5,
        int maxResults = 3)
    {
        if (from == to)
        {
            return [[from]];
        }

        var adjacency = new Dictionary<string, List<string>>();
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.From, out var fromList))
            {
                adjacency[edge.From] = fromList = [];
            }
            fromList.Add(edge.To);

            if (!adjacency.TryGetValue(edge.To, out var toList))
            {
                adjacency[edge.To] = toList = [];
            }
            toList.Add(edge.From);
        }

        if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
        {
            return [];
        }

        // BFS layered exploration; collect distinct paths found at the shortest depth(s).
        var results = new List<IReadOnlyList<string>>();
        var bestDepth = int.MaxValue;
        var queue = new Queue<List<string>>();
        queue.Enqueue([from]);
        var visitedAtDepth = new Dictionary<string, int> { [from] = 0 };

        while (queue.Count > 0 && results.Count < maxResults)
        {
            var path = queue.Dequeue();
            var depth = path.Count - 1;

            if (depth >= maxDepth || depth > bestDepth)
            {
                continue;
            }

            var last = path[^1];
            foreach (var neighbor in adjacency[last].Order())
            {
                if (path.Contains(neighbor))
                {
                    continue;
                }

                var nextPath = new List<string>(path) { neighbor };

                if (neighbor == to)
                {
                    if (nextPath.Count - 1 <= bestDepth)
                    {
                        bestDepth = nextPath.Count - 1;
                        results.Add(nextPath);
                    }

                    continue;
                }

                if (nextPath.Count - 1 < maxDepth
                    && (!visitedAtDepth.TryGetValue(neighbor, out var seenDepth) || seenDepth >= nextPath.Count - 1))
                {
                    visitedAtDepth[neighbor] = nextPath.Count - 1;
                    queue.Enqueue(nextPath);
                }
            }
        }

        return results.Take(maxResults).ToList();
    }
}
