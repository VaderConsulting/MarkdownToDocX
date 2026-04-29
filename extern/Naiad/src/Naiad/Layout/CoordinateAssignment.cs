namespace MermaidSharp.Layout;

static class CoordinateAssignment
{
    /// <summary>
    /// Outward offset of routed attach points from the node’s geometric outline along the connector direction.
    /// Zero matches the VS Code / Mermaid reference: no visible gap between the box border and the connector stroke.
    /// </summary>
    public const double FlowchartEdgeNodeGap = 0;

    /// <summary>
    /// Minimum horizontal distance between two fork arms where they leave the bottom of a node (Mermaid leaves a visible gap).
    /// </summary>
    public const double MinVerticalForkExitSeparationPx = 16;

    public static void Run(LayoutGraph graph, double nodeSep, double rankSep, Direction direction)
    {
        graph.BuildRanks();
        graph.UpdateOrderInRanks();

        var isHorizontal = direction is Direction.LeftToRight or Direction.RightToLeft;

        // Assign Y coordinates based on ranks
        AssignRankCoordinates(graph, rankSep, isHorizontal);

        // Assign X coordinates using simplified Brandes-Köpf
        AssignPositionCoordinates(graph, nodeSep, isHorizontal);

        // Handle direction reversal
        AdjustForDirection(graph, direction);
    }

    static void AssignRankCoordinates(LayoutGraph graph, double rankSep, bool isHorizontal)
    {
        double currentY = 0;

        for (var r = 0; r < graph.Ranks.Length; r++)
        {
            var nodesInRank = graph.Ranks[r];
            var maxHeight = nodesInRank.Count > 0
                ? nodesInRank.Max(_ => isHorizontal ? _.Width : _.Height)
                : 0;

            foreach (var node in nodesInRank)
            {
                if (isHorizontal)
                {
                    node.X = currentY + maxHeight / 2;
                }
                else
                {
                    node.Y = currentY + maxHeight / 2;
                }
            }

            currentY += maxHeight + rankSep;
        }
    }

    static void AssignPositionCoordinates(LayoutGraph graph, double nodeSep, bool isHorizontal)
    {
        // Use block positioning with median alignment
        // This is a simplified version of Brandes-Köpf

        // Pass 1: Position nodes left-aligned within ranks
        for (var r = 0; r < graph.Ranks.Length; r++)
        {
            var nodesInRank = graph.Ranks[r];
            nodesInRank.Sort((a, b) => a.Order.CompareTo(b.Order));
            double currentX = 0;

            foreach (var node in nodesInRank)
            {
                var nodeWidth = isHorizontal ? node.Height : node.Width;
                if (isHorizontal)
                {
                    node.Y = currentX + nodeWidth / 2;
                }
                else
                {
                    node.X = currentX + nodeWidth / 2;
                }
                currentX += nodeWidth + nodeSep;
            }
        }

        // Pass 2: Center alignment based on connected nodes
        for (var iteration = 0; iteration < 4; iteration++)
        {
            // Down pass
            for (var r = 1; r < graph.Ranks.Length; r++)
            {
                AlignToNeighbors(graph, r, true, nodeSep, isHorizontal);
            }

            // Up pass
            for (var r = graph.Ranks.Length - 2; r >= 0; r--)
            {
                AlignToNeighbors(graph, r, false, nodeSep, isHorizontal);
            }
        }

        // Normalize positions to start at 0
        NormalizePositions(graph);
    }

    static void AlignToNeighbors(LayoutGraph graph, int rank, bool useInEdges,
        double nodeSep, bool isHorizontal)
    {
        var nodesInRank = graph.Ranks[rank];
        nodesInRank.Sort((a, b) => a.Order.CompareTo(b.Order));

        var positions = new List<double>();

        foreach (var node in nodesInRank)
        {
            positions.Clear();
            foreach (var neighbor in useInEdges
                         ? graph.GetPredecessors(node.Id)
                         : graph.GetSuccessors(node.Id))
            {
                positions.Add(isHorizontal ? neighbor.Y : neighbor.X);
            }

            if (positions.Count == 0)
            {
                continue;
            }

            positions.Sort();

            var targetPos = Median(positions);
            var currentPos = isHorizontal ? node.Y : node.X;

            // Only move if it improves alignment and doesn't cause overlap
            var delta = targetPos - currentPos;
            if (Math.Abs(delta) > 0.1)
            {
                var canMove = CanMoveNode(graph, node, delta, nodeSep, isHorizontal);
                if (canMove)
                {
                    if (isHorizontal)
                    {
                        node.Y = targetPos;
                    }
                    else
                    {
                        node.X = targetPos;
                    }
                }
            }
        }
    }

    static bool CanMoveNode(LayoutGraph graph, LayoutNode node, double delta,
        double nodeSep, bool isHorizontal)
    {
        var nodesInRank = graph.Ranks[node.Rank];
        var nodePos = isHorizontal ? node.Y : node.X;
        var newPos = nodePos + delta;
        var nodeSize = isHorizontal ? node.Height : node.Width;

        foreach (var other in nodesInRank)
        {
            if (other.Id == node.Id)
            {
                continue;
            }

            var otherPos = isHorizontal ? other.Y : other.X;
            var otherSize = isHorizontal ? other.Height : other.Width;
            var minDist = (nodeSize + otherSize) / 2 + nodeSep;

            if (Math.Abs(newPos - otherPos) < minDist)
            {
                return false;
            }
        }

        return true;
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        var mid = values.Count / 2;
        if (values.Count % 2 == 0)
        {
            return (values[mid - 1] + values[mid]) / 2;
        }

        return values[mid];
    }

    static void NormalizePositions(LayoutGraph graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return;
        }

        var minX = graph.Nodes.Values.Min(_ => _.X - _.Width / 2);
        var minY = graph.Nodes.Values.Min(_ => _.Y - _.Height / 2);

        foreach (var node in graph.Nodes.Values)
        {
            node.X -= minX;
            node.Y -= minY;
        }
    }

    static void AdjustForDirection(LayoutGraph graph, Direction direction)
    {
        if (graph.Nodes.Count == 0)
        {
            return;
        }

        switch (direction)
        {
            case Direction.BottomToTop:
                var maxY = graph.Nodes.Values.Max(_ => _.Y);
                foreach (var node in graph.Nodes.Values)
                {
                    node.Y = maxY - node.Y;
                }
                break;

            case Direction.RightToLeft:
                var maxX = graph.Nodes.Values.Max(_ => _.X);
                foreach (var node in graph.Nodes.Values)
                {
                    node.X = maxX - node.X;
                }
                break;
        }
    }

    /// <summary>
    /// Flowchart edge marker shortening is handled in the renderer (per-marker-type offsets).
    /// Keep layout routing points un-shortened to avoid double-trimming.
    /// </summary>
    internal const double ArrowMarkerOffset = 0;

    /// <summary>
    /// Places the source-side attach on the bottom outline where the ray from the node’s geometric centre
    /// toward the target’s top-centre attach intersects that edge—so outgoing forks spread like Mermaid
    /// (continuing those segments backward meets at the box centre, not the bottom middle).
    /// </summary>
    internal static void PlaceVerticalFlowSourceOnCentreRayTowardTarget(
        LayoutNode source,
        LayoutNode target,
        out double sourceEdgeX,
        out double sourceEdgeY)
    {
        var gap = FlowchartEdgeNodeGap;
        var cx = source.X;
        var cy = source.Y;
        var exitY = cy + source.Height / 2 + gap;
        var targetTopCentreX = target.X;
        var targetTopY = target.Y - target.Height / 2 - gap;
        var denom = targetTopY - cy;

        if (Math.Abs(denom) < 1e-6)
        {
            sourceEdgeX = cx;
        }
        else
        {
            var u = (exitY - cy) / denom;
            sourceEdgeX = cx + u * (targetTopCentreX - cx);
        }

        var minX = cx - source.Width / 2;
        var maxX = cx + source.Width / 2;
        sourceEdgeX = Math.Clamp(sourceEdgeX, minX, maxX);
        sourceEdgeY = exitY;
    }

    /// <summary>
    /// After centre-ray exits are computed, forces a minimum gap along the bottom edge between arms of the same fork
    /// so connectors do not visually pinch at one point (Mermaid reference shows two distinct bottom intersections).
    /// </summary>
    public static void ApplyVerticalForkExitGapToDiagramEdges(GraphDiagramBase diagram, Direction direction)
    {
        if (direction is Direction.LeftToRight or Direction.RightToLeft)
        {
            return;
        }

        foreach (var sourceNode in diagram.Nodes)
        {
            var outs = diagram.Edges
                .Where(edge => edge.SourceId == sourceNode.Id && edge.Points.Count >= 1)
                .ToList();

            if (outs.Count < 2)
            {
                continue;
            }

            var cx = sourceNode.Position.X;
            var left = cx - sourceNode.Width / 2;
            var right = cx + sourceNode.Width / 2;

            var ordered = outs.OrderBy(edge => edge.Points[0].X).ToList();
            var distinctY = ordered.Select(edge => edge.Points[0].Y).Distinct().ToList();

            if (distinctY.Count != 1)
            {
                continue;
            }

            var exitY = distinctY[0];
            var xs = ordered.Select(edge => edge.Points[0].X).ToArray();

            var minGap = MinVerticalForkExitSeparationPx;
            var span = right - left;

            if (outs.Count > 1 && span > 1e-6)
            {
                var maxFeasible = span / (outs.Count - 1);
                if (minGap > maxFeasible)
                {
                    minGap = maxFeasible * 0.98;
                }
            }

            for (var pass = 0; pass < 32; pass++)
            {
                var changed = false;

                for (var i = 0; i < xs.Length - 1; i++)
                {
                    if (xs[i + 1] - xs[i] < minGap - 1e-6)
                    {
                        var mid = (xs[i] + xs[i + 1]) / 2;
                        xs[i] = mid - minGap / 2;
                        xs[i + 1] = mid + minGap / 2;
                        changed = true;
                    }
                }

                for (var i = 0; i < xs.Length; i++)
                {
                    xs[i] = Math.Clamp(xs[i], left, right);
                }

                if (!changed)
                {
                    break;
                }
            }

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Points[0] = new Position(xs[i], exitY);
            }
        }
    }

    public static void RouteEdges(LayoutGraph graph, Direction direction, GraphDiagramBase? diagram = null)
    {
        var isHorizontal = direction is Direction.LeftToRight or Direction.RightToLeft;
#pragma warning disable IDE0007
        bool UseErPorts = diagram is MermaidSharp.Diagrams.EntityRelationship.ERLayoutGraph;
        MermaidSharp.Diagrams.Flowchart.FlowchartModel? FlowchartDiagram = diagram as MermaidSharp.Diagrams.Flowchart.FlowchartModel;
#pragma warning restore IDE0007

        // Horizontal flowchart attach-point rules (small-N):
        // - 2 -> 1 fan-in (both sources on the left): allocate symmetric slots on target left edge.
        // - 1 -> 2 fan-out (both targets on the right): both edges leave from the vertical middle of the source right edge.
        //
        // This is intentionally scoped to N=2 to avoid broad routing changes.
        var FlowchartHorizontalAttachOverrides = (FlowchartHorizontalAttachOverrides?)null;
        if (isHorizontal && FlowchartDiagram is not null)
        {
            FlowchartHorizontalAttachOverrides = BuildFlowchartHorizontalAttachOverrides(graph);
        }

        // Build lookup for dummy nodes by (OriginalEdgeSource, OriginalEdgeTarget)
        var dummyLookup = new Dictionary<(string, string), List<LayoutNode>>();
        foreach (var node in graph.Nodes.Values)
        {
            if (node.IsDummy && node.OriginalEdgeSource is not null && node.OriginalEdgeTarget is not null)
            {
                var key = (node.OriginalEdgeSource, node.OriginalEdgeTarget);
                if (!dummyLookup.TryGetValue(key, out var list))
                {
                    list = [];
                    dummyLookup[key] = list;
                }
                list.Add(node);
            }
        }

        // Sort each dummy list by rank once
        foreach (var list in dummyLookup.Values)
        {
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }

#pragma warning disable IDE0007
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)>? ErAttachByEdge = null;
        if (!isHorizontal && UseErPorts)
        {
            ErAttachByEdge = BuildErVerticalAttachPoints(graph);
        }

        // For vertical flowcharts (TD/BT), allocate anchors using a slot scheme:
        // split the facing edge into (2N-1) slots for N parallel edges, using the centres of slots 1,3,5...
        // (e.g., for N=2, split into 3 slots and use 1st and 3rd).
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)>? FlowchartVerticalAttachByEdge = null;
        var FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge = (Dictionary<LayoutEdge, (double SourceX, double SourceY)>?)null;
        if (!isHorizontal && FlowchartDiagram is not null)
        {
            FlowchartVerticalAttachByEdge = BuildFlowchartVerticalAttachPoints(graph);
            FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge = BuildFlowchartVerticalDecisionDiamondSourceAttachOverrides(graph, FlowchartDiagram);
        }
