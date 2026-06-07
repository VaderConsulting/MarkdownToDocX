using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds merge-back routes from side branches onto the main spine column with clearance below the source.
/// </summary>
public static class FlowchartMergeBackRouteBuilder
{
    private const double MERGE_BACK_CLEARANCE_PX = 32.0;

    /// <summary>
    /// Attempts to build a merge-back route into a spine-aligned target with a spread top port.
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

        if (FlowchartTerminalCollectorPortAllocator.ShouldApplyTerminalCollectorRouting(Contract, Edge.TargetId))
        {
            return false;
        }

        if (!FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                SourceBounds,
                SourceShape,
                TargetBounds.CenterX,
                TargetBounds.CenterY,
                Edge.MarkerStart,
                out double SourceX,
                out double SourceY))
        {
            return false;
        }

        if (!FlowchartMultiIncomingTargetPortAllocator.TryResolveSpreadTopInboundAttach(
                Edge,
                Contract,
                BoundsById,
                TargetBounds,
                TargetShape,
                SourceX,
                SourceY,
                out double TargetX,
                out double TargetY))
        {
            if (!FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                    TargetBounds,
                    TargetShape,
                    SourceX,
                    SourceY,
                    Edge.MarkerEnd,
                    out TargetX,
                    out TargetY))
            {
                return false;
            }
        }

        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                SourceX,
                SourceY,
                TargetX,
                TargetY)
            && FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
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
            return true;
        }

        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double ClearanceY = SourceY - Math.Max(MERGE_BACK_CLEARANCE_PX, SourceStubLength * 1.35);
        if (Direction == FlowchartDirection.BottomToTop)
        {
            ClearanceY = SourceY + Math.Max(MERGE_BACK_CLEARANCE_PX, SourceStubLength * 1.35);
        }

        FlowchartPerpendicularRouteBuilder.AppendPerpendicularRouteWithCorridorY(
            LayoutPoints,
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape,
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            ClearanceY);
        return LayoutPoints.Count >= 2;
    }
}
