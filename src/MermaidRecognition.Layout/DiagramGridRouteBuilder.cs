using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds orthogonal grid routes with obstacle-aware elbow and Z-shape selection.
/// </summary>
public static class DiagramGridRouteBuilder
{
    /// <summary>
    /// Radius for rounded corners when the route is drawn.
    /// </summary>
    public const int CORNER_RADIUS_PX = 20;

    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double HOOK_DOT_PRODUCT_THRESHOLD = -0.15;

    /// <summary>
    /// Rebuilds every edge route from assigned ports using obstacle-aware orthogonal routing.
    /// </summary>
    internal static void RebuildAllRoutes(
        FlowchartLayoutResult Copy,
        List<DiagramGridLayout.GridNode> Placed,
        Dictionary<string, NodeContract> Nodes,
        DiagramContract Contract,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBounds(Placed, Frames);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            string? SourceShape = Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

            double StartX = Route.Points[0].X;
            double StartY = Route.Points[0].Y;
            double EndX = Route.Points[Route.Points.Count - 1].X;
            double EndY = Route.Points[Route.Points.Count - 1].Y;

            List<(double X, double Y)> Path = BuildRoute(
                StartX,
                StartY,
                SourceBounds,
                SourceShape,
                EndX,
                EndY,
                TargetBounds,
                TargetShape,
                BoundsById,
                Route.SourceId,
                Route.TargetId,
                Contract,
                Direction,
                Horizontal);

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in Path)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static List<(double X, double Y)> BuildRoute(
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        double SourceStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds);
        double TargetStubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(TargetBounds);
        (double SourceStubX, double SourceStubY) = FlowchartPerpendicularRouteBuilder.ResolveStubOuterPoint(
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            SourceStubLength);
        (double TargetStubX, double TargetStubY) = FlowchartPerpendicularRouteBuilder.ResolveStubOuterPoint(
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            TargetStubLength);

        List<(double X, double Y)> Best = [];
        int BestScore = int.MaxValue;
        foreach (List<(double X, double Y)> Candidate in EnumerateCandidates(
                     StartX,
                     StartY,
                     SourceStubX,
                     SourceStubY,
                     TargetStubX,
                     TargetStubY,
                     EndX,
                     EndY,
                     BoundsById,
                     SourceId,
                     TargetId,
                     Contract,
                     Direction,
                     Horizontal))
        {
            List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
            if (!PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
            {
                continue;
            }

            int Score = ScorePath(Simplified);
            if (Score < BestScore)
            {
                BestScore = Score;
                Best = Simplified;
            }
        }

        if (Best.Count > 0)
        {
            return Best;
        }

        List<(double X, double Y)> Fallback = [];
        FlowchartPerpendicularRouteBuilder.TryAppendStraightOrObstacleAvoidingRoute(
            Fallback,
            StartX,
            StartY,
            SourceBounds,
            SourceShape,
            EndX,
            EndY,
            TargetBounds,
            TargetShape,
            Direction,
            BoundsById,
            SourceId,
            TargetId);
        List<(double X, double Y)> SimplifiedFallback = SimplifyCollinear(Fallback);
        if (PathIsValid(SimplifiedFallback, BoundsById, SourceId, TargetId, Contract))
        {
            return SimplifiedFallback;
        }

        return BuildForcedCorridorRoute(
            StartX,
            StartY,
            SourceStubX,
            SourceStubY,
            TargetStubX,
            TargetStubY,
            EndX,
            EndY,
            BoundsById,
            SourceId,
            TargetId,
            Contract,
            Horizontal);
    }

