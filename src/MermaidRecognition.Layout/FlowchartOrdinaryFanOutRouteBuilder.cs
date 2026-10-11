using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// B14 ordinary-box fan-out: downstream exit, upstream entry, and rank-gap Z routes.
/// </summary>
public static class FlowchartOrdinaryFanOutRouteBuilder
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Returns the downstream exit and upstream entry sides for the diagram direction.
    /// </summary>
    public static bool TryGetFlowSides(FlowchartDirection Direction, out string ExitSide, out string EntrySide)
    {
        switch (Direction)
        {
            case FlowchartDirection.LeftToRight:
                ExitSide = "right";
                EntrySide = "left";
                return true;
            case FlowchartDirection.RightToLeft:
                ExitSide = "left";
                EntrySide = "right";
                return true;
            case FlowchartDirection.TopToBottom:
                ExitSide = "bottom";
                EntrySide = "top";
                return true;
            case FlowchartDirection.BottomToTop:
                ExitSide = "top";
                EntrySide = "bottom";
                return true;
            default:
                ExitSide = string.Empty;
                EntrySide = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Returns true when the source is an ordinary rectangle hub with 2–3 outward fan-out branches.
    /// </summary>
    public static bool TryIsOrdinaryFanOutHub(
        DiagramContract Contract,
        string SourceId,
        string? SourceShape,
        FlowchartDirection Direction,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (IsDecisionShape(SourceShape)
            || !BoundsById.TryGetValue(SourceId, out FlowchartLayoutNodeBounds SourceBounds))
        {
            return false;
        }

        List<EdgeContract> Outgoing = GetVisibleEdgesFromSource(Contract, SourceId);
        if (Outgoing.Count < 2 || Outgoing.Count > 3)
        {
            return false;
        }

        return !HasForwardPathBetweenTargets(Contract, Outgoing)
            && AllTargetsAreDownstream(Outgoing, BoundsById, SourceBounds, Direction)
            && AllTargetsFavorDownstreamFace(Outgoing, BoundsById, SourceBounds, Direction);
    }

    internal static bool HasForwardPathBetweenTargets(DiagramContract Contract, List<EdgeContract> Outgoing)
    {
        // A direct link that skips part of a chain is a bypass, not an
        // independent fan-out branch. Keep it on obstacle-aware routing.
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        HashSet<string> Targets = new(Outgoing.ConvertAll(Edge => Edge.TargetId), StringComparer.Ordinal);
        foreach (string Target in Targets)
        {
            HashSet<string> Visited = new(StringComparer.Ordinal) { Target, Outgoing[0].SourceId };
            Queue<string> Pending = new();
            Pending.Enqueue(Target);
            while (Pending.Count > 0)
            {
                string Current = Pending.Dequeue();
                foreach (EdgeContract Edge in Contract.Edges)
                {
                    if (Edge.SourceId != Current || BackEdges.Contains(Edge)
                        || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                        || !Visited.Add(Edge.TargetId))
                    {
                        continue;
                    }
                    if (Targets.Contains(Edge.TargetId)) return true;
                    Pending.Enqueue(Edge.TargetId);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Builds B14 layout points for an ordinary fan-out branch.
    /// </summary>
    public static bool TryBuildEdgeRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];

        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
            || !BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds)
            || !TryIsOrdinaryFanOutHub(Contract, Edge.SourceId, SourceShape, Direction, BoundsById))
        {
            return false;
        }

        List<EdgeContract> Outgoing = GetVisibleEdgesFromSource(Contract, Edge.SourceId);
        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        bool BottomToTop = Direction == FlowchartDirection.BottomToTop;
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight || RightToLeft;

        if (HorizontalFlow)
        {
            SortOutgoingByTargetCrossAxisDescending(Outgoing, BoundsById);
        }
        else
        {
            SortOutgoingByTargetCrossAxisAscending(Outgoing, BoundsById);
        }

        int BranchIndex = Outgoing.FindIndex(
            Candidate => string.Equals(Candidate.TargetId, Edge.TargetId, StringComparison.Ordinal));
        if (BranchIndex < 0)
        {
            return false;
        }

        double SourceX;
        double SourceY;
        double TargetX;
        double TargetY;
        if (HorizontalFlow)
        {
            (SourceX, SourceY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanOutSourceAttach(
                SourceBounds,
                BranchIndex,
                Outgoing.Count,
                RightToLeft,
                Edge.MarkerStart);
            (TargetX, TargetY) = FlowchartHorizontalRouteGuideBuilder.ResolveLrFanOutTargetAttach(
                TargetBounds,
                RightToLeft,
                Edge.MarkerEnd);
            AppendHorizontalRankGapZRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                RightToLeft);
        }
        else
        {
            (SourceX, SourceY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanOutSourceAttach(
                SourceBounds,
                BranchIndex,
                Outgoing.Count,
                Direction,
                Edge.MarkerStart,
                OutgoingFromSource: Outgoing.Count);
            (TargetX, TargetY) = FlowchartVerticalRouteGuideBuilder.ResolveVerticalHubFanOutTargetAttach(
                TargetBounds,
                Direction,
                Edge.MarkerEnd);
            AppendVerticalRankGapZRoute(
                LayoutPoints,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape,
                BottomToTop);
        }

        return LayoutPoints.Count >= 2;
    }

    /// <summary>
    /// B14 LR/RL: horizontal stub out, vertical at the rank-gap midpoint, horizontal into upstream face centre.
    /// </summary>
    public static void AppendHorizontalRankGapZRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        bool RightToLeft)
    {
        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(SourceX, SourceY, TargetX, TargetY))
        {
            FlowchartPerpendicularRouteBuilder.AppendPerpendicularRoute(
                Points,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
            return;
        }

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
        double CorridorX = ResolveHorizontalRankGapMidpointX(SourceBounds, TargetBounds, RightToLeft);

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(CorridorX - SourceStubX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, SourceStubY));
        }

        if (Math.Abs(TargetStubY - SourceStubY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, TargetStubY));
        }

        if (Math.Abs(TargetStubX - CorridorX) > COLLINEAR_EPSILON_PX
            || Math.Abs(TargetStubY - SourceStubY) <= COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// B14 TB/BT mirror: vertical stub out, horizontal at the rank-gap midpoint, vertical into upstream face centre.
    /// </summary>
    public static void AppendVerticalRankGapZRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        bool BottomToTop)
    {
        if (FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(SourceX, SourceY, TargetX, TargetY))
        {
            FlowchartPerpendicularRouteBuilder.AppendPerpendicularRoute(
                Points,
                SourceX,
                SourceY,
                SourceBounds,
                SourceShape,
                TargetX,
                TargetY,
                TargetBounds,
                TargetShape);
            return;
        }

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
        double CorridorY = ResolveVerticalRankGapMidpointY(SourceBounds, TargetBounds, BottomToTop);

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(CorridorY - SourceStubY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((SourceStubX, CorridorY));
        }

        if (Math.Abs(TargetStubX - SourceStubX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, CorridorY));
        }

        if (Math.Abs(TargetStubY - CorridorY) > COLLINEAR_EPSILON_PX
            || Math.Abs(TargetStubX - SourceStubX) <= COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((TargetX, TargetY));
    }

    private static double ResolveHorizontalRankGapMidpointX(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool RightToLeft)
    {
        double SourceEdge = RightToLeft
            ? SourceBounds.CenterX - (SourceBounds.Width / 2.0)
            : SourceBounds.CenterX + (SourceBounds.Width / 2.0);
        double TargetEdge = RightToLeft
            ? TargetBounds.CenterX + (TargetBounds.Width / 2.0)
            : TargetBounds.CenterX - (TargetBounds.Width / 2.0);

        return (SourceEdge + TargetEdge) / 2.0;
    }

    private static double ResolveVerticalRankGapMidpointY(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool BottomToTop)
    {
        double SourceEdge = BottomToTop
            ? SourceBounds.CenterY + (SourceBounds.Height / 2.0)
            : SourceBounds.CenterY - (SourceBounds.Height / 2.0);
        double TargetEdge = BottomToTop
            ? TargetBounds.CenterY - (TargetBounds.Height / 2.0)
            : TargetBounds.CenterY + (TargetBounds.Height / 2.0);

        return (SourceEdge + TargetEdge) / 2.0;
    }

    private static bool AllTargetsFavorDownstreamFace(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartDirection Direction)
    {
        if (!TryGetFlowSides(Direction, out string ExitSide, out _))
        {
            return false;
        }

        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                return false;
            }

            string FacingSide = ResolveFacingSide(SourceBounds, TargetBounds);
            if (!string.Equals(FacingSide, ExitSide, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveFacingSide(
        FlowchartLayoutNodeBounds From,
        FlowchartLayoutNodeBounds To)
    {
        double Dx = To.CenterX - From.CenterX;
        double Dy = To.CenterY - From.CenterY;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            return Dx >= 0 ? "right" : "left";
        }

        return Dy >= 0 ? "top" : "bottom";
    }

    private static bool AllTargetsAreDownstream(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartDirection Direction)
    {
        foreach (EdgeContract OutgoingEdge in Outgoing)
        {
            if (!BoundsById.TryGetValue(OutgoingEdge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                return false;
            }

            switch (Direction)
            {
                case FlowchartDirection.LeftToRight:
                    if (TargetBounds.CenterX <= SourceBounds.CenterX + COLLINEAR_EPSILON_PX)
                    {
                        return false;
                    }

                    break;
                case FlowchartDirection.RightToLeft:
                    if (TargetBounds.CenterX >= SourceBounds.CenterX - COLLINEAR_EPSILON_PX)
                    {
                        return false;
                    }

                    break;
                case FlowchartDirection.TopToBottom:
                    if (TargetBounds.CenterY >= SourceBounds.CenterY - COLLINEAR_EPSILON_PX)
                    {
                        return false;
                    }

                    break;
                case FlowchartDirection.BottomToTop:
                    if (TargetBounds.CenterY <= SourceBounds.CenterY + COLLINEAR_EPSILON_PX)
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static void SortOutgoingByTargetCrossAxisAscending(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById) => Outgoing.Sort((Left, Right) =>
    {
        double LeftCross = GetCrossAxis(Left.TargetId, BoundsById, HorizontalFlow: false);
        double RightCross = GetCrossAxis(Right.TargetId, BoundsById, HorizontalFlow: false);
        int CrossCompare = LeftCross.CompareTo(RightCross);
        return CrossCompare != 0 ? CrossCompare : Left.CreationIndex.CompareTo(Right.CreationIndex);
    });

    private static void SortOutgoingByTargetCrossAxisDescending(
        List<EdgeContract> Outgoing,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById) => Outgoing.Sort((Left, Right) =>
    {
        double LeftCross = GetCrossAxis(Left.TargetId, BoundsById, HorizontalFlow: true);
        double RightCross = GetCrossAxis(Right.TargetId, BoundsById, HorizontalFlow: true);
        int CrossCompare = RightCross.CompareTo(LeftCross);
        return CrossCompare != 0 ? CrossCompare : Left.CreationIndex.CompareTo(Right.CreationIndex);
    });

    private static double GetCrossAxis(
        string NodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow)
    {
        if (BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
        {
            return HorizontalFlow ? Bounds.CenterY : Bounds.CenterX;
        }

        return 0.0;
    }

    private static List<EdgeContract> GetVisibleEdgesFromSource(DiagramContract Contract, string SourceId)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || FlowchartEdgeGuidePointBuilder.UsesBottomFaceCorridor(Candidate, Contract))
            {
                continue;
            }

            if (string.Equals(Candidate.SourceId, SourceId, StringComparison.Ordinal))
            {
                Edges.Add(Candidate);
            }
        }

        return Edges;
    }

    private static (double X, double Y) ComputeStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape)
    {
        double StubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(Bounds);
        return FlowchartPerpendicularRouteBuilder.ResolveStubOuterPoint(
            BoundaryX,
            BoundaryY,
            Bounds,
            Shape,
            StubLength);
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
