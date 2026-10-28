using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Identifies which SVG path builder was used so marker approach points match the visible stroke.
/// </summary>
public enum FlowchartRenderedPathKind
{
    CurveBasisFromGuidePoints,

    DecisionBranch,

    VerticalFanRoute,

    HorizontalFanRoute,

    Corridor,

    RetryLoop,

    StadiumMerge
}

/// <summary>
/// Builds orthogonal SVG paths from routed guide points, preserving rounded right-angle corners.
/// </summary>
public static class FlowchartCurvePathBuilder
{
    private const double SOURCE_TANGENT_CONTROL_BLEND_RATIO = 0.25;

    private const double INBOUND_TANGENT_CONTROL_BLEND_RATIO = 0.22;

    private const double TERMINAL_APPROACH_BLEND_RATIO = 0.55;

    private const double MAX_TERMINAL_APPROACH_BLEND_PX = 10.0;

    private const double AXIS_ALIGNMENT_TOLERANCE_PX = 0.75;

    /// <summary>
    /// After whole-pixel snapping, connection points this close in x or y are one straight segment.
    /// </summary>
    public const double AXIS_ALIGNED_ENDPOINT_TOLERANCE_PX = 1.0;

    private const double RETRY_LOOP_CORNER_RADIUS_PX = 28.0;

    /// <summary>
    /// Builds a multi-point routed edge. Linear style stays sharp; other styles retain rounded corners.
    /// </summary>
    /// <param name="Points">Guide points in SVG coordinates (top-left origin).</param>
    /// <param name="CurveStyle">Mermaid flowchart.curve value; only linear disables rounded corners.</param>
    public static string BuildPathDataFromGuidePoints(
        IReadOnlyList<(double X, double Y)> Points,
        string CurveStyle)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        string NormalisedCurve = string.IsNullOrWhiteSpace(CurveStyle) ? "basis" : CurveStyle.Trim();
        if (string.Equals(NormalisedCurve, "linear", StringComparison.OrdinalIgnoreCase))
        {
            return BuildLinearPathData(Points);
        }

        if (TryCollapseAxisAlignedEndpoints(Points, out List<(double X, double Y)> StraightSegment))
        {
            return BuildLinearPathData(StraightSegment);
        }

        return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    /// <summary>
    /// Drops middle waypoints when the snapped source and target connection points share x or y.
    /// </summary>
    /// <param name="Points">Edge waypoints, including the two connection points.</param>
    /// <param name="Segment">The two snapped connection points when they line up.</param>
    /// <returns>True when the edge must be one straight segment.</returns>
    public static bool TryCollapseAxisAlignedEndpoints(
        IReadOnlyList<(double X, double Y)> Points,
        out List<(double X, double Y)> Segment)
    {
        Segment = [];
        if (Points == null || Points.Count < 2)
        {
            return false;
        }

        (double StartX, double StartY) = Points[0];
        (double EndX, double EndY) = Points[Points.Count - 1];
        double SnappedStartX = Math.Round(StartX, MidpointRounding.AwayFromZero);
        double SnappedStartY = Math.Round(StartY, MidpointRounding.AwayFromZero);
        double SnappedEndX = Math.Round(EndX, MidpointRounding.AwayFromZero);
        double SnappedEndY = Math.Round(EndY, MidpointRounding.AwayFromZero);
        double DeltaX = Math.Abs(SnappedStartX - SnappedEndX);
        double DeltaY = Math.Abs(SnappedStartY - SnappedEndY);
        bool SameColumn = DeltaX <= AXIS_ALIGNED_ENDPOINT_TOLERANCE_PX;
        bool SameRow = DeltaY <= AXIS_ALIGNED_ENDPOINT_TOLERANCE_PX;
        if (!SameColumn && !SameRow)
        {
            return false;
        }

        if (SameColumn && SameRow)
        {
            if (DeltaY >= DeltaX)
            {
                SameRow = false;
            }
            else
            {
                SameColumn = false;
            }
        }

        if (SameColumn)
        {
            double SharedX = Math.Round((SnappedStartX + SnappedEndX) / 2.0, MidpointRounding.AwayFromZero);
            if (Math.Abs(SnappedStartY - SnappedEndY) < 1.0)
            {
                return false;
            }

            Segment.Add((SharedX, SnappedStartY));
            Segment.Add((SharedX, SnappedEndY));
            return true;
        }

        double SharedY = Math.Round((SnappedStartY + SnappedEndY) / 2.0, MidpointRounding.AwayFromZero);
        if (Math.Abs(SnappedStartX - SnappedEndX) < 1.0)
        {
            return false;
        }

        Segment.Add((SnappedStartX, SharedY));
        Segment.Add((SnappedEndX, SharedY));
        return true;
    }

