using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds vertical-flow guide polylines per Mermaid spec §15 and Naiad <c>FlowchartRenderer</c>.
/// </summary>
public static class FlowchartVerticalRouteGuideBuilder
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double LONG_BYPASS_CROSS_AXIS_THRESHOLD_PX = 96.0;

    private const double LONG_BYPASS_MAIN_AXIS_THRESHOLD_PX = 240.0;

    private const double LONG_BYPASS_GUIDE_BLEND = 0.24;

    /// <summary>
    /// F31 TB hub fan-out: bottom-left / bottom-centre / bottom-right on the lower face.
    /// </summary>
    public static (double X, double Y) ResolveVerticalHubFanOutSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        int BranchIndex,
        int BranchCount,
        FlowchartDirection Direction,
        EdgeEndpointMarker SourceEndpointMarker = EdgeEndpointMarker.None,
        int OutgoingFromSource = int.MaxValue)
    {
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
        else
        {
            SourceAttachX = ResolveVerticalHubBranchAttachX(CentreX, HalfExtent, BranchIndex, BranchCount);
        }

        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        if (Direction == FlowchartDirection.BottomToTop)
        {
            return (SourceAttachX, CentreY + HalfHeight + SourceGapPx);
        }

        return (SourceAttachX, CentreY - HalfHeight - SourceGapPx);
    }

    /// <summary>
    /// F31 TB hub fan-out target: top-centre entry for workers below the hub.
    /// </summary>
    public static (double X, double Y) ResolveVerticalHubFanOutTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction,
        EdgeEndpointMarker TargetEndpointMarker = EdgeEndpointMarker.None) => ResolveVerticalHubFanInTargetAttach(
            TargetBounds,
            BranchIndex: 0,
            BranchCount: 1,
            Direction,
            TargetEndpointMarker);

    /// <summary>
    /// F111 dense bipartite / TB hub fan-in: top-left / top-centre / top-right on the upper face.
    /// </summary>
    public static (double X, double Y) ResolveVerticalHubFanInTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        int BranchIndex,
        int BranchCount,
        FlowchartDirection Direction,
        EdgeEndpointMarker TargetEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double CentreX = TargetBounds.CenterX;
        double CentreY = TargetBounds.CenterY;
        double HalfExtent = Math.Max(HalfWidth, HalfHeight);

        double TargetAttachX = ResolveVerticalHubBranchAttachX(CentreX, HalfExtent, BranchIndex, BranchCount);

        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        if (Direction == FlowchartDirection.BottomToTop)
        {
            return (TargetAttachX, CentreY - HalfHeight - TargetGapPx);
        }

        return (TargetAttachX, CentreY + HalfHeight + TargetGapPx);
    }

    private static double ResolveVerticalHubBranchAttachX(
        double CentreX,
        double HalfExtent,
        int BranchIndex,
        int BranchCount)
    {
        if (BranchCount <= 1)
        {
            return CentreX;
        }

        if (BranchCount == 2)
        {
            if (BranchIndex == 0)
            {
                return CentreX - (HalfExtent / 2.0);
            }

            return CentreX + (HalfExtent / 2.0);
        }

        if (BranchIndex == 1)
        {
            return CentreX;
        }

        if (BranchIndex == 0)
        {
            return CentreX - (HalfExtent / 2.0);
        }

        return CentreX + (HalfExtent / 2.0);
    }

    /// <summary>
    /// F31/F32/F33 TB/BT fan: <c>(P0, (P0.X, midY), (P3.X, midY), P3)</c>.
    /// </summary>
    public static List<(double X, double Y)> BuildVerticalFanSRouteGuidedPoints(
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double MidY = (StartY + EndY) / 2.0;
        if (Math.Abs(EndX - StartX) < COLLINEAR_EPSILON_PX)
        {
            return
            [
                (StartX, StartY),
                (StartX, MidY),
                (EndX, EndY)
            ];
        }

        return
        [
            (StartX, StartY),
            (StartX, MidY),
            (EndX, MidY),
            (EndX, EndY)
        ];
    }

    /// <summary>
    /// F29/F30 TB decision fan-out: outward bow using the target leg (|P3.X − hub|).
    /// </summary>
    public static (double X, double Y) BuildVerticalDecisionBranchGuidePoint(
        double HubCentreX,
        double SourceX,
        double SourceY,
        double TargetX,
        double TargetY,
        FlowchartDecisionPortRole Role)
    {
        double MidY = (SourceY + TargetY) / 2.0;
        if (Role == FlowchartDecisionPortRole.BottomVertex
            || Role == FlowchartDecisionPortRole.TopVertex
            || Math.Abs(TargetX - SourceX) < COLLINEAR_EPSILON_PX)
        {
            return (SourceX, MidY);
        }

        if (Role == FlowchartDecisionPortRole.SideLeft && TargetX < HubCentreX - COLLINEAR_EPSILON_PX)
        {
            double HorizontalSpan = Math.Abs(TargetX - SourceX);
            double VerticalSpan = Math.Abs(SourceY - TargetY);
            if (HorizontalSpan > LONG_BYPASS_CROSS_AXIS_THRESHOLD_PX
                && VerticalSpan > LONG_BYPASS_MAIN_AXIS_THRESHOLD_PX)
            {
                return (SourceX + ((TargetX - SourceX) * LONG_BYPASS_GUIDE_BLEND), MidY);
            }

            return (TargetX, MidY);
        }

        if (Role == FlowchartDecisionPortRole.SideRight && TargetX > HubCentreX + COLLINEAR_EPSILON_PX)
        {
            double HorizontalSpan = Math.Abs(TargetX - SourceX);
            double VerticalSpan = Math.Abs(SourceY - TargetY);
            if (HorizontalSpan > LONG_BYPASS_CROSS_AXIS_THRESHOLD_PX
                && VerticalSpan > LONG_BYPASS_MAIN_AXIS_THRESHOLD_PX)
            {
                return (SourceX + ((TargetX - SourceX) * LONG_BYPASS_GUIDE_BLEND), MidY);
            }

            return (TargetX, MidY);
        }

        double Spread = Math.Abs(TargetX - HubCentreX);
        double GuideX;
        if (Math.Abs(SourceX - HubCentreX) < COLLINEAR_EPSILON_PX)
        {
            GuideX = HubCentreX;
        }
        else if (SourceX < HubCentreX)
        {
            GuideX = HubCentreX - Spread;
        }
        else
        {
            GuideX = HubCentreX + Spread;
        }

        return (GuideX, MidY);
    }

    /// <summary>
    /// Appends a five-point decision branch route using an explicit exterior guide coordinate.
    /// </summary>
    public static void AppendVerticalDecisionBranchRouteWithGuidePoint(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double GuideX,
        double GuideY)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));
        Points.Add((GuideX, GuideY));
        Points.Add((TargetStubX, TargetStubY));
        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// Appends a readable exterior corridor: stub → cross-axis lane → flow-axis descent → target stub.
    /// </summary>
    public static void AppendVerticalExteriorCorridorRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double CorridorCrossAxis)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        if (FlowchartDiamondAttachHelper.IsDiamondShape(SourceShape)
            && Math.Abs(SourceX - SourceBounds.CenterX) <= FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
        {
            SourceStubX = SourceX;
            EnsureMinimumAxisStubLength(
                SourceX,
                SourceY,
                ref SourceStubX,
                ref SourceStubY,
                SourceBounds);
        }

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));
        Points.Add((CorridorCrossAxis, SourceStubY));
        Points.Add((CorridorCrossAxis, TargetStubY));

        if (Math.Abs(TargetStubX - CorridorCrossAxis) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// Appends a smooth exterior bypass around intervening spine-column obstacles.
    /// </summary>
    public static void AppendVerticalSkipBypassRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double BypassCrossAxis,
        double? ElevatedHorizontalChordY = null,
        double? ReturnChordY = null)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        double HorizontalChordY = ElevatedHorizontalChordY ?? SourceStubY;
        double DropY = ReturnChordY ?? TargetStubY;

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(BypassCrossAxis - SourceStubX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((BypassCrossAxis, SourceStubY));
        }

        bool RouteUpward = TargetStubY < SourceStubY - COLLINEAR_EPSILON_PX;
        if (RouteUpward)
        {
            Points.Add((BypassCrossAxis, TargetStubY));
        }
        else if (Math.Abs(HorizontalChordY - SourceStubY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((BypassCrossAxis, HorizontalChordY));
            Points.Add((BypassCrossAxis, DropY));
        }
        else
        {
            Points.Add((BypassCrossAxis, DropY));
        }

        if (Math.Abs(TargetStubX - BypassCrossAxis) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, DropY));
        }

        if (Math.Abs(DropY - TargetStubY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((TargetX, TargetY));
    }

    private static void EnsureMinimumAxisStubLength(
        double BoundaryX,
        double BoundaryY,
        ref double StubX,
        ref double StubY,
        FlowchartLayoutNodeBounds Bounds)
    {
        double MinimumStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(Bounds);
        double DeltaX = StubX - BoundaryX;
        double DeltaY = StubY - BoundaryY;
        double SegmentLength = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (SegmentLength + COLLINEAR_EPSILON_PX >= MinimumStubLength)
        {
            return;
        }

        if (Math.Abs(DeltaX) <= COLLINEAR_EPSILON_PX)
        {
            double Direction = DeltaY >= 0.0 ? 1.0 : -1.0;
            if (Math.Abs(DeltaY) <= COLLINEAR_EPSILON_PX)
            {
                Direction = -1.0;
            }

            StubY = BoundaryY + (Direction * MinimumStubLength);
            return;
        }

        if (Math.Abs(DeltaY) <= COLLINEAR_EPSILON_PX)
        {
            double Direction = DeltaX >= 0.0 ? 1.0 : -1.0;
            if (Math.Abs(DeltaX) <= COLLINEAR_EPSILON_PX)
            {
                Direction = -1.0;
            }

            StubX = BoundaryX + (Direction * MinimumStubLength);
        }
    }

    /// <summary>
    /// Appends boundary → source stub → decision guide → target stub → boundary for TB/BT decision branches.
    /// </summary>
    public static void AppendVerticalDecisionBranchRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double HubCentreX,
        FlowchartDecisionPortRole Role,
        bool IsInbound = false)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        (double GuideX, double GuideY) = IsInbound
            ? BuildVerticalDecisionBranchGuidePoint(
                HubCentreX,
                TargetStubX,
                TargetStubY,
                SourceStubX,
                SourceStubY,
                Role)
            : BuildVerticalDecisionBranchGuidePoint(
                HubCentreX,
                SourceStubX,
                SourceStubY,
                TargetStubX,
                TargetStubY,
                Role);

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));
        Points.Add((GuideX, GuideY));
        Points.Add((TargetStubX, TargetStubY));
        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// Appends an S-route fan polyline with perpendicular terminal stubs (spec §15 corridor at stub X).
    /// </summary>
    public static void AppendVerticalFanSRouteWithStubs(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        double MidY = (SourceStubY + TargetStubY) / 2.0;

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(TargetStubX - SourceStubX) < COLLINEAR_EPSILON_PX)
        {
            Points.Add((SourceStubX, MidY));
        }
        else
        {
            Points.Add((SourceStubX, MidY));
            Points.Add((TargetStubX, MidY));
        }

        Points.Add((TargetStubX, TargetStubY));
        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// Appends an S-route fan polyline between boundary attach points.
    /// </summary>
    public static void AppendVerticalFanSRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        double TargetX,
        double TargetY)
    {
        List<(double X, double Y)> GuidedPoints = BuildVerticalFanSRouteGuidedPoints(
            SourceX,
            SourceY,
            TargetX,
            TargetY);
        Points.AddRange(GuidedPoints);
    }

    public static (double X, double Y) ComputeStubOuterPoint(
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
