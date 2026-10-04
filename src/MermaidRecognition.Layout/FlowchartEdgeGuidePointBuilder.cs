using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds edge guide points for native SVG (spec §11.8–11.9): straight only for axis-aligned 1:1 links;
/// fan-out/fan-in multiples use curved S-routes or decision guides with curveBasis interpolation.
/// </summary>
public static class FlowchartEdgeGuidePointBuilder
{
    private const double AXIS_ALIGNMENT_TOLERANCE_PX = 1.0;

    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double SMOOTH_CURVE_BULGE_RATIO = 0.35;

    /// <summary>
    /// Returns true when source and target node centres share a horizontal row or vertical column.
    /// </summary>
    public static bool SharesAxisAlignedRowOrColumn(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        bool SharesColumn = Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) <= AXIS_ALIGNMENT_TOLERANCE_PX;
        bool SharesRow = Math.Abs(SourceBounds.CenterY - TargetBounds.CenterY) <= AXIS_ALIGNMENT_TOLERANCE_PX;
        return SharesColumn || SharesRow;
    }

    /// <summary>
    /// Returns true when an edge may render as a single straight segment: axis-aligned centres and a 1:1 link
    /// (exactly one visible outgoing at the source and one visible incoming at the target).
    /// Fan-out (source out-degree &gt; 1) and fan-in (target in-degree &gt; 1) require curved guides.
    /// </summary>
    public static bool ShouldUseStraightSegment(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        DiagramContract Contract,
        string SourceId,
        string TargetId)
    {
        if (!SharesAxisAlignedRowOrColumn(SourceBounds, TargetBounds))
        {
            return false;
        }

        int OutgoingCount = CountVisibleEdges(Contract, SourceId, true);
        int IncomingCount = CountVisibleEdges(Contract, TargetId, false);

        if (OutgoingCount > 1 || IncomingCount > 1)
        {
            return false;
        }

        return OutgoingCount == 1 && IncomingCount == 1;
    }

    /// <summary>
    /// Builds guide points in SVG coordinates for a curved edge (curveBasis / linear path rendering).
    /// </summary>
    public static List<(double X, double Y)> BuildCurvedGuidePoints(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        string? SourceShape,
        string? TargetShape,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartDirection Direction,
        double SourceHubCenterSvgX,
        double SourceHubCenterSvgY)
    {
        List<(double X, double Y)> Points = [];
        (double StartX, double StartY) Start = (StartX, StartY);
        (double EndX, double EndY) End = (EndX, EndY);

        int OutgoingCount = CountVisibleEdges(Contract, Edge.SourceId, true);
        int IncomingCount = CountVisibleEdges(Contract, Edge.TargetId, false);
        bool SourceIsDecision = IsDecisionShape(SourceShape);
        bool TargetIsDecision = IsDecisionShape(TargetShape);

        if (TryBuildCrossGroupGuidePoints(
                Contract,
                Edge.SourceId,
                Edge.TargetId,
                Start,
                End,
                Direction,
                out List<(double X, double Y)> CrossGroupPoints))
        {
            AppendGuidePoints(Points, CrossGroupPoints);
            return Points;
        }

        if (SourceIsDecision && OutgoingCount >= 2)
        {
            AppendGuidePoints(Points, BuildDecisionFanOutGuidePoints(
                Start,
                End,
                SourceHubCenterSvgX,
                SourceHubCenterSvgY,
                Direction));
            return Points;
        }

        if (TargetIsDecision && IncomingCount >= 2)
        {
            AppendGuidePoints(Points, BuildDecisionFanInGuidePoints(Start, End, Direction, EndX));
            return Points;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (HorizontalFlow)
        {
            AppendGuidePoints(Points, BuildHorizontalFlowSRouteGuidePoints(Start, End, SourceHubCenterSvgY));
        }
        else
        {
            AppendGuidePoints(Points, BuildVerticalFlowSRouteGuidePoints(Start, End, SourceHubCenterSvgX));
        }

        return Points;
    }

    /// <summary>
    /// Appends an orthogonal route whose first and last segments are perpendicular to the resolved attach faces.
    /// </summary>
    public static void AppendFaceAwareOrthogonalRoute(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        FlowchartEdgeAttachFace SourceFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            SourceBounds,
            StartX,
            StartY);
        FlowchartEdgeAttachFace TargetFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            TargetBounds,
            EndX,
            EndY);

        bool SourceUsesVerticalStub = SourceFace == FlowchartEdgeAttachFace.Top
            || SourceFace == FlowchartEdgeAttachFace.Bottom;
        bool TargetUsesVerticalStub = TargetFace == FlowchartEdgeAttachFace.Top
            || TargetFace == FlowchartEdgeAttachFace.Bottom;

        double MidX = (StartX + EndX) / 2.0;
        double MidY = (StartY + EndY) / 2.0;

        AppendGuidePointIfDistinct(Points, StartX, StartY);

        if (SourceUsesVerticalStub && TargetUsesVerticalStub)
        {
            AppendGuidePointIfDistinct(Points, StartX, MidY);
            if (Math.Abs(EndX - StartX) > COLLINEAR_EPSILON_PX)
            {
                AppendGuidePointIfDistinct(Points, EndX, MidY);
            }
        }
        else if (!SourceUsesVerticalStub && !TargetUsesVerticalStub)
        {
            AppendGuidePointIfDistinct(Points, MidX, StartY);
            if (Math.Abs(EndY - StartY) > COLLINEAR_EPSILON_PX)
            {
                AppendGuidePointIfDistinct(Points, MidX, EndY);
            }
        }
        else if (!SourceUsesVerticalStub && TargetUsesVerticalStub)
        {
            AppendGuidePointIfDistinct(Points, MidX, StartY);
            AppendGuidePointIfDistinct(Points, MidX, MidY);
            AppendGuidePointIfDistinct(Points, EndX, MidY);
        }
        else
        {
            AppendGuidePointIfDistinct(Points, StartX, MidY);
            AppendGuidePointIfDistinct(Points, MidX, MidY);
            AppendGuidePointIfDistinct(Points, MidX, EndY);
        }

        AppendGuidePointIfDistinct(Points, EndX, EndY);
    }

    private static void AppendGuidePointIfDistinct(
        List<(double X, double Y)> Points,
        double X,
        double Y)
    {
        if (Points.Count == 0)
        {
            Points.Add((X, Y));
            return;
        }

        (double LastX, double LastY) = Points[Points.Count - 1];
        if (Math.Abs(LastX - X) > COLLINEAR_EPSILON_PX || Math.Abs(LastY - Y) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((X, Y));
        }
    }

    private static List<(double X, double Y)> BuildVerticalFlowSRouteGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        double HubCenterX)
    {
        if (FlowchartEdgeConnectionGeometry.AreAttachPointsAxisAligned(Start.X, Start.Y, End.X, End.Y))
        {
            return [Start, End];
        }

        double MidY = (Start.Y + End.Y) / 2.0;
        double BulgeX = FlowchartCurveBulgeHelper.ResolveVerticalFlowBulgeX(
            Start.X,
            End.X,
            HubCenterX,
            SMOOTH_CURVE_BULGE_RATIO);

        return
        [
            Start,
            (BulgeX, MidY),
            End
        ];
    }

    private static List<(double X, double Y)> BuildHorizontalFlowSRouteGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        double HubCenterY)
    {
        if (FlowchartEdgeConnectionGeometry.AreAttachPointsAxisAligned(Start.X, Start.Y, End.X, End.Y))
        {
            return [Start, End];
        }

        double MidX = (Start.X + End.X) / 2.0;
        double BulgeY = FlowchartCurveBulgeHelper.ResolveHorizontalFlowBulgeY(
            Start.Y,
            End.Y,
            HubCenterY,
            SMOOTH_CURVE_BULGE_RATIO);

        return
        [
            Start,
            (MidX, BulgeY),
            End
        ];
    }

    private static List<(double X, double Y)> BuildDecisionFanOutGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        double HubCenterSvgX,
        double HubCenterSvgY,
        FlowchartDirection Direction)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (HorizontalFlow)
        {
            return BuildHorizontalOutwardCurveGuidePoints(Start, End, HubCenterSvgY);
        }

        return BuildVerticalOutwardCurveGuidePoints(Start, End, HubCenterSvgX);
    }

    private static List<(double X, double Y)> BuildDecisionFanInGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        FlowchartDirection Direction,
        double HubCenterCrossAxis)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        if (HorizontalFlow)
        {
            return BuildHorizontalFlowSRouteGuidePoints(Start, End, HubCenterCrossAxis);
        }

        return BuildVerticalFlowSRouteGuidePoints(Start, End, HubCenterCrossAxis);
    }

    private static List<(double X, double Y)> BuildVerticalOutwardCurveGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        double HubCentreX) => BuildVerticalFlowSRouteGuidePoints(Start, End, HubCentreX);

    private static List<(double X, double Y)> BuildHorizontalOutwardCurveGuidePoints(
        (double X, double Y) Start,
        (double X, double Y) End,
        double HubCentreY) => BuildHorizontalFlowSRouteGuidePoints(Start, End, HubCentreY);

    /// <summary>
    /// Builds layout-space curved guides (MSAGL Y-up) with perpendicular stubs.
    /// </summary>
    public static List<(double X, double Y)> BuildLayoutCurvedGuidePoints(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        string? SourceShape,
        string? TargetShape,
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartDirection Direction,
        double SourceHubCenterX,
        double SourceHubCenterY,
        double TargetHubCenterX,
        double TargetHubCenterY)
    {
        List<(double X, double Y)> Points = [];
        (double StartX, double StartY) Start = (StartX, StartY);
        (double EndX, double EndY) End = (EndX, EndY);

        if (FlowchartEdgeConnectionGeometry.AreAttachPointsAxisAligned(StartX, StartY, EndX, EndY))
        {
            Points.Add((StartX, StartY));
            Points.Add((EndX, EndY));
            return Points;
        }

        int OutgoingCount = CountVisibleEdges(Contract, Edge.SourceId, true);
        int IncomingCount = CountVisibleEdges(Contract, Edge.TargetId, false);
        bool SourceIsDecision = IsDecisionShape(SourceShape);
        bool TargetIsDecision = IsDecisionShape(TargetShape);

        if (SourceIsDecision && OutgoingCount >= 2)
        {
            AppendGuidePoints(Points, BuildDecisionFanOutGuidePoints(
                Start,
                End,
                SourceHubCenterX,
                SourceHubCenterY,
                Direction));
            return Points;
        }

        if (TargetIsDecision && IncomingCount >= 2)
        {
            AppendGuidePoints(Points, BuildDecisionFanInGuidePoints(Start, End, Direction, TargetHubCenterX));
            return Points;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool PreferTargetHub = IncomingCount >= 2 && OutgoingCount < 2;
        double RouteHubCenterX = PreferTargetHub ? TargetHubCenterX : SourceHubCenterX;
        double RouteHubCenterY = PreferTargetHub ? TargetHubCenterY : SourceHubCenterY;

        if (HorizontalFlow)
        {
            AppendGuidePoints(Points, BuildHorizontalFlowSRouteGuidePoints(Start, End, RouteHubCenterY));
        }
        else
        {
            AppendGuidePoints(Points, BuildVerticalFlowSRouteGuidePoints(Start, End, RouteHubCenterX));
        }

        return Points;
    }

    /// <summary>
    /// Cross-subgraph routes in layout space using axis-aligned S-routes with perpendicular stubs.
    /// </summary>
    public static bool TryBuildCrossGroupLayoutGuidePoints(
        DiagramContract Contract,
        string SourceId,
        string TargetId,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool PreferOrthogonalCorridor,
        out List<(double X, double Y)> Points)
    {
        Points = [];

        string? SourceGroupId = FindGroupForNode(SourceId, Contract);
        string? TargetGroupId = FindGroupForNode(TargetId, Contract);
        if (string.Equals(SourceGroupId, TargetGroupId, StringComparison.Ordinal))
        {
            return false;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        (double StartX, double StartY) Start = (StartX, StartY);
        (double EndX, double EndY) End = (EndX, EndY);

        bool HasHorizontalObstacle = HorizontalFlow
            && FlowchartRouteObstacleHelper.HorizontalChordIntersectsUnrelatedNode(
                StartX,
                StartY,
                EndX,
                SourceId,
                TargetId,
                BoundsById);

        if (!PreferOrthogonalCorridor
            && !HasHorizontalObstacle
            && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(StartX, StartY, EndX, EndY))
        {
            Points.Add((StartX, StartY));
            Points.Add((EndX, EndY));
            return true;
        }

        double RouteHubCenterX = FlowchartCrossSubgraphRouteHelper.ResolveCrossGroupGuideCenterX(
            Contract,
            SourceId,
            TargetId,
            StartX,
            StartY,
            EndX,
            EndY,
            Direction,
            BoundsById);

        if (HorizontalFlow && (PreferOrthogonalCorridor || HasHorizontalObstacle))
        {
            AppendDottedCrossGroupExteriorRoute(
                Contract,
                Points,
                StartX,
                StartY,
                SourceBounds,
                null,
                EndX,
                EndY,
                TargetBounds,
                null,
                SourceId,
                TargetId,
                BoundsById);
        }
        else
        {
            FlowchartPerpendicularRouteBuilder.AppendCurvedPerpendicularRoute(
                Points,
                StartX,
                StartY,
                SourceBounds,
                null,
                EndX,
                EndY,
                TargetBounds,
                null,
                RouteHubCenterX);
        }

        return Points.Count >= 2;
    }

    private static bool TryBuildCrossGroupGuidePoints(
        DiagramContract Contract,
        string SourceId,
        string TargetId,
        (double X, double Y) Start,
        (double X, double Y) End,
        FlowchartDirection Direction,
        out List<(double X, double Y)> Points)
    {
        Points = [];

        string? SourceGroupId = FindGroupForNode(SourceId, Contract);
        string? TargetGroupId = FindGroupForNode(TargetId, Contract);
        if (string.Equals(SourceGroupId, TargetGroupId, StringComparison.Ordinal))
        {
            return false;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        double HubCenterX = (Start.X + End.X) / 2.0;
        double HubCenterY = (Start.Y + End.Y) / 2.0;
        if (HorizontalFlow)
        {
            Points.AddRange(BuildHorizontalFlowSRouteGuidePoints(Start, End, HubCenterY));
        }
        else
        {
            Points.AddRange(BuildVerticalFlowSRouteGuidePoints(Start, End, HubCenterX));
        }

        return Points.Count >= 2;
    }

    private const double DOTTED_CROSS_GROUP_CLEARANCE_PX = 28.0;

    private static void AppendDottedCrossGroupExteriorRoute(
        DiagramContract Contract,
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (TryAppendUnderCorridorRoute(Contract, Points, StartX, StartY, SourceBounds, EndX, EndY, TargetBounds, BoundsById))
        {
            return;
        }

        double PreTargetX = TargetBounds.CenterX - (TargetBounds.Width / 2.0) - DOTTED_CROSS_GROUP_CLEARANCE_PX;
        if (PreTargetX < StartX)
        {
            PreTargetX = StartX;
        }

        double ChordY = Math.Max(StartY, EndY);
        double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
            StartX,
            ChordY,
            EndX,
            SourceNodeId,
            TargetNodeId,
            BoundsById,
            DOTTED_CROSS_GROUP_CLEARANCE_PX);
        double MaxObstacleTop = FlowchartRouteObstacleHelper.ResolveMaximumObstacleTopOnHorizontalSpan(
            Contract,
            BoundsById,
            StartX,
            EndX,
            SourceNodeId,
            TargetNodeId);
        if (MaxObstacleTop + DOTTED_CROSS_GROUP_CLEARANCE_PX > CorridorY)
        {
            CorridorY = MaxObstacleTop + DOTTED_CROSS_GROUP_CLEARANCE_PX;
        }
        (double SourceStubX, double SourceStubY) = FlowchartVerticalRouteGuideBuilder.ComputeStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = FlowchartVerticalRouteGuideBuilder.ComputeStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape);

        Points.Add((StartX, StartY));
        Points.Add((SourceStubX, SourceStubY));
        if (Math.Abs(CorridorY - SourceStubY) > 0.75)
        {
            Points.Add((SourceStubX, CorridorY));
        }

        Points.Add((TargetStubX, CorridorY));
        if (Math.Abs(TargetStubY - CorridorY) > 0.75)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((EndX, EndY));
    }

    /// <summary>
    /// Left-to-right cross-group link that would otherwise cut through unrelated nodes or titles (F60 audit copy).
    /// The edge leaves the middle of the source's bottom face, runs in a corridor below every node and group box it passes,
    /// and rises into the middle of the target's bottom face. Both ends meet their nodes at right angles,
    /// and the route stays clear of the subgraph title bands, which sit at the top of each box.
    /// </summary>
    /// <returns>True when the route was appended.</returns>
    private static bool TryAppendUnderCorridorRoute(
        DiagramContract Contract,
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double SourceHalfWidth = SourceBounds.Width / 2.0;
        double TargetHalfWidth = TargetBounds.Width / 2.0;
        double SourceGap = Math.Max(0.0, Math.Abs(StartX - SourceBounds.CenterX) - SourceHalfWidth);
        double TargetGap = Math.Max(0.0, Math.Abs(EndX - TargetBounds.CenterX) - TargetHalfWidth);
        double SourceX = SourceBounds.CenterX;
        double TargetX = TargetBounds.CenterX;
        if (Math.Abs(TargetX - SourceX) <= SourceHalfWidth + TargetHalfWidth)
        {
            return false;
        }

        double SourceY = SourceBounds.CenterY - (SourceBounds.Height / 2.0) - SourceGap;
        double TargetY = TargetBounds.CenterY - (TargetBounds.Height / 2.0) - TargetGap;
        double SpanMinX = Math.Min(SourceX, TargetX);
        double SpanMaxX = Math.Max(SourceX, TargetX);
        double LowestBottom = Math.Min(SourceY, TargetY);

        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            if (Right < SpanMinX || Left > SpanMaxX)
            {
                continue;
            }

            LowestBottom = Math.Min(LowestBottom, Bounds.CenterY - (Bounds.Height / 2.0));
        }

        foreach (FlowchartSubgraphLayoutRect Rect in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById))
        {
            if (Rect.Right < SpanMinX || Rect.Left > SpanMaxX)
            {
                continue;
            }

            LowestBottom = Math.Min(LowestBottom, Rect.Bottom);
        }

        double CorridorY = LowestBottom - UNDER_CORRIDOR_CLEARANCE_PX;
        Points.Add((SourceX, SourceY));
        Points.Add((SourceX, CorridorY));
        Points.Add((TargetX, CorridorY));
        Points.Add((TargetX, TargetY));
        return true;
    }

    private const double UNDER_CORRIDOR_CLEARANCE_PX = 20.0;

    /// <summary>
    /// Returns true for a dotted edge between two different groups in a left-to-right or right-to-left diagram.
    /// Such an edge leaves and enters through the bottom faces (under-corridor route), so it must not take a slot
    /// on the side faces that the solid flow edges use (F60 audit copy beside Verify manifest to Normalise).
    /// </summary>
    public static bool UsesBottomFaceCorridor(EdgeContract Edge, DiagramContract Contract)
    {
        if (!string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        if (Direction != FlowchartDirection.LeftToRight && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        string? SourceGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract);
        string? TargetGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract);
        return !string.Equals(SourceGroupId, TargetGroupId, StringComparison.Ordinal);
    }

    private static string? FindGroupForNode(string NodeId, DiagramContract Contract)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                return Node.ParentGroupId;
            }
        }

        return null;
    }

    private static int CountVisibleEdges(DiagramContract Contract, string NodeId, bool Outgoing)
    {
        int Count = 0;
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Outgoing)
            {
                if (string.Equals(Candidate.SourceId, NodeId, StringComparison.Ordinal))
                {
                    Count++;
                }
            }
            else if (string.Equals(Candidate.TargetId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static void AppendGuidePoints(
        List<(double X, double Y)> Target,
        List<(double X, double Y)> Source)
    {
        foreach ((double X, double Y) Point in Source)
        {
            Target.Add(Point);
        }
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
}