#pragma warning restore IDE0007

        foreach (var edge in graph.Edges)
        {
            var source = graph.GetNode(edge.SourceId);
            var target = graph.GetNode(edge.TargetId);

            if (source is null || target is null)
            {
                continue;
            }

            edge.Points.Clear();

            // When ER edges are split by dummy nodes (Ranker), we still need to honour ER attach points on the
            // real node side of each segment. Therefore, do not use the "dummy shortcut" for ER vertical routing.
            if ((source.IsDummy || target.IsDummy) && !(UseErPorts && !isHorizontal))
            {
                // Part of a long edge (dummy nodes inserted by ranker).
                //
                // For flowcharts, Mermaid/dagre-d3 routes these segments orthogonally so curveBasis produces the expected elbows.
                // If we keep only a straight segment between dummy centres, the smoothed curve looks wrong.
                edge.Points.Add(new(source.X, source.Y));

                if (FlowchartDiagram is not null)
                {
                    if (isHorizontal)
                    {
                        edge.Points.Add(new(target.X, source.Y));
                    }
                    else
                    {
                        edge.Points.Add(new(source.X, target.Y));
                    }
                }

                edge.Points.Add(new(target.X, target.Y));
            }
            else
            {
                // Regular edge - create path through dummy nodes if any
                double sourceEdgeX;
                double sourceEdgeY;
                double targetEdgeXNoShorten;
                double targetEdgeYNoShorten;
                if (isHorizontal)
                {
                    if (FlowchartDiagram is not null)
                    {
#pragma warning disable IDE0007
                        Position toward = new Position(target.X, target.Y);
                        Position sourceAttach = ComputeFlowchartAttachPoint(source, FlowchartDiagram.GetNode(edge.SourceId), toward);
                        Position targetAttach = ComputeFlowchartAttachPoint(target, FlowchartDiagram.GetNode(edge.TargetId), new Position(source.X, source.Y));
#pragma warning restore IDE0007
                        sourceEdgeX = sourceAttach.X;
                        sourceEdgeY = sourceAttach.Y;
                        targetEdgeXNoShorten = targetAttach.X;
                        targetEdgeYNoShorten = targetAttach.Y;

                        if (FlowchartHorizontalAttachOverrides is not null
                            && FlowchartHorizontalAttachOverrides.TargetAttachOverrideByEdge.TryGetValue(edge, out var OverrideTargetAttach))
                        {
                            targetEdgeXNoShorten = OverrideTargetAttach.TargetX;
                            targetEdgeYNoShorten = OverrideTargetAttach.TargetY;
                        }

                        if (FlowchartHorizontalAttachOverrides is not null
                            && FlowchartHorizontalAttachOverrides.SourceAttachOverrideByEdge.TryGetValue(edge, out var OverrideSourceAttach))
                        {
                            // Decision nodes (diamonds) must choose exit points based on the true shape intersection
                            // toward each target. Do not collapse multiple outgoing edges onto one shared port.
                            var sourceModel = FlowchartDiagram.GetNode(edge.SourceId);
                            if (sourceModel is null || sourceModel.Shape != MermaidSharp.Models.NodeShape.Diamond)
                            {
                                sourceEdgeX = OverrideSourceAttach.SourceX;
                                sourceEdgeY = OverrideSourceAttach.SourceY;
                            }
                        }
                    }
                    else
                    {
                        sourceEdgeX = source.X + source.Width / 2 + FlowchartEdgeNodeGap;
                        sourceEdgeY = source.Y;
                        targetEdgeXNoShorten = target.X - target.Width / 2 - FlowchartEdgeNodeGap;
                        targetEdgeYNoShorten = target.Y;
                    }
                }
                else
                {
#pragma warning disable IDE0007
                    if (UseErPorts && ErAttachByEdge is not null && ErAttachByEdge.TryGetValue(edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ErAttach))
                    {
                        sourceEdgeX = ErAttach.SourceX;
                        sourceEdgeY = ErAttach.SourceY;
                        targetEdgeXNoShorten = ErAttach.TargetX;
                        targetEdgeYNoShorten = ErAttach.TargetY;
                    }
                    else if (FlowchartDiagram is not null)
                    {
                        if (FlowchartVerticalAttachByEdge is not null
                            && FlowchartVerticalAttachByEdge.TryGetValue(edge, out (double SourceX, double SourceY, double TargetX, double TargetY) FlowchartAttach))
                        {
                            sourceEdgeX = FlowchartAttach.SourceX;
                            sourceEdgeY = FlowchartAttach.SourceY;
                            targetEdgeXNoShorten = FlowchartAttach.TargetX;
                            targetEdgeYNoShorten = FlowchartAttach.TargetY;

                            if (FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge is not null
                                && FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge.TryGetValue(edge, out (double SourceX, double SourceY) OverrideSource))
                            {
                                sourceEdgeX = OverrideSource.SourceX;
                                sourceEdgeY = OverrideSource.SourceY;
                            }

                            // Decision fan-out in vertical flowcharts: regardless of which face slotting chose for the
                            // real target node, force top-centre entry when the original source is a diamond and the
                            // target is below it. This must work even when the immediate segment source is a dummy.
                            if (TryGetOriginalEdgeEndpoints(graph, edge, out string OriginalSourceId, out string OriginalTargetId))
                            {
                                MermaidSharp.Models.Node? originalSourceModel = FlowchartDiagram.GetNode(OriginalSourceId);
                                if (originalSourceModel is not null
                                    && originalSourceModel.Shape == MermaidSharp.Models.NodeShape.Diamond
                                    && string.Equals(edge.TargetId, OriginalTargetId, StringComparison.Ordinal))
                                {
                                    LayoutNode? originalSourceLayout = graph.GetNode(OriginalSourceId);
                                    if (originalSourceLayout is not null && !target.IsDummy && target.Y > originalSourceLayout.Y + 0.01)
                                    {
                                        targetEdgeXNoShorten = target.X;
                                        targetEdgeYNoShorten = target.Y - (target.Height / 2.0) - FlowchartEdgeNodeGap;
                                    }
                                }
                            }

                            // Decision nodes (diamonds) should attach based on true shape intersection toward the
                            // opposite endpoint, not by rectangular face slotting. However, for multi-outgoing
                            // decision diamonds we may override the exit faces to avoid reusing the same vertex.
                            MermaidSharp.Models.Node? sourceModel = FlowchartDiagram.GetNode(edge.SourceId);
                            if (sourceModel is not null && sourceModel.Shape == MermaidSharp.Models.NodeShape.Diamond)
                            {
                                var hasDecisionSourceOverride = FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge is not null
                                    && FlowchartVerticalDecisionDiamondSourceAttachOverrideByEdge.ContainsKey(edge);

                                if (!hasDecisionSourceOverride)
                                {
                                    Position toward = new Position(target.X, target.Y);
                                    Position sourceAttach = ComputeFlowchartAttachPoint(source, sourceModel, toward);
                                    sourceEdgeX = sourceAttach.X;
                                    sourceEdgeY = sourceAttach.Y;
                                }

                                // For vertical decision fan-out, enter destinations from the top edge at top-centre.
                                // Node-local: (x = width/2, y = 0). Absolute: (x = Target.X, y = TopEdgeY).
                                if (!target.IsDummy && target.Y > source.Y + 0.01)
                                {
                                    targetEdgeXNoShorten = target.X;
                                    targetEdgeYNoShorten = target.Y - (target.Height / 2.0) - FlowchartEdgeNodeGap;
                                }
                            }

                            MermaidSharp.Models.Node? targetModel = FlowchartDiagram.GetNode(edge.TargetId);
                            if (targetModel is not null && targetModel.Shape == MermaidSharp.Models.NodeShape.Diamond)
                            {
                                Position toward = new Position(source.X, source.Y);
                                Position targetAttach = ComputeFlowchartAttachPoint(target, targetModel, toward);
                                targetEdgeXNoShorten = targetAttach.X;
                                targetEdgeYNoShorten = targetAttach.Y;
                            }
                        }
                        else
                        {
                            // Fallback: bias to top/bottom faces by using axis-aligned "toward" points.
                            Position towardForSource = new Position(source.X, target.Y);
                            Position towardForTarget = new Position(target.X, source.Y);
                            Position sourceAttach = ComputeFlowchartAttachPoint(source, FlowchartDiagram.GetNode(edge.SourceId), towardForSource);
                            Position targetAttach = ComputeFlowchartAttachPoint(target, FlowchartDiagram.GetNode(edge.TargetId), towardForTarget);
                            sourceEdgeX = sourceAttach.X;
                            sourceEdgeY = sourceAttach.Y;
                            targetEdgeXNoShorten = targetAttach.X;
                            targetEdgeYNoShorten = targetAttach.Y;

                            // Same rule for fallback routing: decision fan-out downward enters from top-centre.
                            MermaidSharp.Models.Node? sourceModel = FlowchartDiagram.GetNode(edge.SourceId);
                            if (sourceModel is not null && sourceModel.Shape == MermaidSharp.Models.NodeShape.Diamond
                                && !target.IsDummy && target.Y > source.Y + 0.01)
                            {
                                targetEdgeXNoShorten = target.X;
                                targetEdgeYNoShorten = target.Y - (target.Height / 2.0) - FlowchartEdgeNodeGap;
                            }
                        }
                    }
                    else
                    {
                        PlaceVerticalFlowSourceOnCentreRayTowardTarget(source, target, out sourceEdgeX, out sourceEdgeY);
                        targetEdgeXNoShorten = target.X;
                        targetEdgeYNoShorten = target.Y - target.Height / 2 - FlowchartEdgeNodeGap;
                    }
#pragma warning restore IDE0007
                }

                edge.Points.Add(new(sourceEdgeX, sourceEdgeY));

                // Find dummy nodes for this edge using pre-built lookup
#pragma warning disable IDE0007
                bool HasDummies = dummyLookup.TryGetValue((edge.SourceId, edge.TargetId), out List<LayoutNode>? dummies);
#pragma warning restore IDE0007
                if (HasDummies && dummies is not null)
                {
                    foreach (var dummy in dummies)
                    {
                        edge.Points.Add(new(dummy.X, dummy.Y));
                    }
                }
                else if (UseErPorts && !isHorizontal)
                {
                    // ER: Cardinality markers use orient="auto", so the tangent at each endpoint must be
                    // perpendicular to the entity edge. If we "bulge" sideways here, markers lean/rotate.
                    //
                    // Add short orthogonal stubs off the entity boundary so the curveBasis spline approaches
                    // each box squarely (matching Mermaid/VSC output).
                    const double ER_ENDPOINT_STUB_PX = 24.0;

#pragma warning disable IDE0007
                    (double SourceStubX, double SourceStubY) = ComputeErEndpointStub(
                        source,
                        attachX: sourceEdgeX,
                        attachY: sourceEdgeY,
                        stubPx: ER_ENDPOINT_STUB_PX);

                    (double TargetStubX, double TargetStubY) = ComputeErEndpointStub(
                        target,
                        attachX: targetEdgeXNoShorten,
                        attachY: targetEdgeYNoShorten,
                        stubPx: ER_ENDPOINT_STUB_PX);
#pragma warning restore IDE0007

                    edge.Points.Add(new(SourceStubX, SourceStubY));

                    // Keep routing simple and deterministic: connect stubs with at most one corner.
                    // Prefer an L that preserves each stub's primary axis.
                    if (Math.Abs(SourceStubX - TargetStubX) < 0.001 || Math.Abs(SourceStubY - TargetStubY) < 0.001)
                    {
                        // Same row/column: a single segment is enough.
                    }
                    else
                    {
                        // Choose a corner that keeps the first leg aligned with the source stub direction.
                        edge.Points.Add(new(SourceStubX, TargetStubY));
                    }

                    edge.Points.Add(new(TargetStubX, TargetStubY));
                }
                else if (FlowchartDiagram is not null)
                {
                    // Mermaid/dagre-d3 routes diagonal links with at least one orthogonal waypoint so the curveBasis spline
                    // produces the characteristic smooth "elbow" rather than a straight diagonal segment.
                    //
                    // If there are dummy nodes, the points already contain intermediate waypoints; only add this for
                    // short direct edges.
                    if (Math.Abs(sourceEdgeX - targetEdgeXNoShorten) > 0.001 && Math.Abs(sourceEdgeY - targetEdgeYNoShorten) > 0.001)
                    {
                        // Use a single midpoint elbow (dagre-style) rather than a full 2-elbow Manhattan route.
                        // Mermaid’s dagre-d3 output for TD/BT commonly includes a single intermediate point whose
                        // X matches the target port and whose Y is around the mid-span.
                        if (isHorizontal)
                        {
#pragma warning disable IDE0007
                            double midX = (sourceEdgeX + targetEdgeXNoShorten) / 2.0;
#pragma warning restore IDE0007
                            edge.Points.Add(new(midX, sourceEdgeY));
                            edge.Points.Add(new(midX, targetEdgeYNoShorten));
                        }
                        else
                        {
#pragma warning disable IDE0007
                            double midY = (sourceEdgeY + targetEdgeYNoShorten) / 2.0;
#pragma warning restore IDE0007

                            // Vertical obstacle-aware routing.
                            //
                            // Dagre-d3 will introduce bendpoints to avoid passing through intermediate nodes.
                            // Our layout graph does not compute obstacle-avoiding paths, so emulate the common
                            // "detour column" behaviour for TD/BT when another node blocks the straight corridor.
                            //
                            // If no obstacle is detected, fall back to a simple 2-elbow route.
                            var hasObstacle = false;
                            var corridorMinX = Math.Min(sourceEdgeX, targetEdgeXNoShorten);
                            var corridorMaxX = Math.Max(sourceEdgeX, targetEdgeXNoShorten);
                            var corridorTopY = Math.Min(sourceEdgeY, targetEdgeYNoShorten);
                            var corridorBottomY = Math.Max(sourceEdgeY, targetEdgeYNoShorten);

                            var obstacleMinLeft = double.PositiveInfinity;
                            var obstacleMaxRight = double.NegativeInfinity;

                            foreach (var candidate in graph.Nodes.Values)
                            {
                                if (candidate.IsDummy)
                                {
                                    continue;
                                }

                                if (string.Equals(candidate.Id, source.Id, StringComparison.Ordinal)
                                    || string.Equals(candidate.Id, target.Id, StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                var candTop = candidate.Y - (candidate.Height / 2.0);
                                var candBottom = candidate.Y + (candidate.Height / 2.0);
                                if (candBottom <= corridorTopY + 0.1 || candTop >= corridorBottomY - 0.1)
                                {
                                    continue;
                                }

                                var candLeft = candidate.X - (candidate.Width / 2.0);
                                var candRight = candidate.X + (candidate.Width / 2.0);
                                if (candRight >= corridorMinX - 0.1 && candLeft <= corridorMaxX + 0.1)
                                {
                                    hasObstacle = true;
                                    obstacleMinLeft = Math.Min(obstacleMinLeft, candLeft);
                                    obstacleMaxRight = Math.Max(obstacleMaxRight, candRight);
                                }
                            }

                            if (hasObstacle)
                            {
                                const double DETOUR_MARGIN_PX = 24.0;
                                var detourLeftX = obstacleMinLeft - DETOUR_MARGIN_PX;
                                var detourRightX = obstacleMaxRight + DETOUR_MARGIN_PX;

                                var detourX = 0.0;
                                if (Math.Abs(targetEdgeXNoShorten - detourLeftX) <= Math.Abs(targetEdgeXNoShorten - detourRightX))
                                {
                                    detourX = detourLeftX;
                                }
                                else
                                {
                                    detourX = detourRightX;
                                }

                                edge.Points.Add(new(detourX, midY));
                                edge.Points.Add(new(detourX, targetEdgeYNoShorten - 1.0));
                                edge.Points.Add(new(targetEdgeXNoShorten, targetEdgeYNoShorten - 1.0));
                            }
                            else
                            {
                                edge.Points.Add(new(sourceEdgeX, midY));
                                edge.Points.Add(new(targetEdgeXNoShorten, midY));
                            }
                        }
                    }
                }

                // Calculate the target endpoint (already inset by FlowchartEdgeNodeGap), shortened for arrow marker
                var targetEdgeX = targetEdgeXNoShorten;
                var targetEdgeY = targetEdgeYNoShorten;

                var lastPoint = edge.Points[^1];
                var dx = targetEdgeX - lastPoint.X;
                var dy = targetEdgeY - lastPoint.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);

                if (length > ArrowMarkerOffset)
                {
                    var ratio = (length - ArrowMarkerOffset) / length;
                    targetEdgeX = lastPoint.X + dx * ratio;
                    targetEdgeY = lastPoint.Y + dy * ratio;
                }

                edge.Points.Add(new(targetEdgeX, targetEdgeY));
            }
        }
    }

    static Position ComputeFlowchartAttachPoint(LayoutNode node, MermaidSharp.Models.Node? modelNode, Position toward)
    {
#pragma warning disable IDE0007
        // Mermaid uses dagre-d3 shape-specific intersect functions (e.g., intersectRect, intersectPolygon).
        // We implement the subset needed for our fixtures so edges leave from the actual node outline
        // (not just the bounding box), especially for diamonds.

        double cx = node.X;
        double cy = node.Y;
        double w = node.Width;
        double h = node.Height;
        MermaidSharp.Models.NodeShape shape = modelNode?.Shape ?? MermaidSharp.Models.NodeShape.Rectangle;

        switch (shape)
        {
            case MermaidSharp.Models.NodeShape.Diamond:
                return IntersectDiamond(cx, cy, Math.Max(w, h), toward);

            case MermaidSharp.Models.NodeShape.Circle:
            case MermaidSharp.Models.NodeShape.DoubleCircle:
                return IntersectEllipse(cx, cy, w / 2.0, h / 2.0, toward);

            default:
                return IntersectRect(cx, cy, w / 2.0, h / 2.0, toward);
        }
#pragma warning restore IDE0007
    }

    private static Dictionary<LayoutEdge, (double SourceX, double SourceY)> BuildFlowchartVerticalDecisionDiamondSourceAttachOverrides(
        LayoutGraph graph,
        MermaidSharp.Diagrams.Flowchart.FlowchartModel diagram)
    {
        var OverrideByEdge = new Dictionary<LayoutEdge, (double SourceX, double SourceY)>();

        foreach (var node in graph.Nodes.Values)
        {
            if (node.IsDummy)
            {
                continue;
            }

            var model = diagram.GetNode(node.Id);
            if (model is null || model.Shape != MermaidSharp.Models.NodeShape.Diamond)
            {
                continue;
            }

            // Consider only the immediate outgoing segments from the diamond.
            var outgoing = graph.Edges
                .Where(Edge => string.Equals(Edge.SourceId, node.Id, StringComparison.Ordinal))
                .ToList();

            if (outgoing.Count != 2 && outgoing.Count != 3)
            {
                continue;
            }

            var s = Math.Max(node.Width, node.Height);
            var half = s / 2.0;

            // Candidate exit points (screen-space):
            // - Bottom-left edge midpoint
            // - Bottom vertex
            // - Bottom-right edge midpoint
            //
            // If the fan-out is predominantly upward (BT diagrams or loopbacks), mirror to the top side.
            var bottomLeftMid = (X: node.X - (half / 2.0), Y: node.Y + (half / 2.0));
            var bottomVertex = (X: node.X, Y: node.Y + half);
            var bottomRightMid = (X: node.X + (half / 2.0), Y: node.Y + (half / 2.0));

            var topLeftMid = (X: node.X - (half / 2.0), Y: node.Y - (half / 2.0));
            var topVertex = (X: node.X, Y: node.Y - half);
            var topRightMid = (X: node.X + (half / 2.0), Y: node.Y - (half / 2.0));

            // Determine dominant fan-out direction by averaging target delta Y.
            var avgDy = 0.0;
            for (var i = 0; i < outgoing.Count; i++)
            {
                var t = graph.GetNode(outgoing[i].TargetId);
                if (t is not null)
                {
                    avgDy += (t.Y - node.Y);
                }
            }
            avgDy = avgDy / outgoing.Count;

            var useBottom = avgDy >= 0.0;

            var leftMid = useBottom ? bottomLeftMid : topLeftMid;
            var midVertex = useBottom ? bottomVertex : topVertex;
            var rightMid = useBottom ? bottomRightMid : topRightMid;

            if (outgoing.Count == 2)
            {
                // Order by target X so left branch uses left face, with a loop-up preference.
                outgoing.Sort((a, b) =>
                {
                    var at = graph.GetNode(a.TargetId);
                    var bt = graph.GetNode(b.TargetId);
                    var ax = at?.X ?? 0.0;
                    var bx = bt?.X ?? 0.0;
                    var ay = at?.Y ?? 0.0;
                    var by = bt?.Y ?? 0.0;

                    var aIsUp = useBottom ? (ay < node.Y - 0.01) : (ay > node.Y + 0.01);
                    var bIsUp = useBottom ? (by < node.Y - 0.01) : (by > node.Y + 0.01);
                    if (aIsUp != bIsUp)
                    {
                        return aIsUp ? -1 : 1;
                    }

                    return ax.CompareTo(bx);
                });

                OverrideByEdge[outgoing[0]] = (leftMid.X, leftMid.Y);
                OverrideByEdge[outgoing[1]] = (rightMid.X, rightMid.Y);
            }
            else
            {
                // 3-way: use left face, bottom/top vertex, right face.
                // Pick the most "downward" (or upward if mirrored) edge for the vertex; remaining two go left/right by X.
                var vertexEdge = (LayoutEdge?)null;
                var bestDy = useBottom ? double.NegativeInfinity : double.PositiveInfinity;
                for (var i = 0; i < outgoing.Count; i++)
                {
                    var t = graph.GetNode(outgoing[i].TargetId);
                    if (t is null)
                    {
                        continue;
                    }

                    var dy = t.Y - node.Y;
                    if (useBottom)
                    {
                        if (dy > bestDy)
                        {
                            bestDy = dy;
                            vertexEdge = outgoing[i];
                        }
                    }
                    else
                    {
                        if (dy < bestDy)
                        {
                            bestDy = dy;
                            vertexEdge = outgoing[i];
                        }
                    }
                }

                if (vertexEdge is null)
                {
                    vertexEdge = outgoing[1];
                }

                OverrideByEdge[vertexEdge] = (midVertex.X, midVertex.Y);

                var remaining = outgoing.Where(e => !ReferenceEquals(e, vertexEdge)).ToList();
                remaining.Sort((a, b) =>
                {
                    var at = graph.GetNode(a.TargetId);
                    var bt = graph.GetNode(b.TargetId);
                    var ax = at?.X ?? 0.0;
                    var bx = bt?.X ?? 0.0;
                    return ax.CompareTo(bx);
                });

                if (remaining.Count == 2)
                {
                    OverrideByEdge[remaining[0]] = (leftMid.X, leftMid.Y);
                    OverrideByEdge[remaining[1]] = (rightMid.X, rightMid.Y);
                }
            }
        }

        return OverrideByEdge;
    }

    private sealed class FlowchartHorizontalAttachOverrides
    {
        public FlowchartHorizontalAttachOverrides(
            Dictionary<LayoutEdge, (double SourceX, double SourceY)> sourceAttachOverrideByEdge,
            Dictionary<LayoutEdge, (double TargetX, double TargetY)> targetAttachOverrideByEdge)
        {
            SourceAttachOverrideByEdge = sourceAttachOverrideByEdge;
            TargetAttachOverrideByEdge = targetAttachOverrideByEdge;
        }

        public Dictionary<LayoutEdge, (double SourceX, double SourceY)> SourceAttachOverrideByEdge { get; }

        public Dictionary<LayoutEdge, (double TargetX, double TargetY)> TargetAttachOverrideByEdge { get; }
    }

    private static FlowchartHorizontalAttachOverrides BuildFlowchartHorizontalAttachOverrides(LayoutGraph graph)
    {
        var SourceAttachOverrideByEdge = new Dictionary<LayoutEdge, (double SourceX, double SourceY)>();
        var TargetAttachOverrideByEdge = new Dictionary<LayoutEdge, (double TargetX, double TargetY)>();

        // Only consider real node endpoints (no dummies) for these small-N horizontal rules.
        var RealEdges = graph.Edges
            .Where(Edge =>
            {
                var Source = graph.GetNode(Edge.SourceId);
                var Target = graph.GetNode(Edge.TargetId);
                return Source is not null
                       && Target is not null
                       && !Source.IsDummy
                       && !Target.IsDummy;
            })
            .ToList();

        // --- 2 -> 1 fan-in to target from the left (allocate symmetric left-edge slots) ---
        var IncomingByTargetId = RealEdges
            .GroupBy(Edge => Edge.TargetId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.ToList(), StringComparer.Ordinal);

        foreach (var (TargetId, IncomingEdges) in IncomingByTargetId)
        {
            if (IncomingEdges.Count != 2)
            {
                continue;
            }

            var TargetNode = graph.GetNode(TargetId);
            if (TargetNode is null)
            {
                continue;
            }

            // Require both sources are to the left of the target.
            var allFromLeft = true;
            for (var i = 0; i < IncomingEdges.Count; i++)
            {
                var SourceNode = graph.GetNode(IncomingEdges[i].SourceId);
                if (SourceNode is null || SourceNode.X >= TargetNode.X)
                {
                    allFromLeft = false;
                    break;
                }
            }

            if (!allFromLeft)
            {
                continue;
            }

            // Slot rule: N=2 => (2N-1)=3 slots; use slot centres 1 and 3 => t = 1/6 and 5/6.
            var leftX = TargetNode.X - (TargetNode.Width / 2.0) - FlowchartEdgeNodeGap;
            var topY = TargetNode.Y - (TargetNode.Height / 2.0) - FlowchartEdgeNodeGap;
            var height = TargetNode.Height + (FlowchartEdgeNodeGap * 2.0);

            var upperY = topY + (height * (1.0 / 6.0));
            var lowerY = topY + (height * (5.0 / 6.0));

            IncomingEdges.Sort((a, b) =>
            {
                var Sa = graph.GetNode(a.SourceId);
                var Sb = graph.GetNode(b.SourceId);
                var ya = Sa?.Y ?? 0.0;
                var yb = Sb?.Y ?? 0.0;
                return ya.CompareTo(yb);
            });

            TargetAttachOverrideByEdge[IncomingEdges[0]] = (leftX, upperY);
            TargetAttachOverrideByEdge[IncomingEdges[1]] = (leftX, lowerY);

            // For this small-N fan-in, Mermaid leaves the sources from the right-centre (not the bottom-right corner).
            for (var i = 0; i < IncomingEdges.Count; i++)
            {
                var Edge = IncomingEdges[i];
                var SourceNode = graph.GetNode(Edge.SourceId);
                if (SourceNode is null)
                {
                    continue;
                }

                var rightX = SourceNode.X + (SourceNode.Width / 2.0) + FlowchartEdgeNodeGap;
                var midY = SourceNode.Y;
                SourceAttachOverrideByEdge[Edge] = (rightX, midY);
            }
        }

        // --- 1 -> N fan-out from source to the right ---
        var OutgoingBySourceId = RealEdges
            .GroupBy(Edge => Edge.SourceId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.ToList(), StringComparer.Ordinal);

        foreach (var (SourceId, OutgoingEdges) in OutgoingBySourceId)
        {
            if (OutgoingEdges.Count < 2)
            {
                continue;
            }

            var SourceNode = graph.GetNode(SourceId);
            if (SourceNode is null)
            {
                continue;
            }

            // Require both targets are to the right of the source.
            var allToRight = true;
            for (var i = 0; i < OutgoingEdges.Count; i++)
            {
                var TargetNode = graph.GetNode(OutgoingEdges[i].TargetId);
                if (TargetNode is null || TargetNode.X <= SourceNode.X)
                {
                    allToRight = false;
                    break;
                }
            }

            if (!allToRight)
            {
                continue;
            }

            var rightX = SourceNode.X + (SourceNode.Width / 2.0) + FlowchartEdgeNodeGap;
            var midY = SourceNode.Y;

            // Source: all fan-out edges share right-centre.
            // Targets: attach at left-centre (Mermaid default for multiple separate targets).
            for (var i = 0; i < OutgoingEdges.Count; i++)
            {
                var Edge = OutgoingEdges[i];
                SourceAttachOverrideByEdge[Edge] = (rightX, midY);

                var TargetNode = graph.GetNode(Edge.TargetId);
                if (TargetNode is null)
                {
                    continue;
                }

                var leftX = TargetNode.X - (TargetNode.Width / 2.0) - FlowchartEdgeNodeGap;
                var targetMidY = TargetNode.Y;
                TargetAttachOverrideByEdge[Edge] = (leftX, targetMidY);
            }
        }

        return new FlowchartHorizontalAttachOverrides(SourceAttachOverrideByEdge, TargetAttachOverrideByEdge);
    }

    internal static bool TryComputeStraightFlowchartEdgePoints(
        MermaidSharp.Models.GraphDiagramBase diagram,
        MermaidSharp.Models.Edge edge,
        out List<Position> points)
    {
#pragma warning disable IDE0007
        points = new List<Position>();

        if (diagram == null)
        {
            return false;
        }

        if (edge == null)
        {
            return false;
        }

        MermaidSharp.Models.Node? sourceModelNode = diagram.GetNode(edge.SourceId);
        MermaidSharp.Models.Node? targetModelNode = diagram.GetNode(edge.TargetId);

        if (sourceModelNode == null || targetModelNode == null)
        {
            return false;
        }

        Position sourceCentre = sourceModelNode.Position;
        Position targetCentre = targetModelNode.Position;

        // Use the same shape-aware intersection logic used by routed edges.
        LayoutNode sourceLayoutNode = new LayoutNode
        {
            Id = sourceModelNode.Id,
            X = sourceCentre.X,
            Y = sourceCentre.Y,
            Width = sourceModelNode.Width,
            Height = sourceModelNode.Height
        };

        LayoutNode targetLayoutNode = new LayoutNode
        {
            Id = targetModelNode.Id,
            X = targetCentre.X,
            Y = targetCentre.Y,
            Width = targetModelNode.Width,
            Height = targetModelNode.Height
        };

        Position sourceAttach = ComputeFlowchartAttachPoint(sourceLayoutNode, sourceModelNode, targetCentre);
        Position targetAttach = ComputeFlowchartAttachPoint(targetLayoutNode, targetModelNode, sourceCentre);

        points.Add(sourceAttach);
        points.Add(targetAttach);
        return true;
#pragma warning restore IDE0007
    }

    static Position IntersectRect(double cx, double cy, double halfWidth, double halfHeight, Position toward)
    {
#pragma warning disable IDE0007
        double dx = toward.X - cx;
        double dy = toward.Y - cy;

        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
        {
            return new Position(cx, cy);
        }

        double sx = dx == 0 ? double.PositiveInfinity : halfWidth / Math.Abs(dx);
        double sy = dy == 0 ? double.PositiveInfinity : halfHeight / Math.Abs(dy);
        double s = Math.Min(sx, sy);
        return new Position(cx + (dx * s), cy + (dy * s));
#pragma warning restore IDE0007
    }

    static Position IntersectEllipse(double cx, double cy, double rx, double ry, Position toward)
    {
#pragma warning disable IDE0007
        double dx = toward.X - cx;
        double dy = toward.Y - cy;

        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
        {
            return new Position(cx, cy);
        }

        // Solve (x/rx)^2 + (y/ry)^2 = 1 along ray t*(dx,dy).
        double a = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        if (a <= 0.0)
        {
            return new Position(cx, cy);
        }

        double t = 1.0 / Math.Sqrt(a);
        return new Position(cx + (dx * t), cy + (dy * t));
#pragma warning restore IDE0007
    }

    static Position IntersectDiamond(double cx, double cy, double s, Position toward)
    {
#pragma warning disable IDE0007
        // Diamond vertices: top, right, bottom, left.
        Position v0 = new Position(cx, cy - (s / 2.0));
        Position v1 = new Position(cx + (s / 2.0), cy);
        Position v2 = new Position(cx, cy + (s / 2.0));
        Position v3 = new Position(cx - (s / 2.0), cy);

        Position[] verts = [v0, v1, v2, v3];

        Position best = new Position(cx, cy);
        double bestT = double.PositiveInfinity;

        Position p0 = new Position(cx, cy);
        Position p1 = toward;

        for (int i = 0; i < verts.Length; i++)
        {
            Position a = verts[i];
            Position b = verts[(i + 1) % verts.Length];
            if (TryIntersectSegments(p0, p1, a, b, out Position hit, out double tRay))
            {
                if (tRay >= 0 && tRay < bestT)
                {
                    bestT = tRay;
                    best = hit;
                }
            }
        }

        return bestT < double.PositiveInfinity ? best : new Position(cx, cy);
#pragma warning restore IDE0007
    }

    static bool TryIntersectSegments(Position p0, Position p1, Position q0, Position q1, out Position hit, out double tRay)
    {
#pragma warning disable IDE0007
        // p(t) = p0 + t*(p1-p0), q(u) = q0 + u*(q1-q0)
        double rx = p1.X - p0.X;
        double ry = p1.Y - p0.Y;
        double sx = q1.X - q0.X;
        double sy = q1.Y - q0.Y;

        double denom = (rx * sy) - (ry * sx);
        if (Math.Abs(denom) < 1e-9)
        {
            hit = Position.Zero;
            tRay = 0;
            return false;
        }

        double qpx = q0.X - p0.X;
        double qpy = q0.Y - p0.Y;

        double t = ((qpx * sy) - (qpy * sx)) / denom;
        double u = ((qpx * ry) - (qpy * rx)) / denom;

        if (t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0)
        {
            hit = new Position(p0.X + (t * rx), p0.Y + (t * ry));
            tRay = t;
            return true;
        }

        hit = Position.Zero;
        tRay = t;
        return false;
#pragma warning restore IDE0007
    }

    static (double StubX, double StubY) ComputeErEndpointStub(LayoutNode node, double attachX, double attachY, double stubPx)
    {
#pragma warning disable IDE0007
        double Left = node.X - node.Width / 2.0;
        double Right = node.X + node.Width / 2.0;
        double Top = node.Y - node.Height / 2.0;
        double Bottom = node.Y + node.Height / 2.0;

        const double EPS = 0.75;

        if (Math.Abs(attachX - Left) <= EPS)
        {
            return (attachX - stubPx, attachY);
        }

        if (Math.Abs(attachX - Right) <= EPS)
        {
            return (attachX + stubPx, attachY);
        }

        if (Math.Abs(attachY - Top) <= EPS)
        {
            return (attachX, attachY - stubPx);
        }

        if (Math.Abs(attachY - Bottom) <= EPS)
        {
            return (attachX, attachY + stubPx);
        }

        // Fallback (should be rare): push away from centre along dominant axis.
        double Dx = attachX - node.X;
        double Dy = attachY - node.Y;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            double Sign = Dx >= 0 ? 1.0 : -1.0;
            return (attachX + Sign * stubPx, attachY);
        }
        else
        {
            double Sign = Dy >= 0 ? 1.0 : -1.0;
            return (attachX, attachY + Sign * stubPx);
        }
