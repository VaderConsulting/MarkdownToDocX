using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Orthogonal shortest path around padded node and group boxes.
/// Inspired by <c>VisibilityGraph</c>,
/// <c>SingleSourceSingleTargetShortestPathOnVisibilityGraph</c>,
/// and the rectilinear scan grid in <c>VisibilityGraphGenerator</c>.
/// </summary>
public static class FlowchartPaddedVisibilityRouter
{
    internal static long GridPathCallCount { get; private set; }
    internal static double GridPathElapsedMilliseconds { get; private set; }
    internal static int GridPathLastVertexCount { get; private set; }
    internal static int GridPathMaximumVertexCount { get; private set; }
    internal static int GridPathLastVisitedStateCount { get; private set; }
    internal static int GridPathMaximumVisitedStateCount { get; private set; }

    internal static void ResetDiagnostics()
    {
        GridPathCallCount = 0;
        GridPathElapsedMilliseconds = 0;
        GridPathLastVertexCount = 0;
        GridPathMaximumVertexCount = 0;
        GridPathLastVisitedStateCount = 0;
        GridPathMaximumVisitedStateCount = 0;
    }
    private const double OBSTACLE_PADDING_PX = 10.0;

    private const double BEND_PENALTY_FRACTION = 0.04;

    private const double VERTEX_MERGE_PX = 2.0;

    private const double PORT_STUB_PX = 12.0;