    /// <summary>
    /// Returns true when a resolved route should render as an axis-aligned polyline that follows every guide point.
    /// </summary>
    public static bool ShouldUseOrthogonalPolylinePathData(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartEdgeRouteKind RouteKind)
    {
        if (RouteKind != FlowchartEdgeRouteKind.RetryLoop)
        {
            return false;
        }

        if (GuidePoints.Count < 4)
        {
            return false;
        }

        if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(GuidePoints))
        {
            if (GuidePoints.Count < 5)
            {
                return false;
            }

            List<(double X, double Y)> TailPoints = [];
            for (int Index = 1; Index < GuidePoints.Count; Index++)
            {
                TailPoints.Add(GuidePoints[Index]);
            }

            if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(TailPoints)
                || !HasExteriorDetourGuideRoute(GuidePoints))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExteriorDetourGuideRoute(IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count < 4)
        {
            return false;
        }

        (double StartX, double StartY) = GuidePoints[0];
        (double EndX, double EndY) = GuidePoints[GuidePoints.Count - 1];
        double SpineCoordinate = (StartX + EndX) / 2.0;
        const double EXTERIOR_DETOUR_TOLERANCE_PX = 32.0;

        foreach ((double PointX, double PointY) in GuidePoints)
        {
            if (Math.Abs(PointX - SpineCoordinate) > EXTERIOR_DETOUR_TOLERANCE_PX)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Exterior corridor routes: interpolate guide waypoints with curveBasis (Mermaid default), not Manhattan polylines.
    /// </summary>
    public static string BuildCorridorRoutePathData(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartEdgeRouteKind RouteKind = FlowchartEdgeRouteKind.Corridor)
    {
        if (ShouldUseOrthogonalPolylinePathData(Points, RouteKind))
        {
            return BuildRetryLoopPathData(Points);
        }

        List<(double X, double Y)> Orthogonal = FlowchartPaddedVisibilityRouter.EnsureOrthogonal(Points);
        FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Orthogonal, DropCornerOvershoot: true);
        return BuildLinearPathData(Orthogonal);
    }

    private static int ResolveCorridorCurveEndIndex(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count <= 4)
        {
            return Math.Max(1, Points.Count - 2);
        }

        if (Points.Count == 5)
        {
            return 3;
        }

        int TargetStubIndex = Points.Count - 2;
        bool HorizontalReturnBeforeTargetStub =
            Math.Abs(Points[TargetStubIndex - 1].Y - Points[TargetStubIndex].Y) < 0.5
            && Math.Abs(Points[TargetStubIndex - 1].X - Points[TargetStubIndex].X) > 0.5
            && Math.Abs(Points[TargetStubIndex].Y - Points[Points.Count - 1].Y) > 0.5;
        if (HorizontalReturnBeforeTargetStub)
        {
            return TargetStubIndex;
        }

