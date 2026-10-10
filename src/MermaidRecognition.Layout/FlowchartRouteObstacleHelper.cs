using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Geometric obstacle tests for route classification and validation.
/// </summary>
public static class FlowchartRouteObstacleHelper
{
    /// <summary>
    /// Returns true when a line segment intersects an axis-aligned rectangle interior.
    /// </summary>
    public static bool SegmentIntersectsRectangleInterior(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double RectangleLeft,
        double RectangleTop,
        double RectangleRight,
        double RectangleBottom)
    {
        if (SegmentIntersectsRectangle(StartX, StartY, EndX, EndY, RectangleLeft, RectangleTop, RectangleRight, RectangleBottom))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when the centre chord between two nodes crosses the spine corridor interior.
    /// </summary>
    public static bool CentreChordIntersectsSpineCorridor(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartSpineCorridorBounds Corridor) => SegmentIntersectsRectangleInterior(
            SourceBounds.CenterX,
            SourceBounds.CenterY,
            TargetBounds.CenterX,
            TargetBounds.CenterY,
            Corridor.Left,
            Corridor.Top,
            Corridor.Right,
            Corridor.Bottom);

    /// <summary>
    /// Returns true when a horizontal chord between two attach points intersects an unrelated node body.
    /// </summary>
    public static bool HorizontalChordIntersectsUnrelatedNode(
        double StartX,
        double ChordY,
        double EndX,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            if (NodeRight <= FlowMinX || NodeLeft >= FlowMaxX)
            {
                continue;
            }

            if (SegmentIntersectsNodeInterior(StartX, ChordY, EndX, ChordY, CandidateBounds))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves a corridor Y above intervening node bodies on a horizontal span.
    /// </summary>
    public static double ResolveCorridorYAboveHorizontalObstacles(
        double StartX,
        double ChordY,
        double EndX,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double ClearancePx)
    {
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);
        double CorridorY = ChordY;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            if (NodeRight <= FlowMinX || NodeLeft >= FlowMaxX)
            {
                continue;
            }

            if (!SegmentIntersectsNodeInterior(StartX, ChordY, EndX, ChordY, CandidateBounds))
            {
                continue;
            }

            double NodeTop = CandidateBounds.CenterY + (CandidateBounds.Height / 2.0);
            if (NodeTop > CorridorY)
            {
                CorridorY = NodeTop;
            }
        }

        return CorridorY + ClearancePx;
    }

    /// <summary>
    /// Resolves a corridor Y below intervening node bodies on a horizontal span.
    /// </summary>
    public static double ResolveCorridorYBelowHorizontalObstacles(
        double StartX,
        double ChordY,
        double EndX,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double ClearancePx)
    {
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);
        double CorridorY = ChordY;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            if (NodeRight <= FlowMinX || NodeLeft >= FlowMaxX)
            {
                continue;
            }

            if (!SegmentIntersectsNodeInterior(StartX, ChordY, EndX, ChordY, CandidateBounds))
            {
                continue;
            }

