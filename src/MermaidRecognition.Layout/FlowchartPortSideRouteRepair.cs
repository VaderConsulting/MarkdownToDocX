using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Changes terminal sides when an obstacle otherwise forces more than two corners.</summary>
internal static class FlowchartPortSideRouteRepair
{
    private sealed record Candidate(List<FlowchartLayoutPoint> Points, string Exit, string Entry, double Length);
    private sealed record Snapshot(FlowchartLayoutEdgeRoute Route, List<FlowchartLayoutPoint> Points,
        string Exit, string Entry);

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract)
    {
        if (Layout.NodeBounds.Count == 0 || Layout.EdgeRoutes.Count == 0) return;
        FlowchartLayoutScore.Result Initial = FlowchartLayoutScore.Measure(Contract, Layout);
        List<Snapshot> InitialRoutes = Layout.EdgeRoutes.Select(Route => new Snapshot(Route,
            Route.Points.Select(Copy).ToList(), Route.ExitSide, Route.EntrySide)).ToList();
        var Bounds = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route =>
                     Corners(Route.Points) > 2 && !Back.Contains((Route.SourceId, Route.TargetId))).ToList())
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) continue;
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(Contract, Layout);
            int BeforeFaults = RejectingFaults(Before);
            int BeforeCrossings = Before.Crossings;
            List<FlowchartLayoutPoint> Original = Route.Points.Select(Copy).ToList();
            string OriginalExit = Route.ExitSide, OriginalEntry = Route.EntrySide;
            Candidate? Best = null;
            int BestFaults = BeforeFaults;
            foreach (Candidate Candidate in Candidates(Source, Target, Bounds.Values)
                         .Where(Candidate => Clear(Candidate.Points, Route, Bounds.Values))
                         .OrderBy(Candidate => Candidate.Length))
            {
                Set(Route, Candidate);
                FlowchartLayoutScore.Result Measured = FlowchartLayoutScore.Measure(Contract, Layout);
                int Faults = RejectingFaults(Measured);
                if (Measured.Overlaps == 0 && Measured.Crossings <= BeforeCrossings && Faults < BestFaults)
                {
                    Best = Candidate;
                    BestFaults = Faults;
                    if (Faults == BeforeFaults - 1) break;
                }
                Restore(Route, Original, OriginalExit, OriginalEntry);
            }
            if (Best != null) Set(Route, Best);
            else Restore(Route, Original, OriginalExit, OriginalEntry);
        }
        FlowchartLayoutScore.Result Final = FlowchartLayoutScore.Measure(Contract, Layout);
        bool Keep = Initial.Score.HasValue
            ? Final.Score.HasValue && Final.Score.Value < Initial.Score.Value
            : Final.Score.HasValue;
        if (!Keep)
            foreach (Snapshot Saved in InitialRoutes)
                Restore(Saved.Route, Saved.Points, Saved.Exit, Saved.Entry);
    }

    private static IEnumerable<Candidate> Candidates(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, IEnumerable<FlowchartLayoutNodeBounds> Boxes)
    {
        string[] Sides = ["top", "right", "bottom", "left"];
        double Gap = DiagramGridMetrics.NODE_GAP_PX;
        List<double> XTracks = Boxes.SelectMany(Box => new[]
            { Box.CenterX - Box.Width / 2.0 - Gap / 2.0, Box.CenterX + Box.Width / 2.0 + Gap / 2.0 }).Distinct().ToList();
        List<double> YTracks = Boxes.SelectMany(Box => new[]
            { Box.CenterY - Box.Height / 2.0 - Gap / 2.0, Box.CenterY + Box.Height / 2.0 + Gap / 2.0 }).Distinct().ToList();
        foreach (string Exit in Sides)
        foreach (string Entry in Sides)
        {
            FlowchartLayoutPoint Start = Port(Source, Exit), End = Port(Target, Entry);
            bool StartHorizontal = Exit is "left" or "right";
            bool EndHorizontal = Entry is "left" or "right";
            if (StartHorizontal != EndHorizontal)
            {
                FlowchartLayoutPoint Bend = StartHorizontal ? Point(End.X, Start.Y) : Point(Start.X, End.Y);
                List<FlowchartLayoutPoint> Points = Collapse([Start, Bend, End]);
                if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry);
            }
            else if (StartHorizontal)
            {
                foreach (double X in XTracks.Append((Start.X + End.X) / 2.0).Distinct())
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(X, Start.Y), Point(X, End.Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry);
                }
            }
            else
            {
                foreach (double Y in YTracks.Append((Start.Y + End.Y) / 2.0).Distinct())
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(Start.X, Y), Point(End.X, Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry);
                }
            }
        }
    }

    private static Candidate New(List<FlowchartLayoutPoint> Points, string Exit, string Entry) => new(Points, Exit, Entry,
        Enumerable.Range(1, Points.Count - 1).Sum(Index => Math.Abs(Points[Index].X - Points[Index - 1].X)
            + Math.Abs(Points[Index].Y - Points[Index - 1].Y)));
    private static int RejectingFaults(FlowchartLayoutScore.Result Result) => Result.Violations.Count(Failure =>
        Failure.Kind is not "ArrangementKept");
    private static int Corners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
            if ((Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5)
                != (Math.Abs(Points[Index + 1].Y - Points[Index].Y) < 0.5)) Result++;
        return Result;
    }
    private static FlowchartLayoutPoint Port(FlowchartLayoutNodeBounds Box, string Side) => Side switch
    {
        "left" => Point(Box.CenterX - Box.Width / 2.0, Box.CenterY),
        "right" => Point(Box.CenterX + Box.Width / 2.0, Box.CenterY),
        "top" => Point(Box.CenterX, Box.CenterY + Box.Height / 2.0),
        _ => Point(Box.CenterX, Box.CenterY - Box.Height / 2.0)
    };
    private static bool Directions(IReadOnlyList<FlowchartLayoutPoint> Points, string Exit, string Entry) =>
        Points.Count >= 2 && Leaves(Points[0], Points[1], Exit) && Enters(Points[^2], Points[^1], Entry)
        && Enumerable.Range(1, Points.Count - 1).All(Index =>
            Math.Abs(Points[Index].X - Points[Index - 1].X) < 0.5
            || Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5);
    private static bool Leaves(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X < A.X, "right" => B.X > A.X, "top" => B.Y > A.Y, _ => B.Y < A.Y
    };
    private static bool Enters(FlowchartLayoutPoint A, FlowchartLayoutPoint B, string Side) => Side switch
    {
        "left" => B.X > A.X, "right" => B.X < A.X, "top" => B.Y < A.Y, _ => B.Y > A.Y
    };
    private static bool Clear(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        IEnumerable<FlowchartLayoutNodeBounds> Boxes)
    {
        foreach (FlowchartLayoutNodeBounds Box in Boxes)
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
    private static List<FlowchartLayoutPoint> Collapse(List<FlowchartLayoutPoint> Points)
    {
        for (int Index = Points.Count - 2; Index > 0; Index--)
            if ((Math.Abs(Points[Index - 1].X - Points[Index].X) < 0.5 && Math.Abs(Points[Index].X - Points[Index + 1].X) < 0.5)
                || (Math.Abs(Points[Index - 1].Y - Points[Index].Y) < 0.5 && Math.Abs(Points[Index].Y - Points[Index + 1].Y) < 0.5))
                Points.RemoveAt(Index);
        return Points;
    }
    private static void Set(FlowchartLayoutEdgeRoute Route, Candidate Candidate)
    {
        Route.Points.Clear(); Route.Points.AddRange(Candidate.Points.Select(Copy));
        Route.ExitSide = Candidate.Exit; Route.EntrySide = Candidate.Entry;
    }
    private static void Restore(FlowchartLayoutEdgeRoute Route, List<FlowchartLayoutPoint> Points, string Exit, string Entry)
    {
        Route.Points.Clear(); Route.Points.AddRange(Points.Select(Copy)); Route.ExitSide = Exit; Route.EntrySide = Entry;
    }
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Value) => Point(Value.X, Value.Y);
    private static FlowchartLayoutPoint Point(double X, double Y) => new() { X = Math.Round(X), Y = Math.Round(Y) };
}
