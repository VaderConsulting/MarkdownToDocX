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

        // Undo edge reversals before routing so cycle edges (e.g. Retry→Receive) get correct geometry (F34).
        Acyclic.Undo(graph);

        // Phase 5: Route edges
        CoordinateAssignment.RouteEdges(graph, options.Direction, diagram);

        // Apply positions back to diagram
        ApplyLayout(graph, diagram, options);
        RepairMissingOrDiagonalFlowchartEdges(diagram);

        if (diagram is FlowchartModel flowchartAfterLayout)
        {
            CoordinateAssignment.ApplyFlowchartDecisionBranchEndpointSnapping(flowchartAfterLayout);
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
        // - Each subgraph can carry a local `dir` (direction) that should affect layout inside the group.
        // - Nested subgraphs are laid out bottom-up: children first, then the parent packs direct members + child group nodes.
        //
        // Mermaid nested-cluster workaround: for nested subgraphs, LR/TB/BT/RL/TD produce the same layout in
        // practice, so compound layout always uses TD (TopToBottom). Root subgraphs still honour `direction`.
        //
        // Our internal layout engine does not implement Dagre compound graphs, so we approximate Mermaid’s output by:
        // 1) Laying out each subgraph internally (deepest first) with TD for nested clusters.
        // 2) Laying out a diagram-level graph when there are multiple root subgraphs and/or free nodes.
        // 3) Offsetting member positions and edge routes into each cluster box (deepest first).

        Dictionary<string, MermaidSharp.Models.Subgraph> subgraphById = new Dictionary<string, MermaidSharp.Models.Subgraph>(StringComparer.Ordinal);
        Dictionary<string, string> subgraphIdByMemberNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> memberNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (MermaidSharp.Models.Subgraph rootSubgraph in diagram.Subgraphs)
        {
            RegisterSubgraphTree(rootSubgraph, subgraphById, subgraphIdByMemberNodeId, memberNodeIds);
        }

        const double SUBGRAPH_PADDING_PX = 18.0;
        const double SUBGRAPH_TITLE_BAND_PX = 26.0;
        const double SUBGRAPH_VERTICAL_CONTENT_SLACK_PX = SUBGRAPH_TITLE_BAND_PX;

        Dictionary<string, (double InternalMinX, double InternalMinY, double InternalMaxX, double InternalMaxY)> internalBoundsBySubgraphId =
            new Dictionary<string, (double, double, double, double)>(StringComparer.Ordinal);

        List<(MermaidSharp.Models.Subgraph Subgraph, bool IsNested)> subgraphsPostOrder =
            new List<(MermaidSharp.Models.Subgraph, bool)>();
        foreach (MermaidSharp.Models.Subgraph rootSubgraph in diagram.Subgraphs)
        {
            CollectSubgraphsPostOrder(rootSubgraph, subgraphsPostOrder, isNested: false);
        }

        // 1) Internal layout for each subgraph (nested children before parents).
        foreach ((MermaidSharp.Models.Subgraph subgraph, bool isNested) in subgraphsPostOrder)
        {
            LayoutSubgraphCompound(
                subgraph,
                diagram,
                options,
                internalBoundsBySubgraphId,
                SUBGRAPH_PADDING_PX,
                SUBGRAPH_TITLE_BAND_PX,
                SUBGRAPH_VERTICAL_CONTENT_SLACK_PX,
                isNested);
        }

        // 2) Diagram-level layout only when multiple root clusters and/or nodes outside subgraphs exist.
        bool HasFreeNodes = diagram.Nodes.Any(n => !n.IsGroup && !memberNodeIds.Contains(n.Id));
        bool MultipleRootSubgraphs = diagram.Subgraphs.Count > 1;

        if (MultipleRootSubgraphs || HasFreeNodes)
        {
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
                bool sourceIsOuter = outer.Nodes.Any(n => n.Id == edge.SourceId);
                bool targetIsOuter = outer.Nodes.Any(n => n.Id == edge.TargetId);

                if (sourceIsOuter && targetIsOuter)
                {
                    outer.Edges.Add(edge);
                    continue;
                }

                if (sourceIsOuter
                    && subgraphIdByMemberNodeId.TryGetValue(edge.TargetId, out string? targetMemberSubgraphId))
                {
                    outer.Edges.Add(new Edge
                    {
                        SourceId = edge.SourceId,
                        TargetId = targetMemberSubgraphId,
                        Type = edge.Type,
                        LineStyle = edge.LineStyle,
                        Label = edge.Label,
                        MinRankSpan = Math.Max(1, edge.MinRankSpan)
                    });
                    continue;
                }

                if (targetIsOuter
                    && subgraphIdByMemberNodeId.TryGetValue(edge.SourceId, out string? sourceMemberSubgraphId))
                {
                    outer.Edges.Add(new Edge
                    {
                        SourceId = sourceMemberSubgraphId,
                        TargetId = edge.TargetId,
                        Type = edge.Type,
                        LineStyle = edge.LineStyle,
                        Label = edge.Label,
                        MinRankSpan = Math.Max(1, edge.MinRankSpan)
                    });
                    continue;
                }

                if (!sourceIsOuter
                    && !targetIsOuter
                    && subgraphIdByMemberNodeId.TryGetValue(edge.SourceId, out string? sourceSubgraphId)
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

            LayoutPlainGraph(outer, options);

            // Mermaid stacks disconnected root clusters vertically in source order when there are no outer edges.
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
        }

        // 3) Offset members into cluster boxes (deepest subgraphs first).
        foreach ((MermaidSharp.Models.Subgraph subgraph, bool _) in subgraphsPostOrder)
        {
            OffsetSubgraphIntoGroupBox(
                subgraph,
                diagram,
                SUBGRAPH_PADDING_PX,
                SUBGRAPH_TITLE_BAND_PX);
        }

        // 4) Resize cluster boxes from final member positions (layout-time bounds are stale after child offsets).
        foreach ((MermaidSharp.Models.Subgraph subgraph, bool _) in subgraphsPostOrder)
        {
            FinalizeSubgraphGroupNodeBounds(
                subgraph,
                diagram,
                SUBGRAPH_PADDING_PX,
                SUBGRAPH_TITLE_BAND_PX,
                SUBGRAPH_VERTICAL_CONTENT_SLACK_PX);
        }

        // 4b) Compound subgraph layout routes on a temporary inner model; some same-subgraph edges can
        // still end up with no polyline (e.g. A->B, C->D). Fill those before cross-boundary routing.
        RepairMissingOrDiagonalFlowchartEdges(diagram);

        // 5) Route cross-boundary edges (external <-> member) after final member placement.
        // Mermaid routes edges directly to member nodes even when the other endpoint is outside the cluster.
        // We compute deterministic, boundary-clipped endpoints (shape intersection) once all node positions are final.
#pragma warning disable IDE0007
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
        }
#pragma warning restore IDE0007

        // 6) Subgraph group → subgraph group (e.g. Region_A --> Region_B): straight boundary-clipped segment.
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

        CoordinateAssignment.ApplyFlowchartDecisionBranchEndpointSnapping(diagram);
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
        Acyclic.Undo(graph);
        CoordinateAssignment.RouteEdges(graph, options.Direction, diagram);

        ApplyLayout(graph, diagram, options);
        RepairMissingOrDiagonalFlowchartEdges(diagram);
        if (diagram is FlowchartModel flowchartPlain)
        {
            CoordinateAssignment.ApplyFlowchartDecisionBranchEndpointSnapping(flowchartPlain);
        }
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Fill empty edge polylines and replace two-point diagonals with direction-aware Manhattan attaches (F58).
    /// </summary>
    static void RepairMissingOrDiagonalFlowchartEdges(GraphDiagramBase diagram)
    {
        if (diagram is not FlowchartModel)
        {
            return;
        }

#pragma warning disable IDE0007
        foreach (Edge edge in diagram.Edges)
        {
            bool NeedsStraightRouting = edge.Points.Count < 2;

            if (!NeedsStraightRouting && edge.Points.Count == 2)
            {
                Position First = edge.Points[0];
                Position Last = edge.Points[1];
                double DeltaX = Math.Abs(Last.X - First.X);
                double DeltaY = Math.Abs(Last.Y - First.Y);
                NeedsStraightRouting = DeltaX > MINLEN_AXIS_COLLAPSE_MAX_PERPENDICULAR_DRIFT_PX
                    && DeltaY > MINLEN_AXIS_COLLAPSE_MAX_PERPENDICULAR_DRIFT_PX;
            }

            if (!NeedsStraightRouting)
            {
                continue;
            }

            if (!CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out List<MermaidSharp.Models.Position> manhattanPoints)
                || manhattanPoints.Count < 2)
            {
                continue;
            }

            edge.Points.Clear();
            for (int i = 0; i < manhattanPoints.Count; i++)
            {
                edge.Points.Add(manhattanPoints[i]);
            }
        }
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
                MinRankSpan = Math.Max(1, edge.MinRankSpan),
                LabelWidthPx = edge.LabelLayoutWidthPx,
                LabelHeightPx = edge.LabelLayoutHeightPx
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
                CollectEdgePoints(graph, edge, diagram, options);
            }

            if (edge.Points.Count < 2
                && diagram is FlowchartModel
                && CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out var FallbackPoints))
            {
                edge.Points.Clear();
                for (var PointIndex = 0; PointIndex < FallbackPoints.Count; PointIndex++)
                {
                    edge.Points.Add(FallbackPoints[PointIndex]);
                }
            }

            // Do not attempt to patch up missing flowchart points here.
            // Flowchart subgraph cross-boundary routing is handled in the flowchart-specific subgraph layout path
            // once final member-node positions are known.
        }
    }

    static void CollectEdgePoints(LayoutGraph graph, Edge edge, GraphDiagramBase? diagram, LayoutOptions options)
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

        SimplifyCollinearPolyline(edge.Points);
        CollapsePredominantlyAxisAlignedMinlenPolyline(edge.Points);

        // Dummy chains concatenate segment centres; for flowcharts the visible arrow must attach on the
        // real source/target outlines (TB: exit bottom, enter top). Without this, long minlen links end
        // inside the target box and the arrowhead is hidden (F28).
        if (dummies.Count > 0
            && diagram is not null
            && CoordinateAssignment.TryComputeStraightFlowchartEdgePoints(diagram, edge, out List<MermaidSharp.Models.Position> boundaryPoints)
            && boundaryPoints.Count >= 2
            && edge.Points.Count >= 2)
        {
            edge.Points[0] = boundaryPoints[0];
            edge.Points[^1] = boundaryPoints[^1];
            CollapsePredominantlyAxisAlignedMinlenPolyline(edge.Points);
        }
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Long <c>minlen</c> chains can pick up small X drift per dummy segment; collapse to endpoints for straight TB/LR links (F28).
    /// Do not collapse when both axes differ materially — that would turn intentional Manhattan elbows into diagonals (F58).
    /// </summary>
    private const double MINLEN_AXIS_COLLAPSE_MAX_PERPENDICULAR_DRIFT_PX = 48.0;

    static void CollapsePredominantlyAxisAlignedMinlenPolyline(List<Position> Points)
    {
        if (Points.Count < 3)
        {
            return;
        }

#pragma warning disable IDE0007
        Position First = Points[0];
        Position Last = Points[^1];
        double DeltaX = Math.Abs(Last.X - First.X);
        double DeltaY = Math.Abs(Last.Y - First.Y);
#pragma warning restore IDE0007

        if (DeltaY > DeltaX * 2.0 && DeltaX <= MINLEN_AXIS_COLLAPSE_MAX_PERPENDICULAR_DRIFT_PX)
        {
            Points.Clear();
            Points.Add(First);
            Points.Add(Last);
        }
        else if (DeltaX > DeltaY * 2.0 && DeltaY <= MINLEN_AXIS_COLLAPSE_MAX_PERPENDICULAR_DRIFT_PX)
        {
            Points.Clear();
            Points.Add(First);
            Points.Add(Last);
        }
    }

    static void SimplifyCollinearPolyline(List<Position> Points)
    {
        if (Points.Count < 3)
        {
            return;
        }

#pragma warning disable IDE0007
        List<Position> Simplified = new List<Position>(Points.Count) { Points[0] };
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            Position Prev = Simplified[^1];
            Position Cur = Points[Index];
            Position Next = Points[Index + 1];
            bool CollinearX = Math.Abs(Prev.X - Cur.X) < 0.01 && Math.Abs(Cur.X - Next.X) < 0.01;
            bool CollinearY = Math.Abs(Prev.Y - Cur.Y) < 0.01 && Math.Abs(Cur.Y - Next.Y) < 0.01;
#pragma warning restore IDE0007
            if (CollinearX || CollinearY)
            {
                continue;
            }

            Simplified.Add(Cur);
        }

        Simplified.Add(Points[^1]);
        Points.Clear();
        Points.AddRange(Simplified);
    }

    static void RegisterSubgraphTree(
        MermaidSharp.Models.Subgraph subgraph,
        Dictionary<string, MermaidSharp.Models.Subgraph> subgraphById,
        Dictionary<string, string> subgraphIdByMemberNodeId,
        HashSet<string> memberNodeIds)
    {
#pragma warning disable IDE0007
        subgraphById[subgraph.Id] = subgraph;

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            RegisterSubgraphTree(nested, subgraphById, subgraphIdByMemberNodeId, memberNodeIds);
        }

        foreach (string nodeId in subgraph.NodeIds)
        {
            memberNodeIds.Add(nodeId);
            subgraphIdByMemberNodeId[nodeId] = subgraph.Id;
        }
