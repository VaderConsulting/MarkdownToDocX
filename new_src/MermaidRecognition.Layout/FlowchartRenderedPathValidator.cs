using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Validates rendered SVG edge geometry against node bodies, kinks, and perpendicular terminal attach.
/// </summary>
public static class FlowchartRenderedPathValidator
{
    private const double NODE_INTERIOR_PADDING_PX = 4.0;

    private const double ENDPOINT_EXCLUSION_PX = 10.0;

    /// <summary>
    /// Result of validating one rendered edge path.
    /// </summary>
    public sealed class FlowchartRenderedPathValidationResult
    {
        public bool ClearsUnrelatedNodeBodies { get; set; } = true;

        public bool PerpendicularAttach { get; set; } = true;

        public bool NoVisibleKink { get; set; } = true;

        public string? IntersectedNodeId { get; set; }

        public string? FailureMessage { get; set; }
    }

    /// <summary>
    /// Validates one rendered SVG path against the resolved layout route for the same edge.
    /// </summary>
    public static FlowchartRenderedPathValidationResult Validate(
        string PathData,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlyList<(double X, double Y)> LayoutGuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        FlowchartRenderedPathValidationResult Result = new FlowchartRenderedPathValidationResult();

        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> SvgSamples)
            || !FlowchartSvgLayoutCoordinateMapper.TryCreate(Contract, Layout, out FlowchartSvgLayoutCoordinateMapper? Mapper)
            || Mapper is null)
        {
            Result.PerpendicularAttach = false;
            Result.NoVisibleKink = false;
            Result.FailureMessage = "Rendered path could not be flattened for validation.";
            return Result;
        }

        List<(double X, double Y)> LayoutSamples = Mapper.ConvertSamplesToLayout(SvgSamples);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Edge endpoints are missing layout bounds.";
            return Result;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

        if (!FlowchartPerpendicularAttachValidator.TryValidateRenderedPathTerminals(
                LayoutSamples,
                LayoutGuidePoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                out string PerpendicularFailure))
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = PerpendicularFailure;
        }
        else if (RouteKind != FlowchartEdgeRouteKind.Decision
            && !UsesPureFanRouteRenderedPath(LayoutGuidePoints, RouteKind, Direction)
            && !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutGuidePoints))
        {
            (double SourceBoundarySvgX, double SourceBoundarySvgY) = Mapper.LayoutToSvg(
                LayoutGuidePoints[0].X,
                LayoutGuidePoints[0].Y);
            (double SourceStubSvgX, double SourceStubSvgY) = Mapper.LayoutToSvg(
                LayoutGuidePoints[1].X,
                LayoutGuidePoints[1].Y);
            (double TargetStubSvgX, double TargetStubSvgY) = Mapper.LayoutToSvg(
                LayoutGuidePoints[LayoutGuidePoints.Count - 2].X,
                LayoutGuidePoints[LayoutGuidePoints.Count - 2].Y);
            (double TargetBoundarySvgX, double TargetBoundarySvgY) = Mapper.LayoutToSvg(
                LayoutGuidePoints[LayoutGuidePoints.Count - 1].X,
                LayoutGuidePoints[LayoutGuidePoints.Count - 1].Y);

            if (!FlowchartSvgPathGeometryHelper.TryValidateSourceLineMatchesGuide(
                    PathData,
                    (SourceBoundarySvgX, SourceBoundarySvgY),
                    (SourceStubSvgX, SourceStubSvgY))
                || !FlowchartSvgPathGeometryHelper.TryValidateTargetLineMatchesGuide(
                    PathData,
                    (TargetStubSvgX, TargetStubSvgY),
                    (TargetBoundarySvgX, TargetBoundarySvgY)))
            {
                Result.PerpendicularAttach = false;
                Result.FailureMessage = "Rendered terminal line segments do not match perpendicular guide stubs.";
            }
        }

        if (RouteKind != FlowchartEdgeRouteKind.Straight
            && RouteKind != FlowchartEdgeRouteKind.GroupedTwoByTwo
            && !FlowchartCurvePathBuilder.ShouldUseOrthogonalPolylinePathData(LayoutGuidePoints, RouteKind)
            && !IsExteriorDetourGuideRoute(LayoutGuidePoints, Direction)
            && FlowchartSvgPathGeometryHelper.HasVisibleKink(LayoutSamples))
        {
            Result.NoVisibleKink = false;
            if (string.IsNullOrWhiteSpace(Result.FailureMessage))
            {
                Result.FailureMessage = "Rendered path contains a visible kink in the smooth body.";
            }
        }

        IReadOnlyList<(double X, double Y)> ClearanceSamples = LayoutSamples;
        if (LayoutGuidePoints.Count >= 4
            && (FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(LayoutGuidePoints)
                || FlowchartCurvePathBuilder.ShouldUseOrthogonalPolylinePathData(LayoutGuidePoints, RouteKind)))
        {
            ClearanceSamples = LayoutGuidePoints;
        }

        if (!PathClearsUnrelatedNodeBodies(
                ClearanceSamples,
                BoundsById,
                Edge.SourceId,
                Edge.TargetId,
                out string IntersectedNodeId))
        {
            Result.ClearsUnrelatedNodeBodies = false;
            Result.IntersectedNodeId = IntersectedNodeId;
            if (string.IsNullOrWhiteSpace(Result.FailureMessage))
            {
                Result.FailureMessage = "Rendered path passes through node " + IntersectedNodeId + ".";
            }
        }

        return Result;
    }

    /// <summary>
    /// Validates a rendered straight SVG line against the resolved layout guide terminals.
    /// </summary>
    public static FlowchartRenderedPathValidationResult ValidateStraightLine(
        double SvgX1,
        double SvgY1,
        double SvgX2,
        double SvgY2,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlyList<(double X, double Y)> LayoutGuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        FlowchartRenderedPathValidationResult Result = new FlowchartRenderedPathValidationResult();

        if (!FlowchartSvgLayoutCoordinateMapper.TryCreate(Contract, Layout, out FlowchartSvgLayoutCoordinateMapper? Mapper)
            || Mapper is null)
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Rendered line could not be mapped into layout coordinates.";
            return Result;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Edge endpoints are missing layout bounds.";
            return Result;
        }

        string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
        bool AllowDiagonalCrossEdge = RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo;

        if (!FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
                LayoutGuidePoints,
                SourceBounds,
                SourceShape,
                TargetBounds,
                TargetShape,
                AllowDiagonalCrossEdge,
                out string GuideFailure))
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Layout guide attach: " + GuideFailure;
            return Result;
        }

        (double LayoutX1, double LayoutY1) = Mapper.SvgToLayout(SvgX1, SvgY1);
        (double LayoutX2, double LayoutY2) = Mapper.SvgToLayout(SvgX2, SvgY2);

        double GuideStartX = LayoutGuidePoints[0].X;
        double GuideStartY = LayoutGuidePoints[0].Y;
        double GuideEndX = LayoutGuidePoints[LayoutGuidePoints.Count - 1].X;
        double GuideEndY = LayoutGuidePoints[LayoutGuidePoints.Count - 1].Y;
        if (LayoutGuidePoints.Count >= 4)
        {
            GuideStartX = LayoutGuidePoints[1].X;
            GuideStartY = LayoutGuidePoints[1].Y;
            GuideEndX = LayoutGuidePoints[LayoutGuidePoints.Count - 2].X;
            GuideEndY = LayoutGuidePoints[LayoutGuidePoints.Count - 2].Y;
        }

        double GuideDeltaX = GuideEndX - GuideStartX;
        double GuideDeltaY = GuideEndY - GuideStartY;
        double LineDeltaX = LayoutX2 - LayoutX1;
        double LineDeltaY = LayoutY2 - LayoutY1;

        double GuideLength = Math.Sqrt((GuideDeltaX * GuideDeltaX) + (GuideDeltaY * GuideDeltaY));
        double LineLength = Math.Sqrt((LineDeltaX * LineDeltaX) + (LineDeltaY * LineDeltaY));
        if (GuideLength <= 1e-6 || LineLength <= 1e-6)
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Rendered straight line segment is degenerate.";
            return Result;
        }

        double AlignmentDot = ((GuideDeltaX / GuideLength) * (LineDeltaX / LineLength))
            + ((GuideDeltaY / GuideLength) * (LineDeltaY / LineLength));
        if (AlignmentDot < 0.98)
        {
            Result.PerpendicularAttach = false;
            Result.FailureMessage = "Rendered straight line direction does not match the layout guide.";
        }

        return Result;
    }

    private static bool UsesPureFanRouteRenderedPath(
        IReadOnlyList<(double X, double Y)> LayoutGuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        if (FlowchartCurvePathBuilder.ShouldUseOrthogonalPolylinePathData(LayoutGuidePoints, RouteKind))
        {
            return true;
        }

        FlowchartRenderedPathKind PathKind = FlowchartCurvePathBuilder.ResolveRenderedPathKind(
            LayoutGuidePoints,
            RouteKind,
            Direction);
        return PathKind == FlowchartRenderedPathKind.HorizontalFanRoute
            || PathKind == FlowchartRenderedPathKind.VerticalFanRoute;
    }

    private static bool PathClearsUnrelatedNodeBodies(
        IReadOnlyList<(double X, double Y)> LayoutSamples,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId,
        out string IntersectedNodeId)
    {
        IntersectedNodeId = string.Empty;

        if (LayoutSamples.Count < 2)
        {
            return true;
        }

        double TotalLength = 0.0;
        for (int PointIndex = 1; PointIndex < LayoutSamples.Count; PointIndex++)
        {
            TotalLength += SegmentLength(LayoutSamples[PointIndex - 1], LayoutSamples[PointIndex]);
        }

        double TraversedLength = 0.0;
        for (int SegmentIndex = 0; SegmentIndex < LayoutSamples.Count - 1; SegmentIndex++)
        {
            (double StartX, double StartY) = LayoutSamples[SegmentIndex];
            (double EndX, double EndY) = LayoutSamples[SegmentIndex + 1];
            double SegmentLen = SegmentLength((StartX, StartY), (EndX, EndY));
            double SegmentStartDistance = TraversedLength;
            double SegmentEndDistance = TraversedLength + SegmentLen;
            TraversedLength = SegmentEndDistance;

            bool NearSourceEndpoint = SegmentEndDistance <= ENDPOINT_EXCLUSION_PX;
            bool NearTargetEndpoint = SegmentStartDistance >= (TotalLength - ENDPOINT_EXCLUSION_PX);
            if (NearSourceEndpoint || NearTargetEndpoint)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
            {
                FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
                if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                    || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!SegmentIntersectsPaddedInterior(StartX, StartY, EndX, EndY, CandidateBounds, NODE_INTERIOR_PADDING_PX))
                {
                    continue;
                }

                IntersectedNodeId = CandidateBounds.NodeId;
                return false;
            }
        }

        return true;
    }

    private static bool SegmentIntersectsPaddedInterior(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds NodeBounds,
        double PaddingPx)
    {
        double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0) + PaddingPx;
        double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0) - PaddingPx;
        double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0) - PaddingPx;
        double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0) + PaddingPx;
        return FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
            StartX,
            StartY,
            EndX,
            EndY,
            Left,
            Top,
            Right,
            Bottom);
    }

    private static double SegmentLength((double X, double Y) Start, (double X, double Y) End)
    {
        double DeltaX = End.X - Start.X;
        double DeltaY = End.Y - Start.Y;
        return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
    }

    private static bool IsExteriorDetourGuideRoute(
        IReadOnlyList<(double X, double Y)> LayoutGuidePoints,
        FlowchartDirection Direction)
    {
        if (LayoutGuidePoints.Count < 4)
        {
            return false;
        }

        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;
        (double StartX, double StartY) = LayoutGuidePoints[0];
        (double EndX, double EndY) = LayoutGuidePoints[LayoutGuidePoints.Count - 1];
        double SpineCoordinate = VerticalFlow
            ? (StartX + EndX) / 2.0
            : (StartY + EndY) / 2.0;
        const double EXTERIOR_DETOUR_TOLERANCE_PX = 32.0;

        foreach ((double PointX, double PointY) in LayoutGuidePoints)
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
}
