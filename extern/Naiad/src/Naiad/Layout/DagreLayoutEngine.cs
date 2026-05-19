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
            CoordinateAssignment.ApplyVerticalPassThroughNodeAlignment(diagram, options.Direction);
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

        Dictionary<string, string> subgraphIdByMemberNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
        {
            foreach (string nodeId in subgraph.NodeIds)
            {
                if (!subgraphIdByMemberNodeId.ContainsKey(nodeId))
                {
                    subgraphIdByMemberNodeId[nodeId] = subgraph.Id;
                }
            }
        }

        const double SUBGRAPH_PADDING_PX = 18.0;
        const double SUBGRAPH_TITLE_BAND_PX = 26.0;
        const double SUBGRAPH_VERTICAL_CONTENT_SLACK_PX = SUBGRAPH_TITLE_BAND_PX;

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
                // Mermaid clusters provide additional vertical breathing room so contents do not sit hard against the bottom.
                // This slack also allows centring of the internal content rectangle within the cluster's available content area.
                groupNode.Height = (maxY - minY) + (SUBGRAPH_PADDING_PX * 2.0) + SUBGRAPH_TITLE_BAND_PX + SUBGRAPH_VERTICAL_CONTENT_SLACK_PX;
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
            // Outer graph edges include:
            // - edges between outer nodes (normal)
            // - edges between an outer node and a subgraph member, mapped to the subgraph id so the group is ranked/ordered like Mermaid
            // - edges between member nodes in different subgraphs, mapped to (subgraph A) -> (subgraph B) so the groups are ranked/ordered like Mermaid
            bool sourceIsOuter = outer.Nodes.Any(n => n.Id == edge.SourceId);
            bool targetIsOuter = outer.Nodes.Any(n => n.Id == edge.TargetId);

            if (sourceIsOuter && targetIsOuter)
            {
                outer.Edges.Add(edge);
                continue;
            }

            if (sourceIsOuter && memberNodeIds.Contains(edge.TargetId))
            {
                foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
                {
                    if (subgraph.NodeIds.Contains(edge.TargetId, StringComparer.Ordinal))
                    {
                        outer.Edges.Add(new Edge
                        {
                            SourceId = edge.SourceId,
                            TargetId = subgraph.Id,
                            Type = edge.Type,
                            LineStyle = edge.LineStyle,
                            Label = edge.Label,
                            MinRankSpan = Math.Max(1, edge.MinRankSpan)
                        });
                        break;
                    }
                }
                continue;
            }

            if (targetIsOuter && memberNodeIds.Contains(edge.SourceId))
            {
                foreach (MermaidSharp.Models.Subgraph subgraph in diagram.Subgraphs)
                {
                    if (subgraph.NodeIds.Contains(edge.SourceId, StringComparer.Ordinal))
                    {
                        outer.Edges.Add(new Edge
                        {
                            SourceId = subgraph.Id,
                            TargetId = edge.TargetId,
                            Type = edge.Type,
                            LineStyle = edge.LineStyle,
                            Label = edge.Label,
                            MinRankSpan = Math.Max(1, edge.MinRankSpan)
                        });
                        break;
                    }
                }
            }

            if (!sourceIsOuter && !targetIsOuter && memberNodeIds.Contains(edge.SourceId) && memberNodeIds.Contains(edge.TargetId))
            {
                if (subgraphIdByMemberNodeId.TryGetValue(edge.SourceId, out string? sourceSubgraphId)
                    && subgraphIdByMemberNodeId.TryGetValue(edge.TargetId, out string? targetSubgraphId)
                    && !string.Equals(sourceSubgraphId, targetSubgraphId, StringComparison.Ordinal))
                {
                    outer.Edges.Add(new Edge
                    {
                        SourceId = sourceSubgraphId,
                        TargetId = targetSubgraphId,
                        Type = edge.Type,
                        LineStyle = edge.LineStyle,
                        Label = edge.Label,
                        MinRankSpan = Math.Max(1, edge.MinRankSpan)
                    });
                }
            }
        }

        LayoutPlainGraph(outer, options);

        // Mermaid behaviour for multiple disconnected root subgraphs:
        // When there are no edges between clusters, Mermaid stacks the clusters vertically in source order
        // (small cluster above large cluster in the common Commands/Events fixture), even when the diagram
        // direction is LR. Our outer layout has no constraints in that case and can place groups in an
        // arbitrary order.
        //
        // Emulate Mermaid by applying a deterministic vertical stack when:
        // - the outer graph contains only group nodes, and
        // - there are no outer edges.
        if (outer.Edges.Count == 0 && outer.Nodes.Count > 1 && outer.Nodes.All(n => n.IsGroup))
        {
            Dictionary<string, int> SubgraphIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < diagram.Subgraphs.Count; i++)
            {
                MermaidSharp.Models.Subgraph subgraph = diagram.Subgraphs[i];
                if (!SubgraphIndexById.ContainsKey(subgraph.Id))
                {
                    SubgraphIndexById[subgraph.Id] = i;
                }
            }

            List<Node> orderedGroups = outer.Nodes
                .OrderBy(n => SubgraphIndexById.TryGetValue(n.Id, out int index) ? index : int.MaxValue)
                .ToList();

            double maxWidth = orderedGroups.Max(n => n.Width);
            double x = maxWidth / 2.0;
            double yCursor = 0.0;

            for (var i = 0; i < orderedGroups.Count; i++)
            {
                Node group = orderedGroups[i];
                double y = yCursor + (group.Height / 2.0);
                group.Position = new Position(x, y);
                yCursor = yCursor + group.Height + options.RankSeparation;
            }
        }

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

            double internalWidth = b.InternalMaxX - b.InternalMinX;
            double internalHeight = b.InternalMaxY - b.InternalMinY;

            double availableWidth = groupNode.Width - (SUBGRAPH_PADDING_PX * 2.0);
            // Mermaid’s cluster label sits at the top of the box but does not "consume" a fixed title band
            // that pushes the internal nodes downward. Keep member nodes centred within the full padded box.
            double availableHeight = groupNode.Height - (SUBGRAPH_PADDING_PX * 2.0);

            double extraX = (availableWidth - internalWidth) / 2.0;
            double extraY = (availableHeight - internalHeight) / 2.0;

            double targetMinX = groupLeft + SUBGRAPH_PADDING_PX + extraX;
            double targetMinY = groupTop + SUBGRAPH_PADDING_PX + extraY;

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

        // 4) Route cross-boundary edges (external <-> member) after final member placement.
        // Mermaid routes edges directly to member nodes even when the other endpoint is outside the cluster.
        // We compute deterministic, boundary-clipped endpoints (shape intersection) once all node positions are final.
        foreach (Edge edge in diagram.Edges)
        {
            bool sourceIsMember = subgraphIdByMemberNodeId.ContainsKey(edge.SourceId);
            bool targetIsMember = subgraphIdByMemberNodeId.ContainsKey(edge.TargetId);

            if (sourceIsMember && targetIsMember)
            {
                // Edges entirely within the same subgraph are already routed by the internal layout.
                // If they cross subgraph boundaries (member -> member across different subgraphs), route them now.
                if (subgraphIdByMemberNodeId.TryGetValue(edge.SourceId, out string? sourceSubgraphId)
                    && subgraphIdByMemberNodeId.TryGetValue(edge.TargetId, out string? targetSubgraphId)
                    && !string.Equals(sourceSubgraphId, targetSubgraphId, StringComparison.Ordinal))
                {
#pragma warning disable IDE0007
                    if (CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out List<MermaidSharp.Models.Position> crossSubgraphPoints))
                    {
                        edge.Points.Clear();
                        for (int i = 0; i < crossSubgraphPoints.Count; i++)
                        {
                            edge.Points.Add(crossSubgraphPoints[i]);
                        }
                    }
#pragma warning restore IDE0007
                }
                continue;
            }

            if (!sourceIsMember && !targetIsMember)
            {
                continue;
            }

            // Only handle edges where both endpoints are real nodes.
            // (If a diagram references a subgraph id as a node, that is handled by the outer layout graph.)
            if (subgraphById.ContainsKey(edge.SourceId) || subgraphById.ContainsKey(edge.TargetId))
            {
                continue;
            }