#pragma warning restore IDE0007
    }

    static void CollectSubgraphsPostOrder(
        MermaidSharp.Models.Subgraph subgraph,
        List<(MermaidSharp.Models.Subgraph Subgraph, bool IsNested)> postOrder,
        bool isNested)
    {
#pragma warning disable IDE0007
        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            CollectSubgraphsPostOrder(nested, postOrder, isNested: true);
        }

        postOrder.Add((subgraph, isNested));
#pragma warning restore IDE0007
    }

    static HashSet<string> CollectMemberNodeIdsRecursive(MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            foreach (string nestedId in CollectMemberNodeIdsRecursive(nested))
            {
                ids.Add(nestedId);
            }
        }

        foreach (string nodeId in subgraph.NodeIds)
        {
            ids.Add(nodeId);
        }

        return ids;
#pragma warning restore IDE0007
    }

    static bool ContainsMemberRecursive(MermaidSharp.Models.Subgraph subgraph, string nodeId)
    {
#pragma warning disable IDE0007
        if (subgraph.NodeIds.Contains(nodeId, StringComparer.Ordinal))
        {
            return true;
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            if (ContainsMemberRecursive(nested, nodeId))
            {
                return true;
            }
        }

        return false;
#pragma warning restore IDE0007
    }

    static bool TryMapEndpointForSubgraphLayout(
        string nodeId,
        MermaidSharp.Models.Subgraph parentSubgraph,
        out string mappedId)
    {
#pragma warning disable IDE0007
        if (parentSubgraph.NodeIds.Contains(nodeId, StringComparer.Ordinal))
        {
            mappedId = nodeId;
            return true;
        }

        foreach (MermaidSharp.Models.Subgraph nested in parentSubgraph.NestedSubgraphs)
        {
            if (ContainsMemberRecursive(nested, nodeId))
            {
                mappedId = nested.Id;
                return true;
            }
        }

        mappedId = string.Empty;
        return false;
#pragma warning restore IDE0007
    }

    static MermaidSharp.Models.Direction ResolveSubgraphCompoundLayoutDirection(
        MermaidSharp.Models.Subgraph subgraph,
        bool isNestedSubgraph)
    {
#pragma warning disable IDE0007
        // Intended (Mermaid): honour the subgraph’s declared direction (LR/TB/BT/RL/TD) for compound layout.
        // return subgraph.Direction;

        // Mermaid nested-cluster workaround: nested subgraph direction is ignored; always TD.
        if (isNestedSubgraph)
        {
            return MermaidSharp.Models.Direction.TopToBottom;
        }

        return subgraph.Direction;
#pragma warning restore IDE0007
    }

    static bool IsEdgeWhollyInsideNestedSubgraph(Edge edge, MermaidSharp.Models.Subgraph parentSubgraph)
    {
#pragma warning disable IDE0007
        foreach (MermaidSharp.Models.Subgraph nested in parentSubgraph.NestedSubgraphs)
        {
            if (ContainsMemberRecursive(nested, edge.SourceId) && ContainsMemberRecursive(nested, edge.TargetId))
            {
                return true;
            }
        }

        return false;
#pragma warning restore IDE0007
    }

    static void LayoutSubgraphCompound(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        LayoutOptions options,
        Dictionary<string, (double InternalMinX, double InternalMinY, double InternalMaxX, double InternalMaxY)> internalBoundsBySubgraphId,
        double subgraphPaddingPx,
        double subgraphTitleBandPx,
        double subgraphVerticalContentSlackPx,
        bool isNestedSubgraph)
    {
#pragma warning disable IDE0007
        MermaidSharp.Models.Direction layoutDirection = ResolveSubgraphCompoundLayoutDirection(subgraph, isNestedSubgraph);

        FlowchartModel inner = new FlowchartModel
        {
            Direction = layoutDirection
        };

        HashSet<string> nestedMemberNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (MermaidSharp.Models.Subgraph nestedForFilter in subgraph.NestedSubgraphs)
        {
            foreach (string nestedMemberId in CollectMemberNodeIdsRecursive(nestedForFilter))
            {
                nestedMemberNodeIds.Add(nestedMemberId);
            }
        }

        foreach (string nodeId in subgraph.NodeIds)
        {
            if (nestedMemberNodeIds.Contains(nodeId))
            {
                continue;
            }

            Node? node = diagram.GetNode(nodeId);
            if (node != null)
            {
                inner.Nodes.Add(node);
            }
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            Node? groupNode = diagram.GetNode(nested.Id);
            if (groupNode != null)
            {
                inner.Nodes.Add(groupNode);
            }
        }

        foreach (Edge edge in diagram.Edges)
        {
            if (IsEdgeWhollyInsideNestedSubgraph(edge, subgraph))
            {
                continue;
            }

            if (!TryMapEndpointForSubgraphLayout(edge.SourceId, subgraph, out string sourceId))
            {
                continue;
            }

            if (!TryMapEndpointForSubgraphLayout(edge.TargetId, subgraph, out string targetId))
            {
                continue;
            }

            if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(sourceId, edge.SourceId, StringComparison.Ordinal)
                && string.Equals(targetId, edge.TargetId, StringComparison.Ordinal))
            {
                inner.Edges.Add(edge);
            }
            else
            {
                inner.Edges.Add(new Edge
                {
                    SourceId = sourceId,
                    TargetId = targetId,
                    Type = edge.Type,
                    LineStyle = edge.LineStyle,
                    Label = edge.Label,
                    MinRankSpan = Math.Max(1, edge.MinRankSpan)
                });
            }
        }

        LayoutOptions innerOptions = new LayoutOptions
        {
            Direction = layoutDirection,
            NodeSeparation = options.NodeSeparation,
            RankSeparation = options.RankSeparation,
            Ranker = options.Ranker
        };

        LayoutPlainGraph(inner, innerOptions);
        CopySubgraphCompoundEdgeRoutesToDiagram(inner, diagram, subgraph);

        if (inner.Nodes.Count == 0)
        {
            internalBoundsBySubgraphId[subgraph.Id] = (0, 0, 0, 0);
            return;
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            AnchorNestedSubgraphMembersToGroupNode(nested, diagram);
        }

        EnforceCompoundFlowAxisSeparation(subgraph, diagram, inner.Nodes, innerOptions);

        AlignCompoundNodesOnFlowAxis(subgraph, diagram, inner.Nodes, layoutDirection);

        if (!TryGetSubgraphContentBounds(subgraph, diagram, out double minX, out double minY, out double maxX, out double maxY))
        {
            minX = inner.Nodes.Min(n => n.Position.X - (n.Width / 2.0));
            minY = inner.Nodes.Min(n => n.Position.Y - (n.Height / 2.0));
            maxX = inner.Nodes.Max(n => n.Position.X + (n.Width / 2.0));
            maxY = inner.Nodes.Max(n => n.Position.Y + (n.Height / 2.0));
        }

        internalBoundsBySubgraphId[subgraph.Id] = (minX, minY, maxX, maxY);

        Node? subgraphGroupNode = diagram.GetNode(subgraph.Id);
        if (subgraphGroupNode != null)
        {
            subgraphGroupNode.IsGroup = true;
            subgraphGroupNode.Width = (maxX - minX) + (subgraphPaddingPx * 2.0);
            subgraphGroupNode.Height = (maxY - minY) + (subgraphPaddingPx * 2.0) + subgraphTitleBandPx + subgraphVerticalContentSlackPx;
        }