#pragma warning restore IDE0007
    }

    static Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> BuildErVerticalAttachPoints(LayoutGraph graph)
    {
#pragma warning disable IDE0007
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge =
            new Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)>();

        // Per the ER geometry source-of-truth: allocate anchors based on relationship grouping by facing direction,
        // then distribute evenly across the facing perimeter slot list.
        //
        // Important: Dagre layout may split an original diagram edge (A -> B) into a chain of dummy nodes:
        //
        //   A -> d1 -> d2 -> ... -> B
        //
        // In that case, we must still allocate anchors for A and B, but apply them to the *segment edges adjacent
        // to the real nodes* (A -> firstDummy and lastDummy -> B). The dummy nodes record the original edge endpoints.
        Dictionary<string, List<LayoutEdge>> IncidentByRealNodeId = new Dictionary<string, List<LayoutEdge>>(StringComparer.Ordinal);
        for (int i = 0; i < graph.Edges.Count; i++)
        {
            LayoutEdge Edge = graph.Edges[i];
            LayoutNode? Source = graph.GetNode(Edge.SourceId);
            LayoutNode? Target = graph.GetNode(Edge.TargetId);
            if (Source is null || Target is null)
            {
                continue;
            }

            if (!Source.IsDummy)
            {
                if (!IncidentByRealNodeId.TryGetValue(Source.Id, out List<LayoutEdge>? List))
                {
                    List = new List<LayoutEdge>();
                    IncidentByRealNodeId[Source.Id] = List;
                }
                List.Add(Edge);
            }

            if (!Target.IsDummy)
            {
                if (!IncidentByRealNodeId.TryGetValue(Target.Id, out List<LayoutEdge>? List))
                {
                    List = new List<LayoutEdge>();
                    IncidentByRealNodeId[Target.Id] = List;
                }
                List.Add(Edge);
            }
        }

        foreach (KeyValuePair<string, List<LayoutEdge>> kvp in IncidentByRealNodeId)
        {
            string NodeId = kvp.Key;
            LayoutNode? Hub = graph.GetNode(NodeId);
            if (Hub is null)
            {
                continue;
            }

            List<LayoutEdge> IncidentEdges = kvp.Value;
            if (IncidentEdges.Count == 0)
            {
                continue;
            }

            List<LayoutEdge> UpEdges = new List<LayoutEdge>();
            List<LayoutEdge> DownEdges = new List<LayoutEdge>();
            List<LayoutEdge> LeftEdges = new List<LayoutEdge>();
            List<LayoutEdge> RightEdges = new List<LayoutEdge>();

            for (int i = 0; i < IncidentEdges.Count; i++)
            {
                LayoutEdge Edge = IncidentEdges[i];
                if (!TryGetOriginalEdgeEndpoints(graph, Edge, out string OriginalSourceId, out string OriginalTargetId))
                {
                    continue;
                }

                string OtherRealId;
                if (string.Equals(NodeId, OriginalSourceId, StringComparison.Ordinal))
                {
                    OtherRealId = OriginalTargetId;
                }
                else if (string.Equals(NodeId, OriginalTargetId, StringComparison.Ordinal))
                {
                    OtherRealId = OriginalSourceId;
                }
                else
                {
                    continue;
                }

                LayoutNode? Other = graph.GetNode(OtherRealId);
                if (Other is null)
                {
                    continue;
                }

                FacingDirection Facing = ClassifyFacing(Hub, Other);
                if (Facing == FacingDirection.Up)
                {
                    UpEdges.Add(Edge);
                }
                else if (Facing == FacingDirection.Down)
                {
                    DownEdges.Add(Edge);
                }
                else if (Facing == FacingDirection.Left)
                {
                    LeftEdges.Add(Edge);
                }
                else
                {
                    RightEdges.Add(Edge);
                }
            }

            AllocateAnchorsForFacingGroup(graph, Hub, NodeId, UpEdges, FacingDirection.Up, AttachByEdge);
            AllocateAnchorsForFacingGroup(graph, Hub, NodeId, DownEdges, FacingDirection.Down, AttachByEdge);
            AllocateAnchorsForFacingGroup(graph, Hub, NodeId, LeftEdges, FacingDirection.Left, AttachByEdge);
            AllocateAnchorsForFacingGroup(graph, Hub, NodeId, RightEdges, FacingDirection.Right, AttachByEdge);
        }

        for (int i = 0; i < graph.Edges.Count; i++)
        {
            LayoutEdge Edge = graph.Edges[i];
            LayoutNode? Source = graph.GetNode(Edge.SourceId);
            LayoutNode? Target = graph.GetNode(Edge.TargetId);
            if (Source is null || Target is null)
            {
                continue;
            }

            double DefaultSourceY = Source.Y + Source.Height / 2 + FlowchartEdgeNodeGap;
            double DefaultTargetY = Target.Y - Target.Height / 2 - FlowchartEdgeNodeGap;

            if (!AttachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) Existing))
            {
                AttachByEdge[Edge] = (Source.X, DefaultSourceY, Target.X, DefaultTargetY);
                continue;
            }

            double SourceX = Existing.SourceX;
            double TargetX = Existing.TargetX;
            double SourceY = Existing.SourceY;
            double TargetY = Existing.TargetY;

            if (double.IsNaN(SourceX))
            {
                SourceX = Source.X;
            }
            if (double.IsNaN(TargetX))
            {
                TargetX = Target.X;
            }

            if (double.IsNaN(SourceY))
            {
                SourceY = DefaultSourceY;
            }
            if (double.IsNaN(TargetY))
            {
                TargetY = DefaultTargetY;
            }

            AttachByEdge[Edge] = (SourceX, SourceY, TargetX, TargetY);
        }

        return AttachByEdge;
