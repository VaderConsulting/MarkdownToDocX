namespace MermaidSharp.Layout;

public class DagreLayoutEngine : ILayoutEngine
{
    public LayoutResult Layout(GraphDiagramBase diagram, LayoutOptions options)
    {
#pragma warning disable IDE0007
        if (diagram.Nodes.Count == 0)
        {
            return new() { Width = 0, Height = 0 };
        }

        if (diagram is FlowchartModel flowchart && flowchart.Subgraphs.Count > 0)
        {
            return LayoutFlowchartWithSubgraphs(flowchart, options);
        }
#pragma warning restore IDE0007

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

    static LayoutResult LayoutFlowchartWithSubgraphs(FlowchartModel diagram, LayoutOptions options)
    {
#pragma warning disable IDE0007
        // Mermaid source-traced behaviour:
        // - Subgraphs are represented as group nodes in the layout graph (flowDb.ts emits nodes with isGroup + parentId).
        // - Each subgraph can carry a local `dir` (direction) that affects layout inside the group.
        //
        // Our internal layout engine does not implement Dagre compound graphs, so we approximate Mermaid’s output by:
        // 1) Laying out each root subgraph internally (with its own direction) to determine the group node size.
        // 2) Laying out an outer graph containing group nodes (and any non-member nodes) using the diagram direction.
        // 3) Offsetting the internal node positions and edge routes into their group’s box.

        // NOTE: This initial implementation targets the common case used in `test.md` fixtures:
        // - root-only subgraphs (no nesting)
        // - cross-subgraph edges reference the group ids (e.g. Region_A --> Region_B)
        // Nested subgraphs can be added later once we have more fixtures.

        Dictionary<string, MermaidSharp.Models.Subgraph> subgraphById = new Dictionary<string, MermaidSharp.Models.Subgraph>(StringComparer.Ordinal);
        foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
        {
            subgraphById[subgraph.Id] = subgraph;
        }

        HashSet<string> memberNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
        {
            foreach (string nodeId in subgraph.NodeIds)
            {
                memberNodeIds.Add(nodeId);
            }
        }

        const double SUBGRAPH_PADDING_PX = 18.0;
        const double SUBGRAPH_TITLE_BAND_PX = 26.0;

        Dictionary<string, (double InternalMinX, double InternalMinY, double InternalMaxX, double InternalMaxY)> internalBoundsBySubgraphId =
            new Dictionary<string, (double, double, double, double)>(StringComparer.Ordinal);

        // 1) Internal layout for each subgraph.
        foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
        {
            FlowchartModel inner = new FlowchartModel
            {
                Direction = subgraph.Direction
            };

            foreach (string nodeId in subgraph.NodeIds)
            {
                Node? node = diagram.GetNode(nodeId);
                if (node != null)
                {
                    inner.Nodes.Add(node);
                }
            }

            foreach (Edge edge in diagram.Edges)
            {
                if (!subgraph.NodeIds.Contains(edge.SourceId, StringComparer.Ordinal)
                    || !subgraph.NodeIds.Contains(edge.TargetId, StringComparer.Ordinal))
                {
                    continue;
                }

                inner.Edges.Add(edge);
            }

            LayoutOptions innerOptions = new LayoutOptions
            {
                Direction = subgraph.Direction,
                NodeSeparation = options.NodeSeparation,
                RankSeparation = options.RankSeparation,
                Ranker = options.Ranker
            };

            // Layout inner nodes/edges without subgraphs to avoid recursion.
            LayoutPlainGraph(inner, innerOptions);

            if (inner.Nodes.Count == 0)
            {
                internalBoundsBySubgraphId[subgraph.Id] = (0, 0, 0, 0);
                continue;
            }

            double minX = inner.Nodes.Min(n => n.Position.X - (n.Width / 2.0));
            double minY = inner.Nodes.Min(n => n.Position.Y - (n.Height / 2.0));
            double maxX = inner.Nodes.Max(n => n.Position.X + (n.Width / 2.0));
            double maxY = inner.Nodes.Max(n => n.Position.Y + (n.Height / 2.0));

            internalBoundsBySubgraphId[subgraph.Id] = (minX, minY, maxX, maxY);

            // Set group node size so the outer graph can pack it.
            Node? groupNode = diagram.GetNode(subgraph.Id);
            if (groupNode != null)
            {
                groupNode.IsGroup = true;
                groupNode.Width = (maxX - minX) + (SUBGRAPH_PADDING_PX * 2.0);
                groupNode.Height = (maxY - minY) + (SUBGRAPH_PADDING_PX * 2.0) + SUBGRAPH_TITLE_BAND_PX;
            }
        }

        // 2) Outer layout: include group nodes + any nodes not inside any subgraph.
        FlowchartModel outer = new FlowchartModel
        {
            Direction = diagram.Direction
        };

        foreach (Node node in diagram.Nodes)
        {
            if (node.IsGroup || !memberNodeIds.Contains(node.Id))
            {
                outer.Nodes.Add(node);
            }
        }

        foreach (Edge edge in diagram.Edges)
        {
            if (outer.Nodes.Any(n => n.Id == edge.SourceId) && outer.Nodes.Any(n => n.Id == edge.TargetId))
            {
                outer.Edges.Add(edge);
            }
        }

        LayoutPlainGraph(outer, options);

        // 3) Offset inner nodes/edges into the group boxes placed by outer layout.
        foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
        {
            if (!internalBoundsBySubgraphId.TryGetValue(subgraph.Id, out var b))
            {
                continue;
            }

            Node? groupNode = diagram.GetNode(subgraph.Id);
            if (groupNode == null)
            {
                continue;
            }

            double groupLeft = groupNode.Position.X - (groupNode.Width / 2.0);
            double groupTop = groupNode.Position.Y - (groupNode.Height / 2.0);

            double targetMinX = groupLeft + SUBGRAPH_PADDING_PX;
            double targetMinY = groupTop + SUBGRAPH_PADDING_PX + SUBGRAPH_TITLE_BAND_PX;

            double dx = targetMinX - b.InternalMinX;
            double dy = targetMinY - b.InternalMinY;

            foreach (string nodeId in subgraph.NodeIds)
            {
                Node? node = diagram.GetNode(nodeId);
                if (node == null)
                {
                    continue;
                }

                node.Position = new Position(node.Position.X + dx, node.Position.Y + dy);
            }

            foreach (Edge edge in diagram.Edges)
            {
                if (!subgraph.NodeIds.Contains(edge.SourceId, StringComparer.Ordinal)
                    || !subgraph.NodeIds.Contains(edge.TargetId, StringComparer.Ordinal))
                {
                    continue;
                }

                for (int i = 0; i < edge.Points.Count; i++)
                {
                    Position p = edge.Points[i];
                    edge.Points[i] = new Position(p.X + dx, p.Y + dy);
                }
            }
        }

        // Flowchart parity tweak used elsewhere.
        CoordinateAssignment.ApplyVerticalForkExitGapToDiagramEdges(diagram, options.Direction);

        double width = diagram.Nodes.Max(_ => _.Position.X + _.Width / 2);
        double height = diagram.Nodes.Max(_ => _.Position.Y + _.Height / 2);

        return new LayoutResult
        {
            Width = width,
            Height = height
        };
#pragma warning restore IDE0007
    }

    static void LayoutPlainGraph(GraphDiagramBase diagram, LayoutOptions options)
    {
#pragma warning disable IDE0007
        LayoutGraph graph = BuildLayoutGraph(diagram);

        Acyclic.Run(graph);
        Ranker.Run(graph, options.Ranker);
        Ordering.Run(graph);
        CoordinateAssignment.Run(graph, options.NodeSeparation, options.RankSeparation, options.Direction);
        CoordinateAssignment.RouteEdges(graph, options.Direction, diagram);
        Acyclic.Undo(graph);

        ApplyLayout(graph, diagram, options);
#pragma warning restore IDE0007
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
