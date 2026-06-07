using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds routes that bypass intervening spine-column nodes such as archive cylinders.
/// </summary>
public static class FlowchartSkipBypassRouteBuilder
{
    private const double BYPASS_PADDING_PX = 24.0;

    /// <summary>
    /// Attempts to route around intervening spine-column obstacles between aligned nodes.
    /// </summary>
    public static bool TryBuildRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext? SpineContext,
        List<(double X, double Y)> LayoutPoints)
    {
        if (SpineContext is null)
        {
            return false;
        }

        if (Direction != FlowchartDirection.TopToBottom
            && Direction != FlowchartDirection.BottomToTop)
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            return false;
        }

        FlowchartDecisionPortRole PortRole = FlowchartDecisionPortRole.BottomVertex;
        if (!FlowchartDecisionPortAllocator.TryResolveDecisionFanOutAttachPoints(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                TargetShape,
                Direction,
                out double SourceX,
                out double SourceY,
                out double TargetX,
                out double TargetY,
                out PortRole,
                out _))
        {
            if (!FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                    SourceBounds,
                    SourceShape,
                    TargetBounds.CenterX,
                    TargetBounds.CenterY,
                    Edge.MarkerStart,
                    out SourceX,
                    out SourceY)
                || !FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                    TargetBounds,
                    TargetShape,
                    SourceBounds.CenterX,
                    SourceBounds.CenterY,
                    Edge.MarkerEnd,
                    out TargetX,
                    out TargetY))
            {
                return false;
            }
        }

        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        if (FlowchartMultiIncomingTargetPortAllocator.TryResolveSpreadTopInboundAttach(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                TargetBounds,
                TargetShape,
                Direction,
                SpineContext,
                SourceX,
                SourceY,
                out double SpreadTargetX,
                out double SpreadTargetY))
        {
            TargetX = SpreadTargetX;
            TargetY = SpreadTargetY;
        }

        double BypassCrossAxis = ResolveBypassCrossAxis(
            BoundsById,
            SpineContext,
            SourceBounds,
            TargetBounds,
            SourceX,
            PortRole);

        (double SourceStubX, double SourceStubY) = FlowchartVerticalRouteGuideBuilder.ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = FlowchartVerticalRouteGuideBuilder.ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);
        double HorizontalChordY = SourceStubY;
        double? ReturnChordY = null;
        bool RouteUpward = TargetBounds.CenterY < SourceBounds.CenterY - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX;
        if (!RouteUpward)
        {
            double SpanMinX = Math.Min(SourceStubX, BypassCrossAxis);
            double SpanMaxX = Math.Max(SourceStubX, BypassCrossAxis);
            double MaxObstacleTop = FlowchartRouteObstacleHelper.ResolveMaximumObstacleTopOnHorizontalSpan(
                SpanMinX,
                SpanMaxX,
                Edge.SourceId,
                Edge.TargetId,
                BoundsById);
            double MinimumChordY = MaxObstacleTop + BYPASS_PADDING_PX + 16.0;
            if (MinimumChordY > HorizontalChordY)
            {
                HorizontalChordY = MinimumChordY;
            }

            double ReturnSpanMinX = Math.Min(BypassCrossAxis, TargetStubX);
            double ReturnSpanMaxX = Math.Max(BypassCrossAxis, TargetStubX);
            double MaxObstacleBottom = FlowchartRouteObstacleHelper.ResolveMaximumObstacleBottomOnHorizontalSpan(
                ReturnSpanMinX,
                ReturnSpanMaxX,
                Edge.SourceId,
                Edge.TargetId,
                BoundsById);
            if (!double.IsNegativeInfinity(MaxObstacleBottom))
            {
                double MinimumReturnY = MaxObstacleBottom + BYPASS_PADDING_PX;
                if (MinimumReturnY > TargetStubY)
                {
                    ReturnChordY = MinimumReturnY;
                }
            }
        }

        FlowchartVerticalRouteGuideBuilder.AppendVerticalSkipBypassRoute(
            LayoutPoints,
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape,
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            BypassCrossAxis,
            HorizontalChordY,
            ReturnChordY);
        return LayoutPoints.Count >= 2;
    }

    private static double ResolveBypassCrossAxis(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartSpineLayoutContext SpineContext,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        double SourceAttachX,
        FlowchartDecisionPortRole PortRole)
    {
        double MaximumRight = SpineContext.SpineCrossAxis;
        double MinimumLeft = SpineContext.SpineCrossAxis;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceBounds.NodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetBounds.NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!FlowchartSpineGraphHelper.IsOnSpineColumn(CandidateBounds, SpineContext))
            {
                continue;
            }

            if (CandidateBounds.CenterY >= SourceBounds.CenterY - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            if (CandidateBounds.CenterY <= TargetBounds.CenterY + FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                continue;
            }

            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            MaximumRight = Math.Max(MaximumRight, NodeRight);
            MinimumLeft = Math.Min(MinimumLeft, NodeLeft);
        }

        bool RouteRight = PortRole == FlowchartDecisionPortRole.SideRight
            || SourceAttachX >= SpineContext.SpineCrossAxis - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX;
        if (RouteRight)
        {
            return MaximumRight + BYPASS_PADDING_PX;
        }

        return MinimumLeft - BYPASS_PADDING_PX;
    }
}