#pragma warning restore IDE0007
    }

    static Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> BuildFlowchartVerticalAttachPoints(LayoutGraph graph)
    {
#pragma warning disable IDE0007
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge =
            new Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)>();

        Dictionary<string, List<LayoutEdge>> IncidentByRealNodeId = new Dictionary<string, List<LayoutEdge>>(StringComparer.Ordinal);
        for (int i = 0; i < graph.Edges.Count; i++)
        {
            LayoutEdge Edge = graph.Edges[i];
            LayoutNode? Source = graph.GetNode(Edge.SourceId);
            LayoutNode? Target = graph.GetNode(Edge.TargetId);
            if (Source is null || Target is null)
            {
                continue;
            }

            if (!Source.IsDummy)
            {
                if (!IncidentByRealNodeId.TryGetValue(Source.Id, out List<LayoutEdge>? List))
                {
                    List = new List<LayoutEdge>();
                    IncidentByRealNodeId[Source.Id] = List;
                }
                List.Add(Edge);
            }

            if (!Target.IsDummy)
            {
                if (!IncidentByRealNodeId.TryGetValue(Target.Id, out List<LayoutEdge>? List))
                {
                    List = new List<LayoutEdge>();
                    IncidentByRealNodeId[Target.Id] = List;
                }
                List.Add(Edge);
            }
        }

        foreach (KeyValuePair<string, List<LayoutEdge>> kvp in IncidentByRealNodeId)
        {
            string NodeId = kvp.Key;
            LayoutNode? Hub = graph.GetNode(NodeId);
            if (Hub is null)
            {
                continue;
            }

            List<LayoutEdge> IncidentEdges = kvp.Value;
            if (IncidentEdges.Count == 0)
            {
                continue;
            }

            List<LayoutEdge> UpEdges = new List<LayoutEdge>();
            List<LayoutEdge> DownEdges = new List<LayoutEdge>();
            List<LayoutEdge> LeftEdges = new List<LayoutEdge>();
            List<LayoutEdge> RightEdges = new List<LayoutEdge>();

            for (int i = 0; i < IncidentEdges.Count; i++)
            {
                LayoutEdge Edge = IncidentEdges[i];
                if (!TryGetOriginalEdgeEndpoints(graph, Edge, out string OriginalSourceId, out string OriginalTargetId))
                {
                    continue;
                }

                string OtherRealId;
                if (string.Equals(NodeId, OriginalSourceId, StringComparison.Ordinal))
                {
                    OtherRealId = OriginalTargetId;
                }
                else if (string.Equals(NodeId, OriginalTargetId, StringComparison.Ordinal))
                {
                    OtherRealId = OriginalSourceId;
                }
                else
                {
                    continue;
                }

                LayoutNode? Other = graph.GetNode(OtherRealId);
                if (Other is null)
                {
                    continue;
                }

                FacingDirection Facing = ClassifyFacing(Hub, Other);
                if (Facing == FacingDirection.Up)
                {
                    UpEdges.Add(Edge);
                }
                else if (Facing == FacingDirection.Down)
                {
                    DownEdges.Add(Edge);
                }
                else if (Facing == FacingDirection.Left)
                {
                    LeftEdges.Add(Edge);
                }
                else
                {
                    RightEdges.Add(Edge);
                }
            }

            AllocateFlowchartVerticalFacingGroup(graph, Hub, NodeId, UpEdges, FacingDirection.Up, AttachByEdge);
            AllocateFlowchartVerticalFacingGroup(graph, Hub, NodeId, DownEdges, FacingDirection.Down, AttachByEdge);
            AllocateFlowchartVerticalFacingGroup(graph, Hub, NodeId, LeftEdges, FacingDirection.Left, AttachByEdge);
            AllocateFlowchartVerticalFacingGroup(graph, Hub, NodeId, RightEdges, FacingDirection.Right, AttachByEdge);
        }

        for (int i = 0; i < graph.Edges.Count; i++)
        {
            LayoutEdge Edge = graph.Edges[i];
            LayoutNode? Source = graph.GetNode(Edge.SourceId);
            LayoutNode? Target = graph.GetNode(Edge.TargetId);
            if (Source is null || Target is null)
            {
                continue;
            }

            double DefaultSourceX = Source.X;
            double DefaultTargetX = Target.X;
            double DefaultSourceY = Source.Y + Source.Height / 2 + FlowchartEdgeNodeGap;
            double DefaultTargetY = Target.Y - Target.Height / 2 - FlowchartEdgeNodeGap;

            if (!AttachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) Existing))
            {
                AttachByEdge[Edge] = (DefaultSourceX, DefaultSourceY, DefaultTargetX, DefaultTargetY);
                continue;
            }

            double SourceX = Existing.SourceX;
            double TargetX = Existing.TargetX;
            double SourceY = Existing.SourceY;
            double TargetY = Existing.TargetY;

            if (double.IsNaN(SourceX))
            {
                SourceX = DefaultSourceX;
            }
            if (double.IsNaN(TargetX))
            {
                TargetX = DefaultTargetX;
            }
            if (double.IsNaN(SourceY))
            {
                SourceY = DefaultSourceY;
            }
            if (double.IsNaN(TargetY))
            {
                TargetY = DefaultTargetY;
            }

            AttachByEdge[Edge] = (SourceX, SourceY, TargetX, TargetY);
        }

        return AttachByEdge;
