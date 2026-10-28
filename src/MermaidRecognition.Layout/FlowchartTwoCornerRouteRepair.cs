using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Finds a clear route with at most two corners using the assigned ports.</summary>
internal static class FlowchartTwoCornerRouteRepair
{
    internal static Action<string, int, int, int, bool>? RouteCandidateObserver { get; set; }

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        HashSet<(string SourceId, string TargetId)> Back = [.. FlowchartBackEdgeHelper.FindBackEdges(Contract).Select(Edge => (Edge.SourceId, Edge.TargetId))];
        double Left = Bounds.Values.Min(Box => Box.CenterX - (Box.Width / 2.0)) - (2 * DiagramGridMetrics.NODE_GAP_PX);
        double Right = Bounds.Values.Max(Box => Box.CenterX + (Box.Width / 2.0)) + (2 * DiagramGridMetrics.NODE_GAP_PX);
        double Bottom = Bounds.Values.Min(Box => Box.CenterY - (Box.Height / 2.0)) - (2 * DiagramGridMetrics.NODE_GAP_PX);
        double Top = Bounds.Values.Max(Box => Box.CenterY + (Box.Height / 2.0)) + (2 * DiagramGridMetrics.NODE_GAP_PX);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count > 4
            && !Back.Contains((Route.SourceId, Route.TargetId))))
        {
            int CandidateCount = 0, ValidCount = 0, NonWorseCount = 0;
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            List<List<FlowchartLayoutPoint>> Candidates = [];
            bool ExitHorizontal = Route.ExitSide is "left" or "right";
            bool EntryHorizontal = Route.EntrySide is "left" or "right";
            if (ExitHorizontal != EntryHorizontal)
            {
                FlowchartLayoutPoint Bend = ExitHorizontal ? New(End.X, Start.Y) : New(Start.X, End.Y);
                Candidates.Add([Copy(Start), Bend, Copy(End)]);
            }
            else if (ExitHorizontal)
            {
                foreach (double X in Route.Points.Skip(1).Take(Route.Points.Count - 2).Select(Point => Point.X)
                    .Append((Start.X + End.X) / 2.0).Append(Left).Append(Right).Distinct())
                {
                    Candidates.Add([Copy(Start), New(X, Start.Y), New(X, End.Y), Copy(End)]);
                }
            }
            else
            {
                foreach (double Y in Route.Points.Skip(1).Take(Route.Points.Count - 2).Select(Point => Point.Y)
                    .Append((Start.Y + End.Y) / 2.0).Append(Bottom).Append(Top).Distinct())
                {
                    Candidates.Add([Copy(Start), New(Start.X, Y), New(End.X, Y), Copy(End)]);
                }
            }
            List<FlowchartLayoutPoint>? Best = Candidates.Select(Collapse).Where(Candidate =>
                {
                    CandidateCount++;
                    if (!Valid(Candidate, Route, Bounds))
                    {
                        return false;
                    }

                    ValidCount++;
                    if (!NoWorse(Candidate, Route, Layout))
                    {
                        return false;
                    }

                    NonWorseCount++;
                    return true;
                })
                .OrderBy(Candidate => Crossings(Candidate, Route, Layout))
                .ThenBy(Candidate => Length(Candidate)).FirstOrDefault();
            if (Best == null)
            {
                RouteCandidateObserver?.Invoke(Route.EdgeId, CandidateCount, ValidCount, NonWorseCount, false);
                continue;
            }
            Route.Points.Clear();
            Route.Points.AddRange(Best);
            RouteCandidateObserver?.Invoke(Route.EdgeId, CandidateCount, ValidCount, NonWorseCount, true);
        }
    }

    private static bool Valid(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        if (Points.Count < 2 || Points.Count > 4)
        {
            return false;
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
            if (Math.Abs(A.X - B.X) > 0.5 && Math.Abs(A.Y - B.Y) > 0.5)
            {
                return false;
            }

            if ((Index == 1 || Index == Points.Count - 1)
                && Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y) < 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
            {
                return false;
            }
        }
        if (!LeavesSide(Points[0], Points[1], Route.ExitSide)
            || !EntersSide(Points[^2], Points[^1], Route.EntrySide))
        {
            return false;
        }

        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId)
            {
                continue;
            }

            double Left = Box.CenterX - (Box.Width / 2.0) - 6, Right = Box.CenterX + (Box.Width / 2.0) + 6;
            double Bottom = Box.CenterY - (Box.Height / 2.0) - 6, Top = Box.CenterY + (Box.Height / 2.0) + 6;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top))
                {
                    return false;
                }

                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool NoWorse(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        FlowchartLayoutEdgeRoute Route, FlowchartLayoutResult Layout)
    {
        int BeforeCrossings = 0, AfterCrossings = 0;
        double BeforeOverlap = 0.0, AfterOverlap = 0.0;
        foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes.Where(Other => Other != Route))
        {
            (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Tuples(Route.Points), Tuples(Other.Points));
            (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points));
            BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
            BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
        }
        return AfterCrossings <= BeforeCrossings && AfterOverlap <= BeforeOverlap + 0.01;
    }

    private static int Crossings(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        FlowchartLayoutEdgeRoute Route, FlowchartLayoutResult Layout) => Layout.EdgeRoutes.Where(Other => Other != Route)
        .Sum(Other => FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points)).Crossings);

    private static List<FlowchartLayoutPoint> Collapse(List<FlowchartLayoutPoint> Points)
    {
        for (int Index = Points.Count - 2; Index > 0; Index--)
        {
            if ((Math.Abs(Points[Index - 1].X - Points[Index].X) < 0.5
                    && Math.Abs(Points[Index].X - Points[Index + 1].X) < 0.5)
                || (Math.Abs(Points[Index - 1].Y - Points[Index].Y) < 0.5
                    && Math.Abs(Points[Index].Y - Points[Index + 1].Y) < 0.5))
            {
                Points.RemoveAt(Index);
            }
        }

        return Points;
    }

    private static bool LeavesSide(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X < A.X, "right" => B.X > A.X, "top" => B.Y > A.Y, "bottom" => B.Y < A.Y, _ => true
    };
    private static bool EntersSide(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X > A.X, "right" => B.X < A.X, "top" => B.Y < A.Y, "bottom" => B.Y > A.Y, _ => true
    };
    private static double Length(IReadOnlyList<FlowchartLayoutPoint> Points) => Enumerable.Range(1, Points.Count - 1)
        .Sum(Index => Math.Abs(Points[Index].X - Points[Index - 1].X) + Math.Abs(Points[Index].Y - Points[Index - 1].Y));
    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        [.. Points.Select(Point => (Point.X, Point.Y))];
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => New(Point.X, Point.Y);
    private static FlowchartLayoutPoint New(double X, double Y) => new() { X = X, Y = Y };
}
