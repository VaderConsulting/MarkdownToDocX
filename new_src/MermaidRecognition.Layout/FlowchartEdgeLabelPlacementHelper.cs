using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Positions edge labels offset from connector paths so text does not sit on the line.
/// </summary>
public static class FlowchartEdgeLabelPlacementHelper
{
    public const double DEFAULT_OFFSET_PX = 14.0;

    public const double EDGE_LABEL_LINE_HEIGHT_FACTOR = 1.5;

    /// <summary>
    /// Returns rendered edge-label height for SVG text-anchor middle placement.
    /// </summary>
    public static double ComputeRenderedEdgeLabelHeight(
        FlowchartLabelDimensions LabelDimensions,
        double FontSizePx)
    {
        string[] Lines = LabelDimensions.DisplayText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        if (Lines.Length == 1)
        {
            return FontSizePx;
        }

        double LineHeightPx = FontSizePx * EDGE_LABEL_LINE_HEIGHT_FACTOR;
        return Lines.Length * LineHeightPx;
    }

    /// <summary>
    /// Returns a label anchor at half the polyline arc-length along the connector (Mermaid parity).
    /// </summary>
    public static (double X, double Y) ComputePolylineHalfDistancePosition(
        IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count == 0)
        {
            return (0.0, 0.0);
        }

        if (GuidePoints.Count == 1)
        {
            return GuidePoints[0];
        }