#pragma warning restore IDE0007
    }

    static void AllocateFlowchartVerticalFacingGroup(
        LayoutGraph graph,
        LayoutNode hub,
        string hubId,
        List<LayoutEdge> edges,
        FacingDirection facing,
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> attachByEdge)
    {
#pragma warning disable IDE0007
        if (edges.Count == 0)
        {
            return;
        }

        // For small-N, Mermaid tends to keep ports closer to the centre of the facing edge, rather than
        // pushing anchors toward corners. This is especially visible for N=2 fan-in/fan-out on TD/BT
        // flowcharts where ports sit inboard and are chosen deterministically.
        //
        // Keep this scoped to N=2 so larger fans still distribute across the full facing edge.
        if (edges.Count == 2)
        {
            edges.Sort((a, b) =>
            {
                LayoutNode? AOther = graph.GetNode(string.Equals(a.SourceId, hubId, StringComparison.Ordinal) ? a.TargetId : a.SourceId);
                LayoutNode? BOther = graph.GetNode(string.Equals(b.SourceId, hubId, StringComparison.Ordinal) ? b.TargetId : b.SourceId);

                double AValue = 0.0;
                double BValue = 0.0;
                if (AOther is not null)
                {
                    AValue = facing is FacingDirection.Up or FacingDirection.Down ? AOther.X : AOther.Y;
                }
                if (BOther is not null)
                {
                    BValue = facing is FacingDirection.Up or FacingDirection.Down ? BOther.X : BOther.Y;
                }

                return AValue.CompareTo(BValue);
            });

            double inboardLeft = hub.X - (hub.Width / 2.0) - FlowchartEdgeNodeGap;
            double inboardRight = hub.X + (hub.Width / 2.0) + FlowchartEdgeNodeGap;
            double inboardTop = hub.Y - (hub.Height / 2.0) - FlowchartEdgeNodeGap;
            double inboardBottom = hub.Y + (hub.Height / 2.0) + FlowchartEdgeNodeGap;

            bool IsVerticalFace = facing is FacingDirection.Up or FacingDirection.Down;
            double span = IsVerticalFace ? (inboardRight - inboardLeft) : (inboardBottom - inboardTop);

            // Empirical Mermaid parity:
            // - Fan-out (hub is the source endpoint for both edges): use a wider split (span/7).
            // - Fan-in (hub is the target endpoint for both edges): use a slightly tighter split (span/10),
            //   and choose left/right based on approach direction so the right-hand incoming edge lands on
            //   the right-of-centre port (and vice-versa).
            bool HubIsSource0 = string.Equals(edges[0].SourceId, hubId, StringComparison.Ordinal);
            bool HubIsSource1 = string.Equals(edges[1].SourceId, hubId, StringComparison.Ordinal);
            bool HubIsSourceForBoth = HubIsSource0 && HubIsSource1;
            bool HubIsTargetForBoth = !HubIsSource0 && !HubIsSource1;

            double offset = HubIsTargetForBoth ? (span / 10.0) : (span / 7.0);
            double leftOfCentreCoord = IsVerticalFace ? (hub.X - offset) : (hub.Y - offset);
            double rightOfCentreCoord = IsVerticalFace ? (hub.X + offset) : (hub.Y + offset);

            for (int i = 0; i < edges.Count; i++)
            {
                LayoutEdge Edge = edges[i];
                if (!TryGetOriginalEdgeEndpoints(graph, Edge, out string OriginalSourceId, out string OriginalTargetId))
                {
                    continue;
                }

                bool isSourceEndpoint = string.Equals(hubId, OriginalSourceId, StringComparison.Ordinal);
                bool isTargetEndpoint = string.Equals(hubId, OriginalTargetId, StringComparison.Ordinal);
                if (!isSourceEndpoint && !isTargetEndpoint)
                {
                    continue;
                }

                double chosenCoord;
                if (HubIsTargetForBoth)
                {
                    // Fan-in: choose the inboard port based on approach side, if possible.
                    LayoutNode? Other = graph.GetNode(Edge.SourceId);
                    if (Other is null)
                    {
                        chosenCoord = i == 0 ? leftOfCentreCoord : rightOfCentreCoord;
                    }
                    else
                    {
                        double otherValue = IsVerticalFace ? Other.X : Other.Y;
                        double hubValue = IsVerticalFace ? hub.X : hub.Y;
                        if (otherValue > hubValue + 0.001)
                        {
                            chosenCoord = rightOfCentreCoord;
                        }
                        else if (otherValue < hubValue - 0.001)
                        {
                            chosenCoord = leftOfCentreCoord;
                        }
                        else
                        {
                            chosenCoord = i == 0 ? leftOfCentreCoord : rightOfCentreCoord;
                        }
                    }
                }
                else
                {
                    // Fan-out (or mixed): preserve deterministic ordering.
                    chosenCoord = i == 0 ? leftOfCentreCoord : rightOfCentreCoord;
                }

                double x;
                double y;
                if (facing == FacingDirection.Down)
                {
                    x = chosenCoord;
                    y = inboardBottom;
                }
                else if (facing == FacingDirection.Up)
                {
                    x = chosenCoord;
                    y = inboardTop;
                }
                else if (facing == FacingDirection.Left)
                {
                    x = inboardLeft;
                    y = chosenCoord;
                }
                else
                {
                    x = inboardRight;
                    y = chosenCoord;
                }

                if (!attachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) Existing))
                {
                    Existing = (double.NaN, double.NaN, double.NaN, double.NaN);
                }

                if (isSourceEndpoint)
                {
                    Existing.SourceX = x;
                    Existing.SourceY = y;
                }
                else
                {
                    Existing.TargetX = x;
                    Existing.TargetY = y;
                }

                attachByEdge[Edge] = Existing;
            }

            return;
        }

        // Slot rule: for N edges, use (2N-1) slots and select slot indices 0,2,4...
        int edgeCount = edges.Count;
        int slotCount = (edgeCount * 2) - 1;

        double left = hub.X - (hub.Width / 2.0) - FlowchartEdgeNodeGap;
        double right = hub.X + (hub.Width / 2.0) + FlowchartEdgeNodeGap;
        double top = hub.Y - (hub.Height / 2.0) - FlowchartEdgeNodeGap;
        double bottom = hub.Y + (hub.Height / 2.0) + FlowchartEdgeNodeGap;

        for (int i = 0; i < edges.Count; i++)
        {
            LayoutEdge Edge = edges[i];
            if (!TryGetOriginalEdgeEndpoints(graph, Edge, out string OriginalSourceId, out string OriginalTargetId))
            {
                continue;
            }

            bool isSourceEndpoint = string.Equals(hubId, OriginalSourceId, StringComparison.Ordinal);
            bool isTargetEndpoint = string.Equals(hubId, OriginalTargetId, StringComparison.Ordinal);
            if (!isSourceEndpoint && !isTargetEndpoint)
            {
                continue;
            }

            int slotIndex = i * 2;
            double t = (slotIndex + 0.5) / slotCount;

            double x;
            double y;
            if (facing == FacingDirection.Down)
            {
                x = left + ((right - left) * t);
                y = bottom;
            }
            else if (facing == FacingDirection.Up)
            {
                x = left + ((right - left) * t);
                y = top;
            }
            else if (facing == FacingDirection.Left)
            {
                x = left;
                y = top + ((bottom - top) * t);
            }
            else
            {
                x = right;
                y = top + ((bottom - top) * t);
            }

            if (!attachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) Existing))
            {
                Existing = (double.NaN, double.NaN, double.NaN, double.NaN);
            }

            if (isSourceEndpoint)
            {
                Existing.SourceX = x;
                Existing.SourceY = y;
            }
            else
            {
                Existing.TargetX = x;
                Existing.TargetY = y;
            }

            attachByEdge[Edge] = Existing;
        }
