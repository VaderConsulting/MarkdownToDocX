namespace MermaidSharp.Layout;

static class Ranker
{
    public static void Run(LayoutGraph graph, RankerType rankerType)
    {
        switch (rankerType)
        {
            case RankerType.LongestPath:
                LongestPath(graph);
                break;
            case RankerType.TightTree:
                TightTree(graph);
                break;
            case RankerType.NetworkSimplex:
                NetworkSimplex(graph);
                break;
        }

        NormalizeRanks(graph);
        InsertDummyNodes(graph);
    }

    static void LongestPath(LayoutGraph graph)
    {
        var visited = new HashSet<string>();

        foreach (var node in graph.Nodes.Values)
        {
            if (!visited.Contains(node.Id))
            {
                DfsLongestPath(graph, node, visited);
            }
        }
    }

    static int DfsLongestPath(LayoutGraph graph, LayoutNode node, HashSet<string> visited)
    {
        if (!visited.Add(node.Id))
        {
            return node.Rank;
        }

        if (node.InEdges.Count == 0)
        {
            node.Rank = 0;
            return 0;
        }

        var best = 0;
        foreach (var inEdge in node.InEdges)
        {
            var pred = graph.GetNode(inEdge.SourceId);
            if (pred is null)
            {
                continue;
            }

            var predRank = DfsLongestPath(graph, pred, visited);
            var span = Math.Max(1, inEdge.MinRankSpan);
            best = Math.Max(best, predRank + span);
        }

        node.Rank = best;
        return node.Rank;
    }

    /// <summary>
    /// Minimum rank allowed for <paramref name="Node"/> given incoming edges (dagre minlen).
    /// </summary>
    static int MinRankFromIncomingEdges(LayoutGraph graph, LayoutNode Node)
    {
        if (Node.InEdges.Count == 0)
        {
            return 0;
        }

        var best = 0;
        foreach (var inEdge in Node.InEdges)
        {
            var pred = graph.GetNode(inEdge.SourceId);
            if (pred is null)
            {
                continue;
            }

            best = Math.Max(best, pred.Rank + Math.Max(1, inEdge.MinRankSpan));
        }

        return best;
    }

    /// <summary>
    /// Largest rank <paramref name="Node"/> may take without violating outgoing <see cref="LayoutEdge.MinRankSpan"/> constraints.
    /// </summary>
    static int MaxRankAllowedByOutgoingEdges(LayoutGraph graph, LayoutNode Node)
    {
        if (Node.OutEdges.Count == 0)
        {
            return int.MaxValue;
        }

        var cap = int.MaxValue;
        foreach (var outEdge in Node.OutEdges)
        {
            var succ = graph.GetNode(outEdge.TargetId);
            if (succ is null)
            {
                continue;
            }

            var span = Math.Max(1, outEdge.MinRankSpan);
            cap = Math.Min(cap, succ.Rank - span);
        }

        return cap;
    }

    static void TightTree(LayoutGraph graph)
    {
        // Tight tree is similar to longest path but considers edge weights
        // For simplicity, we'll use longest path with slight optimization
        LongestPath(graph);

        // Pull nodes down to minimize edge length where possible — must respect MinRankSpan on both sides
        bool changed;
        do
        {
            changed = false;
            foreach (var node in graph.Nodes.Values.OrderByDescending(_ => _.Rank))
            {
                var successors = graph.GetSuccessors(node.Id).ToList();
                if (successors.Count > 0)
                {
                    var minSuccRank = successors.Min(_ => _.Rank);
                    var pullToward = minSuccRank - 1;
                    var maxFromOutgoing = MaxRankAllowedByOutgoingEdges(graph, node);
                    var targetRank = Math.Min(pullToward, maxFromOutgoing);
                    if (targetRank > node.Rank)
                    {
                        var minAllowedRank = MinRankFromIncomingEdges(graph, node);

                        if (targetRank >= minAllowedRank)
                        {
                            node.Rank = targetRank;
                            changed = true;
                        }
                    }
                }
            }
        } while (changed);
    }

    static void NetworkSimplex(LayoutGraph graph) =>
        // Network simplex is complex - fall back to tight tree for now
        // Full implementation would use linear programming approach
        TightTree(graph);

    static void NormalizeRanks(LayoutGraph graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return;
        }

        var minRank = graph.Nodes.Values.Min(_ => _.Rank);
        foreach (var node in graph.Nodes.Values)
        {
            node.Rank -= minRank;
        }
    }

    static void InsertDummyNodes(LayoutGraph graph)
    {
        var edgesToProcess = graph.Edges.ToList();
        var dummyCount = 0;

        foreach (var edge in edgesToProcess)
        {
            var source = graph.GetNode(edge.SourceId);
            var target = graph.GetNode(edge.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            var rankDiff = target.Rank - source.Rank;
            if (rankDiff > 1)
            {
                // Need dummy nodes
                var prevNodeId = edge.SourceId;
                for (var r = source.Rank + 1; r < target.Rank; r++)
                {
                    var dummyId = $"_dummy_{dummyCount++}";
                    var dummy = new LayoutNode
                    {
                        Id = dummyId,
                        Width = 0,
                        Height = 0,
                        Rank = r,
                        IsDummy = true,
                        OriginalEdgeSource = edge.SourceId,
                        OriginalEdgeTarget = edge.TargetId
                    };
                    graph.AddNode(dummy);

                    var newEdge = new LayoutEdge
                    {
                        SourceId = prevNodeId,
                        TargetId = dummyId
                    };
                    graph.AddEdge(newEdge);

                    prevNodeId = dummyId;
                }

                // Connect last dummy to target
                var finalEdge = new LayoutEdge
                {
                    SourceId = prevNodeId,
                    TargetId = edge.TargetId
                };
                graph.AddEdge(finalEdge);

                // Remove original edge connections
                source.OutEdges.Remove(edge);
                target.InEdges.Remove(edge);
            }
        }
    }
}
