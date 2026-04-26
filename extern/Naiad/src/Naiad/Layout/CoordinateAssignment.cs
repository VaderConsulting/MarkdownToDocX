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

        // Per the ER geometry source-of-truth: allocate anchors based on relationship grouping by facing direction,
        // then distribute evenly across the facing perimeter slot list.
        Dictionary<string, List<LayoutEdge>> IncidentByNodeId = new Dictionary<string, List<LayoutEdge>>(StringComparer.Ordinal);
        for (int i = 0; i < RegularEdges.Count; i++)
        {
            LayoutEdge Edge = RegularEdges[i];

            if (!IncidentByNodeId.TryGetValue(Edge.SourceId, out List<LayoutEdge>? SourceEdges))
            {
                SourceEdges = new List<LayoutEdge>();
                IncidentByNodeId[Edge.SourceId] = SourceEdges;
            }
            SourceEdges.Add(Edge);

            if (!IncidentByNodeId.TryGetValue(Edge.TargetId, out List<LayoutEdge>? TargetEdges))
            {
                TargetEdges = new List<LayoutEdge>();
                IncidentByNodeId[Edge.TargetId] = TargetEdges;
            }
            TargetEdges.Add(Edge);
        }

        foreach (KeyValuePair<string, List<LayoutEdge>> kvp in IncidentByNodeId)
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
                string OtherId = string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal) ? Edge.TargetId : Edge.SourceId;
                LayoutNode? Other = graph.GetNode(OtherId);
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

        // For Up-facing (parents above): the updated source-of-truth emphasises that a two-parent target (e.g. LINE_ITEM)
        // should use corner-adjacent upper-perimeter anchors (upper-left shoulder and upper-right shoulder), rather than
        // collapsing into TC. In our Up slot list, those correspond to T1 (index 2) and T4 (index 6).
        if (facing == FacingDirection.Up)
        {
            if (n == 2)
            {
                return i == 0 ? 2 : 6;
            }

            if (n == 3)
            {
                if (i == 0)
                {
                    return 2;
                }
                if (i == 1)
                {
                    return 4;
                }
                return 6;
            }
        }

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
