using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Shortest path around padded node and group boxes.
/// Inspired by <c>VisibilityGraph</c> and
/// <c>SingleSourceSingleTargetShortestPathOnVisibilityGraph</c> in the adopted MSAGL slice.
/// </summary>
public static class FlowchartPaddedVisibilityRouter
{
    private const double OBSTACLE_PADDING_PX = 10.0;

    private const double BEND_PENALTY_FRACTION = 0.04;

    private const double VERTEX_MERGE_PX = 2.0;

    /// <summary>
    /// Routes from the source port to the target port around padded obstacles.
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
        Points = [];
        List<PaddedBox> Boxes = [];
        foreach (FlowchartLayoutNodeBounds Obstacle in Obstacles)
        {
            Boxes.Add(Pad(Obstacle, OBSTACLE_PADDING_PX));
        }

        if (SegmentClears(StartX, StartY, EndX, EndY, Boxes))
        {
            Points.Add((StartX, StartY));
            Points.Add((EndX, EndY));
            return true;
        }

        List<(double X, double Y)> Vertices = [];
        AddVertex(Vertices, StartX, StartY);
        AddVertex(Vertices, EndX, EndY);
        foreach (PaddedBox Box in Boxes)
        {
            AddVertex(Vertices, Box.Left, Box.Bottom);
            AddVertex(Vertices, Box.Right, Box.Bottom);
            AddVertex(Vertices, Box.Right, Box.Top);
            AddVertex(Vertices, Box.Left, Box.Top);
        }

        int Count = Vertices.Count;
        List<int>[] Neighbours = new List<int>[Count];
        for (int Index = 0; Index < Count; Index++)
        {
            Neighbours[Index] = [];
        }

        for (int Left = 0; Left < Count; Left++)
        {
            for (int Right = Left + 1; Right < Count; Right++)
            {
                if (!SegmentClears(
                    Vertices[Left].X,
                    Vertices[Left].Y,
                    Vertices[Right].X,
                    Vertices[Right].Y,
                    Boxes))
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

        Simplify(Points, Boxes);
        return Points.Count >= 2;
    }

    /// <summary>
    /// Builds the obstacle list for one edge: every box that is not an endpoint or an ancestor group.
    /// </summary>
    /// <param name="Contract">Diagram.</param>
    /// <param name="BoundsById">Node and group bounds.</param>
    /// <param name="Edge">Edge being routed.</param>
    /// <returns>Obstacles for <see cref="TryRoute"/>.</returns>
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

    private static void Simplify(List<(double X, double Y)> Points, IReadOnlyList<PaddedBox> Boxes)
    {
        bool Changed = true;
        while (Changed && Points.Count > 2)
        {
            Changed = false;
            for (int Index = 1; Index < Points.Count - 1; Index++)
            {
                if (SegmentClears(
                    Points[Index - 1].X,
                    Points[Index - 1].Y,
                    Points[Index + 1].X,
                    Points[Index + 1].Y,
                    Boxes))
                {
                    Points.RemoveAt(Index);
                    Changed = true;
                    break;
                }
            }
        }
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
