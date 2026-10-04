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
/// Builds SVG path data from routed guide points using Mermaid edge curve styles (d3.line / spec §3.17).
/// </summary>
public static class FlowchartCurvePathBuilder
{
    private const double SOURCE_TANGENT_CONTROL_BLEND_RATIO = 0.25;

    private const double INBOUND_TANGENT_CONTROL_BLEND_RATIO = 0.22;

    private const double TERMINAL_APPROACH_BLEND_RATIO = 0.55;

    private const double MAX_TERMINAL_APPROACH_BLEND_PX = 10.0;

    private const double AXIS_ALIGNMENT_TOLERANCE_PX = 0.75;

    /// <summary>
    /// Builds SVG path data for a multi-point routed edge using <paramref name="CurveStyle"/>.
    /// </summary>
    /// <param name="Points">Guide points in SVG coordinates (top-left origin).</param>
    /// <param name="CurveStyle">Mermaid flowchart.curve value (default basis).</param>
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

        return BuildCurveBasisPathData(Points);
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

        if (Points.Count >= 4)
        {
            return BuildCorridorCurveBasisPathData(Points);
        }

        return BuildCurveBasisPathData(Points);
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
    /// Corridor routes keep straight perpendicular terminal stubs and smooth only the exterior body.
    /// </summary>
    private static string BuildCorridorCurveBasisPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 4)
        {
            return BuildCurveBasisPathData(Points);
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

        List<(double X, double Y)> BodyPoints = [];
        int CurveEndIndex = ResolveCorridorCurveEndIndex(Points);
        for (int Index = 1; Index <= CurveEndIndex; Index++)
        {
            BodyPoints.Add(Points[Index]);
        }

        string BodyPath = BuildPureD3CurveBasisPathData(BodyPoints);
        int CubicIndex = BodyPath.IndexOf('C', StringComparison.Ordinal);
        if (CubicIndex >= 0)
        {
            Builder.Append(BodyPath.AsSpan(CubicIndex));
        }

        if (CurveEndIndex >= Points.Count - 1)
        {
            Builder.Append('L');
            Builder.Append(FormatCoordinate(Points[Points.Count - 1].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[Points.Count - 1].Y));
        }
        else
        {
            for (int Index = CurveEndIndex; Index < Points.Count - 1; Index++)
            {
                Builder.Append('L');
                Builder.Append(FormatCoordinate(Points[Index + 1].X));
                Builder.Append(',');
                Builder.Append(FormatCoordinate(Points[Index + 1].Y));
            }
        }

        return Builder.ToString();
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
    /// F34 LR retry loops: straight source stub, then a smooth U-shaped corridor curve into the target.
    /// </summary>
    public static string BuildRetryLoopPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        if (Points.Count < 7)
        {
            return BuildLinearPathData(Points);
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
        Builder.Append('L');
        Builder.Append(FormatCoordinate(Points[3].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[3].Y));
        Builder.Append('L');
        Builder.Append(FormatCoordinate(Points[4].X));
        Builder.Append(',');
        Builder.Append(FormatCoordinate(Points[4].Y));

        string TargetApproach = BuildCurveBasisPathData(new List<(double X, double Y)>
        {
            Points[4],
            Points[5],
            Points[6]
        });
        int TargetLineIndex = TargetApproach.IndexOf('L', StringComparison.Ordinal);
        if (TargetLineIndex >= 0)
        {
            Builder.Append(TargetApproach.AsSpan(TargetLineIndex));
        }
        else
        {
            Builder.Append('L');
            Builder.Append(FormatCoordinate(Points[6].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[6].Y));
        }

        return Builder.ToString();
    }

    /// <summary>
    /// F29/F30/F35 decision branches: pure d3 curveBasis on boundary → guide → boundary (Naiad / spec §15).
    /// </summary>
    public static string BuildDecisionBranchPathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count == 5)
        {
            StringBuilder Builder = new StringBuilder();
            Builder.Append('M');
            Builder.Append(FormatCoordinate(Points[0].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[0].Y));
            Builder.Append('L');
            Builder.Append(FormatCoordinate(Points[1].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[1].Y));

            List<(double X, double Y)> BranchCurvePoints =
            [
                Points[1],
                Points[2],
                Points[3]
            ];
            string BranchBody = BuildPureD3CurveBasisPathData(BranchCurvePoints);
            int CubicIndex = BranchBody.IndexOf('C', StringComparison.Ordinal);
            if (CubicIndex >= 0)
            {
                Builder.Append(BranchBody.AsSpan(CubicIndex));
            }

            Builder.Append('L');
            Builder.Append(FormatCoordinate(Points[4].X));
            Builder.Append(',');
            Builder.Append(FormatCoordinate(Points[4].Y));

            return Builder.ToString();
        }

        return BuildCurveBasisPathData(Points);
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
        if (ShouldUseOrthogonalPolylinePathData(GuidePoints, RouteKind))
        {
            return BuildRetryLoopPathData(GuidePoints);
        }

        if (RouteKind == FlowchartEdgeRouteKind.Corridor)
        {
            return BuildCorridorRoutePathData(GuidePoints, RouteKind);
        }

        if (RouteKind == FlowchartEdgeRouteKind.Decision)
        {
            return BuildDecisionBranchPathData(GuidePoints);
        }

        if (IsStadiumMergeGuideRoute(GuidePoints))
        {
            return BuildStadiumMergeHookPathData(GuidePoints);
        }

        bool HorizontalFanFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (GuidePoints.Count >= 5)
        {
            if (HorizontalFanFlow)
            {
                return BuildHorizontalFanRoutePathData(GuidePoints);
            }

            return BuildVerticalFanRoutePathData(GuidePoints);
        }

        return BuildCurveBasisPathData(GuidePoints);
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

        if (RouteKind == FlowchartEdgeRouteKind.Corridor)
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
            return BuildCurveBasisPathData(Points);
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
            return BuildPureD3CurveBasisPathData(BoundaryCurvePoints);
        }

        return BuildCurveBasisPathData(Points);
    }

    /// <summary>
    /// F33 LR side fan routes: pure d3 curveBasis on boundary S-route (Naiad / spec §15).
    /// </summary>
    public static string BuildHorizontalFanRoutePathData(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count >= 5)
        {
            return BuildCurveBasisPathData(Points);
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
            return BuildPureD3CurveBasisPathData(BoundaryCurvePoints);
        }

        return BuildCurveBasisPathData(Points);
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