        (double AnchorX, double AnchorY, double _, double _) =
            SampleGuidePolylineAtFraction(GuidePoints, 0.5);
        return (AnchorX, AnchorY);
    }

    private const double AXIS_ALIGNED_SEGMENT_EPSILON_PX = 0.5;

    private const double MIN_LABELLED_SEGMENT_LENGTH_PX = 12.0;

    /// <summary>
    /// Positions an LR labelled rectangle fan-out label on the primary horizontal connector segment.
    /// </summary>
    public static (double X, double Y) ComputeLrLabelledRectFanOutEdgeLabelPosition(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        double LabelX = (SourceBounds.CenterX + TargetBounds.CenterX) / 2.0;
        double LabelY = TargetBounds.CenterY;
        return (LabelX, LabelY);
    }

    /// <summary>
    /// Positions an LR labelled rectangle fan-out label on the longest horizontal guide segment when present.
    /// </summary>
    public static (double X, double Y) ComputeLrLabelledRectFanOutEdgeLabelPosition(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        if (TryGetLongestAxisAlignedSegmentMidpoint(
                LayoutPoints,
                HorizontalSegment: true,
                out double MidX,
                out double MidY))
        {
            return (MidX, MidY);
        }

        return ComputeLrLabelledRectFanOutEdgeLabelPosition(SourceBounds, TargetBounds);
    }

    /// <summary>
    /// Returns the midpoint of the longest axis-aligned segment on a guide polyline.
    /// </summary>
    public static bool TryGetLongestAxisAlignedSegmentMidpoint(
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        bool HorizontalSegment,
        out double MidX,
        out double MidY)
    {
        MidX = 0.0;
        MidY = 0.0;
        if (LayoutPoints.Count < 2)
        {
            return false;
        }

        double BestLength = 0.0;
        for (int Index = 0; Index < LayoutPoints.Count - 1; Index++)
        {
            (double StartX, double StartY) = LayoutPoints[Index];
            (double EndX, double EndY) = LayoutPoints[Index + 1];
            if (HorizontalSegment)
            {
                if (Math.Abs(StartY - EndY) > AXIS_ALIGNED_SEGMENT_EPSILON_PX)
                {
                    continue;
                }

                double SegmentLength = Math.Abs(EndX - StartX);
                if (SegmentLength < MIN_LABELLED_SEGMENT_LENGTH_PX || SegmentLength <= BestLength)
                {
                    continue;
                }

                BestLength = SegmentLength;
                MidX = (StartX + EndX) / 2.0;
                MidY = (StartY + EndY) / 2.0;
            }
            else
            {
                if (Math.Abs(StartX - EndX) > AXIS_ALIGNED_SEGMENT_EPSILON_PX)
                {
                    continue;
                }

                double SegmentLength = Math.Abs(EndY - StartY);
                if (SegmentLength < MIN_LABELLED_SEGMENT_LENGTH_PX || SegmentLength <= BestLength)
                {
                    continue;
                }

                BestLength = SegmentLength;
                MidX = (StartX + EndX) / 2.0;
                MidY = (StartY + EndY) / 2.0;
            }
        }

        return BestLength > 0.0;
    }

    /// <summary>
    /// Resolves edge-label placement for any routed connector.
    /// </summary>
    public static (double X, double Y) ResolveEdgeLabelPosition(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartDirection Direction,
        string? SourceShape,
        string? EdgeLabel) => ResolveEdgeLabelPosition(
            SourceBounds,
            TargetBounds,
            LayoutPoints,
            Direction,
            SourceShape,
            EdgeLabel,
            null,
            null,
            null,
            null);

    /// <summary>
    /// Resolves edge-label placement for any routed connector with optional diagram context.
    /// </summary>
    public static (double X, double Y) ResolveEdgeLabelPosition(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartDirection Direction,
        string? SourceShape,
        string? EdgeLabel,
        EdgeContract? Edge,
        DiagramContract? Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>? BoundsById,
        string? RouteClass)
    {
        if (Edge is not null
            && string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase)
            && LayoutPoints.Count >= 4
            && TryGetHighestHorizontalCorridorLabelPosition(LayoutPoints, out (double CorridorX, double CorridorY) CorridorLabel))
        {
            return CorridorLabel;
        }

        if (Edge is not null
            && Contract is not null
            && BoundsById is not null
            && FlowchartFanOutPortAllocator.IsLrLabelledRectFanOutEdge(
                Edge,
                Contract,
                BoundsById,
                SourceShape,
                Direction))
        {
            return ComputeLrLabelledRectFanOutEdgeLabelPosition(
                LayoutPoints,
                SourceBounds,
                TargetBounds);
        }

        double LabelX;
        double LabelY;
        if (IsDecisionShape(SourceShape)
            && !string.IsNullOrWhiteSpace(EdgeLabel)
            && LayoutPoints.Count >= 4
            && string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal)
            && HasExteriorCorridorDetour(LayoutPoints, Direction)
            && TryGetExteriorCorridorLabelPosition(LayoutPoints, Direction, out (double CorridorX, double CorridorY) ExteriorLabel))
        {
            return ExteriorLabel;
        }

        if (IsDecisionShape(SourceShape)
            && !string.IsNullOrWhiteSpace(EdgeLabel)
            && LayoutPoints.Count >= 2
            && string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
        {
            (LabelX, LabelY) = ComputeDecisionBranchLabelPosition(
                LayoutPoints,
                SourceBounds,
                EdgeLabel,
                Direction);
        }
        else if (LayoutPoints.Count >= 2
            && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                LayoutPoints[0].X,
                LayoutPoints[0].Y,
                LayoutPoints[LayoutPoints.Count - 1].X,
                LayoutPoints[LayoutPoints.Count - 1].Y))
        {
            (LabelX, LabelY) = ComputeSpineMidspanEdgeLabelPosition(
                SourceBounds,
                TargetBounds,
                LayoutPoints,
                Direction);
        }
        else if (LayoutPoints.Count >= 2)
        {
            (LabelX, LabelY) = ComputeOffsetLabelPosition(LayoutPoints);
        }
        else
        {
            (LabelX, LabelY) = ComputeSpineMidspanEdgeLabelPosition(
                SourceBounds,
                TargetBounds,
                LayoutPoints,
                Direction);
        }

        return (LabelX, LabelY);
    }

    /// <summary>
    /// Returns a label anchor at half the polyline arc-length along the connector.
    /// </summary>
    public static (double X, double Y) ComputeOffsetLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        double OffsetPx = DEFAULT_OFFSET_PX)
    {
        if (GuidePoints.Count < 2)
        {
            return ComputePolylineHalfDistancePosition(GuidePoints);
        }

        (double AnchorX, double AnchorY, double TangentX, double TangentY) =
            SampleGuidePolylineAtFraction(GuidePoints, 0.5);
        return ApplyPerpendicularLabelOffset(AnchorX, AnchorY, TangentX, TangentY, OffsetPx);
    }

    /// <summary>
    /// Returns a label anchor at the midpoint of a straight connector segment.
    /// </summary>
    public static (double X, double Y) ComputeOffsetLabelPositionForStraightSegment(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double OffsetPx = DEFAULT_OFFSET_PX)
    {
        double MidX = (StartX + EndX) / 2.0;
        double MidY = (StartY + EndY) / 2.0;
        double DeltaX = EndX - StartX;
        double DeltaY = EndY - StartY;
        return ApplyPerpendicularLabelOffset(MidX, MidY, DeltaX, DeltaY, OffsetPx);
    }

    /// <summary>
    /// Positions a centre-spine edge label on the connector axis at the port midspan (Mermaid F25 parity).
    /// </summary>
    public static (double X, double Y) ComputeSpineMidspanEdgeLabelPosition(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<(double X, double Y)> LayoutPoints,
        FlowchartDirection Direction)
    {
        if (LayoutPoints.Count < 2)
        {
            bool HorizontalFallback = Direction == FlowchartDirection.LeftToRight
                || Direction == FlowchartDirection.RightToLeft;
            if (HorizontalFallback)
            {
                double MidX = (SourceBounds.CenterX + TargetBounds.CenterX) / 2.0;
                double SpineY = (SourceBounds.CenterY + TargetBounds.CenterY) / 2.0;
                return (MidX, SpineY);
            }

            double FallbackSpineX = (SourceBounds.CenterX + TargetBounds.CenterX) / 2.0;
            double MidY = (SourceBounds.CenterY + TargetBounds.CenterY) / 2.0;
            return (FallbackSpineX, MidY);
        }

        (double StartX, double StartY) = LayoutPoints[0];
        (double EndX, double EndY) = LayoutPoints[LayoutPoints.Count - 1];
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (HorizontalFlow)
        {
            double MidX = (StartX + EndX) / 2.0;
            double SpineY = (StartY + EndY) / 2.0;
            return (MidX, SpineY);
        }

        double SpineX = (StartX + EndX) / 2.0;
        double MidConnectorY = (StartY + EndY) / 2.0;
        return (SpineX, MidConnectorY);
    }

    private static (double X, double Y, double TangentX, double TangentY) SampleGuidePolylineAtFraction(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        double Fraction)
    {
        double ClampedFraction = Math.Max(0.0, Math.Min(1.0, Fraction));
        double TotalDistance = 0.0;
        for (int PointIndex = 1; PointIndex < GuidePoints.Count; PointIndex++)
        {
            (double PreviousX, double PreviousY) = GuidePoints[PointIndex - 1];
            (double PointX, double PointY) = GuidePoints[PointIndex];
            double DeltaX = PointX - PreviousX;
            double DeltaY = PointY - PreviousY;
            TotalDistance += Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        if (TotalDistance <= 1e-7)
        {
            return (GuidePoints[0].X, GuidePoints[0].Y, 1.0, 0.0);
        }

        double TargetDistance = TotalDistance * ClampedFraction;
        for (int PointIndex = 1; PointIndex < GuidePoints.Count; PointIndex++)
        {
            (double PreviousX, double PreviousY) = GuidePoints[PointIndex - 1];
            (double PointX, double PointY) = GuidePoints[PointIndex];
            double DeltaX = PointX - PreviousX;
            double DeltaY = PointY - PreviousY;
            double SegmentDistance = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
            if (SegmentDistance <= 1e-7)
            {
                continue;
            }

            if (SegmentDistance < TargetDistance)
            {
                TargetDistance -= SegmentDistance;
                continue;
            }

            double DistanceRatio = TargetDistance / SegmentDistance;
            return (
                PreviousX + (DeltaX * DistanceRatio),
                PreviousY + (DeltaY * DistanceRatio),
                DeltaX,
                DeltaY);
        }

        (double LastX, double LastY) = GuidePoints[GuidePoints.Count - 1];
        (double PenultimateX, double PenultimateY) = GuidePoints[GuidePoints.Count - 2];
        return (LastX, LastY, LastX - PenultimateX, LastY - PenultimateY);
    }

    /// <summary>
    /// Returns the canonical expected anchor for a local decision-branch label (validation / F29–F30).
    /// TB branches use half-polyline placement on the guided S-route (Naiad parity); LR branches offset perpendicular.
    /// </summary>
    public static (double X, double Y) ComputeLocalDecisionBranchLabelExpectedPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartLayoutNodeBounds SourceBounds,
        string? EdgeLabel,
        FlowchartDirection Direction,
        double OffsetPx = DEFAULT_OFFSET_PX)
    {
        _ = EdgeLabel;
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        List<(double X, double Y)> LabelPolyline = BuildDecisionBranchLabelPolyline(GuidePoints);
        (double AnchorX, double AnchorY) = ComputePolylineHalfDistancePosition(LabelPolyline);

        if (!HorizontalFlow)
        {
            return (AnchorX, AnchorY);
        }

        if (LabelPolyline.Count >= 2
            && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                LabelPolyline[0].X,
                LabelPolyline[0].Y,
                LabelPolyline[LabelPolyline.Count - 1].X,
                LabelPolyline[LabelPolyline.Count - 1].Y))
        {
            return ComputeOffsetLabelPositionForStraightSegment(
                LabelPolyline[0].X,
                LabelPolyline[0].Y,
                LabelPolyline[LabelPolyline.Count - 1].X,
                LabelPolyline[LabelPolyline.Count - 1].Y,
                OffsetPx);
        }

        (double _, double _, double TangentX, double TangentY) =
            SampleGuidePolylineAtFraction(LabelPolyline, 0.5);
        return ApplyPerpendicularLabelOffset(AnchorX, AnchorY, TangentX, TangentY, OffsetPx);
    }

    /// <summary>
    /// Returns the canonical expected anchor for a local decision-branch label (validation / F29–F30).
    /// </summary>
    public static (double X, double Y) ComputeLocalDecisionBranchLabelExpectedPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        double HubCentreX,
        string? EdgeLabel,
        FlowchartDirection Direction,
        double OffsetPx = DEFAULT_OFFSET_PX)
    {
        FlowchartLayoutNodeBounds HubBounds = new FlowchartLayoutNodeBounds
        {
            NodeId = string.Empty,
            CenterX = HubCentreX,
            CenterY = 0.0,
            Width = 0.0,
            Height = 0.0
        };
        return ComputeLocalDecisionBranchLabelExpectedPosition(
            GuidePoints,
            HubBounds,
            EdgeLabel,
            Direction,
            OffsetPx);
    }

    /// <summary>
    /// Positions a decision branch label on the connector midspan.
    /// </summary>
    public static (double X, double Y) ComputeDecisionBranchLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartLayoutNodeBounds SourceBounds,
        string? EdgeLabel,
        FlowchartDirection Direction,
        double OffsetPx = DEFAULT_OFFSET_PX) => ComputeLocalDecisionBranchLabelExpectedPosition(
            GuidePoints,
            SourceBounds,
            EdgeLabel,
            Direction,
            OffsetPx);

    /// <summary>
    /// Builds the polyline used for decision-branch label midspan (source, target-leg guide, target).
    /// </summary>
    public static List<(double X, double Y)> BuildDecisionBranchLabelPolyline(
        IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count >= 5)
        {
            return
            [
                GuidePoints[0],
                GuidePoints[2],
                GuidePoints[GuidePoints.Count - 1]
            ];
        }

        if (GuidePoints.Count >= 3)
        {
            List<(double X, double Y)> Copy = [.. GuidePoints];

            return Copy;
        }

        if (GuidePoints.Count == 2)
        {
            return
            [
                GuidePoints[0],
                GuidePoints[1]
            ];
        }

        return [GuidePoints[0]];
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static (double X, double Y) ApplyPerpendicularLabelOffset(
        double AnchorX,
        double AnchorY,
        double TangentX,
        double TangentY,
        double OffsetPx)
    {
        double Length = Math.Sqrt((TangentX * TangentX) + (TangentY * TangentY));
        if (Length <= 1e-7)
        {
            return (AnchorX, AnchorY + OffsetPx);
        }

        double NormalX = -TangentY / Length;
        double NormalY = TangentX / Length;
        return (AnchorX + (NormalX * OffsetPx), AnchorY + (NormalY * OffsetPx));
    }

    /// <summary>
    /// Positions an exterior exception branch label on the connector midspan.
    /// </summary>
    public static (double X, double Y) ComputeExceptionBranchLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        double HubCentreX,
        string? EdgeLabel,
        double OffsetPx = DEFAULT_OFFSET_PX) => ComputeDecisionBranchLabelPosition(
            GuidePoints,
            new FlowchartLayoutNodeBounds
            {
                NodeId = string.Empty,
                CenterX = HubCentreX,
                CenterY = 0.0,
                Width = 0.0,
                Height = 0.0
            },
            EdgeLabel,
            FlowchartDirection.TopToBottom,
            OffsetPx);

    /// <summary>
    /// Returns true when a routed polyline detours away from the source/target spine to avoid obstacles.
    /// </summary>
    public static bool HasExteriorCorridorDetourForValidation(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartDirection Direction) => HasExteriorCorridorDetour(GuidePoints, Direction);

    private static bool HasExteriorCorridorDetour(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartDirection Direction)
    {
        if (GuidePoints.Count < 4
            || !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(GuidePoints))
        {
            return false;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        (double StartX, double StartY) = GuidePoints[0];
        (double EndX, double EndY) = GuidePoints[GuidePoints.Count - 1];
        double SpineCoordinate = VerticalFlow
            ? (StartX + EndX) / 2.0
            : (StartY + EndY) / 2.0;
        const double EXTERIOR_DETOUR_TOLERANCE_PX = 32.0;

        foreach ((double PointX, double PointY) in GuidePoints)
        {
            double CrossAxisDelta = VerticalFlow
                ? Math.Abs(PointX - SpineCoordinate)
                : Math.Abs(PointY - SpineCoordinate);
            if (CrossAxisDelta > EXTERIOR_DETOUR_TOLERANCE_PX)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHighestHorizontalCorridorLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        out (double X, double Y) LabelPosition) => TryGetHorizontalCorridorLabelPosition(
            GuidePoints,
            PreferMaximumY: true,
            out LabelPosition);

    private static bool TryGetExteriorCorridorLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartDirection Direction,
        out (double X, double Y) LabelPosition)
    {
        if (TryGetExteriorVerticalCorridorLabelPosition(GuidePoints, Direction, out LabelPosition))
        {
            return true;
        }

        bool PreferMaximumY = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        return TryGetHorizontalCorridorLabelPosition(
            GuidePoints,
            PreferMaximumY,
            out LabelPosition);
    }

    private static bool TryGetExteriorVerticalCorridorLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartDirection Direction,
        out (double X, double Y) LabelPosition)
    {
        LabelPosition = (0.0, 0.0);
        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        if (!VerticalFlow)
        {
            return false;
        }

        (double StartX, double StartY) = GuidePoints[0];
        (double EndX, double EndY) = GuidePoints[GuidePoints.Count - 1];
        double SpineCoordinate = (StartX + EndX) / 2.0;
        const double EXTERIOR_DETOUR_TOLERANCE_PX = 32.0;
        bool HasCandidate = false;
        double BestSpan = 0.0;
        double BestExteriorDistance = 0.0;
        double BestCorridorX = 0.0;
        double BestMidY = 0.0;

        for (int Index = 1; Index < GuidePoints.Count; Index++)
        {
            (double SegmentStartX, double SegmentStartY) = GuidePoints[Index - 1];
            (double SegmentEndX, double SegmentEndY) = GuidePoints[Index];
            if (Math.Abs(SegmentStartX - SegmentEndX) > 0.75)
            {
                continue;
            }

            double Span = Math.Abs(SegmentEndY - SegmentStartY);
            if (Span < 24.0)
            {
                continue;
            }

            double ExteriorDistance = Math.Abs(SegmentStartX - SpineCoordinate);
            if (ExteriorDistance <= EXTERIOR_DETOUR_TOLERANCE_PX)
            {
                continue;
            }

            bool IsBetterExterior = ExteriorDistance > BestExteriorDistance;
            bool IsBetterSpan = Math.Abs(ExteriorDistance - BestExteriorDistance) <= 0.75 && Span > BestSpan;
            if (!HasCandidate || IsBetterExterior || IsBetterSpan)
            {
                HasCandidate = true;
                BestExteriorDistance = ExteriorDistance;
                BestSpan = Span;
                BestCorridorX = SegmentStartX;
                BestMidY = (SegmentStartY + SegmentEndY) / 2.0;
            }
        }

        if (!HasCandidate)
        {
            return false;
        }

        double OffsetX = BestCorridorX < SpineCoordinate
            ? -DEFAULT_OFFSET_PX
            : DEFAULT_OFFSET_PX;
        LabelPosition = (BestCorridorX + OffsetX, BestMidY);
        return true;
    }

    private static bool TryGetHorizontalCorridorLabelPosition(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        bool PreferMaximumY,
        out (double X, double Y) LabelPosition)
    {
        LabelPosition = (0.0, 0.0);
        bool HasCandidate = false;
        double BestSpan = 0.0;
        double BestY = PreferMaximumY ? double.NegativeInfinity : double.PositiveInfinity;

        for (int Index = 1; Index < GuidePoints.Count; Index++)
        {
            (double StartX, double StartY) = GuidePoints[Index - 1];
            (double EndX, double EndY) = GuidePoints[Index];
            if (Math.Abs(StartY - EndY) > 0.75)
            {
                continue;
            }

            double Span = Math.Abs(EndX - StartX);
            if (Span < 24.0)
            {
                continue;
            }

            bool IsBetterY = PreferMaximumY
                ? StartY > BestY
                : StartY < BestY;
            if (!HasCandidate || IsBetterY || (Math.Abs(StartY - BestY) <= 0.75 && Span > BestSpan))
            {
                HasCandidate = true;
                BestY = StartY;
                BestSpan = Span;
                LabelPosition = ((StartX + EndX) / 2.0, StartY);
            }
        }

        return HasCandidate;
    }
}
