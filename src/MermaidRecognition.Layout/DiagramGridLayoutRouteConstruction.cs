using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Route-construction helpers used by the named route stage.</summary>
public static partial class DiagramGridLayout
{    private static void RebuildOrdinaryFanOutRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        IReadOnlySet<string>? SourceIds = null)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (SourceIds != null && !SourceIds.Contains(Route.SourceId))
            {
                continue;
            }

            if (!Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                || FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape))
            {
                continue;
            }

            if (!FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Route.SourceId,
                    SourceNode.Shape,
                    Direction,
                    BoundsById))
            {
                continue;
            }

            if (!FlowchartOrdinaryFanOutRouteBuilder.TryGetFlowSides(
                    Direction, out string AllocatedFlowExit, out string AllocatedFlowEntry)
                || !string.Equals(Route.ExitSide, AllocatedFlowExit, StringComparison.Ordinal)
                || !string.Equals(Route.EntrySide, AllocatedFlowEntry, StringComparison.Ordinal))
            {
                // Joint allocation owns endpoint sides. This specialized path
                // only applies to rank-forward ports; never rewrite a port to
                // the flow side after allocation when endpoint geometry chose
                // a different side.
                continue;
            }

            EdgeContract? Edge = null;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
                {
                    Edge = Candidate;
                    break;
                }
            }

            if (Edge == null)
            {
                continue;
            }

            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode)
                ? TargetNode.Shape
                : null;
            if (!FlowchartOrdinaryFanOutRouteBuilder.TryBuildEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceNode.Shape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            // Keep the preallocated endpoints, including a centre port already
            // reserved by a straight continuation. The route builder supplies
            // the rank-gap track between those jointly selected ports.
            if (Route.Points.Count >= 2)
            {
                FlowchartLayoutPoint Start = Route.Points[0];
                FlowchartLayoutPoint End = Route.Points[^1];
                LayoutPoints = [(Start.X, Start.Y)];
                if (Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft)
                {
                    if (Math.Abs(Start.Y - End.Y) > 0.5)
                    {
                        double Track = (Start.X + End.X) / 2;
                        LayoutPoints.Add((Track, Start.Y));
                        LayoutPoints.Add((Track, End.Y));
                    }
                }
                else if (Math.Abs(Start.X - End.X) > 0.5)
                {
                    double Track = (Start.Y + End.Y) / 2;
                    LayoutPoints.Add((Start.X, Track));
                    LayoutPoints.Add((End.X, Track));
                }
                LayoutPoints.Add((End.X, End.Y));
            }

            // A fan template is only a candidate. It may not replace a valid
            // obstacle-aware route with a midpoint track through another node
            // or a protected frame title.
            if (!DiagramGridRouteBuilder.PathIsValid(LayoutPoints, BoundsById,
                    Route.SourceId, Route.TargetId, Contract, MaxCorners: int.MaxValue)) continue;
            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void RebuildAlignedSimpleFanRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        if (Direction is not (FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop)) return;
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = BuildPlacedBounds(Placed, Frames);
        bool Eligible(EdgeContract Edge) => string.IsNullOrWhiteSpace(Edge.Label)
            && Edge.RoutingHint?.IsActive != true
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract) == null
            && FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract) == null;
        bool AlignedAndSymmetric(IReadOnlyList<EdgeContract> Edges, bool Outgoing)
        {
            if (Edges.Count != 3) return false;
            string HubId = Outgoing ? Edges[0].SourceId : Edges[0].TargetId;
            if (!Bounds.TryGetValue(HubId, out FlowchartLayoutNodeBounds Hub)) return false;
            List<FlowchartLayoutNodeBounds> Leaves = [];
            foreach (EdgeContract Edge in Edges)
            {
                string Id = Outgoing ? Edge.TargetId : Edge.SourceId;
                if (!Bounds.TryGetValue(Id, out FlowchartLayoutNodeBounds Leaf)) return false;
                Leaves.Add(Leaf);
            }
            return Leaves.All(Leaf => Math.Abs(Leaf.CenterY - Leaves[0].CenterY) < 0.5)
                && Math.Abs(Leaves.Min(Leaf => Leaf.CenterX) + Leaves.Max(Leaf => Leaf.CenterX)
                    - (2 * Hub.CenterX)) < 0.5
                && Math.Abs(Leaves.OrderBy(Leaf => Math.Abs(Leaf.CenterX - Hub.CenterX)).First().CenterX
                    - Hub.CenterX) < 0.5;
        }

        foreach (IGrouping<string, EdgeContract> Group in Contract.Edges.Where(Eligible).GroupBy(Edge => Edge.SourceId))
        {
            List<EdgeContract> Edges = Group.OrderBy(Edge => Edge.CreationIndex).ToList();
            if (!AlignedAndSymmetric(Edges, Outgoing: true)
                || !Nodes.TryGetValue(Group.Key, out NodeContract? Hub)
                || string.Equals(Hub.Shape, "circle", StringComparison.OrdinalIgnoreCase)
                || !FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract, Group.Key, Hub.Shape, Direction, Bounds)) continue;
            foreach (EdgeContract Edge in Edges)
            {
                FlowchartLayoutEdgeRoute? Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Edge.SourceId
                    && Route.TargetId == Edge.TargetId);
                if (Route == null || !FlowchartOrdinaryFanOutRouteBuilder.TryBuildEdgeRoute(
                        Edge, Contract, Bounds, Hub.Shape,
                        Nodes.TryGetValue(Edge.TargetId, out NodeContract? Target) ? Target.Shape : null,
                        Direction, out List<(double X, double Y)> Points)) continue;
                Route.Points.Clear();
                foreach ((double X, double Y) in Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }

        foreach (IGrouping<string, EdgeContract> Group in Contract.Edges.Where(Eligible).GroupBy(Edge => Edge.TargetId))
        {
            List<EdgeContract> Edges = Group.OrderBy(Edge => Edge.CreationIndex).ToList();
            if (!AlignedAndSymmetric(Edges, Outgoing: false)
                || !Nodes.TryGetValue(Group.Key, out NodeContract? Hub)
                || string.Equals(Hub.Shape, "circle", StringComparison.OrdinalIgnoreCase)
                || FlowchartSharedPortAllocator.IsDecisionShape(Hub.Shape)) continue;
            foreach (EdgeContract Edge in Edges)
            {
                FlowchartLayoutEdgeRoute? Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Edge.SourceId
                    && Route.TargetId == Edge.TargetId);
                if (Route == null || !FlowchartFanInPortAllocator.TryResolveEdgeRoute(
                        Edge, Contract, Bounds,
                        Nodes.TryGetValue(Edge.SourceId, out NodeContract? Source) ? Source.Shape : null,
                        Hub.Shape, Direction, out List<(double X, double Y)> Points)) continue;
                Route.Points.Clear();
                foreach ((double X, double Y) in Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }
    }

    /// <summary>
    /// B15: keeps the forward main chain in one column; back edges never pull nodes sideways.
    /// </summary>
    private static void AlignForwardMainChainColumn(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal)
    {
        if (Horizontal || Placed.Count == 0)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        HashSet<string> HasForwardIncoming = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || BackEdges.Contains(Edge))
            {
                continue;
            }

            HasForwardIncoming.Add(Edge.TargetId);
        }

        Dictionary<string, int> DeclaredOrder = Contract.Nodes
            .Select((Node, Index) => (Node.Id, Index))
            .ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
        string? StartId = null;
        int LongestRun = -1;
        foreach (GridNode Node in Placed.Where(Node => !HasForwardIncoming.Contains(Node.Id))
            .OrderBy(Node => DeclaredOrder.TryGetValue(Node.Id, out int Order) ? Order : int.MaxValue))
        {
            int RunLength = MainRunLength(Node.Id);
            if (RunLength > LongestRun)
            {
                StartId = Node.Id;
                LongestRun = RunLength;
            }
        }

        if (StartId == null || !ById.TryGetValue(StartId, out GridNode? StartNode))
        {
            return;
        }

        HashSet<string> SpineIds = new HashSet<string>(StringComparer.Ordinal);
        string? CurrentId = StartId;
        while (CurrentId is not null && ById.TryGetValue(CurrentId, out GridNode? CurrentNode))
        {
            if (!SpineIds.Add(CurrentId))
            {
                break;
            }

            List<EdgeContract> ForwardOutgoing = [];
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || BackEdges.Contains(Edge)
                    || !string.Equals(Edge.SourceId, CurrentId, StringComparison.Ordinal))
                {
                    continue;
                }

                ForwardOutgoing.Add(Edge);
            }

            if (ForwardOutgoing.Count == 0)
            {
                break;
            }

            ForwardOutgoing.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
            string NextId = ForwardOutgoing[0].TargetId;
            if (!ById.TryGetValue(NextId, out GridNode? NextNode) || NextNode.Rank <= CurrentNode.Rank)
            {
                break;
            }

            CurrentId = NextId;
        }

        int SpineColumn = StartNode.Column;
        foreach (string SpineId in SpineIds)
        {
            if (ById.TryGetValue(SpineId, out GridNode? SpineNode))
            {
                SpineNode.Column = SpineColumn;
            }
        }

        int MainRunLength(string RootId)
        {
            HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
            string? Current = RootId;
            while (Current is not null && ById.ContainsKey(Current) && Visited.Add(Current))
            {
                EdgeContract? Next = Contract.Edges
                    .Where(Edge => string.Equals(Edge.SourceId, Current, StringComparison.Ordinal)
                        && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                        && !BackEdges.Contains(Edge)
                        && ById.ContainsKey(Edge.TargetId))
                    .OrderBy(Edge => Edge.CreationIndex)
                    .FirstOrDefault();
                if (Next is null || !ById.TryGetValue(Next.TargetId, out GridNode? NextNode)
                    || NextNode.Rank <= ById[Current].Rank)
                {
                    break;
                }

                Current = Next.TargetId;
            }

            return Visited.Count;
        }
    }

    /// <summary>
    /// B15: dagre omits back edges from placement, so add placeholder routes before port assignment.
    /// </summary>
    private static void EnsureBackEdgeRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || !BackEdges.Contains(Edge))
            {
                continue;
            }

            bool Found = false;
            foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
            {
                if (string.Equals(Route.SourceId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Route.TargetId, Edge.TargetId, StringComparison.Ordinal))
                {
                    Found = true;
                    break;
                }
            }

            if (Found
                || !ById.TryGetValue(Edge.SourceId, out GridNode? Source)
                || !ById.TryGetValue(Edge.TargetId, out GridNode? Target))
            {
                continue;
            }

            FlowchartLayoutEdgeRoute Placeholder = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Edge.Id ?? string.Empty,
                SourceId = Edge.SourceId,
                TargetId = Edge.TargetId,
                ExitSide = "right",
                EntrySide = "right"
            };
            Placeholder.Points.Add(new FlowchartLayoutPoint { X = Math.Round(Source.X), Y = Math.Round(Source.Y) });
            Placeholder.Points.Add(new FlowchartLayoutPoint { X = Math.Round(Target.X), Y = Math.Round(Target.Y) });
            Copy.EdgeRoutesMutable.Add(Placeholder);
        }
    }

    private static void RebuildSoleForwardDecisionBranchRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            EdgeContract? Edge = FindContractEdge(Contract, Route);
            if (Edge == null || BackEdges.Contains(Edge))
            {
                continue;
            }

            if (!FlowchartLoopBackRouteHelper.TryResolveSoleForwardDecisionBranch(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            if (BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                SetRouteEndpointSides(Route, SourceBounds, TargetBounds, LayoutPoints, HorizontalFlow);
            }

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void RebuildLoopBackRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            EdgeContract? Edge = FindContractEdge(Contract, Route);
            if (Edge == null || !BackEdges.Contains(Edge))
            {
                continue;
            }
            if (Math.Abs(Route.ExitNormalX) > 1e-9 || Math.Abs(Route.ExitNormalY) > 1e-9
                || Math.Abs(Route.EntryNormalX) > 1e-9 || Math.Abs(Route.EntryNormalY) > 1e-9)
            {
                // The final model route stage rebuilds these from their allocated
                // slanted-edge normals; legacy loop templates use cardinal vertices.
                continue;
            }

            string? SourceShape = Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
            if (!HorizontalFlow && Route.ExitSide is "left" or "right" && Route.EntrySide == Route.ExitSide
                && BoundsById.TryGetValue(Route.SourceId, out var AlignedSource)
                && BoundsById.TryGetValue(Route.TargetId, out var AlignedTarget)
                && Math.Abs(HorizontalFlow ? AlignedSource.CenterY - AlignedTarget.CenterY
                    : AlignedSource.CenterX - AlignedTarget.CenterX) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                && Route.Points.Count is >= 4 and <= 5
                && Route.Points.Zip(Route.Points.Skip(1)).All(Pair =>
                    (Math.Abs(Pair.First.X - Pair.Second.X) < 0.5 || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.5)
                    && !FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                        Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y,
                        BoundsById, Route.SourceId, Route.TargetId)))
            {
                // Keep the side ports allocated with the other ends in the diagram.
                continue;
            }
            List<(double X, double Y)> LayoutPoints = [];
            bool Resolved = false;

            if (HorizontalFlow
                && BoundsById.TryGetValue(Route.SourceId, out var RetryBounds)
                && BoundsById.TryGetValue(Route.TargetId, out var InputBounds)
                && RetryBounds.CenterY < InputBounds.CenterY
                && Contract.Edges.Any(ParentEdge => ParentEdge.TargetId == Route.SourceId
                    && Nodes.TryGetValue(ParentEdge.SourceId, out var ParentNode)
                    && FlowchartDagrePresentation.IsDiamondShape(ParentNode.Shape)
                    && BoundsById.TryGetValue(ParentEdge.SourceId, out var ParentBounds)
                    && Math.Abs(ParentBounds.CenterX - RetryBounds.CenterX) < 0.5))
            {
                double StartY = RetryBounds.CenterY - RetryBounds.Height / 2
                    - FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
                double EndY = InputBounds.CenterY - InputBounds.Height / 2
                    - FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
                double LaneY = BoundsById.Values.Min(Bounds => Bounds.CenterY - Bounds.Height / 2)
                    - 2 * DiagramGridMetrics.GRID_UNIT_PX;
                LayoutPoints = [(RetryBounds.CenterX, StartY), (RetryBounds.CenterX, LaneY),
                    (InputBounds.CenterX, LaneY), (InputBounds.CenterX, EndY)];
                Resolved = true;
            }

            if (!Resolved && HorizontalFlow
                && BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds LrSourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds LrTargetBounds)
                && FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                    Edge,
                    Contract,
                    LrSourceBounds,
                    LrTargetBounds,
                    SourceShape,
                    TargetShape))
            {
                Resolved = FlowchartEdgePathBuilder.TryBuildNestedLrRetryLoopPath(
                        Edge,
                        Contract,
                        BoundsById,
                        Nodes,
                        Direction,
                        out LayoutPoints)
                    || FlowchartEdgePathBuilder.TryBuildLrRetryLoopPath(
                        LrSourceBounds,
                        LrTargetBounds,
                        BoundsById.Values.ToList(),
                        Direction,
                        out LayoutPoints,
                        Edge.MarkerStart,
                        Edge.MarkerEnd);
            }

            if (!Resolved
                && !FlowchartLoopBackRouteHelper.TryResolveDecisionLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes,
                    Direction,
                    out LayoutPoints)
                && !FlowchartFanInPortAllocator.TryResolveLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    TargetShape,
                    Direction,
                    out LayoutPoints))
            {
                continue;
            }

            if (LayoutPoints.Count < 2)
            {
                continue;
            }

            if (BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                SetRouteEndpointSides(Route, SourceBounds, TargetBounds, LayoutPoints, HorizontalFlow);
            }

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void SetRouteEndpointSides(
        FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        List<(double X, double Y)> LayoutPoints,
        bool HorizontalFlow)
    {
        if (LayoutPoints.Count == 0)
        {
            return;
        }

        (double StartX, double StartY) = LayoutPoints[0];
        (double EndX, double EndY) = LayoutPoints[^1];
        double SourceOffsetX = Math.Abs(StartX - SourceBounds.CenterX);
        double SourceOffsetY = Math.Abs(StartY - SourceBounds.CenterY);
        double TargetOffsetX = Math.Abs(EndX - TargetBounds.CenterX);
        double TargetOffsetY = Math.Abs(EndY - TargetBounds.CenterY);

        if (HorizontalFlow)
        {
            Route.ExitSide = SourceOffsetY > SourceOffsetX
                ? (StartY >= SourceBounds.CenterY ? "top" : "bottom")
                : (StartX >= SourceBounds.CenterX ? "right" : "left");
            Route.EntrySide = TargetOffsetY > TargetOffsetX
                ? (EndY >= TargetBounds.CenterY ? "top" : "bottom")
                : (EndX >= TargetBounds.CenterX ? "right" : "left");
            return;
        }

        Route.ExitSide = SourceOffsetX > SourceOffsetY
            ? (StartX >= SourceBounds.CenterX ? "right" : "left")
            : (StartY <= SourceBounds.CenterY ? "bottom" : "top");
        Route.EntrySide = TargetOffsetX > TargetOffsetY
            ? (EndX >= TargetBounds.CenterX ? "right" : "left")
            : (EndY <= TargetBounds.CenterY ? "bottom" : "top");
    }

    private static void RebuildDecisionFanOutRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            // Joint port allocation has already assigned the diamond's symmetric edge slot
            // and its true edge normal. Do not replace it with the legacy vertex-first path.
            if (Math.Abs(Route.ExitNormalX) > 1e-9 || Math.Abs(Route.ExitNormalY) > 1e-9)
                continue;
            if (!Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                || !FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape))
            {
                continue;
            }

            int Outgoing = 0;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && !string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Candidate))
                {
                    Outgoing++;
                }
            }

            if (Outgoing < 2)
            {
                continue;
            }

            EdgeContract? Edge = null;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
                {
                    Edge = Candidate;
                    break;
                }
            }

            if (Edge == null || BackEdges.Contains(Edge))
            {
                continue;
            }

            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode)
                ? TargetNode.Shape
                : null;
            FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceNode.Shape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    IncomingPorts: Copy.EdgeRoutes.Where(Inbound => Inbound.TargetId == Route.SourceId
                        && Inbound.Points.Count >= 2).Select(Inbound =>
                            (Inbound.Points[^1].X, Inbound.Points[^1].Y)).ToList());

            bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
            var Source = BoundsById[Route.SourceId];
            var Target = BoundsById[Route.TargetId];
            double Main(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterX : Node.CenterY;
            double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
            double Side = Math.Sign(Cross(Target) - Cross(Source));
            double Flow = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.BottomToTop ? 1 : -1;
            double SourceCross = Cross(Source) + Side * (Horizontal ? Source.Height : Source.Width) / 2;
            double TargetMain = Main(Target) - Flow * (Horizontal ? Target.Width : Target.Height) / 2;
            (double X, double Y) Map(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
            var Vertex = Map(Main(Source), SourceCross);
            bool Occupied = Copy.EdgeRoutes.Any(Inbound => Inbound.TargetId == Route.SourceId
                && Inbound.Points.Count >= 2 && Math.Abs(Inbound.Points[^1].X - Vertex.X) < 1
                && Math.Abs(Inbound.Points[^1].Y - Vertex.Y) < 1);
            int SameSide = Contract.Edges.Count(Candidate => Candidate.SourceId == Route.SourceId
                && !BackEdges.Contains(Candidate) && BoundsById.TryGetValue(Candidate.TargetId, out var Other)
                && Math.Sign(Cross(Other) - Cross(Source)) == Side);
            if (Side != 0 && !Occupied && SameSide == 1
                && !FlowchartDagrePresentation.IsDiamondShape(TargetShape)
                && Math.Abs(Cross(Target) - SourceCross) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
                && (TargetMain - Main(Source)) * Flow >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
            {
                var Direct = new List<(double X, double Y)> { Vertex, Map(Main(Source), Cross(Target)), Map(TargetMain, Cross(Target)) };
                if (!Direct.Zip(Direct.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, BoundsById.Where(Pair => !Frames.Any(Frame => Frame.Id == Pair.Key
                        && IsInsideGroup(Contract, Route.SourceId, Frame.Id) && IsInsideGroup(Contract, Route.TargetId, Frame.Id)))
                        .ToDictionary(Pair => Pair.Key, Pair => Pair.Value), Route.SourceId, Route.TargetId)))
                {
                    LayoutPoints = Direct;
                    SetRouteEndpointSides(Route, Source, Target, Direct, Horizontal);
                }
            }
            if (LayoutPoints.Count < 2) continue;

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static string ChooseElbowEntrySide(
        BoxRef Target,
        BoxRef Source,
        FlowchartDirection Direction,
        string ExitSide)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            return FacingSide(Target, Source);
        }

        if (ExitSide == "left" || ExitSide == "right")
        {
            return Source.CenterY > Target.CenterY ? "top" : "bottom";
        }

        return FacingSide(Target, Source);
    }

    private static string FacingSide(BoxRef From, BoxRef To)
    {
        double Dx = To.CenterX - From.CenterX;
        double Dy = To.CenterY - From.CenterY;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            return Dx >= 0 ? "right" : "left";
        }

        return Dy >= 0 ? "top" : "bottom";
    }

    private static bool TryBox(
        string Id,
        Dictionary<string, GridNode> ById,
        Dictionary<string, GridFrame> FrameById,
        out BoxRef Box)
    {
        if (ById.TryGetValue(Id, out GridNode? Node))
        {
            Box = BoxRef.FromNode(Node);
            return true;
        }

        if (FrameById.TryGetValue(Id, out GridFrame? Frame))
        {
            Box = BoxRef.FromFrame(Frame);
            return true;
        }

        Box = default;
        return false;
    }

}