    private static List<(double X, double Y)> BuildForcedCorridorRoute(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        bool Horizontal)
    {
        if (!Horizontal)
        {
            double LeftCorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            double RightCorridorX = ResolveCorridorXOnOppositeSide(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            List<List<(double X, double Y)>> CorridorCandidates =
            [
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (LeftCorridorX, SourceStubY),
                    (LeftCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ],
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (RightCorridorX, SourceStubY),
                    (RightCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]
            ];

            foreach (List<(double X, double Y)> Candidate in CorridorCandidates)
            {
                List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
                if (PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
                {
                    return Simplified;
                }
            }
        }
        else
        {
            double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubX,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            List<(double X, double Y)> Candidate =
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (SourceStubX, CorridorY),
                (TargetStubX, CorridorY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ];
            List<(double X, double Y)> Simplified = SimplifyCollinear(Candidate);
            if (PathIsValid(Simplified, BoundsById, SourceId, TargetId, Contract))
            {
                return Simplified;
            }
        }

        return SimplifyCollinear(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);
    }

    private static IEnumerable<List<(double X, double Y)>> EnumerateCandidates(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        List<List<(double X, double Y)>> Results = BuildStubBridgeCandidates(
            StartX,
            StartY,
            SourceStubX,
            SourceStubY,
            TargetStubX,
            TargetStubY,
            EndX,
            EndY);

        if (!Horizontal)
        {
            double CorridorX = FlowchartRouteObstacleHelper.ResolveCorridorXBesideVerticalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(CorridorX - SourceStubX) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (CorridorX, SourceStubY),
                    (CorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }

            double RightCorridorX = ResolveCorridorXOnOppositeSide(
                SourceStubX,
                SourceStubY,
                TargetStubY,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(RightCorridorX - SourceStubX) > COLLINEAR_EPSILON_PX
                && Math.Abs(RightCorridorX - CorridorX) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (RightCorridorX, SourceStubY),
                    (RightCorridorX, TargetStubY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }
        }
        else
        {
            double CorridorY = FlowchartRouteObstacleHelper.ResolveCorridorYAboveHorizontalObstacles(
                SourceStubX,
                SourceStubY,
                TargetStubX,
                SourceId,
                TargetId,
                BoundsById,
                FlowchartPerpendicularRouteBuilder.OBSTACLE_AVOIDANCE_CLEARANCE_PX);
            if (Math.Abs(CorridorY - SourceStubY) > COLLINEAR_EPSILON_PX)
            {
                Results.Add(
                [
                    (StartX, StartY),
                    (SourceStubX, SourceStubY),
                    (SourceStubX, CorridorY),
                    (TargetStubX, CorridorY),
                    (TargetStubX, TargetStubY),
                    (EndX, EndY)
                ]);
            }
        }

        return Results;
    }

    private static List<List<(double X, double Y)>> BuildStubBridgeCandidates(
        double StartX,
        double StartY,
        double SourceStubX,
        double SourceStubY,
        double TargetStubX,
        double TargetStubY,
        double EndX,
        double EndY)
    {
        List<List<(double X, double Y)>> Results = [];
        if (Math.Abs(StartX - EndX) <= COLLINEAR_EPSILON_PX || Math.Abs(StartY - EndY) <= COLLINEAR_EPSILON_PX)
        {
            Results.Add([(StartX, StartY), (EndX, EndY)]);
        }

        if (Math.Abs(SourceStubX - TargetStubX) <= COLLINEAR_EPSILON_PX
            || Math.Abs(SourceStubY - TargetStubY) <= COLLINEAR_EPSILON_PX)
        {
            Results.Add(
            [
                (StartX, StartY),
                (SourceStubX, SourceStubY),
                (TargetStubX, TargetStubY),
                (EndX, EndY)
            ]);
        }

        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (TargetStubX, SourceStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);

        double MidX = Math.Round((SourceStubX + TargetStubX) / 2.0);
        double MidY = Math.Round((SourceStubY + TargetStubY) / 2.0);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (MidX, SourceStubY),
            (MidX, TargetStubY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);
        Results.Add(
        [
            (StartX, StartY),
            (SourceStubX, SourceStubY),
            (SourceStubX, MidY),
            (TargetStubX, MidY),
            (TargetStubX, TargetStubY),
            (EndX, EndY)
        ]);

        return Results;
    }

    private static double ResolveCorridorXOnOppositeSide(
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
            if (NodeRight + ClearancePx > CorridorX)
            {
                CorridorX = NodeRight + ClearancePx;
            }
        }

        if (!HasObstacle)
        {
            return ChordX;
        }

        return CorridorX;
    }

    private static bool PathIsValid(
        List<(double X, double Y)> Path,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Path))
        {
            return false;
        }

        if (HasEndpointHook(Path))
        {
            return false;
        }

        if (CountCorners(Path) > 4)
        {
            return false;
        }

        for (int Index = 1; Index < Path.Count; Index++)
        {
            (double SegmentStartX, double SegmentStartY) = Path[Index - 1];
            (double SegmentEndX, double SegmentEndY) = Path[Index];
            if (SegmentCrossesAnyNodeInterior(
                    Index - 1,
                    Path.Count - 1,
                    SegmentStartX,
                    SegmentStartY,
                    SegmentEndX,
                    SegmentEndY,
                    BoundsById,
                    SourceId,
                    TargetId,
                    Contract))
            {
                return false;
            }
        }

        return true;
    }

    private static int ScorePath(List<(double X, double Y)> Path)
    {
        int Score = CountCorners(Path) * 100;
        for (int Index = 1; Index < Path.Count; Index++)
        {
            double Length = Math.Abs(Path[Index].X - Path[Index - 1].X)
                + Math.Abs(Path[Index].Y - Path[Index - 1].Y);
            Score += (int)Math.Round(Length / 10.0);
        }

        return Score;
    }