#pragma warning disable IDE0007
            if (CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out List<MermaidSharp.Models.Position> points))
            {
                edge.Points.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    edge.Points.Add(points[i]);
                }
            }
#pragma warning restore IDE0007
        }

        // 5) Subgraph group → subgraph group (e.g. Region_A --> Region_B): straight boundary-clipped segment.
        // Outer dagre routing can produce multi-point polylines that curve under curveBasis; Mermaid uses a vertical link.
        foreach (var edge in diagram.Edges)
        {
            if (!subgraphById.ContainsKey(edge.SourceId) || !subgraphById.ContainsKey(edge.TargetId))
            {
                continue;
            }

#pragma warning disable IDE0007
            if (CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out List<MermaidSharp.Models.Position> groupToGroupPoints))
            {
                edge.Points.Clear();
                for (int i = 0; i < groupToGroupPoints.Count; i++)
                {
                    edge.Points.Add(groupToGroupPoints[i]);
                }
            }
#pragma warning restore IDE0007
        }

        // Flowchart parity tweak used elsewhere.
        CoordinateAssignment.ApplyVerticalForkExitGapToDiagramEdges(diagram, options.Direction);

#pragma warning disable IDE0007
        double width = diagram.Nodes.Max(_ => _.Position.X + _.Width / 2);
        double height = diagram.Nodes.Max(_ => _.Position.Y + _.Height / 2);
#pragma warning restore IDE0007

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

#pragma warning disable IDE0007
        int NodeOrder = 0;
        foreach (Node node in diagram.Nodes)
        {
            graph.AddNode(new()
            {
                Id = node.Id,
                Width = node.Width,
                Height = node.Height,
                Order = NodeOrder
            });
            NodeOrder++;
        }
#pragma warning restore IDE0007

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

            // Do not attempt to patch up missing flowchart points here.
            // Flowchart subgraph cross-boundary routing is handled in the flowchart-specific subgraph layout path
            // once final member-node positions are known.
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
