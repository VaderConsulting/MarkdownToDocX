using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds a straight centre-spine route with shape-aware boundary attach points.
/// </summary>
public static class FlowchartCentreSpineRouteBuilder
{
    /// <summary>
    /// Attempts to build a two-point vertical spine connector between aligned nodes.
    /// </summary>
    public static bool TryBuildRoute(
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        EdgeEndpointMarker SourceEndpointMarker,
        EdgeEndpointMarker TargetEndpointMarker,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints.Clear();

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            return false;
        }

        if (!FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds))
        {
            return false;
        }

        if (!FlowchartShapeBoundaryAttachHelper.AreSpineColumnAligned(SourceBounds, TargetBounds))
        {
            return false;
        }

        double GuideTargetX = TargetBounds.CenterX;
        double GuideTargetY = TargetBounds.CenterY;
        if (!FlowchartShapeBoundaryAttachHelper.TryResolveOutboundAttach(
                SourceBounds,
                SourceShape,
                GuideTargetX,
                GuideTargetY,
                SourceEndpointMarker,
                out double SourceAttachX,
                out double SourceAttachY))
        {
            return false;
        }

        if (!FlowchartShapeBoundaryAttachHelper.TryResolveInboundAttach(
                TargetBounds,
                TargetShape,
                SourceAttachX,
                SourceAttachY,
                TargetEndpointMarker,
                out double TargetAttachX,
                out double TargetAttachY))
        {
            return false;
        }

        if (!FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                SourceAttachX,
                SourceAttachY,
                TargetAttachX,
                TargetAttachY))
        {
            return false;
        }

        LayoutPoints.Add((SourceAttachX, SourceAttachY));
        LayoutPoints.Add((TargetAttachX, TargetAttachY));
        return true;
    }
}