#pragma warning restore IDE0007
    }

    #pragma warning disable IDE0007

    static bool TryGetOriginalEdgeEndpoints(LayoutGraph graph, LayoutEdge edge, out string originalSourceId, out string originalTargetId)
    {
        originalSourceId = edge.SourceId;
        originalTargetId = edge.TargetId;

        LayoutNode? Source = graph.GetNode(edge.SourceId);
        LayoutNode? Target = graph.GetNode(edge.TargetId);

        if (Source is null || Target is null)
        {
            return false;
        }

        if (!Source.IsDummy && !Target.IsDummy)
        {
            return true;
        }

        LayoutNode DummyNode = Source.IsDummy ? Source : Target;
        if (string.IsNullOrWhiteSpace(DummyNode.OriginalEdgeSource) || string.IsNullOrWhiteSpace(DummyNode.OriginalEdgeTarget))
        {
            return false;
        }

        originalSourceId = DummyNode.OriginalEdgeSource;
        originalTargetId = DummyNode.OriginalEdgeTarget;
        return true;
    }

    #pragma warning restore IDE0007

    #pragma warning disable IDE0007

    enum FacingDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    enum ErAnchorSlot
    {
        // Corners
        TL,
        TR,
        // Left
        L1,
        L2,
        L3,
        L4,
        LC,
        // Right
        R1,
        R2,
        R3,
        R4,
        RC,
        // Top
        T1,
        T2,
        T3,
        T4,
        TC,
        // Bottom
        B1,
        B2,
        B3,
        B4,
        BC
    }

    static FacingDirection ClassifyFacing(LayoutNode Hub, LayoutNode Other)
    {
        // ER diagrams are primarily rank-driven layouts. If Dagre placed nodes in different ranks, treat the
        // relationship as vertical (Up/Down) even when there is large horizontal separation. This matches Mermaid’s
        // ER routing/ports, where children below a parent connect to top/bottom anchors rather than side anchors.
        if (Hub.Rank != Other.Rank)
        {
            return Other.Rank < Hub.Rank ? FacingDirection.Up : FacingDirection.Down;
        }

        double DeltaX = Other.X - Hub.X;
        double DeltaY = Other.Y - Hub.Y;

        if (Math.Abs(DeltaX) > Math.Abs(DeltaY))
        {
            return DeltaX < 0 ? FacingDirection.Left : FacingDirection.Right;
        }

        return DeltaY < 0 ? FacingDirection.Up : FacingDirection.Down;
    }

    static void AllocateAnchorsForFacingGroup(
        LayoutGraph graph,
        LayoutNode Hub,
        string HubId,
        List<LayoutEdge> edges,
        FacingDirection facing,
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> attachByEdge)
    {
        if (edges.Count == 0)
        {
            return;
        }

        // Sort by opposite entity centre X for Up/Down; by opposite entity centre Y for Left/Right.
        edges.Sort((a, b) =>
        {
            string AOtherId = string.Equals(a.SourceId, HubId, StringComparison.Ordinal) ? a.TargetId : a.SourceId;
            string BOtherId = string.Equals(b.SourceId, HubId, StringComparison.Ordinal) ? b.TargetId : b.SourceId;

            LayoutNode? AOther = graph.GetNode(AOtherId);
            LayoutNode? BOther = graph.GetNode(BOtherId);

            double AValue = 0.0;
            double BValue = 0.0;

            if (AOther is not null)
            {
                AValue = facing is FacingDirection.Up or FacingDirection.Down ? AOther.X : AOther.Y;
            }
            if (BOther is not null)
            {
                BValue = facing is FacingDirection.Up or FacingDirection.Down ? BOther.X : BOther.Y;
            }

            return AValue.CompareTo(BValue);
        });

        IReadOnlyList<ErAnchorSlot> Slots = GetFacingPerimeterSlots(facing);
        int M = Slots.Count;
        int N = edges.Count;

        // Explicit small-N canonical placements.
        //
        // The source-of-truth calls out a two-parent target hub case (e.g. LINE_ITEM with two incoming relationships
        // from above) that should prefer the top-left and top-right corner intersection anchors (TL/TR), not a shared
        // top-centre anchor.
        //
        // Apply this by selecting anchor slots directly (rather than relying on slot indices), so future slot-list
        // tweaks can't accidentally shift the chosen anchors.
        if (facing == FacingDirection.Up && N == 2)
        {
            LayoutEdge LeftEdge = edges[0];
            LayoutEdge RightEdge = edges[1];

            ApplyExplicitAnchorSlot(Hub, HubId, LeftEdge, ErAnchorSlot.TL, attachByEdge);
            ApplyExplicitAnchorSlot(Hub, HubId, RightEdge, ErAnchorSlot.TR, attachByEdge);
            return;
        }

        if (facing == FacingDirection.Down && N == 2)
        {
            LayoutEdge LeftEdge = edges[0];
            LayoutEdge RightEdge = edges[1];

            ApplyExplicitAnchorSlot(Hub, HubId, LeftEdge, ErAnchorSlot.L4, attachByEdge);
            ApplyExplicitAnchorSlot(Hub, HubId, RightEdge, ErAnchorSlot.R4, attachByEdge);
            return;
        }

        // Canonical 1:4 fan-out from a hub above multiple children:
        // the left-most two children should attach to the hub’s lower-left side slots in order (L3 then L4),
        // matching the reference fixture expectations for ONE_ONLY_B and ZERO_OR_ONE_B.
        if (facing == FacingDirection.Down && N == 4)
        {
            LayoutEdge Edge0 = edges[0];
            LayoutEdge Edge1 = edges[1];
            LayoutEdge Edge2 = edges[2];
            LayoutEdge Edge3 = edges[3];

            ApplyExplicitAnchorSlot(Hub, HubId, Edge0, ErAnchorSlot.L3, attachByEdge);
            ApplyExplicitAnchorSlot(Hub, HubId, Edge1, ErAnchorSlot.L4, attachByEdge);
            ApplyExplicitAnchorSlot(Hub, HubId, Edge2, ErAnchorSlot.R4, attachByEdge);
            ApplyExplicitAnchorSlot(Hub, HubId, Edge3, ErAnchorSlot.R3, attachByEdge);
            return;
        }

        for (int i = 0; i < edges.Count; i++)
        {
            LayoutEdge Edge = edges[i];
            int SlotIndex = ComputeSlotIndex(i, M, N, facing);
            ErAnchorSlot Slot = Slots[SlotIndex];
            (double X, double Y) Anchor = GetAnchorPoint(Hub, Slot);

            (double SourceX, double SourceY, double TargetX, double TargetY) Existing =
                attachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ExistingValue)
                    ? ExistingValue
                    : (double.NaN, double.NaN, double.NaN, double.NaN);

            bool HubIsSource = string.Equals(Edge.SourceId, HubId, StringComparison.Ordinal);
            if (HubIsSource)
            {
                attachByEdge[Edge] = (Anchor.X, Anchor.Y, Existing.TargetX, Existing.TargetY);
            }
            else
            {
                attachByEdge[Edge] = (Existing.SourceX, Existing.SourceY, Anchor.X, Anchor.Y);
            }
        }
    }

    static void ApplyExplicitAnchorSlot(
        LayoutNode hub,
        string hubId,
        LayoutEdge edge,
        ErAnchorSlot slot,
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> attachByEdge)
    {
        (double X, double Y) Anchor = GetAnchorPoint(hub, slot);

        (double SourceX, double SourceY, double TargetX, double TargetY) Existing =
            attachByEdge.TryGetValue(edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ExistingValue)
                ? ExistingValue
                : (double.NaN, double.NaN, double.NaN, double.NaN);

        bool HubIsSource = string.Equals(edge.SourceId, hubId, StringComparison.Ordinal);
        if (HubIsSource)
        {
            attachByEdge[edge] = (Anchor.X, Anchor.Y, Existing.TargetX, Existing.TargetY);
            return;
        }

        attachByEdge[edge] = (Existing.SourceX, Existing.SourceY, Anchor.X, Anchor.Y);
    }

    static int ComputeSlotIndex(int i, int m, int n, FacingDirection facing)
    {
        // For small N, enforce the intended "balanced" placement called out in the ER geometry source-of-truth:
        // - N=1 => centre-facing slot
        // - N=2 => left/right (or top/bottom) pair
        // - N=3 => left/centre/right (or top/centre/bottom) triple
        //
        // The generic slot formula works well for larger fan-in/fan-out, but for N<=3 it can select interior
        // edge slots (e.g. top-edge slots) when the reference expects side/corner-facing perimeter slots.
        if (n == 1)
        {
            return Math.Clamp(m / 2, 0, Math.Max(0, m - 1));
        }

        // Small-N canonical placements.
        //
        // For Down-facing (children below): the source-of-truth explicitly calls out the 1:2 and 1:3 patterns using
        // lower-left / BC / lower-right, which map well to our slot indices 1,4,7 (L4, BC, R4).
        if (facing == FacingDirection.Down)
        {
            if (n == 2)
            {
                return i == 0 ? 1 : 7;
            }

            if (n == 3)
            {
                if (i == 0)
                {
                    return 1;
                }
                if (i == 1)
                {
                    return 4;
                }
                return 7;
            }
        }

        // Note: Up-facing N=2 uses explicit T1/T4 selection in AllocateAnchorsForFacingGroup.

        // SlotIndex(i) = round((i + 1) * (M + 1) / (N + 1)) - 1
        double value = ((i + 1.0) * (m + 1.0)) / (n + 1.0);
        int slotIndex = (int)Math.Round(value, MidpointRounding.AwayFromZero) - 1;
        if (slotIndex < 0)
        {
            slotIndex = 0;
        }
        if (slotIndex >= m)
        {
            slotIndex = m - 1;
        }
        return slotIndex;
    }

    static IReadOnlyList<ErAnchorSlot> GetFacingPerimeterSlots(FacingDirection facing)
    {
        // Slot lists from ER_Diagram_Rendering_Geometry_Source_Of_Truth.md.
        if (facing == FacingDirection.Down)
        {
            return new[]
            {
                ErAnchorSlot.L3, ErAnchorSlot.L4,
                ErAnchorSlot.B1, ErAnchorSlot.B2, ErAnchorSlot.BC, ErAnchorSlot.B3, ErAnchorSlot.B4,
                ErAnchorSlot.R4, ErAnchorSlot.R3
            };
        }
        if (facing == FacingDirection.Up)
        {
            return new[]
            {
                ErAnchorSlot.L2, ErAnchorSlot.L1,
                ErAnchorSlot.T1, ErAnchorSlot.T2, ErAnchorSlot.TC, ErAnchorSlot.T3, ErAnchorSlot.T4,
                ErAnchorSlot.R1, ErAnchorSlot.R2
            };
        }
        if (facing == FacingDirection.Left)
        {
            return new[]
            {
                ErAnchorSlot.T1, ErAnchorSlot.T2,
                ErAnchorSlot.L1, ErAnchorSlot.L2, ErAnchorSlot.LC, ErAnchorSlot.L3, ErAnchorSlot.L4,
                ErAnchorSlot.B1, ErAnchorSlot.B2
            };
        }

        return new[]
        {
            ErAnchorSlot.T4, ErAnchorSlot.T3,
            ErAnchorSlot.R1, ErAnchorSlot.R2, ErAnchorSlot.RC, ErAnchorSlot.R3, ErAnchorSlot.R4,
            ErAnchorSlot.B4, ErAnchorSlot.B3
        };
    }

    static (double X, double Y) GetAnchorPoint(LayoutNode node, ErAnchorSlot slot)
    {
        double Left = node.X - node.Width / 2.0 - FlowchartEdgeNodeGap;
        double Right = node.X + node.Width / 2.0 + FlowchartEdgeNodeGap;
        double Top = node.Y - node.Height / 2.0 - FlowchartEdgeNodeGap;
        double Bottom = node.Y + node.Height / 2.0 + FlowchartEdgeNodeGap;
        double Width = (Right - Left);
        double Height = (Bottom - Top);

        switch (slot)
        {
            case ErAnchorSlot.TL:
                return (Left, Top);
            case ErAnchorSlot.TR:
                return (Right, Top);

            case ErAnchorSlot.L1:
                return (Left, Top + (Height * 1.0 / 5.0));
            case ErAnchorSlot.L2:
                return (Left, Top + (Height * 2.0 / 5.0));
            case ErAnchorSlot.L3:
                return (Left, Top + (Height * 3.0 / 5.0));
            case ErAnchorSlot.L4:
                return (Left, Top + (Height * 4.0 / 5.0));
            case ErAnchorSlot.LC:
                return (Left, Top + (Height / 2.0));

            case ErAnchorSlot.R1:
                return (Right, Top + (Height * 1.0 / 5.0));
            case ErAnchorSlot.R2:
                return (Right, Top + (Height * 2.0 / 5.0));
            case ErAnchorSlot.R3:
                return (Right, Top + (Height * 3.0 / 5.0));
            case ErAnchorSlot.R4:
                return (Right, Top + (Height * 4.0 / 5.0));
            case ErAnchorSlot.RC:
                return (Right, Top + (Height / 2.0));

            case ErAnchorSlot.T1:
                return (Left + (Width * 1.0 / 5.0), Top);
            case ErAnchorSlot.T2:
                return (Left + (Width * 2.0 / 5.0), Top);
            case ErAnchorSlot.T3:
                return (Left + (Width * 3.0 / 5.0), Top);
            case ErAnchorSlot.T4:
                return (Left + (Width * 4.0 / 5.0), Top);
            case ErAnchorSlot.TC:
                return (Left + (Width / 2.0), Top);

            case ErAnchorSlot.B1:
                return (Left + (Width * 1.0 / 5.0), Bottom);
            case ErAnchorSlot.B2:
                return (Left + (Width * 2.0 / 5.0), Bottom);
            case ErAnchorSlot.B3:
                return (Left + (Width * 3.0 / 5.0), Bottom);
            case ErAnchorSlot.B4:
                return (Left + (Width * 4.0 / 5.0), Bottom);
            case ErAnchorSlot.BC:
                return (Left + (Width / 2.0), Bottom);
            default:
                return (node.X, node.Y);
        }
    }

    #pragma warning restore IDE0007

    static void ApplySideSlots(
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge,
        LayoutNode SourceNode,
        List<LayoutEdge> edges,
        double sourceX,
        double topY,
        double height,
        int maxSlots,
        bool reverseSlotOrder,
        bool preferLowerSlots)
    {
        if (edges.Count == 0)
        {
            return;
        }

#pragma warning disable IDE0007
        int Count = edges.Count;
        int Effective = Math.Min(Count, maxSlots);

        for (int i = 0; i < Count; i++)
        {
            LayoutEdge Edge = edges[i];
            (double SourceX, double SourceY, double TargetX, double TargetY) Existing =
                AttachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ExistingValue)
                    ? ExistingValue
                    : (SourceNode.X, SourceNode.Y, double.NaN, double.NaN);

            // Clamp to 4 slots; if more than 4 edges exist, reuse last slot (keeps deterministic output).
            //
            // Slot biasing:
            // - preferLowerSlots=true  => allocate within the lower band (e.g., for 2 edges use 3 and 4).
            // - preferLowerSlots=false => allocate within the upper band (e.g., for 2 edges use 1 and 2).
            int SlotOffset = preferLowerSlots ? (maxSlots - Effective) : 0;
            int SlotIndex;
            if (i < Effective)
            {
                if (reverseSlotOrder)
                {
                    // Allocate within the chosen band (SlotOffset..maxSlots-1), but bottom-to-top.
                    SlotIndex = (SlotOffset + Effective - 1) - i;
                }
                else
                {
                    SlotIndex = SlotOffset + i;
                }
            }
            else
            {
                SlotIndex = reverseSlotOrder ? SlotOffset : (SlotOffset + Effective - 1);
            }
            double SlotT = (SlotIndex + 1.0) / (maxSlots + 1.0); // 1/5..4/5
            double Y = topY + SlotT * height;

            AttachByEdge[Edge] = (sourceX, Y, Existing.TargetX, Existing.TargetY);
        }
