namespace MermaidSharp.Layout;

public class DagreLayoutEngine : ILayoutEngine
{
    public LayoutResult Layout(GraphDiagramBase diagram, LayoutOptions options)
    {
        if (diagram.Nodes.Count == 0)
        {
            return new() { Width = 0, Height = 0 };
        }

        // Build internal graph
        var graph = BuildLayoutGraph(diagram);

        // Phase 1: Make acyclic
        Acyclic.Run(graph);

        // Phase 2: Assign ranks
        Ranker.Run(graph, options.Ranker);

        // Phase 3: Order nodes within ranks
        Ordering.Run(graph);

        // Phase 4: Assign coordinates
        CoordinateAssignment.Run(graph, options.NodeSeparation, options.RankSeparation, options.Direction);

        // Phase 5: Route edges
        CoordinateAssignment.RouteEdges(graph, options.Direction, diagram);

        // Undo edge reversals
        Acyclic.Undo(graph);

        // Apply positions back to diagram
        ApplyLayout(graph, diagram, options);

        if (diagram is FlowchartModel)
        {
            CoordinateAssignment.ApplyVerticalForkExitGapToDiagramEdges(diagram, options.Direction);
        }

        // Calculate bounds (don't add margin again - positions already include it)
        var width = diagram.Nodes.Max(_ => _.Position.X + _.Width / 2);
        var height = diagram.Nodes.Max(_ => _.Position.Y + _.Height / 2);

        return new()
        {
            Width = width,
            Height = height
        };
    }

    static LayoutGraph BuildLayoutGraph(GraphDiagramBase diagram)
    {
        var graph = new LayoutGraph();

        foreach (var node in diagram.Nodes)
        {
            graph.AddNode(new()
            {
                Id = node.Id,
                Width = node.Width,
                Height = node.Height
            });
        }

        foreach (var edge in diagram.Edges)
        {
            graph.AddEdge(new()
            {
                SourceId = edge.SourceId,
                TargetId = edge.TargetId,
                MinRankSpan = Math.Max(1, edge.MinRankSpan)
            });
        }

        return graph;
    }

    static void ApplyLayout(LayoutGraph graph, GraphDiagramBase diagram, LayoutOptions options)
    {
        // Don't add margin here - let the renderer handle padding
        foreach (var node in diagram.Nodes)
        {
            var layoutNode = graph.GetNode(node.Id);
            if (layoutNode is not null)
            {
                node.Position = new(layoutNode.X, layoutNode.Y);
                node.Rank = layoutNode.Rank;
                node.Order = layoutNode.Order;
            }
        }

        // Build edge lookup for O(1) access instead of O(n) FirstOrDefault per edge
        var edgeLookup = new Dictionary<(string, string), LayoutEdge>(graph.Edges.Count);
        foreach (var le in graph.Edges)
        {
            edgeLookup.TryAdd((le.SourceId, le.TargetId), le);
        }

        foreach (var edge in diagram.Edges)
        {
            // Find the original edge or reconstruct from dummies
            edgeLookup.TryGetValue((edge.SourceId, edge.TargetId), out var layoutEdge);

            if (layoutEdge is not null)
            {
                edge.Points.Clear();
                foreach (var point in layoutEdge.Points)
                {
                    edge.Points.Add(new(point.X, point.Y));
                }
            }
            else
            {
                // Edge was split by dummy nodes - collect points
                CollectEdgePoints(graph, edge, options);
            }
        }
    }

    static void CollectEdgePoints(LayoutGraph graph, Edge edge, LayoutOptions options)
    {
        edge.Points.Clear();

        var source = graph.GetNode(edge.SourceId);
        var target = graph.GetNode(edge.TargetId);

        if (source is null || target is null)
        {
            return;
        }

        // When Ranker inserts dummy nodes, the original edge (sourceId->targetId) is split into a chain:
        // source -> dummy1 -> dummy2 -> ... -> target. Each segment edge already has routed points computed by
        // CoordinateAssignment.RouteEdges. Reconstruct the full polyline by concatenating those segment points.
        // This is critical for diagram families (like ER) where flowchart-specific attach logic is incorrect.
#pragma warning disable IDE0007
        List<LayoutNode> dummies = graph.Nodes.Values
            .Where(_ => _.IsDummy
                        && _.OriginalEdgeSource == edge.SourceId
                        && _.OriginalEdgeTarget == edge.TargetId)
            .OrderBy(_ => _.Rank)
            .ToList();

        List<string> chainNodeIds = new List<string>(2 + dummies.Count)
        {
            edge.SourceId
        };
        for (int i = 0; i < dummies.Count; i++)
        {
            chainNodeIds.Add(dummies[i].Id);
        }
        chainNodeIds.Add(edge.TargetId);

        for (int i = 0; i < chainNodeIds.Count - 1; i++)
        {
            string segSourceId = chainNodeIds[i];
            string segTargetId = chainNodeIds[i + 1];
            LayoutEdge? segEdge = graph.Edges.FirstOrDefault(_ => _.SourceId == segSourceId && _.TargetId == segTargetId);
            if (segEdge is null || segEdge.Points.Count == 0)
            {
                continue;
            }

            for (int p = 0; p < segEdge.Points.Count; p++)
            {
                Position pt = segEdge.Points[p];
                if (edge.Points.Count > 0)
                {
                    Position last = edge.Points[^1];
                    if (Math.Abs(last.X - pt.X) < 0.001 && Math.Abs(last.Y - pt.Y) < 0.001)
                    {
                        continue;
                    }
                }

                edge.Points.Add(new(pt.X, pt.Y));
            }
        }
#pragma warning restore IDE0007
    }
}
