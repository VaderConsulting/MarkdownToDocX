using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves source and target attach points for non-decision fan-out edges based on relative target position.
/// </summary>
public static class FlowchartFanOutPortAllocator
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Returns whether the edge is an LR labelled rectangle fan-out branch (F24).
    /// </summary>
    public static bool IsLrLabelledRectFanOutEdge(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        FlowchartDirection Direction)
    {
        if (string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
        {
            return false;
        }

        List<EdgeContract> Outgoing = FlowchartSharedPortAllocator.GetRoutableEdgesFromSource(Contract, Edge.SourceId);
        return TryIsHorizontalHubFanOut(Outgoing, BoundsById, SourceBounds, SourceShape, Direction);
    }

    /// <summary>
    /// Resolves layout attach points and guided waypoints for rectangle hub fan-out edges.
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

        if (FlowchartSharedPortAllocator.IsDecisionShape(SourceShape))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        if (FlowchartSharedPortAllocator.CountRoutableEdges(Contract, Edge.SourceId, true) < 2)
        {
            return false;
        }

        List<EdgeContract> Outgoing = FlowchartSharedPortAllocator.GetRoutableEdgesFromSource(Contract, Edge.SourceId);

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        bool UseVerticalHubFanOutPorts = TryIsVerticalHubFanOut(
            Outgoing,
            BoundsById,
            SourceBounds,
            SourceShape,
            Direction);
        bool UseHorizontalHubFanOutPorts = TryIsHorizontalHubFanOut(
            Outgoing,
            BoundsById,
            SourceBounds,
            SourceShape,
            Direction);

        if (UseVerticalHubFanOutPorts)
        {
            SortOutgoingByTargetCrossAxisAscending(Outgoing, BoundsById, HorizontalFlow: false);
        }
        else if (UseHorizontalHubFanOutPorts)
        {
            SortOutgoingByTargetCrossAxisDescending(Outgoing, BoundsById, HorizontalFlow: true);
        }

        int BranchIndex = Outgoing.FindIndex(
            Candidate => string.Equals(Candidate.TargetId, Edge.TargetId, StringComparison.Ordinal));

        if (BranchIndex < 0)
        {
            return false;
        }

        double SourceX;
        double SourceY;
        double TargetX;
        double TargetY;
        if (UseVerticalHubFanOutPorts
            && FlowchartHubSidePortHelper.TryResolveClearHubSideAttach(
                SourceBounds,
                TargetBounds,
                Direction,
                Edge.MarkerStart,
                BoundsById.Values,
                out double HubSideX,
                out double HubSideY,
                out double HubOutwardX))
        {
            (double OtherX, double OtherY, double OtherOutwardY) = FlowchartHubSidePortHelper.ResolveOtherNodeFlowFaceAttach(
                TargetBounds,
                SourceBounds,
                Edge.MarkerEnd);
            FlowchartHubSidePortHelper.AppendQuarterRoute(
                LayoutPoints,
                HubIsSource: true,
                HubSideX,
                HubSideY,
                HubOutwardX,
                SourceBounds,
                OtherX,
                OtherY,
                OtherOutwardY,
                TargetBounds);
            return true;
        }

        if (UseVerticalHubFanOutPorts)
        {
            (SourceX, SourceY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanOutSourceAttach(
                SourceBounds,
                BranchIndex,
                Outgoing.Count,
                Direction,
                Edge.MarkerStart,
                OutgoingFromSource: Outgoing.Count);

            List<EdgeContract> IncomingToTarget = FlowchartSharedPortAllocator.GetRoutableEdgesToTarget(Contract, Edge.TargetId);
            bool UseFanInTargetPorts = TryIsVerticalHubFanIn(
                IncomingToTarget,
                BoundsById,
                TargetBounds,
                TargetShape,
                Direction);
            if (UseFanInTargetPorts)
            {
                SortIncomingBySourceCrossAxisAscending(IncomingToTarget, BoundsById, HorizontalFlow: false);
                int IncomingBranchIndex = IncomingToTarget.FindIndex(
                    Candidate => string.Equals(Candidate.SourceId, Edge.SourceId, StringComparison.Ordinal));
                if (IncomingBranchIndex < 0)
                {
                    return false;
                }

                (TargetX, TargetY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanInTargetAttach(
                    TargetBounds,
                    IncomingBranchIndex,
                    IncomingToTarget.Count,
                    Direction,
                    Edge.MarkerEnd);
            }
            else
            {
                (TargetX, TargetY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanOutTargetAttach(
                    TargetBounds,
                    Direction,
                    Edge.MarkerEnd);
            }
        }
        else if (UseHorizontalHubFanOutPorts)
        {
            (SourceX, SourceY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanOutSourceAttach(
                SourceBounds,
                BranchIndex,
                Outgoing.Count,
                RightToLeft,
                Edge.MarkerStart);
            (TargetX, TargetY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanOutTargetAttach(
                TargetBounds,
                RightToLeft,
                Edge.MarkerEnd);
        }
        else
        {
            (SourceX, SourceY) = ResolveFanOutSourceAttach(
                SourceBounds,
                TargetBounds,
                Direction,
                Edge.MarkerStart);
            (TargetX, TargetY) = ResolveFanOutTargetAttach(
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
            FlowchartPerpendicularRouteBuilder.AppendPerpendicularRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
            return LayoutPoints.Count >= 2;
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
                SourceBounds.CenterX);
        }

        return LayoutPoints.Count >= 2;
    }

    private static bool TryIsHorizontalHubFanOut(
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

        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                return false;
            }

            if (RightToLeft)
            {
                if (TargetBounds.CenterX >= SourceBounds.CenterX - COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (TargetBounds.CenterX <= SourceBounds.CenterX + COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

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

    private static void SortOutgoingByTargetCrossAxisDescending(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow) => Outgoing.Sort((Left, Right) =>
                                     {
                                         double LeftCross = GetCrossAxis(Left.TargetId, BoundsById, HorizontalFlow);
                                         double RightCross = GetCrossAxis(Right.TargetId, BoundsById, HorizontalFlow);
                                         int CrossCompare = RightCross.CompareTo(LeftCross);
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

    private static void SortIncomingBySourceCrossAxisAscending(
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

    private static (double X, double Y) ResolveFanOutSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker SourceEndpointMarker)
    {
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        double HalfWidth = SourceBounds.Width / 2.0;
        double HalfHeight = SourceBounds.Height / 2.0;
        double CentreX = SourceBounds.CenterX;
        double CentreY = SourceBounds.CenterY;

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

    private static (double X, double Y) ResolveFanOutTargetAttach(
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