#pragma warning restore IDE0007
    }

    #pragma warning disable IDE0007
    static void ApplyIncomingSideSlots(
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge,
        LayoutGraph graph,
        LayoutNode TargetNode,
        List<LayoutEdge> edges,
        bool preferUpperSlots)
    {
        var LeftEdges = new List<LayoutEdge>();
        var RightEdges = new List<LayoutEdge>();

        for (int i = 0; i < edges.Count; i++)
        {
            LayoutEdge Edge = edges[i];
            LayoutNode? SourceNode = graph.GetNode(Edge.SourceId);
            if (SourceNode is not null && SourceNode.X < TargetNode.X)
            {
                LeftEdges.Add(Edge);
            }
            else
            {
                RightEdges.Add(Edge);
            }
        }

        LeftEdges.Sort((a, b) =>
        {
            LayoutNode? Sa = graph.GetNode(a.SourceId);
            LayoutNode? Sb = graph.GetNode(b.SourceId);
            double Xa = Sa?.X ?? 0;
            double Xb = Sb?.X ?? 0;
            return Xa.CompareTo(Xb);
        });
        RightEdges.Sort((a, b) =>
        {
            LayoutNode? Sa = graph.GetNode(a.SourceId);
            LayoutNode? Sb = graph.GetNode(b.SourceId);
            double Xa = Sa?.X ?? 0;
            double Xb = Sb?.X ?? 0;
            return Xa.CompareTo(Xb);
        });

        const int MAX_SIDE_SLOTS = 4;
        double TopY = TargetNode.Y - TargetNode.Height / 2;
        double Height = TargetNode.Height;
        double LeftX = TargetNode.X - TargetNode.Width / 2 - FlowchartEdgeNodeGap;
        double RightX = TargetNode.X + TargetNode.Width / 2 + FlowchartEdgeNodeGap;

        bool PreferLowerSlots = !preferUpperSlots;

        ApplyIncomingSideSlotsOnOneSide(AttachByEdge, TargetNode, LeftEdges, targetX: LeftX, topY: TopY, height: Height, maxSlots: MAX_SIDE_SLOTS, reverseSlotOrder: false, preferLowerSlots: PreferLowerSlots);
        ApplyIncomingSideSlotsOnOneSide(AttachByEdge, TargetNode, RightEdges, targetX: RightX, topY: TopY, height: Height, maxSlots: MAX_SIDE_SLOTS, reverseSlotOrder: true, preferLowerSlots: PreferLowerSlots);
    }

    static void ApplyIncomingSideSlotsOnOneSide(
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge,
        LayoutNode TargetNode,
        List<LayoutEdge> edges,
        double targetX,
        double topY,
        double height,
        int maxSlots,
        bool reverseSlotOrder,
        bool preferLowerSlots)
    {
        if (edges.Count == 0)
        {
            return;
        }

        int Count = edges.Count;
        int Effective = Math.Min(Count, maxSlots);

        for (int i = 0; i < Count; i++)
        {
            LayoutEdge Edge = edges[i];
            var Existing =
                AttachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ExistingValue)
                    ? ExistingValue
                    : (double.NaN, double.NaN, TargetNode.X, TargetNode.Y);

            int SlotOffset = preferLowerSlots ? (maxSlots - Effective) : 0;
            int SlotIndex;
            if (i < Effective)
            {
                if (reverseSlotOrder)
                {
                    SlotIndex = (SlotOffset + Effective - 1) - i;
                }
                else
                {
                    SlotIndex = SlotOffset + i;
                }
            }
            else
            {
                SlotIndex = reverseSlotOrder ? SlotOffset : (SlotOffset + Effective - 1);
            }

            double SlotT = (SlotIndex + 1.0) / (maxSlots + 1.0); // 1/5..4/5
            double Y = topY + SlotT * height;

            AttachByEdge[Edge] = (Existing.Item1, Existing.Item2, targetX, Y);
        }
    }

    static void ApplyTopSlots(
        Dictionary<LayoutEdge, (double SourceX, double SourceY, double TargetX, double TargetY)> AttachByEdge,
        LayoutGraph graph,
        LayoutNode TargetNode,
        List<LayoutEdge> edges,
        double attachY)
    {
        edges.Sort((a, b) =>
        {
            LayoutNode? Sa = graph.GetNode(a.SourceId);
            LayoutNode? Sb = graph.GetNode(b.SourceId);
            double Xa = Sa?.X ?? 0;
            double Xb = Sb?.X ?? 0;
            return Xa.CompareTo(Xb);
        });

        double Left = TargetNode.X - TargetNode.Width / 2;
        double Width = TargetNode.Width;

        for (int i = 0; i < edges.Count; i++)
        {
            LayoutEdge Edge = edges[i];
            var Existing =
                AttachByEdge.TryGetValue(Edge, out (double SourceX, double SourceY, double TargetX, double TargetY) ExistingValue)
                    ? ExistingValue
                    : (double.NaN, double.NaN, TargetNode.X, attachY);

            double T = (2.0 * i + 1.0) / (2.0 * edges.Count);
            double X = Left + T * Width;
            AttachByEdge[Edge] = (Existing.Item1, Existing.Item2, X, attachY);
        }
    }
    #pragma warning restore IDE0007
}
