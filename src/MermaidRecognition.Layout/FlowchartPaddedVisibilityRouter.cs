using System;
using System.Collections.Generic;

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
    /// <returns>True when a path exists.</returns>
    public static bool TryRoute(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        IReadOnlyList<FlowchartLayoutNodeBounds> Obstacles,
        FlowchartLayoutNodeBounds? SourceBounds,
        FlowchartLayoutNodeBounds? TargetBounds,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        List<PaddedBox> Boxes = [];
        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            Boxes.Add(Pad(Obstacle, OBSTACLE_PADDING_PX));
        }

        (double RouteStartX, double RouteStartY) = OffsetPortOutward(StartX, StartY, SourceBounds, PORT_STUB_PX);
        (double RouteEndX, double RouteEndY) = OffsetPortOutward(EndX, EndY, TargetBounds, PORT_STUB_PX);
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
        Points = [];
        List<double> Xs = [StartX, EndX];
        List<double> Ys = [StartY, EndY];
        foreach (PaddedBox Box in Boxes)
        {
            Xs.Add(Box.Left);
            Xs.Add(Box.Right);
            Ys.Add(Box.Bottom);
            Ys.Add(Box.Top);
        }

        SortUnique(Xs);
        SortUnique(Ys);

        List<(double X, double Y)> Vertices = [];
        AddVertex(Vertices, StartX, StartY);
        AddVertex(Vertices, EndX, EndY);
        foreach (double X in Xs)
        {
            foreach (double Y in Ys)
            {
                if (PointInsideAny(X, Y, Boxes))
                {
                    continue;
                }

                AddVertex(Vertices, X, Y);
            }
        }

        int Count = Vertices.Count;
        if (Count < 2)
        {
            return false;
        }

        List<int>[] Neighbours = new List<int>[Count];
        for (int Index = 0; Index < Count; Index++)
        {
            Neighbours[Index] = [];
        }

        for (int Left = 0; Left < Count; Left++)
        {
            for (int Right = Left + 1; Right < Count; Right++)
            {
                if (!IsAxisAligned(Vertices[Left].X, Vertices[Left].Y, Vertices[Right].X, Vertices[Right].Y))
                {
                    continue;
                }

                if (!SegmentClears(Vertices[Left].X, Vertices[Left].Y, Vertices[Right].X, Vertices[Right].Y, Boxes))
                {
                    continue;
                }

                Neighbours[Left].Add(Right);
                Neighbours[Right].Add(Left);
            }
        }

        double Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
        double BendCost = Math.Max(1.0, Manhattan * BEND_PENALTY_FRACTION);
        if (!TryShortestPath(Vertices, Neighbours, 0, 1, BendCost, out List<int> Path))
        {
            return false;
        }

        foreach (int Index in Path)
        {
            Points.Add(Vertices[Index]);
        }

        SimplifyColinear(Points);
        return Points.Count >= 2;
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

    private static void AddVertex(List<(double X, double Y)> Vertices, double X, double Y)
    {
        foreach ((double ExistingX, double ExistingY) in Vertices)
        {
            if (Math.Abs(ExistingX - X) <= VERTEX_MERGE_PX && Math.Abs(ExistingY - Y) <= VERTEX_MERGE_PX)
            {
                return;
            }
        }

        Vertices.Add((X, Y));
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

    private static bool TryShortestPath(
        IReadOnlyList<(double X, double Y)> Vertices,
        IReadOnlyList<List<int>> Neighbours,
        int Source,
        int Target,
        double BendCost,
        out List<int> Path)
    {
        Path = [];
        int Count = Vertices.Count;
        double[] Distance = new double[Count];
        int[] Previous = new int[Count];
        int[] Incoming = new int[Count];
        bool[] Visited = new bool[Count];
        for (int Index = 0; Index < Count; Index++)
        {
            Distance[Index] = double.PositiveInfinity;
            Previous[Index] = -1;
            Incoming[Index] = -1;
        }

        Distance[Source] = 0.0;
        for (int Step = 0; Step < Count; Step++)
        {
            int Current = -1;
            double Best = double.PositiveInfinity;
            for (int Index = 0; Index < Count; Index++)
            {
                if (!Visited[Index] && Distance[Index] < Best)
                {
                    Best = Distance[Index];
                    Current = Index;
                }
            }

            if (Current < 0 || Current == Target)
            {
                break;
            }

            Visited[Current] = true;
            foreach (int Next in Neighbours[Current])
            {
                if (Visited[Next])
                {
                    continue;
                }

                double Length = Hypot(
                    Vertices[Next].X - Vertices[Current].X,
                    Vertices[Next].Y - Vertices[Current].Y);
                double Extra = 0.0;
                if (Incoming[Current] >= 0
                    && !SameDirection(
                        Vertices[Incoming[Current]],
                        Vertices[Current],
                        Vertices[Next]))
                {
                    Extra = BendCost;
                }

                double Candidate = Distance[Current] + Length + Extra;
                if (Candidate < Distance[Next])
                {
                    Distance[Next] = Candidate;
                    Previous[Next] = Current;
                    Incoming[Next] = Current;
                }
            }
        }

        if (double.IsPositiveInfinity(Distance[Target]))
        {
            return false;
        }

        int Cursor = Target;
        while (Cursor >= 0)
        {
            Path.Add(Cursor);
            Cursor = Previous[Cursor];
        }

        Path.Reverse();
        return Path.Count >= 2 && Path[0] == Source;
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
        if (Spur < 2.0 || Spur > 12.0 || Continuation < 2.0)
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

    private readonly struct PaddedBox
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
    }
}
