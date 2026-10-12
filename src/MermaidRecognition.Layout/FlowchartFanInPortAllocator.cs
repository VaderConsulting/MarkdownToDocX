using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves source and target attach points for non-decision fan-in edges based on relative source position.
/// </summary>
public static class FlowchartFanInPortAllocator
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// How far the loop-back runs straight out of the source's downstream face before it turns.
    /// </summary>
    private const double LOOP_BACK_DROP_PX = 36.0;

    /// <summary>
    /// Gap between the loop-back's return column and the outermost node it passes.
    /// </summary>
    private const double LOOP_BACK_CORRIDOR_CLEARANCE_PX = 14.0;

    /// <summary>
    /// Routes a top-to-bottom or bottom-to-top loop-back edge into the side of its fan-in target (F59 Wait back to Queue).
    /// The source must sit downstream of the target and wholly beyond the target's left or right edge.
    /// The edge leaves the middle of the source's downstream face, turns outwards, climbs in a column clear of every node it passes,
    /// and turns in to join the middle of the target's facing side at a right angle.
    /// The guide points form an axis-aligned loop; the caller renders it with rounded corners (<see cref="FlowchartEdgeRouteKind.RetryLoop"/>).
    /// </summary>
    /// <returns>True when the loop-back route applies and <paramref name="LayoutPoints"/> holds its guide points.</returns>
    public static bool TryResolveLoopBackRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? TargetShape,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];
        if (Direction != FlowchartDirection.TopToBottom && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(TargetShape))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds) || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (FlowchartTerminalCollectorPortAllocator.ShouldApplyTerminalCollectorRouting(Contract, Edge.TargetId) || FlowchartSharedPortAllocator.CountRoutableEdges(Contract, Edge.TargetId, false) < 2)
        {
            return false;
        }

        double FlowSign = Direction == FlowchartDirection.BottomToTop ? 1.0 : -1.0;
        double SourceUpstreamFace = SourceBounds.CenterY - (FlowSign * SourceBounds.Height / 2.0);
        double TargetDownstreamFace = TargetBounds.CenterY + (FlowSign * TargetBounds.Height / 2.0);
        if ((SourceUpstreamFace - TargetDownstreamFace) * FlowSign <= COLLINEAR_EPSILON_PX)
        {
            return false;
        }

        if (!FlowchartHubSidePortHelper.TryResolveHubSideAttach(TargetBounds, SourceBounds, Direction, Edge.MarkerEnd, out double HubX, out double HubY, out double HubOutwardX))
        {
            return false;
        }

        double SourceGap = FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
        double StartX = SourceBounds.CenterX;
        double StartY = SourceBounds.CenterY + (FlowSign * ((SourceBounds.Height / 2.0) + SourceGap));
        double TurnY = StartY + (FlowSign * LOOP_BACK_DROP_PX);
        double SpanMinY = Math.Min(TurnY, HubY);
        double SpanMaxY = Math.Max(TurnY, HubY);

        double CorridorX = StartX + (HubOutwardX * ((SourceBounds.Width / 2.0) + LOOP_BACK_CORRIDOR_CLEARANCE_PX));
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Top < SpanMinY || Bottom > SpanMaxY)
            {
                continue;
            }

            double OuterEdge = Bounds.CenterX + (HubOutwardX * Bounds.Width / 2.0);
            double Candidate = OuterEdge + (HubOutwardX * LOOP_BACK_CORRIDOR_CLEARANCE_PX);
            if ((Candidate - CorridorX) * HubOutwardX > 0.0)
            {
                CorridorX = Candidate;
            }
        }

        double ContentLeft = double.PositiveInfinity;
        double ContentRight = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double NodeLeft = Bounds.CenterX - (Bounds.Width / 2.0);
            double NodeRight = Bounds.CenterX + (Bounds.Width / 2.0);
            if (NodeLeft < ContentLeft)
            {
                ContentLeft = NodeLeft;
            }

            if (NodeRight > ContentRight)
            {
                ContentRight = NodeRight;
            }
        }

        double InsideLeft = ContentLeft + FlowchartSharedRouteQualityHelper.DIAGRAM_MARGIN_PX;
        double InsideRight = ContentRight - FlowchartSharedRouteQualityHelper.DIAGRAM_MARGIN_PX;
        if (CorridorX < InsideLeft || CorridorX > InsideRight)
        {
            return false;
        }

        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            if (ReferenceEquals(Bounds, SourceBounds))
            {
                continue;
            }

            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            bool BlocksDrop = TurnY > Bottom && TurnY < Top && Math.Max(StartX, CorridorX) > Left && Math.Min(StartX, CorridorX) < Right;
            if (BlocksDrop)
            {
                return false;
            }
        }

        LayoutPoints.Add((StartX, StartY));
        LayoutPoints.Add((StartX, TurnY));
        LayoutPoints.Add((CorridorX, TurnY));
        LayoutPoints.Add((CorridorX, HubY));
        LayoutPoints.Add((HubX, HubY));
        return true;
    }

    /// <summary>
    /// Resolves layout attach points and guided waypoints for rectangle collector fan-in edges.
    /// </summary>
    public static bool TryResolveEdgeRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];

        if (FlowchartSharedPortAllocator.IsDecisionShape(TargetShape))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (FlowchartTerminalCollectorPortAllocator.ShouldApplyTerminalCollectorRouting(Contract, Edge.TargetId))
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.CountRoutableEdges(Contract, Edge.TargetId, false) < 2)
        {
            return false;
        }

        List<EdgeContract> Incoming = FlowchartSharedPortAllocator.GetRoutableEdgesToTarget(Contract, Edge.TargetId);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            SortIncomingBySourceCrossAxisDescending(Incoming, BoundsById, HorizontalFlow);
        }
        else
        {
            SortIncomingBySourceCrossAxis(Incoming, BoundsById, HorizontalFlow);
        }
        int BranchIndex = Incoming.FindIndex(
            Candidate => string.Equals(Candidate.SourceId, Edge.SourceId, StringComparison.Ordinal));

        if (BranchIndex < 0)
        {
            return false;
        }

        bool UseParallelBranchColumnFanIn = FlowchartHorizontalRouteGuideBuilder.IsParallelBranchColumnFanIn(
            Incoming,
            BoundsById,
            TargetBounds,
            Direction);
        bool UseVerticalHubFanInPorts = TryIsVerticalHubFanIn(
            Incoming,
            BoundsById,
            TargetBounds,
            TargetShape,
            Direction);
        bool UseHorizontalHubFanInPorts = TryIsHorizontalHubFanIn(
            Incoming,
            BoundsById,
            TargetBounds,
            TargetShape,
            Direction);

        double SourceX;
        double SourceY;
        double TargetX;
        double TargetY;
        if (UseParallelBranchColumnFanIn)
        {
            bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
            List<FlowchartLayoutNodeBounds> SourceBoundsList = [];
            foreach (EdgeContract IncomingEdge in Incoming)
            {
                if (BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds ColumnSourceBounds))
                {
                    SourceBoundsList.Add(ColumnSourceBounds);
                }
            }

            double CorridorX = FlowchartHorizontalRouteGuideBuilder.ResolveParallelBranchColumnCorridorX(
                SourceBoundsList,
                Direction);
            (SourceX, SourceY) = FlowchartHorizontalRouteGuideBuilder.ResolveParallelBranchColumnSourceAttach(
                SourceBounds,
                RightToLeft,
                Edge.MarkerStart);
            (TargetX, TargetY) = FlowchartHorizontalRouteGuideBuilder.ResolveParallelBranchColumnTargetAttach(
                SourceBounds,
                TargetBounds,
                RightToLeft,
                Edge.MarkerEnd);
            FlowchartHorizontalRouteGuideBuilder.AppendParallelBranchColumnFanInRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                CorridorX);
            return LayoutPoints.Count >= 2;
        }

        if (UseVerticalHubFanInPorts
            && FlowchartHubSidePortHelper.TryResolveClearHubSideAttach(
                TargetBounds,
                SourceBounds,
                Direction,
                Edge.MarkerEnd,
                BoundsById.Values,
                out double HubSideX,
                out double HubSideY,
                out double HubOutwardX))
        {
            (double OtherX, double OtherY, double OtherOutwardY) = FlowchartHubSidePortHelper.ResolveOtherNodeFlowFaceAttach(
                SourceBounds,
                TargetBounds,
                Edge.MarkerStart);
            FlowchartHubSidePortHelper.AppendQuarterRoute(
                LayoutPoints,
                HubIsSource: false,
                HubSideX,
                HubSideY,
                HubOutwardX,
                TargetBounds,
                OtherX,
                OtherY,
                OtherOutwardY,
                SourceBounds);
            return true;
        }

        if (UseVerticalHubFanInPorts)
        {
            List<EdgeContract> OutgoingFromSource = FlowchartSharedPortAllocator.GetRoutableEdgesFromSource(Contract, Edge.SourceId);
            bool UseFanOutSourcePorts = TryIsVerticalHubFanOut(
                OutgoingFromSource,
                BoundsById,
                SourceBounds,
                SourceShape,
                Direction);
            if (UseFanOutSourcePorts)
            {
                SortOutgoingByTargetCrossAxisAscending(OutgoingFromSource, BoundsById, HorizontalFlow: false);
                int OutgoingBranchIndex = OutgoingFromSource.FindIndex(
                    Candidate => string.Equals(Candidate.TargetId, Edge.TargetId, StringComparison.Ordinal));
                if (OutgoingBranchIndex < 0)
                {
                    return false;
                }

                (SourceX, SourceY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanOutSourceAttach(
                    SourceBounds,
                    OutgoingBranchIndex,
                    OutgoingFromSource.Count,
                    Direction,
                    Edge.MarkerStart,
                    OutgoingFromSource: OutgoingFromSource.Count);
            }
            else
            {
                int OutgoingFromSourceCount = FlowchartSharedPortAllocator.CountRoutableEdges(Contract, Edge.SourceId, true);
                (SourceX, SourceY) = ResolveVerticalHubFanInSourceAttach(
                    SourceBounds,
                    BranchIndex,
                    Incoming.Count,
                    OutgoingFromSourceCount,
                    Direction,
                    Edge.MarkerStart);
            }

            (TargetX, TargetY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanInTargetAttach(
                TargetBounds,
                BranchIndex,
                Incoming.Count,
                Direction,
                Edge.MarkerEnd);
        }
        else if (UseHorizontalHubFanInPorts)
        {
            bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
            (SourceX, SourceY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanInSourceAttach(
                SourceBounds,
                RightToLeft,
                Edge.MarkerStart);
            (TargetX, TargetY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanInTargetAttach(
                TargetBounds,
                BranchIndex,
                Incoming.Count,
                RightToLeft,
                Edge.MarkerEnd);
        }
        else
        {
            (SourceX, SourceY) = ResolveFanInSourceAttach(
                SourceBounds,
                TargetBounds,
                Direction,
                Edge.MarkerStart);
            (TargetX, TargetY) = ResolveFanInTargetAttach(
                SourceBounds,
                TargetBounds,
                SourceX,
                SourceY,
                Direction,
                Edge.MarkerEnd);
        }

        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                SourceX,
                SourceY,
                TargetX,
                TargetY))
        {
            if (FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
                    LayoutPoints,
                    SourceX,
                    SourceY,
                    SourceBounds,
                    SourceShape,
                    TargetX,
                    TargetY,
                    TargetBounds,
                    TargetShape,
                    Direction,
                    BoundsById,
                    Edge.SourceId,
                    Edge.TargetId))
            {
                return LayoutPoints.Count >= 2;
            }
        }

        if (Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop)
        {
            FlowchartVerticalRouteGuideBuilder.AppendVerticalFanSRouteWithStubs(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
        }
        else if (Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft)
        {
            FlowchartHorizontalRouteGuideBuilder.AppendHorizontalFanSRouteWithStubs(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
        }
        else
        {
            FlowchartPerpendicularRouteBuilder.AppendCurvedPerpendicularRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                TargetBounds.CenterX);
        }

        return LayoutPoints.Count >= 2;
    }

    private static bool TryIsVerticalHubFanIn(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction)
    {
        if (Incoming.Count < 2 || Incoming.Count > 3)
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(TargetShape))
        {
            return false;
        }

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        foreach (EdgeContract IncomingEdge in Incoming)
        {
            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                return false;
            }

            if (Direction == FlowchartDirection.BottomToTop)
            {
                if (SourceBounds.CenterY >= TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (SourceBounds.CenterY <= TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryIsHorizontalHubFanIn(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction)
    {
        if (Incoming.Count < 2 || Incoming.Count > 3)
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(TargetShape))
        {
            return false;
        }

        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        foreach (EdgeContract IncomingEdge in Incoming)
        {
            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                return false;
            }

            if (RightToLeft)
            {
                if (SourceBounds.CenterX <= TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (SourceBounds.CenterX >= TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static void SortIncomingBySourceCrossAxisDescending(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow) => Incoming.Sort((Left, Right) =>
                                     {
                                         double LeftCross = GetCrossAxis(Left.SourceId, BoundsById, HorizontalFlow);
                                         double RightCross = GetCrossAxis(Right.SourceId, BoundsById, HorizontalFlow);
                                         int CrossCompare = RightCross.CompareTo(LeftCross);
                                         if (CrossCompare != 0)
                                         {
                                             return CrossCompare;
                                         }

                                         return Left.CreationIndex.CompareTo(Right.CreationIndex);
                                     });

    private static (double X, double Y) ResolveVerticalHubFanInSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        int BranchIndex,
        int BranchCount,
        int OutgoingFromSource,
        FlowchartDirection Direction,
        EdgeEndpointMarker SourceEndpointMarker)
    {
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        double HalfWidth = SourceBounds.Width / 2.0;
        double HalfHeight = SourceBounds.Height / 2.0;
        double CentreX = SourceBounds.CenterX;
        double CentreY = SourceBounds.CenterY;
        double HalfExtent = Math.Max(HalfWidth, HalfHeight);

        double SourceAttachX;
        if (OutgoingFromSource <= 1)
        {
            SourceAttachX = CentreX;
        }
        else if (BranchCount >= 3 && BranchIndex == 1)
        {
            SourceAttachX = CentreX;
        }
        else if (BranchIndex == 0)
        {
            SourceAttachX = CentreX - (HalfExtent / 2.0);
        }
        else
        {
            SourceAttachX = CentreX + (HalfExtent / 2.0);
        }

        if (Direction == FlowchartDirection.BottomToTop)
        {
            return (SourceAttachX, CentreY + HalfHeight + SourceGapPx);
        }

        return (SourceAttachX, CentreY - HalfHeight - SourceGapPx);
    }

    private static void SortOutgoingByTargetCrossAxisAscending(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow) => Outgoing.Sort((Left, Right) =>
                                     {
                                         double LeftCross = GetCrossAxis(Left.TargetId, BoundsById, HorizontalFlow);
                                         double RightCross = GetCrossAxis(Right.TargetId, BoundsById, HorizontalFlow);
                                         int CrossCompare = LeftCross.CompareTo(RightCross);
                                         if (CrossCompare != 0)
                                         {
                                             return CrossCompare;
                                         }

                                         return Left.CreationIndex.CompareTo(Right.CreationIndex);
                                     });

    private static bool TryIsVerticalHubFanOut(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartDirection Direction)
    {
        if (Outgoing.Count < 2 || Outgoing.Count > 3)
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.IsDecisionShape(SourceShape))
        {
            return false;
        }

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                return false;
            }

            if (Direction == FlowchartDirection.TopToBottom)
            {
                if (TargetBounds.CenterY >= SourceBounds.CenterY - COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (TargetBounds.CenterY <= SourceBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    private static void SortIncomingBySourceCrossAxis(
        List<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow) => Incoming.Sort((Left, Right) =>
                                     {
                                         double LeftCross = GetCrossAxis(Left.SourceId, BoundsById, HorizontalFlow);
                                         double RightCross = GetCrossAxis(Right.SourceId, BoundsById, HorizontalFlow);
                                         int CrossCompare = LeftCross.CompareTo(RightCross);
                                         if (CrossCompare != 0)
                                         {
                                             return CrossCompare;
                                         }

                                         return Left.CreationIndex.CompareTo(Right.CreationIndex);
                                     });

    private static double GetCrossAxis(
        string NodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow)
    {
        if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
        {
            if (HorizontalFlow)
            {
                return Bounds.CenterY;
            }

            return Bounds.CenterX;
        }

        return 0.0;
    }

    /// <summary>
    /// Resolves the source boundary attach for an edge entering a shared target, based on relative geometry.
    /// </summary>
    public static (double X, double Y) ResolveFanInSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker SourceEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = SourceBounds.Width / 2.0;
        double HalfHeight = SourceBounds.Height / 2.0;
        double CentreX = SourceBounds.CenterX;
        double CentreY = SourceBounds.CenterY;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);

        if (Direction == FlowchartDirection.LeftToRight)
        {
            return ResolveHorizontalFlowSourceAttach(
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                TargetBounds,
                OutflowSign: 1,
                SourceGapPx);
        }

        if (Direction == FlowchartDirection.RightToLeft)
        {
            return ResolveHorizontalFlowSourceAttach(
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                TargetBounds,
                OutflowSign: -1,
                SourceGapPx);
        }

        if (Direction == FlowchartDirection.BottomToTop)
        {
            return ResolveVerticalFlowSourceAttach(
                CentreX,
                CentreY,
                HalfWidth,
                HalfHeight,
                TargetBounds,
                OutflowSign: 1,
                SourceGapPx);
        }

        return ResolveVerticalFlowSourceAttach(
            CentreX,
            CentreY,
            HalfWidth,
            HalfHeight,
            TargetBounds,
            OutflowSign: -1,
            SourceGapPx);
    }

    private static (double X, double Y) ResolveVerticalFlowSourceAttach(
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        FlowchartLayoutNodeBounds TargetBounds,
        int OutflowSign,
        double SourceGapPx)
    {
        double CrossDelta = TargetBounds.CenterX - CentreX;

        if (Math.Abs(CrossDelta) <= COLLINEAR_EPSILON_PX)
        {
            if (OutflowSign < 0)
            {
                return (CentreX, CentreY - HalfHeight - SourceGapPx);
            }

            return (CentreX, CentreY + HalfHeight + SourceGapPx);
        }

        if (CrossDelta < 0.0)
        {
            return (CentreX - HalfWidth - SourceGapPx, CentreY);
        }

        return (CentreX + HalfWidth + SourceGapPx, CentreY);
    }

    private static (double X, double Y) ResolveHorizontalFlowSourceAttach(
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        FlowchartLayoutNodeBounds TargetBounds,
        int OutflowSign,
        double SourceGapPx)
    {
        double CrossDelta = TargetBounds.CenterY - CentreY;

        if (Math.Abs(CrossDelta) <= COLLINEAR_EPSILON_PX)
        {
            if (OutflowSign > 0)
            {
                return (CentreX + HalfWidth + SourceGapPx, CentreY);
            }

            return (CentreX - HalfWidth - SourceGapPx, CentreY);
        }

        if (CrossDelta < 0.0)
        {
            return (CentreX, CentreY - HalfHeight - SourceGapPx);
        }

        return (CentreX, CentreY + HalfHeight + SourceGapPx);
    }

    private static (double X, double Y) ResolveFanInTargetAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        double SourceX,
        double SourceY,
        FlowchartDirection Direction,
        EdgeEndpointMarker TargetEndpointMarker)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);

        if (Direction == FlowchartDirection.LeftToRight)
        {
            if (SourceX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
            }

            if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
            }

            return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
        }

        if (Direction == FlowchartDirection.RightToLeft)
        {
            if (SourceX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
            }

            if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
            }

            return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
        }

        if (Direction == FlowchartDirection.BottomToTop)
        {
            if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
            }

            if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
            }

            if (SourceX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
            }

            if (SourceX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
            {
                return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
            }

            return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
        }

        if (SourceY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
        }

        if (SourceY < TargetBounds.CenterY - COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX, TargetBounds.CenterY - HalfHeight - TargetGapPx);
        }

        if (SourceX < TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX - HalfWidth - TargetGapPx, TargetBounds.CenterY);
        }

        if (SourceX > TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
        {
            return (TargetBounds.CenterX + HalfWidth + TargetGapPx, TargetBounds.CenterY);
        }

        return (TargetBounds.CenterX, TargetBounds.CenterY + HalfHeight + TargetGapPx);
    }

}