            double NodeBottom = CandidateBounds.CenterY - (CandidateBounds.Height / 2.0);
            if (NodeBottom < CorridorY)
            {
                CorridorY = NodeBottom;
            }
        }

        return CorridorY - ClearancePx;
    }

    /// <summary>
    /// Returns true when a line segment intersects a node bounding box interior (excluding attach tolerance).
    /// </summary>
    public static bool SegmentIntersectsNodeInterior(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds NodeBounds,
        double InsetPx = 2.0)
    {
        double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0) + InsetPx;
        double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0) - InsetPx;
        double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0) - InsetPx;
        double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0) + InsetPx;
        return SegmentIntersectsRectangleInterior(StartX, StartY, EndX, EndY, Left, Top, Right, Bottom);
    }

    private static bool SegmentIntersectsRectangle(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double RectangleLeft,
        double RectangleTop,
        double RectangleRight,
        double RectangleBottom)
    {
        if (PointInsideRectangle(StartX, StartY, RectangleLeft, RectangleTop, RectangleRight, RectangleBottom)
            || PointInsideRectangle(EndX, EndY, RectangleLeft, RectangleTop, RectangleRight, RectangleBottom))
        {
            return true;
        }

        return LineIntersectsLine(
                StartX,
                StartY,
                EndX,
                EndY,
                RectangleLeft,
                RectangleTop,
                RectangleRight,
                RectangleTop)
            || LineIntersectsLine(
                StartX,
                StartY,
                EndX,
                EndY,
                RectangleRight,
                RectangleTop,
                RectangleRight,
                RectangleBottom)
            || LineIntersectsLine(
                StartX,
                StartY,
                EndX,
                EndY,
                RectangleRight,
                RectangleBottom,
                RectangleLeft,
                RectangleBottom)
            || LineIntersectsLine(
                StartX,
                StartY,
                EndX,
                EndY,
                RectangleLeft,
                RectangleBottom,
                RectangleLeft,
                RectangleTop);
    }

    private static bool PointInsideRectangle(
        double PointX,
        double PointY,
        double RectangleLeft,
        double RectangleTop,
        double RectangleRight,
        double RectangleBottom) => PointX >= RectangleLeft
            && PointX <= RectangleRight
            && PointY <= RectangleTop
            && PointY >= RectangleBottom;

    private static bool LineIntersectsLine(
        double Line1StartX,
        double Line1StartY,
        double Line1EndX,
        double Line1EndY,
        double Line2StartX,
        double Line2StartY,
        double Line2EndX,
        double Line2EndY)
    {
        double Denominator = ((Line1EndX - Line1StartX) * (Line2EndY - Line2StartY))
            - ((Line1EndY - Line1StartY) * (Line2EndX - Line2StartX));
        if (Math.Abs(Denominator) < 1e-9)
        {
            return false;
        }

        double ParameterA = (((Line2StartX - Line1StartX) * (Line2EndY - Line2StartY))
            - ((Line2StartY - Line1StartY) * (Line2EndX - Line2StartX))) / Denominator;
        double ParameterB = (((Line2StartX - Line1StartX) * (Line1EndY - Line1StartY))
            - ((Line2StartY - Line1StartY) * (Line1EndX - Line1StartX))) / Denominator;
        return ParameterA >= 0.0
            && ParameterA <= 1.0
            && ParameterB >= 0.0
            && ParameterB <= 1.0;
    }

    /// <summary>
    /// Returns true when a segment between two attach points crosses any unrelated node body.
    /// </summary>
    public static bool SegmentIntersectsUnrelatedNodeBodies(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceNodeId,
        string TargetNodeId)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (SegmentIntersectsNodeInterior(StartX, StartY, EndX, EndY, CandidateBounds))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves an exterior corridor X to the left of intervening node bodies on a vertical span.
    /// </summary>
    public static double ResolveCorridorXBesideVerticalObstacles(
        double ChordX,
        double StartY,
        double EndY,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double ClearancePx)
    {
        double FlowMinY = Math.Min(StartY, EndY);
        double FlowMaxY = Math.Max(StartY, EndY);
        double CorridorX = ChordX;
        bool HasObstacle = false;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            double NodeTop = CandidateBounds.CenterY + (CandidateBounds.Height / 2.0);
            double NodeBottom = CandidateBounds.CenterY - (CandidateBounds.Height / 2.0);
            if (NodeTop <= FlowMinY || NodeBottom >= FlowMaxY)
            {
                continue;
            }

            if (ChordX < NodeLeft || ChordX > NodeRight)
            {
                continue;
            }

            HasObstacle = true;
            if (NodeLeft - ClearancePx < CorridorX)
            {
                CorridorX = NodeLeft - ClearancePx;
            }
        }

        if (!HasObstacle)
        {
            return ChordX;
        }

        return CorridorX;
    }

    /// <summary>
    /// Returns the highest top edge among node bodies intersecting a horizontal span.
    /// </summary>
    public static double ResolveMaximumObstacleTopOnHorizontalSpan(
        double StartX,
        double EndX,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);
        double MaximumTop = double.NegativeInfinity;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            if (NodeRight <= FlowMinX || NodeLeft >= FlowMaxX)
            {
                continue;
            }

            double NodeTop = CandidateBounds.CenterY + (CandidateBounds.Height / 2.0);
            if (NodeTop > MaximumTop)
            {
                MaximumTop = NodeTop;
            }
        }

        return MaximumTop;
    }

    /// <summary>
    /// Returns the highest top edge among node bodies and unrelated cluster boxes on a horizontal span.
    /// </summary>
    public static double ResolveMaximumObstacleTopOnHorizontalSpan(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double StartX,
        double EndX,
        string SourceNodeId,
        string TargetNodeId)
    {
        double MaximumTop = ResolveMaximumObstacleTopOnHorizontalSpan(
            StartX,
            EndX,
            SourceNodeId,
            TargetNodeId,
            BoundsById);

        string? SourceGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(SourceNodeId, Contract);
        string? TargetGroupId = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(TargetNodeId, Contract);
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);

        List<FlowchartSubgraphLayoutRect> ClusterRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById);
        foreach (FlowchartSubgraphLayoutRect ClusterRect in ClusterRects)
        {
            if (string.Equals(ClusterRect.GroupId, SourceGroupId, StringComparison.Ordinal)
                || string.Equals(ClusterRect.GroupId, TargetGroupId, StringComparison.Ordinal))
            {
                continue;
            }

            if (ClusterRect.Right <= FlowMinX || ClusterRect.Left >= FlowMaxX)
            {
                continue;
            }

            if (ClusterRect.Top > MaximumTop)
            {
                MaximumTop = ClusterRect.Top;
            }
        }

        return MaximumTop;
    }

    /// <summary>
    /// Returns the lowest bottom edge among node bodies intersecting a horizontal span.
    /// </summary>
    public static double ResolveMaximumObstacleBottomOnHorizontalSpan(
        double StartX,
        double EndX,
        string SourceNodeId,
        string TargetNodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double FlowMinX = Math.Min(StartX, EndX);
        double FlowMaxX = Math.Max(StartX, EndX);
        double MaximumBottom = double.NegativeInfinity;

        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds CandidateBounds = Entry.Value;
            if (string.Equals(CandidateBounds.NodeId, SourceNodeId, StringComparison.Ordinal)
                || string.Equals(CandidateBounds.NodeId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            double NodeLeft = CandidateBounds.CenterX - (CandidateBounds.Width / 2.0);
            double NodeRight = CandidateBounds.CenterX + (CandidateBounds.Width / 2.0);
            if (NodeRight <= FlowMinX || NodeLeft >= FlowMaxX)
            {
                continue;
            }

            double NodeBottom = CandidateBounds.CenterY + (CandidateBounds.Height / 2.0);
            if (NodeBottom > MaximumBottom)
            {
                MaximumBottom = NodeBottom;
            }
        }

        return MaximumBottom;
    }
}