        return Points.Count - 3;
    }

    /// <summary>
    /// Corridor routes keep straight perpendicular terminal stubs and rounded right-angle corners.
    /// </summary>
    private static string BuildCorridorCurveBasisPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    /// <summary>
    /// Returns true when SVG path data is a multi-segment axis-aligned polyline (three or more straight segments).
    /// </summary>
    public static bool IsMultiSegmentOrthogonalPathData(string PathData)
    {
        if (string.IsNullOrWhiteSpace(PathData))
        {
            return false;
        }

        string UpperPath = PathData.ToUpperInvariant();
        if (UpperPath.IndexOf('C', StringComparison.Ordinal) >= 0
            || UpperPath.IndexOf('S', StringComparison.Ordinal) >= 0
            || UpperPath.IndexOf('Q', StringComparison.Ordinal) >= 0
            || UpperPath.IndexOf('A', StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        int SegmentCommandCount = 0;
        foreach (char Command in UpperPath)
        {
            if (Command == 'L' || Command == 'H' || Command == 'V' || Command == 'T')
            {
                SegmentCommandCount++;
            }
        }

        return SegmentCommandCount >= 3;
    }

    /// <summary>
    /// F34 LR retry loops: follows the corridor polyline with rounded corners so the loop has no hard right angles.
    /// </summary>
    public static string BuildRetryLoopPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        return BuildRoundedCornerPathData(Points, RETRY_LOOP_CORNER_RADIUS_PX);
    }

    /// <summary>
    /// Builds a polyline path with circular fillets of up to <paramref name="MaxRadius"/>.
    /// Axis-aligned corners are quarter circles; surface-normal terminal joins use their actual turn angle.
    /// Collinear guide points are dropped first, and each radius is capped at half of the shorter adjoining segment.
    /// </summary>
    public static string BuildRoundedCornerPathData(IReadOnlyList<(double X, double Y)> Points, double MaxRadius)
    {
        List<(double X, double Y)> Corners = RemoveCollinearPoints(Points);
        if (Corners.Count < 3)
        {
            return BuildLinearPathData(Corners);
        }

        const double CIRCLE_CUBIC_HANDLE_RATIO = 0.5523;
        StringBuilder Builder = new StringBuilder();
        Builder.Append('M');
        Builder.Append(FormatCoordinate(Corners[0].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Corners[0].Y));

        for (int CornerIndex = 1; CornerIndex < Corners.Count - 1; CornerIndex++)
        {
            (double PreviousX, double PreviousY) = Corners[CornerIndex - 1];
            (double CornerX, double CornerY) = Corners[CornerIndex];
            (double NextX, double NextY) = Corners[CornerIndex + 1];

            double InX = CornerX - PreviousX;
            double InY = CornerY - PreviousY;
            double InLength = Math.Sqrt((InX * InX) + (InY * InY));
            double OutX = NextX - CornerX;
            double OutY = NextY - CornerY;
            double OutLength = Math.Sqrt((OutX * OutX) + (OutY * OutY));
            double IncomingLimit = CornerIndex == 1 ? InLength : InLength / 2.0;
            double OutgoingLimit = CornerIndex == Corners.Count - 2 ? OutLength : OutLength / 2.0;
            double Radius = Math.Min(MaxRadius, Math.Min(IncomingLimit, OutgoingLimit));
            InX /= InLength;
            InY /= InLength;
            OutX /= OutLength;
            OutY /= OutLength;

            double Handle = Radius * CIRCLE_CUBIC_HANDLE_RATIO;
            double Cosine = Math.Clamp((InX * OutX) + (InY * OutY), -1.0, 1.0);
            if (Math.Abs(Cosine) > 1e-6 && Cosine > -0.999999)
            {
                // A surface-normal terminal can meet its axis-aligned track
                // at an acute angle. Use that angle's circular fillet geometry.
                double Angle = Math.Acos(Cosine);
                double Tangent = Math.Tan(Angle / 2);
                if (Tangent > 1e-6)
                {
                    Radius = Math.Min(MaxRadius * Tangent, Math.Min(IncomingLimit, OutgoingLimit));
                    Handle = (4.0 / 3) * (Radius / Tangent) * Math.Tan(Angle / 4);
                }
            }

            double ArcStartX = CornerX - (InX * Radius);
            double ArcStartY = CornerY - (InY * Radius);
            double ArcEndX = CornerX + (OutX * Radius);
            double ArcEndY = CornerY + (OutY * Radius);

            Builder.Append('L');
            Builder.Append(FormatCoordinate(ArcStartX));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(ArcStartY));
            Builder.Append('C');
            Builder.Append(FormatCoordinate(ArcStartX + (InX * Handle)));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(ArcStartY + (InY * Handle)));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(ArcEndX - (OutX * Handle)));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(ArcEndY - (OutY * Handle)));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(ArcEndX));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(ArcEndY));
        }

        Builder.Append('L');
        Builder.Append(FormatCoordinate(Corners[Corners.Count - 1].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Corners[Corners.Count - 1].Y));
        return Builder.ToString();
    }

    private static List<(double X, double Y)> RemoveCollinearPoints(IReadOnlyList<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Result = [];
        foreach ((double X, double Y) Point in Points)
        {
            if (Result.Count > 0
                && Math.Abs(Result[Result.Count - 1].X - Point.X) < 1e-6
                && Math.Abs(Result[Result.Count - 1].Y - Point.Y) < 1e-6)
            {
                continue;
            }

            if (Result.Count >= 2)
            {
                (double AX, double AY) = Result[Result.Count - 2];
                (double BX, double BY) = Result[Result.Count - 1];
                double Cross = ((BX - AX) * (Point.Y - BY)) - ((BY - AY) * (Point.X - BX));
                double Dot = ((BX - AX) * (Point.X - BX)) + ((BY - AY) * (Point.Y - BY));
                if (Math.Abs(Cross) < 1e-3 && Dot >= 0.0)
                {
                    Result[Result.Count - 1] = Point;
                    continue;
                }
            }

            Result.Add(Point);
        }

        return Result;
    }

    /// <summary>
    /// Renders decision-branch guide points with rounded right-angle corners.
    /// </summary>
    public static string BuildDecisionBranchPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    /// <summary>
    /// Returns the point immediately before the endpoint that matches the rendered path tangent.
    /// </summary>
    public static (double X, double Y) ResolveMarkerApproachPoint(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartRenderedPathKind PathKind,
        bool AtTargetEnd)
    {
        if (PathKind == FlowchartRenderedPathKind.DecisionBranch)
        {
            return ResolveDecisionBranchMarkerApproachPoint(Points, AtTargetEnd);
        }

        if (PathKind == FlowchartRenderedPathKind.Corridor)
        {
            if (Points.Count < 2)
            {
                return Points[0];
            }

            int CorridorApproachIndex = AtTargetEnd ? Points.Count - 2 : 1;
            return Points[CorridorApproachIndex];
        }

        if (PathKind == FlowchartRenderedPathKind.RetryLoop)
        {
            if (Points.Count < 2)
            {
                return Points[0];
            }

            if (AtTargetEnd)
            {
                return Points.Count >= 7 ? Points[5] : Points[Points.Count - 2];
            }

            return Points.Count >= 2 ? Points[1] : Points[0];
        }

        if (PathKind == FlowchartRenderedPathKind.VerticalFanRoute)
        {
            return ResolveFanRouteMarkerApproachPoint(Points, HorizontalFlow: false, AtTargetEnd);
        }

        if (PathKind == FlowchartRenderedPathKind.HorizontalFanRoute)
        {
            return ResolveFanRouteMarkerApproachPoint(Points, HorizontalFlow: true, AtTargetEnd);
        }

        if (PathKind == FlowchartRenderedPathKind.StadiumMerge)
        {
            if (Points.Count < 2)
            {
                return Points[0];
            }

            if (AtTargetEnd)
            {
                return Points[Points.Count - 2];
            }

            return Points.Count >= 2 ? Points[1] : Points[0];
        }

        if (Points.Count < 2)
        {
            return Points[0];
        }

        int ApproachIndex = AtTargetEnd ? Points.Count - 2 : 1;
        return Points[ApproachIndex];
    }

    /// <summary>
    /// Returns the stub point adjacent to the rendered boundary so marker tangents match the terminal line segment.
    /// </summary>
    public static (double X, double Y) ResolveDecisionBranchMarkerApproachPoint(
        IReadOnlyList<(double X, double Y)> Points,
        bool AtTargetEnd)
    {
        if (Points.Count < 2)
        {
            return Points[0];
        }

        if (Points.Count >= 5)
        {
            if (AtTargetEnd)
            {
                return Points[Points.Count - 2];
            }

            return Points[1];
        }

        int ApproachIndex = AtTargetEnd ? Points.Count - 2 : 1;
        return Points[ApproachIndex];
    }

    /// <summary>
    /// Builds the same SVG path data used for a resolved edge route.
    /// </summary>
    public static string BuildRenderedEdgePathData(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        _ = RouteKind;
        _ = Direction;
        return BuildLinearPathData(FlowchartPaddedVisibilityRouter.EnsureOrthogonal(GuidePoints));
    }

    /// <summary>
    /// Resolves the rendered-path kind used for marker approach on a resolved edge route.
    /// </summary>
    public static FlowchartRenderedPathKind ResolveRenderedPathKind(
        IReadOnlyList<(double X, double Y)> GuidePoints,
        FlowchartEdgeRouteKind RouteKind,
        FlowchartDirection Direction)
    {
        if (ShouldUseOrthogonalPolylinePathData(GuidePoints, RouteKind))
        {
            return FlowchartRenderedPathKind.RetryLoop;
        }

        if (RouteKind == FlowchartEdgeRouteKind.Corridor
            || RouteKind == FlowchartEdgeRouteKind.Grid)
        {
            return FlowchartRenderedPathKind.Corridor;
        }

        if (RouteKind == FlowchartEdgeRouteKind.Decision)
        {
            return FlowchartRenderedPathKind.DecisionBranch;
        }

        bool HorizontalFanFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (IsStadiumMergeGuideRoute(GuidePoints))
        {
            return FlowchartRenderedPathKind.StadiumMerge;
        }

        if (GuidePoints.Count >= 5)
        {
            if (HorizontalFanFlow)
            {
                return FlowchartRenderedPathKind.HorizontalFanRoute;
            }

            return FlowchartRenderedPathKind.VerticalFanRoute;
        }

        return FlowchartRenderedPathKind.CurveBasisFromGuidePoints;
    }

    /// <summary>
    /// Returns the point on the boundary S-route immediately before the endpoint for marker alignment.
    /// </summary>
    public static (double X, double Y) ResolveFanRouteMarkerApproachPoint(
        IReadOnlyList<(double X, double Y)> Points,
        bool HorizontalFlow,
        bool AtTargetEnd)
    {
        if (Points.Count < 2)
        {
            return Points[0];
        }

        (double StartX, double StartY) = Points[0];
        (double EndX, double EndY) = Points[Points.Count - 1];
        List<(double X, double Y)> BoundaryPoints = HorizontalFlow
            ? FlowchartHorizontalRouteGuideBuilder.BuildHorizontalFanSRouteGuidedPoints(
                StartX,
                StartY,
                EndX,
                EndY)
            : FlowchartVerticalRouteGuideBuilder.BuildVerticalFanSRouteGuidedPoints(
                StartX,
                StartY,
                EndX,
                EndY);

        if (Points.Count >= 5)
        {
            int ApproachIndex = AtTargetEnd ? Points.Count - 2 : 1;
            return Points[ApproachIndex];
        }

        if (AtTargetEnd)
        {
            if (BoundaryPoints.Count >= 2)
            {
                return BoundaryPoints[BoundaryPoints.Count - 2];
            }
        }
        else if (BoundaryPoints.Count >= 2)
        {
            return BoundaryPoints[1];
        }

        int FallbackIndex = AtTargetEnd ? Points.Count - 2 : 1;
        return Points[FallbackIndex];
    }

    /// <summary>
    /// F31/F32 vertical fan routes: curveBasis on stub-expanded guides when present, otherwise boundary S-route.
    /// </summary>
    /// <summary>
    /// F53/F53A stadium merge: vertical stem at source X, then one cubic into the shoulder attach.
    /// </summary>
    public static bool IsStadiumMergeGuideRoute(IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count < 4)
        {
            return false;
        }

        double SourceX = GuidePoints[0].X;
        int VerticalPointCount = 0;
        for (int PointIndex = 0; PointIndex < GuidePoints.Count - 1; PointIndex++)
        {
            if (Math.Abs(GuidePoints[PointIndex].X - SourceX) <= AXIS_ALIGNMENT_TOLERANCE_PX)
            {
                VerticalPointCount++;
            }
        }

        if (VerticalPointCount < 2)
        {
            return false;
        }

        (double TargetX, double TargetY) = GuidePoints[GuidePoints.Count - 1];
        if (Math.Abs(TargetX - SourceX) <= AXIS_ALIGNMENT_TOLERANCE_PX)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// F53/F53A: vertical stem at source X, then one cubic into the stadium shoulder.
    /// </summary>
    public static string BuildStadiumMergeHookPathData(IReadOnlyList<(double X, double Y)> GuidePoints)
    {
        if (GuidePoints.Count < 4)
        {
            return BuildCurveBasisPathData(GuidePoints);
        }

        (double SourceAttachX, double SourceAttachY) = GuidePoints[0];
        (double TargetAttachX, double TargetAttachY) = GuidePoints[GuidePoints.Count - 1];
        StringBuilder Builder = new StringBuilder();
        Builder.Append('M');
        Builder.Append(FormatCoordinate(SourceAttachX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(SourceAttachY));

        int LastVerticalIndex = 0;
        for (int PointIndex = 1; PointIndex < GuidePoints.Count - 1; PointIndex++)
        {
            (double PointX, double PointY) = GuidePoints[PointIndex];
            if (Math.Abs(PointX - SourceAttachX) <= AXIS_ALIGNMENT_TOLERANCE_PX)
            {
                LastVerticalIndex = PointIndex;
            }
        }

        (double MergeApproachX, double MergeApproachY) = GuidePoints[GuidePoints.Count - 2];
        for (int PointIndex = GuidePoints.Count - 2; PointIndex >= 1; PointIndex--)
        {
            (double CandidateX, double CandidateY) = GuidePoints[PointIndex];
            if (PointIndex <= LastVerticalIndex)
            {
                continue;
            }

            if (Math.Abs(CandidateX - SourceAttachX) <= AXIS_ALIGNMENT_TOLERANCE_PX)
            {
                continue;
            }

            MergeApproachX = CandidateX;
            MergeApproachY = CandidateY;
            break;
        }

        if (Math.Abs(MergeApproachX - SourceAttachX) <= AXIS_ALIGNMENT_TOLERANCE_PX)
        {
            MergeApproachX = SourceAttachX + ((TargetAttachX - SourceAttachX) * 0.45);
        }

        for (int PointIndex = 1; PointIndex <= LastVerticalIndex; PointIndex++)
        {
            (double PointX, double PointY) = GuidePoints[PointIndex];
            if (Math.Abs(PointX - SourceAttachX) > AXIS_ALIGNMENT_TOLERANCE_PX)
            {
                continue;
            }

            (double PreviousX, double PreviousY) = GuidePoints[PointIndex - 1];
            if (Math.Abs(PointY - PreviousY) > AXIS_ALIGNMENT_TOLERANCE_PX
                || Math.Abs(PointX - PreviousX) > AXIS_ALIGNMENT_TOLERANCE_PX)
            {
                Builder.Append('L');
                Builder.Append(FormatCoordinate(PointX));
                Builder.Append(',');
                Builder.Append(FormatCoordinate(PointY));
            }
        }

        (double VerticalEndX, double VerticalEndY) = GuidePoints[LastVerticalIndex];
        double Control1X = SourceAttachX;
        double Control1Y = MergeApproachY;
        if (Math.Abs(Control1Y - TargetAttachY) <= AXIS_ALIGNMENT_TOLERANCE_PX)
        {
            Control1Y = VerticalEndY + ((TargetAttachY - VerticalEndY) * 0.55);
        }

        double Control2X = MergeApproachX;
        double Control2Y = MergeApproachY;
        const double SHOULDER_APPROACH_LIFT_PX = 14.0;
        if (Math.Abs(Control2Y - TargetAttachY) <= AXIS_ALIGNMENT_TOLERANCE_PX)
        {
            Control2Y = TargetAttachY - SHOULDER_APPROACH_LIFT_PX;
        }

        Builder.Append('C');
        Builder.Append(FormatCoordinate(Control1X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Control1Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(Control2X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Control2Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(TargetAttachX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(TargetAttachY));

        return Builder.ToString();
    }

    public static string BuildVerticalFanRoutePathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count >= 5)
        {
            return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        }

        if (Points.Count == 4)
        {
            (double StartX, double StartY) = Points[0];
            (double EndX, double EndY) = Points[Points.Count - 1];
            List<(double X, double Y)> BoundaryCurvePoints =
                FlowchartVerticalRouteGuideBuilder.BuildVerticalFanSRouteGuidedPoints(
                    StartX,
                    StartY,
                    EndX,
                    EndY);
            return BuildRoundedCornerPathData(BoundaryCurvePoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        }

        return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    /// <summary>
    /// F33 LR side fan routes use a boundary S-route with rounded right-angle corners.
    /// </summary>
    public static string BuildHorizontalFanRoutePathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count >= 5)
        {
            return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        }

        if (Points.Count == 4)
        {
            (double StartX, double StartY) = Points[0];
            (double EndX, double EndY) = Points[Points.Count - 1];
            List<(double X, double Y)> BoundaryCurvePoints =
                FlowchartHorizontalRouteGuideBuilder.BuildHorizontalFanSRouteGuidedPoints(
                    StartX,
                    StartY,
                    EndX,
                    EndY);
            return BuildRoundedCornerPathData(BoundaryCurvePoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        }

        return BuildRoundedCornerPathData(Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    /// <summary>
    /// Exact d3.curveBasis conversion (Naiad / d3-shape curve/basis.js).
    /// </summary>
    public static string BuildPureD3CurveBasisPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        if (Points.Count == 2)
        {
            return BuildLinearPathData(Points);
        }

        StringBuilder Builder = new StringBuilder();

        double X0 = Points[0].X;
        double Y0 = Points[0].Y;
        double X1 = Points[0].X;
        double Y1 = Points[0].Y;

        Builder.Append('M');
        Builder.Append(FormatCoordinate(Points[0].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[0].Y));

        X1 = Points[1].X;
        Y1 = Points[1].Y;

        double LineEndX = ((5.0 * X0) + X1) / 6.0;
        double LineEndY = ((5.0 * Y0) + Y1) / 6.0;
        Builder.Append('L');
        Builder.Append(FormatCoordinate(LineEndX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(LineEndY));

        for (int PointIndex = 2; PointIndex < Points.Count; PointIndex++)
        {
            double PointX = Points[PointIndex].X;
            double PointY = Points[PointIndex].Y;

            double Control1X = ((2.0 * X0) + X1) / 3.0;
            double Control1Y = ((2.0 * Y0) + Y1) / 3.0;
            double Control2X = (X0 + (2.0 * X1)) / 3.0;
            double Control2Y = (Y0 + (2.0 * Y1)) / 3.0;
            double SegmentEndX = (X0 + (4.0 * X1) + PointX) / 6.0;
            double SegmentEndY = (Y0 + (4.0 * Y1) + PointY) / 6.0;

            Builder.Append('C');
            Builder.Append(FormatCoordinate(Control1X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Control1Y));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(Control2X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Control2Y));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(SegmentEndX));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(SegmentEndY));

            X0 = X1;
            Y0 = Y1;
            X1 = PointX;
            Y1 = PointY;
        }

        double FinalControl1X = ((2.0 * X0) + X1) / 3.0;
        double FinalControl1Y = ((2.0 * Y0) + Y1) / 3.0;
        double FinalControl2X = (X0 + (2.0 * X1)) / 3.0;
        double FinalControl2Y = (Y0 + (2.0 * Y1)) / 3.0;
        double FinalSegmentEndX = (X0 + (4.0 * X1) + X1) / 6.0;
        double FinalSegmentEndY = (Y0 + (4.0 * Y1) + Y1) / 6.0;

        Builder.Append('C');
        Builder.Append(FormatCoordinate(FinalControl1X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalControl1Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(FinalControl2X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalControl2Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(FinalSegmentEndX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalSegmentEndY));

        Builder.Append('L');
        Builder.Append(FormatCoordinate(X1));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Y1));

        return Builder.ToString();
    }

    /// <summary>
    /// Emulates d3 curveBasis with straight terminal stubs and tangent-aware transitions into the curved body.
    /// </summary>
    public static string BuildCurveBasisPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        if (Points.Count == 2)
        {
            return BuildLinearPathData(Points);
        }

        if (TryBuildStubBridgeCurvePathData(Points, out string StubBridgePathData))
        {
            return StubBridgePathData;
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append('M');
        Builder.Append(FormatCoordinate(Points[0].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[0].Y));

        Builder.Append('L');
        Builder.Append(FormatCoordinate(Points[1].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[1].Y));

        int CurveEndIndex = ResolveCurveBasisEndIndex(Points.Count);
        AppendTangentAwareCurveBasisContinuation(Builder, Points, 1, CurveEndIndex);
        Builder.Append('L');
        Builder.Append(FormatCoordinate(Points[Points.Count - 1].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[Points.Count - 1].Y));

        return Builder.ToString();
    }

    /// <summary>
    /// Curves through interior guides and the target stub; the node boundary is reached separately.
    /// </summary>
    private static int ResolveCurveBasisEndIndex(int PointCount)
    {
        if (PointCount < 4)
        {
            return Math.Max(1, PointCount - 2);
        }

        return PointCount - 2;
    }

    private static void AppendSmoothTerminalApproach(
        StringBuilder Builder,
        IReadOnlyList<(double X, double Y)> Points,
        int CurveEndIndex)
    {
        Builder.Append('L');
        Builder.Append(FormatCoordinate(Points[Points.Count - 1].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[Points.Count - 1].Y));
    }

    private static void AppendTangentAwareCurveBasisContinuation(
        StringBuilder Builder,
        IReadOnlyList<(double X, double Y)> Points,
        int StartIndex,
        int EndIndex)
    {
        double X0 = Points[StartIndex].X;
        double Y0 = Points[StartIndex].Y;
        double X1 = Points[StartIndex].X;
        double Y1 = Points[StartIndex].Y;

        bool HasSourceTangent = StartIndex >= 1;

        double SourceTangentX = 0.0;
        double SourceTangentY = 0.0;
        double SourceTangentLength = 0.0;
        if (HasSourceTangent)
        {
            SourceTangentX = Points[StartIndex].X - Points[StartIndex - 1].X;
            SourceTangentY = Points[StartIndex].Y - Points[StartIndex - 1].Y;
            SourceTangentLength = Math.Sqrt((SourceTangentX * SourceTangentX) + (SourceTangentY * SourceTangentY));
        }

        for (int PointIndex = StartIndex + 1; PointIndex <= EndIndex; PointIndex++)
        {
            double PointX = Points[PointIndex].X;
            double PointY = Points[PointIndex].Y;

            double Control1X = ((2.0 * X0) + X1) / 3.0;
            double Control1Y = ((2.0 * Y0) + Y1) / 3.0;
            double Control2X = (X0 + (2.0 * X1)) / 3.0;
            double Control2Y = (Y0 + (2.0 * Y1)) / 3.0;
            double SegmentEndX = (X0 + (4.0 * X1) + PointX) / 6.0;
            double SegmentEndY = (Y0 + (4.0 * Y1) + PointY) / 6.0;

            if (PointIndex == StartIndex + 1 && SourceTangentLength > 1e-6)
            {
                double NextDeltaX = PointX - Points[StartIndex].X;
                double NextDeltaY = PointY - Points[StartIndex].Y;
                double NextDistance = Math.Sqrt((NextDeltaX * NextDeltaX) + (NextDeltaY * NextDeltaY));
                double Blend = Math.Min(SourceTangentLength * SOURCE_TANGENT_CONTROL_BLEND_RATIO, NextDistance * 0.35);
                Control1X = Points[StartIndex].X + ((SourceTangentX / SourceTangentLength) * Blend);
                Control1Y = Points[StartIndex].Y + ((SourceTangentY / SourceTangentLength) * Blend);
            }

            if (PointIndex == EndIndex && PointIndex > StartIndex + 1)
            {
                ApplyInboundAlignedTargetControl(
                    Points,
                    EndIndex,
                    ref Control2X,
                    ref Control2Y);
            }

            Builder.Append('C');
            Builder.Append(FormatCoordinate(Control1X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Control1Y));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(Control2X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Control2Y));
            Builder.Append(' ');
            Builder.Append(FormatCoordinate(SegmentEndX));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(SegmentEndY));

            X0 = X1;
            Y0 = Y1;
            X1 = PointX;
            Y1 = PointY;
        }

        double FinalControl1X = ((2.0 * X0) + X1) / 3.0;
        double FinalControl1Y = ((2.0 * Y0) + Y1) / 3.0;
        double FinalControl2X = (X0 + (2.0 * X1)) / 3.0;
        double FinalControl2Y = (Y0 + (2.0 * Y1)) / 3.0;
        double FinalEndX = (X0 + (4.0 * X1) + X1) / 6.0;
        double FinalEndY = (Y0 + (4.0 * Y1) + Y1) / 6.0;

        if (EndIndex > StartIndex + 1)
        {
            ApplyInboundAlignedTargetControl(
                Points,
                EndIndex,
                ref FinalControl2X,
                ref FinalControl2Y);
        }

        Builder.Append('C');
        Builder.Append(FormatCoordinate(FinalControl1X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalControl1Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(FinalControl2X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalControl2Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(FinalEndX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(FinalEndY));

        Builder.Append('L');
        Builder.Append(FormatCoordinate(X1));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Y1));
    }

    /// <summary>
    /// Builds a smooth S or quarter curve for a short route of the form boundary, stub, optional step guides, stub, boundary.
    /// The body is one cubic whose end tangents match the two stubs, so the edge leaves and enters each node at right
    /// angles without the small hooks that curveBasis produces on a stepped guide polyline.
    /// </summary>
    /// <param name="Points">Guide points in render coordinates.</param>
    /// <param name="PathData">The SVG path data when the route qualifies.</param>
    /// <returns>True when the route matched the stub-step-stub pattern and <paramref name="PathData"/> was built.</returns>
    public static bool TryBuildStubBridgeCurvePathData(IReadOnlyList<(double X, double Y)> Points, out string PathData)
    {
        PathData = string.Empty;
        if (Points.Count < 4 || Points.Count > 6)
        {
            return false;
        }

        (double BoundaryStartX, double BoundaryStartY) = Points[0];
        (double StubStartX, double StubStartY) = Points[1];
        (double StubEndX, double StubEndY) = Points[Points.Count - 2];
        (double BoundaryEndX, double BoundaryEndY) = Points[Points.Count - 1];

        double SourceDirX = StubStartX - BoundaryStartX;
        double SourceDirY = StubStartY - BoundaryStartY;
        double SourceLength = Math.Sqrt((SourceDirX * SourceDirX) + (SourceDirY * SourceDirY));
        double TargetDirX = BoundaryEndX - StubEndX;
        double TargetDirY = BoundaryEndY - StubEndY;
        double TargetLength = Math.Sqrt((TargetDirX * TargetDirX) + (TargetDirY * TargetDirY));
        if (SourceLength < 1e-6 || TargetLength < 1e-6)
        {
            return false;
        }

        SourceDirX /= SourceLength;
        SourceDirY /= SourceLength;
        TargetDirX /= TargetLength;
        TargetDirY /= TargetLength;

        double ChordX = StubEndX - StubStartX;
        double ChordY = StubEndY - StubStartY;
        double SourceProgress = (ChordX * SourceDirX) + (ChordY * SourceDirY);
        double TargetProgress = (ChordX * TargetDirX) + (ChordY * TargetDirY);
        if (SourceProgress <= 1.0 || TargetProgress <= 1.0)
        {
            return false;
        }

        double CrossOffset = Math.Abs((ChordX * SourceDirY) - (ChordY * SourceDirX));
        double DirectionDot = (SourceDirX * TargetDirX) + (SourceDirY * TargetDirY);
        if (CrossOffset <= AXIS_ALIGNMENT_TOLERANCE_PX && DirectionDot > 0.999)
        {
            return false;
        }

        const double INTERIOR_BOX_TOLERANCE_PX = 1.0;
        double MinX = Math.Min(StubStartX, StubEndX) - INTERIOR_BOX_TOLERANCE_PX;
        double MaxX = Math.Max(StubStartX, StubEndX) + INTERIOR_BOX_TOLERANCE_PX;
        double MinY = Math.Min(StubStartY, StubEndY) - INTERIOR_BOX_TOLERANCE_PX;
        double MaxY = Math.Max(StubStartY, StubEndY) + INTERIOR_BOX_TOLERANCE_PX;
        for (int PointIndex = 2; PointIndex < Points.Count - 2; PointIndex++)
        {
            (double PointX, double PointY) = Points[PointIndex];
            if (PointX < MinX || PointX > MaxX || PointY < MinY || PointY > MaxY)
            {
                return false;
            }
        }

        const double CONTROL_PROGRESS_RATIO = 0.5;
        double Control1X = StubStartX + (SourceDirX * SourceProgress * CONTROL_PROGRESS_RATIO);
        double Control1Y = StubStartY + (SourceDirY * SourceProgress * CONTROL_PROGRESS_RATIO);
        double Control2X = StubEndX - (TargetDirX * TargetProgress * CONTROL_PROGRESS_RATIO);
        double Control2Y = StubEndY - (TargetDirY * TargetProgress * CONTROL_PROGRESS_RATIO);

        StringBuilder Builder = new StringBuilder();
        Builder.Append('M');
        Builder.Append(FormatCoordinate(BoundaryStartX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(BoundaryStartY));
        Builder.Append('L');
        Builder.Append(FormatCoordinate(StubStartX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(StubStartY));
        Builder.Append('C');
        Builder.Append(FormatCoordinate(Control1X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Control1Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(Control2X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Control2Y));
        Builder.Append(' ');
        Builder.Append(FormatCoordinate(StubEndX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(StubEndY));
        Builder.Append('L');
        Builder.Append(FormatCoordinate(BoundaryEndX));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(BoundaryEndY));
        PathData = Builder.ToString();
        return true;
    }

    private static void ApplyInboundAlignedTargetControl(
        IReadOnlyList<(double X, double Y)> Points,
        int TargetStubIndex,
        ref double Control2X,
        ref double Control2Y)
    {
        double InboundDeltaX = Points[TargetStubIndex].X - Points[TargetStubIndex - 1].X;
        double InboundDeltaY = Points[TargetStubIndex].Y - Points[TargetStubIndex - 1].Y;
        double InboundLength = Math.Sqrt((InboundDeltaX * InboundDeltaX) + (InboundDeltaY * InboundDeltaY));
        if (InboundLength <= 1e-6)
        {
            return;
        }

        double Blend = Math.Min(InboundLength * INBOUND_TANGENT_CONTROL_BLEND_RATIO, InboundLength * 0.45);
        Control2X = Points[TargetStubIndex].X - ((InboundDeltaX / InboundLength) * Blend);
        Control2Y = Points[TargetStubIndex].Y - ((InboundDeltaY / InboundLength) * Blend);
    }

    private static string BuildLinearPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append('M');
        Builder.Append(FormatCoordinate(Points[0].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[0].Y));

        for (int PointIndex = 1; PointIndex < Points.Count; PointIndex++)
        {
            Builder.Append('L');
            Builder.Append(FormatCoordinate(Points[PointIndex].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[PointIndex].Y));
        }

        return Builder.ToString();
    }

    private static string FormatCoordinate(double Value) => Value.ToString(CultureInfo.InvariantCulture);
}