#pragma warning restore IDE0007
    }

    static void CopySubgraphCompoundEdgeRoutesToDiagram(
        FlowchartModel inner,
        FlowchartModel diagram,
        MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        foreach (Edge innerEdge in inner.Edges)
        {
            foreach (Edge diagramEdge in diagram.Edges)
            {
                if (IsEdgeWhollyInsideNestedSubgraph(diagramEdge, subgraph))
                {
                    continue;
                }

                if (!TryMapEndpointForSubgraphLayout(diagramEdge.SourceId, subgraph, out string sourceId))
                {
                    continue;
                }

                if (!TryMapEndpointForSubgraphLayout(diagramEdge.TargetId, subgraph, out string targetId))
                {
                    continue;
                }

                if (!string.Equals(sourceId, innerEdge.SourceId, StringComparison.Ordinal)
                    || !string.Equals(targetId, innerEdge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReferenceEquals(diagramEdge, innerEdge))
                {
                    break;
                }

                diagramEdge.Points.Clear();
                for (int i = 0; i < innerEdge.Points.Count; i++)
                {
                    diagramEdge.Points.Add(innerEdge.Points[i]);
                }

                break;
            }
        }
#pragma warning restore IDE0007
    }

    static void EnforceCompoundFlowAxisSeparation(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        List<Node> compoundNodes,
        LayoutOptions options)
    {
#pragma warning disable IDE0007
        bool flowIsVertical = options.Direction is MermaidSharp.Models.Direction.TopToBottom
            or MermaidSharp.Models.Direction.BottomToTop;

        if (!flowIsVertical || compoundNodes.Count < 2)
        {
            return;
        }

        List<Node> orderedNodes = compoundNodes
            .OrderBy(node => node.Position.Y)
            .ToList();

        const double SUBGRAPH_TITLE_BAND_PX = 26.0;
        double minGap = options.RankSeparation;
        for (int i = 1; i < orderedNodes.Count; i++)
        {
            Node upper = orderedNodes[i - 1];
            Node lower = orderedNodes[i];
            double upperBottom = upper.Position.Y + (upper.Height / 2.0);
            double lowerTop = lower.Position.Y - (lower.Height / 2.0);
            if (lower.IsGroup)
            {
                lowerTop -= SUBGRAPH_TITLE_BAND_PX / 2.0;
            }
            double requiredLowerTop = upperBottom + minGap;
            if (lowerTop >= requiredLowerTop)
            {
                continue;
            }

            double dy = requiredLowerTop - lowerTop;
            lower.Position = new Position(lower.Position.X, lower.Position.Y + dy);
            foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
            {
                if (string.Equals(nested.Id, lower.Id, StringComparison.Ordinal))
                {
                    OffsetNestedSubgraphMembers(nested, diagram, 0.0, dy);
                }
            }
        }
#pragma warning restore IDE0007
    }

    static void AnchorNestedSubgraphMembersToGroupNode(
        MermaidSharp.Models.Subgraph nested,
        FlowchartModel diagram)
    {
#pragma warning disable IDE0007
        Node? groupNode = diagram.GetNode(nested.Id);
        if (groupNode == null)
        {
            return;
        }

        if (!TryGetSubgraphContentBounds(nested, diagram, out double minX, out double minY, out double maxX, out double maxY))
        {
            return;
        }

        double contentCenterX = (minX + maxX) / 2.0;
        double contentCenterY = (minY + maxY) / 2.0;
        double dx = groupNode.Position.X - contentCenterX;
        double dy = groupNode.Position.Y - contentCenterY;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            return;
        }

        HashSet<string> nestedMemberIds = CollectMemberNodeIdsRecursive(nested);
        foreach (string nodeId in nestedMemberIds)
        {
            if (diagram.GetNode(nodeId) is not Node node)
            {
                continue;
            }

            node.Position = new Position(node.Position.X + dx, node.Position.Y + dy);
        }

        foreach (Edge edge in diagram.Edges)
        {
            if (!nestedMemberIds.Contains(edge.SourceId) || !nestedMemberIds.Contains(edge.TargetId))
            {
                continue;
            }

            for (int i = 0; i < edge.Points.Count; i++)
            {
                Position p = edge.Points[i];
                edge.Points[i] = new Position(p.X + dx, p.Y + dy);
            }
        }
#pragma warning restore IDE0007
    }

    static void AlignCompoundNodesOnFlowAxis(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        List<Node> compoundNodes,
        MermaidSharp.Models.Direction layoutDirection)
    {
#pragma warning disable IDE0007
        if (compoundNodes.Count == 0)
        {
            return;
        }

        // Intended (Mermaid): align using subgraph.Direction.
        // bool alignX = subgraph.Direction is MermaidSharp.Models.Direction.TopToBottom
        //     or MermaidSharp.Models.Direction.BottomToTop;

        bool alignX = layoutDirection is MermaidSharp.Models.Direction.TopToBottom
            or MermaidSharp.Models.Direction.BottomToTop;

        double axisCenter = 0.0;
        for (int i = 0; i < compoundNodes.Count; i++)
        {
            axisCenter += alignX ? compoundNodes[i].Position.X : compoundNodes[i].Position.Y;
        }

        axisCenter /= compoundNodes.Count;

        for (int i = 0; i < compoundNodes.Count; i++)
        {
            Node node = compoundNodes[i];
            if (alignX)
            {
                double dx = axisCenter - node.Position.X;
                if (Math.Abs(dx) < 0.001)
                {
                    continue;
                }

                node.Position = new Position(axisCenter, node.Position.Y);
                foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
                {
                    if (string.Equals(nested.Id, node.Id, StringComparison.Ordinal))
                    {
                        OffsetNestedSubgraphMembers(nested, diagram, dx, 0.0);
                    }
                }
            }
            else
            {
                double dy = axisCenter - node.Position.Y;
                if (Math.Abs(dy) < 0.001)
                {
                    continue;
                }

                node.Position = new Position(node.Position.X, axisCenter);
                foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
                {
                    if (string.Equals(nested.Id, node.Id, StringComparison.Ordinal))
                    {
                        OffsetNestedSubgraphMembers(nested, diagram, 0.0, dy);
                    }
                }
            }
        }
#pragma warning restore IDE0007
    }

    static void IncludeNodeInBounds(Node node, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
#pragma warning disable IDE0007
        double left = node.Position.X - (node.Width / 2.0);
        double top = node.Position.Y - (node.Height / 2.0);
        double right = node.Position.X + (node.Width / 2.0);
        double bottom = node.Position.Y + (node.Height / 2.0);
        minX = Math.Min(minX, left);
        minY = Math.Min(minY, top);
        maxX = Math.Max(maxX, right);
        maxY = Math.Max(maxY, bottom);
#pragma warning restore IDE0007
    }

    static bool TryGetSubgraphContentBounds(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        out double minX,
        out double minY,
        out double maxX,
        out double maxY)
    {
#pragma warning disable IDE0007
        double MinX = double.PositiveInfinity;
        double MinY = double.PositiveInfinity;
        double MaxX = double.NegativeInfinity;
        double MaxY = double.NegativeInfinity;

        foreach (string nodeId in subgraph.NodeIds)
        {
            Node? node = diagram.GetNode(nodeId);
            if (node != null)
            {
                IncludeNodeInBounds(node, ref MinX, ref MinY, ref MaxX, ref MaxY);
            }
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            Node? nestedGroupNode = diagram.GetNode(nested.Id);
            if (nestedGroupNode != null)
            {
                IncludeNodeInBounds(nestedGroupNode, ref MinX, ref MinY, ref MaxX, ref MaxY);
            }

            foreach (string memberId in CollectMemberNodeIdsRecursive(nested))
            {
                Node? member = diagram.GetNode(memberId);
                if (member != null)
                {
                    IncludeNodeInBounds(member, ref MinX, ref MinY, ref MaxX, ref MaxY);
                }
            }
        }

        if (MinX < double.PositiveInfinity)
        {
            minX = MinX;
            minY = MinY;
            maxX = MaxX;
            maxY = MaxY;
            return true;
        }

        minX = 0;
        minY = 0;
        maxX = 0;
        maxY = 0;
        return false;
#pragma warning restore IDE0007
    }

    static void OffsetSubgraphIntoGroupBox(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        double subgraphPaddingPx,
        double subgraphTitleBandPx)
    {
#pragma warning disable IDE0007
        if (!TryGetSubgraphContentBounds(subgraph, diagram, out double minX, out double minY, out double maxX, out double maxY))
        {
            return;
        }

        Node? groupNode = diagram.GetNode(subgraph.Id);
        if (groupNode == null)
        {
            return;
        }

        if (Math.Abs(groupNode.Position.X) < 1e-6 && Math.Abs(groupNode.Position.Y) < 1e-6)
        {
            groupNode.Position = new Position((minX + maxX) / 2.0, (minY + maxY) / 2.0);
        }

        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;
        groupNode.Width = Math.Max(groupNode.Width, contentWidth + (subgraphPaddingPx * 2.0));
        groupNode.Height = Math.Max(
            groupNode.Height,
            contentHeight + (subgraphPaddingPx * 2.0) + subgraphTitleBandPx);

        double groupLeft = groupNode.Position.X - (groupNode.Width / 2.0);
        double groupTop = groupNode.Position.Y - (groupNode.Height / 2.0);

        double availableWidth = groupNode.Width - (subgraphPaddingPx * 2.0);
        double availableHeight = groupNode.Height - (subgraphPaddingPx * 2.0);

        double extraX = (availableWidth - contentWidth) / 2.0;
        double extraY = (availableHeight - contentHeight) / 2.0;

        double targetMinX = groupLeft + subgraphPaddingPx + extraX;
        double targetMinY = groupTop + subgraphPaddingPx + extraY;

        double dx = targetMinX - minX;
        double dy = targetMinY - minY;

        // Only shift direct members and child group nodes. Descendant member nodes were already
        // centred inside their own cluster during the deeper offset passes.
        foreach (string nodeId in subgraph.NodeIds)
        {
            Node? node = diagram.GetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            node.Position = new Position(node.Position.X + dx, node.Position.Y + dy);
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            if (diagram.GetNode(nested.Id) is Node nestedGroupNode)
            {
                nestedGroupNode.Position = new Position(nestedGroupNode.Position.X + dx, nestedGroupNode.Position.Y + dy);
            }

            OffsetNestedSubgraphMembers(nested, diagram, dx, dy);
        }

        HashSet<string> directMemberIds = new HashSet<string>(subgraph.NodeIds, StringComparer.Ordinal);
        foreach (Edge edge in diagram.Edges)
        {
            bool touchesDirectMember = directMemberIds.Contains(edge.SourceId) || directMemberIds.Contains(edge.TargetId);
            if (!touchesDirectMember)
            {
                continue;
            }

            if (IsEdgeWhollyInsideNestedSubgraph(edge, subgraph))
            {
                continue;
            }

            for (int i = 0; i < edge.Points.Count; i++)
            {
                Position p = edge.Points[i];
                edge.Points[i] = new Position(p.X + dx, p.Y + dy);
            }
        }
#pragma warning restore IDE0007
    }

    static void FinalizeSubgraphGroupNodeBounds(
        MermaidSharp.Models.Subgraph subgraph,
        FlowchartModel diagram,
        double subgraphPaddingPx,
        double subgraphTitleBandPx,
        double subgraphVerticalContentSlackPx)
    {
#pragma warning disable IDE0007
        if (!TryGetSubgraphContentBounds(subgraph, diagram, out double minX, out double minY, out double maxX, out double maxY))
        {
            return;
        }

        Node? groupNode = diagram.GetNode(subgraph.Id);
        if (groupNode == null)
        {
            return;
        }

        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;
        groupNode.Width = contentWidth + (subgraphPaddingPx * 2.0);
        groupNode.Height = contentHeight + (subgraphPaddingPx * 2.0) + subgraphTitleBandPx + subgraphVerticalContentSlackPx;

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            FinalizeSubgraphGroupNodeBounds(
                nested,
                diagram,
                subgraphPaddingPx,
                subgraphTitleBandPx,
                subgraphVerticalContentSlackPx);
        }