    /// <summary>
    /// Routes from the source port to the target port on right-angle channels.
    /// </summary>
    /// <param name="StartX">Source port X.</param>
    /// <param name="StartY">Source port Y.</param>
    /// <param name="EndX">Target port X.</param>
    /// <param name="EndY">Target port Y.</param>
    /// <param name="Obstacles">Boxes the path must not enter.</param>
    /// <param name="Points">Guide points when this method returns true.</param>
    /// <returns>True when a path exists.</returns>
    public static bool TryRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<FlowchartLayoutNodeBounds> Obstacles,
        out List<(double X, double Y)> Points)
    {
        return TryRoute(StartX, StartY, EndX, EndY, Obstacles, null, null, out Points);
    }

    /// <summary>
    /// Routes from the source port to the target port, leaving and entering at a right angle.
    /// Inspired by <c>RectilinearEdgeRouter</c> port stubs.
    /// </summary>
    /// <param name="StartX">Source port X.</param>
    /// <param name="StartY">Source port Y.</param>
    /// <param name="EndX">Target port X.</param>
    /// <param name="EndY">Target port Y.</param>
    /// <param name="Obstacles">Boxes the path must not enter.</param>
    /// <param name="SourceBounds">Source box, or null when the port is already off the box.</param>
    /// <param name="TargetBounds">Target box, or null when the port is already off the box.</param>
    /// <param name="Points">Guide points when this method returns true.</param>
    /// <param name="PortStubPx">Straight clearance reserved outside each endpoint.</param>
    /// <param name="ObstaclePaddingPx">Clearance reserved around obstacle bodies.</param>
    /// <returns>True when a path exists.</returns>
    public static bool TryRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<FlowchartLayoutNodeBounds> Obstacles,
        FlowchartLayoutNodeBounds? SourceBounds,
        FlowchartLayoutNodeBounds? TargetBounds,
        out List<(double X, double Y)> Points,
        double PortStubPx = PORT_STUB_PX,
        double ObstaclePaddingPx = OBSTACLE_PADDING_PX)
    {
        Points = [];
        List<PaddedBox> Boxes = [];
        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            Boxes.Add(Pad(Obstacle, ObstaclePaddingPx));
        }

        (double RouteStartX, double RouteStartY) = OffsetPortOutward(StartX, StartY, SourceBounds, PortStubPx);
        (double RouteEndX, double RouteEndY) = OffsetPortOutward(EndX, EndY, TargetBounds, PortStubPx);
        if (PointInsideAny(RouteStartX, RouteStartY, Boxes))
        {
            RouteStartX = StartX;
            RouteStartY = StartY;
        }

        if (PointInsideAny(RouteEndX, RouteEndY, Boxes))
        {
            RouteEndX = EndX;
            RouteEndY = EndY;
        }

        if (!TryRouteOrthogonal(RouteStartX, RouteStartY, RouteEndX, RouteEndY, Boxes, out List<(double X, double Y)> Routed))
        {
            return false;
        }

        if (Math.Abs(RouteStartX - StartX) > 0.01 || Math.Abs(RouteStartY - StartY) > 0.01)
        {
            Routed.Insert(0, (StartX, StartY));
        }

        if (Math.Abs(RouteEndX - EndX) > 0.01 || Math.Abs(RouteEndY - EndY) > 0.01)
        {
            Routed.Add((EndX, EndY));
        }

        CollapseReversalSpurs(Routed);
        Points = Routed;
        return Points.Count >= 2;
    }

    /// <summary>
    /// Inserts right-angle corners so consecutive points never form a diagonal.
    /// </summary>
    /// <param name="Points">Input points.</param>
    /// <returns>An orthogonal polyline.</returns>
    public static List<(double X, double Y)> EnsureOrthogonal(IReadOnlyList<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Orthogonal = [];
        if (Points.Count == 0)
        {
            return Orthogonal;
        }

        Orthogonal.Add(Points[0]);
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double PreviousX, double PreviousY) = Orthogonal[Orthogonal.Count - 1];
            (double NextX, double NextY) = Points[Index];
            bool Horizontal = Math.Abs(NextY - PreviousY) <= 0.6;
            bool Vertical = Math.Abs(NextX - PreviousX) <= 0.6;
            if (!Horizontal && !Vertical)
            {
                Orthogonal.Add((NextX, PreviousY));
            }

            Orthogonal.Add((NextX, NextY));
        }

        SimplifyColinear(Orthogonal);
        return Orthogonal;
    }

    /// <summary>
    /// Builds the obstacle list for one edge: every box that is not an endpoint or an ancestor group.
    /// </summary>
    /// <param name="Contract">Diagram.</param>
    /// <param name="BoundsById">Node and group bounds.</param>
    /// <param name="Edge">Edge being routed.</param>
    /// <returns>Obstacles for the orthogonal visibility router.</returns>
    public static List<FlowchartLayoutNodeBounds> CollectObstacles(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge)
    {
        List<FlowchartLayoutNodeBounds> Obstacles = [];
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            if (IsSkippedEndpoint(Contract, Edge, Bounds.NodeId))
            {
                continue;
            }

            Obstacles.Add(Bounds);
        }

        return Obstacles;
    }

    private static bool TryRouteOrthogonal(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<PaddedBox> Boxes,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        if (IsAxisAligned(StartX, StartY, EndX, EndY) && SegmentClears(StartX, StartY, EndX, EndY, Boxes))
        {
            Points.Add((StartX, StartY));
            Points.Add((EndX, EndY));
            return true;
        }

        List<(double X, double Y)> ElbowA = [(StartX, StartY), (EndX, StartY), (EndX, EndY)];
        List<(double X, double Y)> ElbowB = [(StartX, StartY), (StartX, EndY), (EndX, EndY)];
        bool ElbowAClear = PolylineClears(ElbowA, Boxes);
        bool ElbowBClear = PolylineClears(ElbowB, Boxes);
        if (ElbowAClear && !ElbowBClear)
        {
            Points = ElbowA;
            return true;
        }

        if (ElbowBClear && !ElbowAClear)
        {
            Points = ElbowB;
            return true;
        }

        if (ElbowAClear && ElbowBClear)
        {
            Points = PathLength(ElbowA) <= PathLength(ElbowB) ? ElbowA : ElbowB;
            return true;
        }

        return TryGridPath(StartX, StartY, EndX, EndY, Boxes, out Points);
    }

    private static bool TryGridPath(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<PaddedBox> Boxes,
        out List<(double X, double Y)> Points)
    {
        GridPathCallCount++;
        if (FlowchartRenderContext.Current.IsSvgEdgePainting)
        {
            FlowchartRenderContext.ReportOperationCount("paint-grid-path-searches", 1);
        }

        Stopwatch Timer = Stopwatch.StartNew();
        Points = [];
        PaddedBoxSpatialIndex Spatial = new(Boxes);
        double MinX = Math.Min(StartX, EndX), MaxX = Math.Max(StartX, EndX);
        double MinY = Math.Min(StartY, EndY), MaxY = Math.Max(StartY, EndY);
        double Margin = 160;
        double FullMinX = Boxes.Count == 0 ? MinX : Math.Min(MinX, Boxes.Min(Box => Box.Left));
        double FullMaxX = Boxes.Count == 0 ? MaxX : Math.Max(MaxX, Boxes.Max(Box => Box.Right));
        double FullMinY = Boxes.Count == 0 ? MinY : Math.Min(MinY, Boxes.Min(Box => Box.Bottom));
        double FullMaxY = Boxes.Count == 0 ? MaxY : Math.Max(MaxY, Boxes.Max(Box => Box.Top));
        while (true)
        {
            double SearchLeft = Math.Max(FullMinX, MinX - Margin);
            double SearchRight = Math.Min(FullMaxX, MaxX + Margin);
            double SearchBottom = Math.Max(FullMinY, MinY - Margin);
            double SearchTop = Math.Min(FullMaxY, MaxY + Margin);
            List<PaddedBox> LocalBoxes = Spatial.Query(SearchLeft, SearchBottom, SearchRight, SearchTop);
            List<(double X, double Y)> Vertices = [ (StartX, StartY), (EndX, EndY) ];
            HashSet<long> VertexKeys = [];
            VertexKeys.Add(PackVertexKey(StartX, StartY));
            VertexKeys.Add(PackVertexKey(EndX, EndY));
            List<double> CornerXs = [], CornerYs = [];
            foreach (PaddedBox Box in LocalBoxes)
            {
                CornerXs.Add(Box.Left); CornerXs.Add(Box.Right);
                CornerYs.Add(Box.Bottom); CornerYs.Add(Box.Top);
                AddVertex(Vertices, VertexKeys, Box.Left, Box.Bottom);
                AddVertex(Vertices, VertexKeys, Box.Left, Box.Top);
                AddVertex(Vertices, VertexKeys, Box.Right, Box.Bottom);
                AddVertex(Vertices, VertexKeys, Box.Right, Box.Top);
            }
            SortUnique(CornerXs); SortUnique(CornerYs);
            // Add only endpoint projections to the obstacle-corner visibility graph.
            // This keeps candidate vertices linear in the local obstacle count.
            foreach (double X in CornerXs)
            {
                AddVertex(Vertices, VertexKeys, X, StartY);
                AddVertex(Vertices, VertexKeys, X, EndY);
            }
            foreach (double Y in CornerYs)
            {
                AddVertex(Vertices, VertexKeys, StartX, Y);
                AddVertex(Vertices, VertexKeys, EndX, Y);
            }
            // Keep only candidates inside this search window; endpoints always remain.
            Vertices = [.. Vertices.Where((Point, Index) => Index < 2
                || (Point.X >= SearchLeft && Point.X <= SearchRight && Point.Y >= SearchBottom && Point.Y <= SearchTop))];
            int Count = Vertices.Count;
            GridPathLastVertexCount = Count;
            GridPathMaximumVertexCount = Math.Max(GridPathMaximumVertexCount, Count);
            List<int>[] Neighbours = [.. Enumerable.Range(0, Count).Select(_ => new List<int>())];
            foreach (IGrouping<double, int> SameX in Enumerable.Range(0, Count).GroupBy(Index => Vertices[Index].X))
            {
                Connect([.. SameX.OrderBy(Index => Vertices[Index].Y)], Vertices, Neighbours, Spatial);
            }

            foreach (IGrouping<double, int> SameY in Enumerable.Range(0, Count).GroupBy(Index => Vertices[Index].Y))
            {
                Connect([.. SameY.OrderBy(Index => Vertices[Index].X)], Vertices, Neighbours, Spatial);
            }

            double Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
            double BendCost = Math.Max(1.0, Manhattan * BEND_PENALTY_FRACTION);
            if (Count >= 2 && TryAStarVisibilityPath(Vertices, Neighbours, BendCost,
                out List<int> Path, out int VisitedStates))
            {
                GridPathLastVisitedStateCount = VisitedStates;
                GridPathMaximumVisitedStateCount = Math.Max(GridPathMaximumVisitedStateCount, VisitedStates);
                foreach (int Index in Path)
                {
                    Points.Add(Vertices[Index]);
                }

                SimplifyColinear(Points);
                GridPathElapsedMilliseconds += Timer.Elapsed.TotalMilliseconds;
                return Points.Count >= 2;
            }
            GridPathLastVisitedStateCount = Count * 3;
            GridPathMaximumVisitedStateCount = Math.Max(GridPathMaximumVisitedStateCount, GridPathLastVisitedStateCount);
            if (SearchLeft <= FullMinX && SearchRight >= FullMaxX
                && SearchBottom <= FullMinY && SearchTop >= FullMaxY)
            {
                GridPathElapsedMilliseconds += Timer.Elapsed.TotalMilliseconds;
                return false;
            }
            Margin *= 2;
        }

    }

    private static void Connect(int[] Ordered, IReadOnlyList<(double X, double Y)> Vertices,
        List<int>[] Neighbours, PaddedBoxSpatialIndex Spatial)
    {
        for (int Index = 1; Index < Ordered.Length; Index++)
        {
            int First = Ordered[Index - 1], Second = Ordered[Index];
            if (!Spatial.SegmentClears(Vertices[First].X, Vertices[First].Y,
                Vertices[Second].X, Vertices[Second].Y))
            {
                continue;
            }

            Neighbours[First].Add(Second);
            Neighbours[Second].Add(First);
        }
    }

    private static bool IsSkippedEndpoint(DiagramContract Contract, EdgeContract Edge, string NodeId)
    {
        if (string.Equals(NodeId, Edge.SourceId, StringComparison.Ordinal)
            || string.Equals(NodeId, Edge.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            return ContainsMember(Contract, Group, Edge.SourceId) || ContainsMember(Contract, Group, Edge.TargetId);
        }

        return false;
    }

    private static bool ContainsMember(DiagramContract Contract, GroupContract Group, string NodeId)
    {
        foreach (string ChildId in Group.ChildNodeIds)
        {
            if (string.Equals(ChildId, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            foreach (GroupContract Child in Contract.Groups)
            {
                if (string.Equals(Child.Id, ChildGroupId, StringComparison.Ordinal)
                    && ContainsMember(Contract, Child, NodeId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (double X, double Y) OffsetPortOutward(
        double X,
        double Y,
        FlowchartLayoutNodeBounds? Bounds,
        double StubPx)
    {
        if (Bounds == null)
        {
            return (X, Y);
        }

        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double ToLeft = Math.Abs(X - Left);
        double ToRight = Math.Abs(X - Right);
        double ToTop = Math.Abs(Y - Top);
        double ToBottom = Math.Abs(Y - Bottom);
        double Best = Math.Min(Math.Min(ToLeft, ToRight), Math.Min(ToTop, ToBottom));
        if (Best == ToLeft)
        {
            return (X - StubPx, Y);
        }

        if (Best == ToRight)
        {
            return (X + StubPx, Y);
        }

        if (Best == ToTop)
        {
            return (X, Y + StubPx);
        }

        return (X, Y - StubPx);
    }

    private static PaddedBox Pad(FlowchartLayoutNodeBounds Bounds, double PaddingPx)
    {
        return new PaddedBox(
            Bounds.CenterX - (Bounds.Width / 2.0) - PaddingPx,
            Bounds.CenterX + (Bounds.Width / 2.0) + PaddingPx,
            Bounds.CenterY + (Bounds.Height / 2.0) + PaddingPx,
            Bounds.CenterY - (Bounds.Height / 2.0) - PaddingPx);
    }

    private static void AddVertex(List<(double X, double Y)> Vertices, HashSet<long> VertexKeys, double X, double Y)
    {
        long Key = PackVertexKey(X, Y);
        if (!VertexKeys.Add(Key))
        {
            return;
        }

        foreach ((double ExistingX, double ExistingY) in Vertices)
        {
            if (Math.Abs(ExistingX - X) <= VERTEX_MERGE_PX && Math.Abs(ExistingY - Y) <= VERTEX_MERGE_PX)
            {
                VertexKeys.Remove(Key);
                return;
            }
        }

        Vertices.Add((X, Y));
    }

    private static long PackVertexKey(double X, double Y)
    {
        int PackedX = (int)Math.Round(X / VERTEX_MERGE_PX);
        int PackedY = (int)Math.Round(Y / VERTEX_MERGE_PX);
        return ((long)PackedX << 32) | (uint)PackedY;
    }

    private static void SortUnique(List<double> Values)
    {
        Values.Sort();
        int Write = 0;
        for (int Read = 0; Read < Values.Count; Read++)
        {
            if (Write == 0 || Math.Abs(Values[Read] - Values[Write - 1]) > VERTEX_MERGE_PX)
            {
                Values[Write] = Values[Read];
                Write++;
            }
        }

        if (Write < Values.Count)
        {
            Values.RemoveRange(Write, Values.Count - Write);
        }
    }

    private static bool IsAxisAligned(double StartX, double StartY, double EndX, double EndY)
    {
        return Math.Abs(StartX - EndX) <= 0.6 || Math.Abs(StartY - EndY) <= 0.6;
    }

    private static bool PointInsideAny(double X, double Y, IReadOnlyList<PaddedBox> Boxes)
    {
        foreach (PaddedBox Box in Boxes)
        {
            if (X > Box.Left + 0.6 && X < Box.Right - 0.6 && Y > Box.Bottom + 0.6 && Y < Box.Top - 0.6)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PolylineClears(IReadOnlyList<(double X, double Y)> Points, IReadOnlyList<PaddedBox> Boxes)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            if (!SegmentClears(Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y, Boxes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentClears(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<PaddedBox> Boxes)
    {
        foreach (PaddedBox Box in Boxes)
        {
            if (SegmentHitsInterior(StartX, StartY, EndX, EndY, Box))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentHitsInterior(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        PaddedBox Box)
    {
        double Left = Box.Left + 0.6;
        double Right = Box.Right - 0.6;
        double Bottom = Box.Bottom + 0.6;
        double Top = Box.Top - 0.6;
        if (Right <= Left || Top <= Bottom)
        {
            return false;
        }

        double MinX = Math.Min(StartX, EndX);
        double MaxX = Math.Max(StartX, EndX);
        double MinY = Math.Min(StartY, EndY);
        double MaxY = Math.Max(StartY, EndY);
        if (MaxX < Left || MinX > Right || MaxY < Bottom || MinY > Top)
        {
            return false;
        }

        double Dx = EndX - StartX;
        double Dy = EndY - StartY;
        if (Math.Abs(Dx) < 0.001 && Math.Abs(Dy) < 0.001)
        {
            return StartX > Left && StartX < Right && StartY > Bottom && StartY < Top;
        }

        double TMin = 0.0;
        double TMax = 1.0;
        if (!ClipAxis(StartX, Dx, Left, Right, ref TMin, ref TMax)
            || !ClipAxis(StartY, Dy, Bottom, Top, ref TMin, ref TMax))
        {
            return false;
        }

        double Mid = (TMin + TMax) / 2.0;
        double MidX = StartX + (Dx * Mid);
        double MidY = StartY + (Dy * Mid);
        return MidX > Left && MidX < Right && MidY > Bottom && MidY < Top;
    }

    private static bool ClipAxis(
        double Start,
        double Delta,
        double Minimum,
        double Maximum,
        ref double TMin,
        ref double TMax)
    {
        if (Math.Abs(Delta) < 0.000001)
        {
            return Start >= Minimum && Start <= Maximum;
        }

        double T1 = (Minimum - Start) / Delta;
        double T2 = (Maximum - Start) / Delta;
        if (T1 > T2)
        {
            double Swap = T1;
            T1 = T2;
            T2 = Swap;
        }

        TMin = Math.Max(TMin, T1);
        TMax = Math.Min(TMax, T2);
        return TMin <= TMax;
    }

    private static bool TryAStarVisibilityPath(
        IReadOnlyList<(double X, double Y)> Vertices,
        IReadOnlyList<List<int>> Neighbours,
        double BendCost,
        out List<int> Path,
        out int VisitedStates)
    {
        Path = [];
        VisitedStates = 0;
        const int Directions = 3; // 0 = source, 1 = horizontal, 2 = vertical.
        int Source = 0, Target = 1;
        int StateCount = Vertices.Count * Directions;
        double[] Distance = [.. Enumerable.Repeat(double.PositiveInfinity, StateCount)];
        int[] Previous = [.. Enumerable.Repeat(-1, StateCount)];
        bool[] Closed = new bool[StateCount];
        int StartState = Source * Directions;
        Distance[StartState] = 0;
        PriorityQueue<int, (double Estimate, double Cost, int State)> Queue = new PriorityQueue<int, (double Estimate, double Cost, int State)>();
        Queue.Enqueue(StartState, (Manhattan(Vertices[Source], Vertices[Target]), 0, StartState));
        int GoalState = -1;
        while (Queue.TryDequeue(out int CurrentState, out (double Estimate, double Cost, int State) Priority))
        {
            if (Closed[CurrentState] || Priority.Cost > Distance[CurrentState])
            {
                continue;
            }

            Closed[CurrentState] = true;
            VisitedStates++;
            int Current = CurrentState / Directions;
            int Direction = CurrentState % Directions;
            if (Current == Target) { GoalState = CurrentState; break; }
            foreach (int Next in Neighbours[Current])
            {
                int NextDirection = Math.Abs(Vertices[Next].X - Vertices[Current].X) <= 0.6 ? 2 : 1;
                int NextState = (Next * Directions) + NextDirection;
                if (Closed[NextState])
                {
                    continue;
                }

                double Length = Manhattan(Vertices[Current], Vertices[Next]);
                double Candidate = Distance[CurrentState] + Length
                    + (Direction != 0 && Direction != NextDirection ? BendCost : 0);
                if (Candidate >= Distance[NextState])
                {
                    continue;
                }

                Distance[NextState] = Candidate;
                Previous[NextState] = CurrentState;
                double Estimate = Candidate + Manhattan(Vertices[Next], Vertices[Target]);
                Queue.Enqueue(NextState, (Estimate, Candidate, NextState));
            }
        }
        if (GoalState < 0)
        {
            return false;
        }

        for (int State = GoalState; State >= 0; State = Previous[State])
        {
            Path.Add(State / Directions);
        }

        Path.Reverse();
        return Path.Count >= 2 && Path[0] == Source && Path[^1] == Target;

        static double Manhattan((double X, double Y) A, (double X, double Y) B) =>
            Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    }

    private static bool SameDirection(
        (double X, double Y) Previous,
        (double X, double Y) Current,
        (double X, double Y) Next)
    {
        double Ax = Current.X - Previous.X;
        double Ay = Current.Y - Previous.Y;
        double Bx = Next.X - Current.X;
        double By = Next.Y - Current.Y;
        double Cross = (Ax * By) - (Ay * Bx);
        double Dot = (Ax * Bx) + (Ay * By);
        return Math.Abs(Cross) < 1.0 && Dot > 0.0;
    }

    private static void SimplifyColinear(List<(double X, double Y)> Points)
    {
        bool Changed = true;
        while (Changed && Points.Count > 2)
        {
            Changed = false;
            for (int Index = 1; Index < Points.Count - 1; Index++)
            {
                if (SameDirection(Points[Index - 1], Points[Index], Points[Index + 1]))
                {
                    Points.RemoveAt(Index);
                    Changed = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Drops vertices that the next point immediately retraces, so each segment ends at its bend.
    /// </summary>
    /// <param name="Points">Orthogonal polyline, edited in place.</param>
    /// <param name="DropCornerOvershoot">Also drop a few-pixel spur past a corner. Used when drawing, where orthogonalising a guide can leave that spur.</param>
    public static void CollapseReversalSpurs(List<(double X, double Y)> Points, bool DropCornerOvershoot = false)
    {
        RemoveReversalSpurs(Points, DropCornerOvershoot);
        TrimTerminalOvershoot(Points);
        SimplifyColinear(Points);
    }

    /// <summary>
    /// Shortens a terminal leg that runs past the final approach and comes back a few pixels.
    /// </summary>
    private static void TrimTerminalOvershoot(List<(double X, double Y)> Points)
    {
        if (Points.Count < 5)
        {
            return;
        }

        int Last = Points.Count - 1;
        (double AX, double AY) = Points[Last - 4];
        (double BX, double BY) = Points[Last - 3];
        (double CX, double CY) = Points[Last - 2];
        (double DX, double DY) = Points[Last - 1];
        bool LongHorizontal = Math.Abs(AY - BY) <= 1.5 && Math.Abs(AX - BX) > 24.0;
        bool LongVertical = Math.Abs(AX - BX) <= 1.5 && Math.Abs(AY - BY) > 24.0;
        double Side = Math.Sqrt(Math.Pow(CX - BX, 2.0) + Math.Pow(CY - BY, 2.0));
        double Return = Math.Sqrt(Math.Pow(DX - CX, 2.0) + Math.Pow(DY - CY, 2.0));
        if (Side > 20.0 || Return > 48.0 || Return < 1.0)
        {
            return;
        }

        if (LongHorizontal
            && Math.Abs(CY - DY) <= 1.5
            && ((DX - CX) * (BX - AX)) < 0.0
            && DX > Math.Min(AX, BX) && DX < Math.Max(AX, BX))
        {
            Points[Last - 3] = (DX, BY);
            Points[Last - 2] = (DX, CY);
            return;
        }

        if (LongVertical
            && Math.Abs(CX - DX) <= 1.5
            && ((DY - CY) * (BY - AY)) < 0.0
            && DY > Math.Min(AY, BY) && DY < Math.Max(AY, BY))
        {
            Points[Last - 3] = (BX, DY);
            Points[Last - 2] = (CX, DY);
        }
    }

    /// <summary>
    /// Drops a vertex that the next point immediately retraces, so a segment ends at its bend.
    /// </summary>
    private static void RemoveReversalSpurs(List<(double X, double Y)> Points, bool DropCornerOvershoot)
    {
        bool Changed = true;
        while (Changed && Points.Count > 2)
        {
            Changed = false;
            for (int Index = 1; Index < Points.Count - 1; Index++)
            {
                (double PreviousX, double PreviousY) = Points[Index - 1];
                (double SpurX, double SpurY) = Points[Index];
                (double NextX, double NextY) = Points[Index + 1];
                if (PointLiesOnSegment(NextX, NextY, PreviousX, PreviousY, SpurX, SpurY)
                    || (DropCornerOvershoot && IsShortAxisOvershoot(PreviousX, PreviousY, SpurX, SpurY, NextX, NextY)))
                {
                    Points.RemoveAt(Index);
                    Changed = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// True when the middle vertex sticks a few pixels past a corner and the route turns back.
    /// </summary>
    private static bool IsShortAxisOvershoot(
        double PreviousX,
        double PreviousY,
        double SpurX,
        double SpurY,
        double NextX,
        double NextY)
    {
        bool SameX = Math.Abs(SpurX - PreviousX) <= 0.6 && Math.Abs(SpurX - NextX) <= 0.6;
        bool SameY = Math.Abs(SpurY - PreviousY) <= 0.6 && Math.Abs(SpurY - NextY) <= 0.6;
        if (SameX == SameY)
        {
            return false;
        }

        double Spur = Math.Sqrt(Math.Pow(SpurX - PreviousX, 2.0) + Math.Pow(SpurY - PreviousY, 2.0));
        double Continuation = Math.Sqrt(Math.Pow(NextX - SpurX, 2.0) + Math.Pow(NextY - SpurY, 2.0));
        if (Spur < FlowchartGeometryPolicy.ReversalSpurMinPx
            || Spur > FlowchartGeometryPolicy.ReversalSpurMaxPx
            || Continuation < FlowchartGeometryPolicy.ReversalSpurMinPx)
        {
            return false;
        }

        if (SameX)
        {
            double Low = Math.Min(PreviousY, NextY);
            double High = Math.Max(PreviousY, NextY);
            return SpurY < Low - 0.6 || SpurY > High + 0.6;
        }

        double Left = Math.Min(PreviousX, NextX);
        double Right = Math.Max(PreviousX, NextX);
        return SpurX < Left - 0.6 || SpurX > Right + 0.6;
    }

    private static bool PointLiesOnSegment(
        double PointX,
        double PointY,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double SegmentX = EndX - StartX;
        double SegmentY = EndY - StartY;
        double LengthSquared = (SegmentX * SegmentX) + (SegmentY * SegmentY);
        if (LengthSquared < 1.0)
        {
            return false;
        }

        double Along = ((PointX - StartX) * SegmentX) + ((PointY - StartY) * SegmentY);
        if (Along < 1.0 || Along > LengthSquared - 1.0)
        {
            return false;
        }

        double Cross = Math.Abs(((PointX - StartX) * SegmentY) - ((PointY - StartY) * SegmentX));
        return Cross <= Math.Sqrt(LengthSquared) * 1.25;
    }

    private static double PathLength(IReadOnlyList<(double X, double Y)> Points)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Length += Hypot(Points[Index].X - Points[Index - 1].X, Points[Index].Y - Points[Index - 1].Y);
        }

        return Length;
    }

    private static double Hypot(double Dx, double Dy)
    {
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private readonly struct PaddedBox : IEquatable<PaddedBox>
    {
        public PaddedBox(double Left, double Right, double Top, double Bottom)
        {
            this.Left = Left;
            this.Right = Right;
            this.Top = Top;
            this.Bottom = Bottom;
        }

        public double Left { get; }

        public double Right { get; }

        public double Top { get; }

        public double Bottom { get; }

        public bool Equals(PaddedBox Other) =>
            Left == Other.Left && Right == Other.Right && Top == Other.Top && Bottom == Other.Bottom;

        public override bool Equals(object? Obj) => Obj is PaddedBox Other && Equals(Other);

        public override int GetHashCode() =>
            HashCode.Combine(Left, Right, Top, Bottom);
    }

    /// <summary>Cell index used by the grid visibility search for local point and segment checks.</summary>
    private sealed class PaddedBoxSpatialIndex
    {
        private const double CELL_SIZE = 120;
        private readonly record struct Cell(int X, int Y);
        private readonly Dictionary<Cell, List<PaddedBox>> _cells = [];

        public PaddedBoxSpatialIndex(IReadOnlyList<PaddedBox> Boxes)
        {
            foreach (PaddedBox Box in Boxes)
            {
                (int MinX, int MinY, int MaxX, int MaxY) = CellRange(
                    Box.Left, Box.Bottom, Box.Right, Box.Top);
                for (int X = MinX; X <= MaxX; X++)
                {
                    for (int Y = MinY; Y <= MaxY; Y++)
                {
                    if (!_cells.TryGetValue(new Cell(X, Y), out List<PaddedBox>? Items))
                        {
                            _cells[new Cell(X, Y)] = Items = [];
                        }

                        Items.Add(Box);
                }
                }
            }
        }

        public bool PointInsideAny(double X, double Y)
        {
            (int MinX, int MinY, int MaxX, int MaxY) = CellRange(X, Y, X, Y);
            for (int CellX = MinX; CellX <= MaxX; CellX++)
            {
                for (int CellY = MinY; CellY <= MaxY; CellY++)
            {
                if (!_cells.TryGetValue(new Cell(CellX, CellY), out List<PaddedBox>? Items))
                    {
                        continue;
                    }

                    foreach (PaddedBox Box in Items)
                    {
                        if (X > Box.Left + 0.6 && X < Box.Right - 0.6
                        && Y > Box.Bottom + 0.6 && Y < Box.Top - 0.6)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public List<PaddedBox> Query(double Left, double Bottom, double Right, double Top)
        {
            (int MinX, int MinY, int MaxX, int MaxY) = CellRange(Left, Bottom, Right, Top);
            List<PaddedBox> Found = [];
            HashSet<PaddedBox> Seen = [];
            for (int CellX = MinX; CellX <= MaxX; CellX++)
            {
                for (int CellY = MinY; CellY <= MaxY; CellY++)
            {
                if (!_cells.TryGetValue(new Cell(CellX, CellY), out List<PaddedBox>? Items))
                {
                    continue;
                }

                foreach (PaddedBox Box in Items)
                {
                    if (Box.Left <= Right && Box.Right >= Left && Box.Bottom <= Top && Box.Top >= Bottom
                        && Seen.Add(Box))
                    {
                        Found.Add(Box);
                    }
                }
            }
            }

            Found.Sort(static (First, Second) =>
            {
                int Compare = First.Left.CompareTo(Second.Left);
                if (Compare != 0)
                {
                    return Compare;
                }

                Compare = First.Bottom.CompareTo(Second.Bottom);
                if (Compare != 0)
                {
                    return Compare;
                }

                Compare = First.Right.CompareTo(Second.Right);
                if (Compare != 0)
                {
                    return Compare;
                }

                return First.Top.CompareTo(Second.Top);
            });
            return Found;
        }

        public bool SegmentClears(double X1, double Y1, double X2, double Y2)
        {
            (int MinX, int MinY, int MaxX, int MaxY) = CellRange(Math.Min(X1, X2), Math.Min(Y1, Y2),
                Math.Max(X1, X2), Math.Max(Y1, Y2));
            for (int CellX = MinX; CellX <= MaxX; CellX++)
            {
                for (int CellY = MinY; CellY <= MaxY; CellY++)
            {
                if (!_cells.TryGetValue(new Cell(CellX, CellY), out List<PaddedBox>? Items))
                    {
                        continue;
                    }

                    foreach (PaddedBox Box in Items)
                    {
                        if (SegmentHitsInterior(X1, Y1, X2, Y2, Box))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static (int MinX, int MinY, int MaxX, int MaxY) CellRange(
            double Left, double Top, double Right, double Bottom) =>
            ((int)Math.Floor(Left / CELL_SIZE) - 1, (int)Math.Floor(Top / CELL_SIZE) - 1,
             (int)Math.Floor(Right / CELL_SIZE) + 1, (int)Math.Floor(Bottom / CELL_SIZE) + 1);
    }
}