    private static bool SegmentCrossesAnyNodeInterior(
        int SegmentIndex,
        int LastSegmentIndex,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        DiagramContract Contract)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            FlowchartLayoutNodeBounds NodeBounds = Entry.Value;
            bool IsSource = string.Equals(NodeBounds.NodeId, SourceId, StringComparison.Ordinal);
            bool IsTarget = string.Equals(NodeBounds.NodeId, TargetId, StringComparison.Ordinal);
            if (IsSource && SegmentIndex == 0)
            {
                continue;
            }

            if (IsTarget && SegmentIndex == LastSegmentIndex)
            {
                continue;
            }

            if (IsGroupFrameObstacle(Contract, NodeBounds.NodeId)
                && DiagramGridLayout.IsInsideGroup(Contract, SourceId, NodeBounds.NodeId)
                && DiagramGridLayout.IsInsideGroup(Contract, TargetId, NodeBounds.NodeId))
            {
                continue;
            }

            if (IsGroupFrameObstacle(Contract, NodeBounds.NodeId)
                && string.Equals(NodeBounds.NodeId, TargetId, StringComparison.Ordinal)
                && SegmentIndex == LastSegmentIndex)
            {
                continue;
            }

            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(StartX, StartY, EndX, EndY, NodeBounds))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGroupFrameObstacle(DiagramContract Contract, string NodeId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountCorners(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return 0;
        }

        int Corners = 0;
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            bool IncomingHorizontal = Math.Abs(Path[Index].X - Path[Index - 1].X) > COLLINEAR_EPSILON_PX;
            bool OutgoingHorizontal = Math.Abs(Path[Index + 1].X - Path[Index].X) > COLLINEAR_EPSILON_PX;
            if (IncomingHorizontal != OutgoingHorizontal)
            {
                Corners++;
            }
        }

        return Corners;
    }

    private static bool HasEndpointHook(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return false;
        }

        return HasDirectionReversalNear(Path, NearStart: true) || HasDirectionReversalNear(Path, NearStart: false);
    }

    private static bool HasDirectionReversalNear(List<(double X, double Y)> Path, bool NearStart)
    {
        int SegmentCount = Path.Count - 1;
        if (SegmentCount < 2)
        {
            return false;
        }

        int StartIndex = NearStart ? 0 : Math.Max(0, SegmentCount - 2);
        int EndIndex = NearStart ? Math.Min(1, SegmentCount - 1) : SegmentCount - 1;
        for (int SegmentIndex = StartIndex; SegmentIndex < EndIndex; SegmentIndex++)
        {
            (double Ax, double Ay) = Normalize(
                Path[SegmentIndex + 1].X - Path[SegmentIndex].X,
                Path[SegmentIndex + 1].Y - Path[SegmentIndex].Y);
            (double Bx, double By) = Normalize(
                Path[SegmentIndex + 2].X - Path[SegmentIndex + 1].X,
                Path[SegmentIndex + 2].Y - Path[SegmentIndex + 1].Y);
            if ((Ax * Bx) + (Ay * By) < HOOK_DOT_PRODUCT_THRESHOLD)
            {
                return true;
            }
        }

        return false;
    }

    private static (double X, double Y) Normalize(double VectorX, double VectorY)
    {
        double Length = Math.Sqrt((VectorX * VectorX) + (VectorY * VectorY));
        if (Length <= 1e-6)
        {
            return (0.0, 0.0);
        }

        return (VectorX / Length, VectorY / Length);
    }

    private static List<(double X, double Y)> SimplifyCollinear(List<(double X, double Y)> Path)
    {
        if (Path.Count < 3)
        {
            return Path;
        }

        List<(double X, double Y)> Result = [Path[0]];
        for (int Index = 1; Index < Path.Count - 1; Index++)
        {
            (double Ax, double Ay) = Result[Result.Count - 1];
            (double Bx, double By) = Path[Index];
            (double Cx, double Cy) = Path[Index + 1];
            double Cross = ((Bx - Ax) * (Cy - By)) - ((By - Ay) * (Cx - Bx));
            double Dot = ((Bx - Ax) * (Cx - Bx)) + ((By - Ay) * (Cy - By));
            if (Math.Abs(Cross) < 1e-3 && Dot >= 0.0)
            {
                continue;
            }

            Result.Add(Path[Index]);
        }

        Result.Add(Path[Path.Count - 1]);
        return Result;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildBounds(
        List<DiagramGridLayout.GridNode> Placed,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }
}
