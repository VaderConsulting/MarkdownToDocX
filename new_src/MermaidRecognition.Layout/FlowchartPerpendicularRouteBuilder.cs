using System;
using System.Collections.Generic;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds smooth curved edge routes with perpendicular terminal stubs at both ends.
/// </summary>
public static class FlowchartPerpendicularRouteBuilder
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double CURVE_BULGE_RATIO = 0.35;

    /// <summary>
    /// Appends boundary → source stub → tangent extension → outward curve guides → target approach → target stub → boundary.
    /// </summary>
    public static void AppendCurvedPerpendicularRoute(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double RouteHubCenterX)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);

        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        Points.Add((StartX, StartY));
        Points.Add((SourceStubX, SourceStubY));

        double MidY = (SourceStubY + TargetStubY) / 2.0;

        AppendSmoothBulgeGuide(
            Points,
            SourceStubX,
            SourceStubY,
            TargetStubX,
            TargetStubY,
            RouteHubCenterX,
            MidY);

        Points.Add((TargetStubX, TargetStubY));
        Points.Add((EndX, EndY));
    }

    /// <summary>
    /// Appends boundary → source stub → orthogonal corridor at <paramref name="GuideX"/> → target stub → boundary.
    /// </summary>
    public static void AppendOrthogonalPerpendicularRouteWithGuideX(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double GuideX)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);

        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        Points.Add((StartX, StartY));
        Points.Add((SourceStubX, SourceStubY));
        Points.Add((GuideX, SourceStubY));
        Points.Add((GuideX, TargetStubY));
        Points.Add((TargetStubX, TargetStubY));
        Points.Add((EndX, EndY));
    }

    /// <summary>
    /// Appends boundary → source stub → orthogonal bridge → target stub → boundary.
    /// </summary>
    public static void AppendPerpendicularRoute(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);

        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        Points.Add((StartX, StartY));
        Points.Add((SourceStubX, SourceStubY));
        AppendOrthogonalBridge(Points, SourceStubX, SourceStubY, TargetStubX, TargetStubY);
        Points.Add((TargetStubX, TargetStubY));
        Points.Add((EndX, EndY));
    }

    /// <summary>
    /// Returns true when every segment is axis-aligned (orthogonal polyline).
    /// </summary>
    public static bool IsOrthogonalPolyline(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            double DeltaX = Math.Abs(Points[Index].X - Points[Index - 1].X);
            double DeltaY = Math.Abs(Points[Index].Y - Points[Index - 1].Y);
            if (DeltaX <= COLLINEAR_EPSILON_PX && DeltaY <= COLLINEAR_EPSILON_PX)
            {
                return false;
            }

            if (DeltaX > COLLINEAR_EPSILON_PX && DeltaY > COLLINEAR_EPSILON_PX)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the outer end of a perpendicular terminal stub from a boundary attach point.
    /// </summary>
    public static (double X, double Y) ResolveStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double StubLength)
    {
        return ComputeStubOuterPoint(BoundaryX, BoundaryY, Bounds, Shape, StubLength);
    }

    private static (double X, double Y) ComputeStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double StubLength)
    {
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

    public const double OBSTACLE_AVOIDANCE_CLEARANCE_PX = 24.0;

    /// <summary>
    /// Appends a straight segment when clear, otherwise an exterior perpendicular corridor that avoids node bodies.
    /// </summary>
    public static bool TryAppendStraightOrObstacleAvoidingRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        if (!FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                SourceX,
                SourceY,
                TargetX,
                TargetY,
                BoundsById,
                SourceNodeId,
                TargetNodeId))
        {
            AppendPerpendicularRoute(
                Points,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
            return true;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            double ChordY = Math.Max(SourceY, TargetY);
            double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
                SourceX,
                ChordY,
                TargetX,
                SourceNodeId,
                TargetNodeId,
                BoundsById,
                OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            AppendPerpendicularRouteWithCorridorY(
                Points,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                CorridorY);
        }
        else
        {
            double CorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceX,
                SourceY,
                TargetY,
                SourceNodeId,
                TargetNodeId,
                BoundsById,
                OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            AppendOrthogonalPerpendicularRouteWithGuideX(
                Points,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                CorridorX);
        }

        return Points.Count >= 2;
    }

    /// <summary>
    /// Appends an orthogonal bridge through a fixed corridor Y (LR retry / opposite-port routes).
    /// </summary>
    public static void AppendPerpendicularRouteWithCorridorY(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double CorridorY)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);

        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        Points.Add((StartX, StartY));
        Points.Add((SourceStubX, SourceStubY));

        FlowchartEdgeAttachFace SourceFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            SourceBounds,
            StartX,
            StartY);
        bool SourceUsesVerticalStub = SourceFace == FlowchartEdgeAttachFace.Top
            || SourceFace == FlowchartEdgeAttachFace.Bottom;

        if (SourceUsesVerticalStub)
        {
            Points.Add((SourceStubX, CorridorY));
            Points.Add((TargetStubX, CorridorY));
        }
        else
        {
            double MidX = (SourceStubX + TargetStubX) / 2.0;
            Points.Add((MidX, SourceStubY));
            Points.Add((MidX, CorridorY));
            Points.Add((TargetStubX, CorridorY));
        }

        Points.Add((TargetStubX, TargetStubY));
        Points.Add((EndX, EndY));
    }

    private static void AppendOrthogonalBridge(
        List<(double X, double Y)> Points,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        if (Math.Abs(EndX - StartX) <= COLLINEAR_EPSILON_PX
            || Math.Abs(EndY - StartY) <= COLLINEAR_EPSILON_PX)
        {
            return;
        }

        double MidY = (StartY + EndY) / 2.0;
        Points.Add((StartX, MidY));
        Points.Add((EndX, MidY));
    }

    private static void AppendSmoothBulgeGuide(
        List<(double X, double Y)> Points,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double RouteHubCenterX,
        double MidY)
    {
        double BulgeX = FlowchartCurveBulgeHelper.ResolveVerticalFlowBulgeX(
            SourceStubX,
            TargetStubX,
            RouteHubCenterX,
            CURVE_BULGE_RATIO);

        Points.Add((BulgeX, MidY));
    }

    private static bool TryResolveBoundaryOutwardNormal(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double BoundaryX,
        double BoundaryY,
        out double NormalX,
        out double NormalY)
    {
        NormalX = 0.0;
        NormalY = 0.0;

        if (FlowchartDiamondAttachHelper.IsDiamondShape(Shape)
            && FlowchartDiamondAttachHelper.TryResolveOutwardNormal(
                Bounds,
                BoundaryX,
                BoundaryY,
                out NormalX,
                out NormalY))
        {
            return true;
        }

        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            BoundaryX,
            BoundaryY);

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                NormalX = 0.0;
                NormalY = 1.0;
                return true;

            case FlowchartEdgeAttachFace.Bottom:
                NormalX = 0.0;
                NormalY = -1.0;
                return true;

            case FlowchartEdgeAttachFace.Left:
                NormalX = -1.0;
                NormalY = 0.0;
                return true;

            case FlowchartEdgeAttachFace.Right:
                NormalX = 1.0;
                NormalY = 0.0;
                return true;

            default:
                return false;
        }
    }
}
