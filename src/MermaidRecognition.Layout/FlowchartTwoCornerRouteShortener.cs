using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Finds a clear route with at most two corners using the assigned ports.</summary>
internal static class FlowchartTwoCornerRouteShortener
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, bool IncludeTwoCornerRoutes = false)
    {
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        HashSet<string> Hinted = Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true)
            .Select(Edge => Edge.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        bool HasFlatSide(string Id) => !Nodes.TryGetValue(Id, out NodeContract? Node)
            || Node.Shape is null or "" or "rect" or "rounded" or "round";
        double Left = Bounds.Values.Min(Box => Box.CenterX - Box.Width / 2.0) - 2 * DiagramGridMetrics.NODE_GAP_PX;
        double Right = Bounds.Values.Max(Box => Box.CenterX + Box.Width / 2.0) + 2 * DiagramGridMetrics.NODE_GAP_PX;
        double Bottom = Bounds.Values.Min(Box => Box.CenterY - Box.Height / 2.0) - 2 * DiagramGridMetrics.NODE_GAP_PX;
        double Top = Bounds.Values.Max(Box => Box.CenterY + Box.Height / 2.0) + 2 * DiagramGridMetrics.NODE_GAP_PX;
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route =>
            Route.Points.Count > (IncludeTwoCornerRoutes ? 3 : 4)
            && (!IncludeTwoCornerRoutes || HasFlatSide(Route.SourceId) && HasFlatSide(Route.TargetId))
            && !Hinted.Contains(Route.EdgeId) && !Back.Contains((Route.SourceId, Route.TargetId))))
        {
            FlowchartLayoutPoint Start = Route.Points[0], End = Route.Points[^1];
            var Candidates = new List<List<FlowchartLayoutPoint>>();
            bool ExitHorizontal = Route.ExitSide is "left" or "right";
            bool EntryHorizontal = Route.EntrySide is "left" or "right";
            if (ExitHorizontal != EntryHorizontal)
            {
                var Bend = ExitHorizontal ? New(End.X, Start.Y) : New(Start.X, End.Y);
                Candidates.Add([Copy(Start), Bend, Copy(End)]);
            }
            else if (ExitHorizontal)
            {
                IEnumerable<double> Tracks = Route.Points.Skip(1).Take(Route.Points.Count - 2).Select(Point => Point.X)
                    .Append((Start.X + End.X) / 2.0).Append(Left).Append(Right);
                if (IncludeTwoCornerRoutes)
                    Tracks = Tracks.Concat(Bounds.Values.SelectMany(Box => new[]
                    {
                        Box.CenterX - Box.Width / 2.0 - DiagramGridMetrics.GRID_UNIT_PX,
                        Box.CenterX + Box.Width / 2.0 + DiagramGridMetrics.GRID_UNIT_PX
                    }));
                foreach (double X in Tracks.Distinct())
                    Candidates.Add([Copy(Start), New(X, Start.Y), New(X, End.Y), Copy(End)]);
            }
            else
            {
                IEnumerable<double> Tracks = Route.Points.Skip(1).Take(Route.Points.Count - 2).Select(Point => Point.Y)
                    .Append((Start.Y + End.Y) / 2.0).Append(Bottom).Append(Top);
                if (IncludeTwoCornerRoutes)
                    Tracks = Tracks.Concat(Bounds.Values.SelectMany(Box => new[]
                    {
                        Box.CenterY - Box.Height / 2.0 - DiagramGridMetrics.GRID_UNIT_PX,
                        Box.CenterY + Box.Height / 2.0 + DiagramGridMetrics.GRID_UNIT_PX
                    }));
                foreach (double Y in Tracks.Distinct())
                    Candidates.Add([Copy(Start), New(Start.X, Y), New(End.X, Y), Copy(End)]);
            }
            List<FlowchartLayoutPoint>? Best = Candidates.Select(Collapse).Where(Candidate =>
                    Valid(Candidate, Route, Bounds) && NoWorse(Candidate, Route, Layout))
                .OrderBy(Candidate => Crossings(Candidate, Route, Layout))
                .ThenBy(Candidate => Length(Candidate)).FirstOrDefault();
            if (Best == null) continue;
            Route.Points.Clear();
            Route.Points.AddRange(Best);
        }
    }

    private static bool Valid(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        if (Points.Count < 2 || Points.Count > 4) return false;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
            if (Math.Abs(A.X - B.X) > 0.5 && Math.Abs(A.Y - B.Y) > 0.5) return false;
            if ((Index == 1 || Index == Points.Count - 1)
                && Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y) < 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) return false;
        }
        if (!LeavesSide(Points[0], Points[1], Route.ExitSide)
            || !EntersSide(Points[^2], Points[^1], Route.EntrySide)) return false;
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0 - 6, Right = Box.CenterX + Box.Width / 2.0 + 6;
            double Bottom = Box.CenterY - Box.Height / 2.0 - 6, Top = Box.CenterY + Box.Height / 2.0 + 6;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return false;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return false;
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
            var Before = FlowchartBranchRouteOrder.Intersections(Tuples(Route.Points), Tuples(Other.Points));
            var After = FlowchartBranchRouteOrder.Intersections(Tuples(Candidate), Tuples(Other.Points));
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
            if ((Math.Abs(Points[Index - 1].X - Points[Index].X) < 0.5
                    && Math.Abs(Points[Index].X - Points[Index + 1].X) < 0.5)
                || (Math.Abs(Points[Index - 1].Y - Points[Index].Y) < 0.5
                    && Math.Abs(Points[Index].Y - Points[Index + 1].Y) < 0.5)) Points.RemoveAt(Index);
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
        Points.Select(Point => (Point.X, Point.Y)).ToList();
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => New(Point.X, Point.Y);
    private static FlowchartLayoutPoint New(double X, double Y) => new() { X = X, Y = Y };
}