#pragma warning restore IDE0007
    }

    static void OffsetNestedSubgraphMembers(
        MermaidSharp.Models.Subgraph nested,
        FlowchartModel diagram,
        double dx,
        double dy)
    {
#pragma warning disable IDE0007
        foreach (string nodeId in CollectMemberNodeIdsRecursive(nested))
        {
            Node? node = diagram.GetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            node.Position = new Position(node.Position.X + dx, node.Position.Y + dy);
        }

        HashSet<string> nestedMemberIds = CollectMemberNodeIdsRecursive(nested);
        foreach (Edge edge in diagram.Edges)
        {
            if (!nestedMemberIds.Contains(edge.SourceId) && !nestedMemberIds.Contains(edge.TargetId))
            {
                continue;
            }

            if (nestedMemberIds.Contains(edge.SourceId) && nestedMemberIds.Contains(edge.TargetId))
            {
                for (int i = 0; i < edge.Points.Count; i++)
                {
                    Position p = edge.Points[i];
                    edge.Points[i] = new Position(p.X + dx, p.Y + dy);
                }

                continue;
            }

            for (int i = 0; i < edge.Points.Count; i++)
            {
                Position p = edge.Points[i];
                edge.Points[i] = new Position(p.X + dx, p.Y + dy);
            }
        }

        foreach (MermaidSharp.Models.Subgraph child in nested.NestedSubgraphs)
        {
            if (diagram.GetNode(child.Id) is Node childGroupNode)
            {
                childGroupNode.Position = new Position(childGroupNode.Position.X + dx, childGroupNode.Position.Y + dy);
            }

            OffsetNestedSubgraphMembers(child, diagram, dx, dy);
        }
#pragma warning restore IDE0007
    }
}
