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
#pragma warning restore IDE0007

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

            if (source.IsDummy || target.IsDummy)
            {
                // Part of a long edge - just add the node positions
                edge.Points.Add(new(source.X, source.Y));
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
                    sourceEdgeX = source.X + source.Width / 2 + FlowchartEdgeNodeGap;
                    sourceEdgeY = source.Y;
                    targetEdgeXNoShorten = target.X - target.Width / 2 - FlowchartEdgeNodeGap;
                    targetEdgeYNoShorten = target.Y;
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
                if (dummyLookup.TryGetValue((edge.SourceId, edge.TargetId), out var dummies))
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

        List<LayoutEdge> RegularEdges = graph.Edges
            .Where(_ =>
            {
                LayoutNode? Source = graph.GetNode(_.SourceId);
                LayoutNode? Target = graph.GetNode(_.TargetId);
                if (Source is null || Target is null)
                {
                    return false;
                }

                if (Source.IsDummy || Target.IsDummy)
                {
                    return false;
                }

                return true;
            })
            .ToList();

        Dictionary<string, List<LayoutEdge>> OutgoingBySourceId = new Dictionary<string, List<LayoutEdge>>(StringComparer.Ordinal);
        Dictionary<string, List<LayoutEdge>> IncomingByTargetId = new Dictionary<string, List<LayoutEdge>>(StringComparer.Ordinal);

        for (int i = 0; i < RegularEdges.Count; i++)
        {
            LayoutEdge Edge = RegularEdges[i];

            if (!OutgoingBySourceId.TryGetValue(Edge.SourceId, out List<LayoutEdge>? Outs))
            {
                Outs = new List<LayoutEdge>();
                OutgoingBySourceId[Edge.SourceId] = Outs;
            }
            Outs.Add(Edge);

            if (!IncomingByTargetId.TryGetValue(Edge.TargetId, out List<LayoutEdge>? Ins))
            {
                Ins = new List<LayoutEdge>();
                IncomingByTargetId[Edge.TargetId] = Ins;
            }
            Ins.Add(Edge);
        }

        foreach (KeyValuePair<string, List<LayoutEdge>> kvp in OutgoingBySourceId)
        {
            LayoutNode? SourceNode = graph.GetNode(kvp.Key);
            if (SourceNode is null)
            {
                continue;
            }

            List<LayoutEdge> Outs = kvp.Value;
            if (Outs.Count <= 1)
            {
                continue;
            }

            // ER fan-out hub (one source -> many targets):
            // Use side anchors, biased toward the side segment that visually faces the target group.
            //
            // Key rule (important distinction vs fan-in):
            // - If a source fans out to many targets below, use LOWER-biased side anchors (3L/4L, 4R/3R).
            // - If a source fans out to many targets above, use UPPER-biased side anchors (1L/2L, 2R/1R).

            List<LayoutEdge> LeftEdges = new List<LayoutEdge>();
            List<LayoutEdge> RightEdges = new List<LayoutEdge>();
            double SumTargetY = 0.0;
            int TargetYCount = 0;

            for (int i = 0; i < Outs.Count; i++)
            {
                LayoutEdge Edge = Outs[i];
                LayoutNode? TargetNode = graph.GetNode(Edge.TargetId);
                if (TargetNode is not null)
                {
                    SumTargetY += TargetNode.Y;
                    TargetYCount++;
                }
                if (TargetNode is not null && TargetNode.X < SourceNode.X)
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
                LayoutNode? Ta = graph.GetNode(a.TargetId);
                LayoutNode? Tb = graph.GetNode(b.TargetId);
                double Xa = Ta?.X ?? 0;
                double Xb = Tb?.X ?? 0;
                return Xa.CompareTo(Xb);
            });
            RightEdges.Sort((a, b) =>
            {
                LayoutNode? Ta = graph.GetNode(a.TargetId);
                LayoutNode? Tb = graph.GetNode(b.TargetId);
                double Xa = Ta?.X ?? 0;
                double Xb = Tb?.X ?? 0;
                return Xa.CompareTo(Xb);
            });

            const int MAX_SIDE_SLOTS = 4;
            double TopY = SourceNode.Y - SourceNode.Height / 2;
            double Height = SourceNode.Height;
            double LeftX = SourceNode.X - SourceNode.Width / 2 - FlowchartEdgeNodeGap;
            double RightX = SourceNode.X + SourceNode.Width / 2 + FlowchartEdgeNodeGap;

            double AverageTargetY = TargetYCount > 0 ? (SumTargetY / TargetYCount) : SourceNode.Y;
            bool TargetsAreBelow = AverageTargetY > SourceNode.Y;

            ApplySideSlots(
                AttachByEdge,
                SourceNode,
                LeftEdges,
                sourceX: LeftX,
                topY: TopY,
                height: Height,
                maxSlots: MAX_SIDE_SLOTS,
                reverseSlotOrder: false,
                preferLowerSlots: TargetsAreBelow);

            ApplySideSlots(
                AttachByEdge,
                SourceNode,
                RightEdges,
                sourceX: RightX,
                topY: TopY,
                height: Height,
                maxSlots: MAX_SIDE_SLOTS,
                reverseSlotOrder: true,
                preferLowerSlots: TargetsAreBelow);
        }

        foreach (KeyValuePair<string, List<LayoutEdge>> kvp in IncomingByTargetId)
        {
            LayoutNode? TargetNode = graph.GetNode(kvp.Key);
            if (TargetNode is null)
            {
                continue;
            }

            List<LayoutEdge> Ins = kvp.Value;
            if (Ins.Count <= 1)
            {
                continue;
            }

            // ER fan-in hub (many sources -> one target):
            // Do NOT reuse the fan-out rule. The target should use anchors that face the incoming source group.
            // - If sources are above the target, use UPPER-biased side anchors (1L/2L, 2R/1R) or top anchors.
            // - If sources are below the target, use LOWER-biased side anchors (3L/4L, 4R/3R) or bottom anchors.

            double SumSourceY = 0.0;
            int SourceYCount = 0;
            for (int i = 0; i < Ins.Count; i++)
            {
                LayoutNode? SourceNode = graph.GetNode(Ins[i].SourceId);
                if (SourceNode is not null)
                {
                    SumSourceY += SourceNode.Y;
                    SourceYCount++;
                }
            }

            double AverageSourceY = SourceYCount > 0 ? (SumSourceY / SourceYCount) : TargetNode.Y;
            bool SourcesAreAbove = AverageSourceY < TargetNode.Y;

            double VerticalSeparation = Math.Abs(TargetNode.Y - AverageSourceY);
            bool PreferTopAnchors = SourcesAreAbove && VerticalSeparation > (TargetNode.Height * 0.75);

            if (PreferTopAnchors)
            {
                ApplyTopSlots(AttachByEdge, graph, TargetNode, Ins, attachY: TargetNode.Y - TargetNode.Height / 2 - FlowchartEdgeNodeGap);
            }
            else
            {
                ApplyIncomingSideSlots(AttachByEdge, graph, TargetNode, Ins, preferUpperSlots: SourcesAreAbove);
            }
        }

        for (int i = 0; i < RegularEdges.Count; i++)
        {
            LayoutEdge Edge = RegularEdges[i];
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
