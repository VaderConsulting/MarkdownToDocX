using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds exterior exception routes in a far-margin corridor lane outside the spine column.
/// </summary>
public static class FlowchartLongExceptionRouteBuilder
{
    private const double EXTERIOR_MARGIN_PADDING_PX = 20.0;

    /// <summary>
    /// Attempts to build a corridor route around the spine column to a far-margin target.
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
                out FlowchartDecisionPortRole Role,
                out double HubCentreX))
        {
            return false;
        }

        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);
        double CorridorCrossAxis = ResolveExteriorCorridorCrossAxis(
            SourceX,
            TargetStubX,
            HubCentreX,
            Role,
            SpineContext,
            BoundsById);

        FlowchartVerticalRouteGuideBuilder.AppendVerticalExteriorCorridorRoute(
            LayoutPoints,
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape,
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape,
            CorridorCrossAxis);
        return LayoutPoints.Count >= 2;
    }

    private static double ResolveExteriorCorridorCrossAxis(
        double SourceAttachX,
        double TargetStubX,
        double HubCentreX,
        FlowchartDecisionPortRole Role,
        FlowchartSpineLayoutContext? SpineContext,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (SpineContext is not null
            && FlowchartSpineCorridorHelper.TryBuildCorridor(BoundsById, SpineContext, out FlowchartSpineCorridorBounds Corridor))
        {
            if (TargetStubX < Corridor.Left - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                return Math.Min(TargetStubX, Corridor.Left - EXTERIOR_MARGIN_PADDING_PX);
            }

            if (TargetStubX > Corridor.Right + FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
            {
                return Math.Max(TargetStubX, Corridor.Right + EXTERIOR_MARGIN_PADDING_PX);
            }
        }

        if (Role == FlowchartDecisionPortRole.SideLeft
            || TargetStubX < HubCentreX - FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
        {
            return Math.Min(TargetStubX, SourceAttachX) - EXTERIOR_MARGIN_PADDING_PX;
        }

        if (Role == FlowchartDecisionPortRole.SideRight
            || TargetStubX > HubCentreX + FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
        {
            return Math.Max(TargetStubX, SourceAttachX) + EXTERIOR_MARGIN_PADDING_PX;
        }

        return TargetStubX;
    }

    private static (double X, double Y) ComputeStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape)
    {
        double StubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(Bounds);

        if (FlowchartDiamondAttachHelper.IsDiamondShape(Shape)
            && FlowchartDiamondAttachHelper.TryResolveOutwardNormal(
                Bounds,
                BoundaryX,
                BoundaryY,
                out double NormalX,
                out double NormalY))
        {
            return (BoundaryX + (NormalX * StubLength), BoundaryY + (NormalY * StubLength));
        }

        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            BoundaryX,
            BoundaryY);

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                return (BoundaryX, BoundaryY + StubLength);

            case FlowchartEdgeAttachFace.Bottom:
                return (BoundaryX, BoundaryY - StubLength);

            case FlowchartEdgeAttachFace.Left:
                return (BoundaryX - StubLength, BoundaryY);

            case FlowchartEdgeAttachFace.Right:
                return (BoundaryX + StubLength, BoundaryY);

            default:
                return (BoundaryX, BoundaryY - StubLength);
        }
    }
}
