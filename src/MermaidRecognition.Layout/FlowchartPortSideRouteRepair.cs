using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Tries legal terminal sides and slots before accepting a bent ordinary relationship.</summary>
internal static class FlowchartPortSideRouteRepair
{
    private sealed record Candidate(List<FlowchartLayoutPoint> Points, string Exit, string Entry,
        int FlowPriority, double Length);
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
        var Hinted = Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true)
            .Select(Edge => Edge.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        RepairRejectedRoutes(Layout, Contract, Bounds, Back, Direction);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route =>
                     Corners(Route.Points) is > 0 and <= 2
                     && !Hinted.Contains(Route.EdgeId)
                     && !Back.Contains((Route.SourceId, Route.TargetId))).ToList())
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
                || IsGeometricBackEdge(Source, Target, Direction)
                || !CanReassign(Route.SourceId, Nodes) || !CanReassign(Route.TargetId, Nodes)) continue;
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(
                Contract, Layout, IncludeArrangementCheck: false);
            int BeforeFaults = RejectingFaults(Before);
            List<FlowchartLayoutPoint> Original = Route.Points.Select(Copy).ToList();
            string OriginalExit = Route.ExitSide, OriginalEntry = Route.EntrySide;
            Candidate? Best = null;
            int BestFaults = BeforeFaults;
            double? BestScore = Before.Score;
            List<(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Points)> OtherPaths =
                Layout.EdgeRoutes.Where(Other => Other != Route)
                    .Select(Other => (Other, Tuples(Other.Points))).ToList();
            List<(double X, double Y)> OriginalPath = Tuples(Route.Points);
            foreach (Candidate Candidate in Candidates(Source, Target, Bounds.Values, Direction)
                         .Where(Candidate => Clear(Candidate.Points, Route, Bounds.Values)
                             && PortsAreFree(Candidate, Route, Layout)
                             && HasMinimumTerminalRuns(Candidate.Points)
                             && HasLabelRoom(Candidate.Points, Edge, Contract)
                             && NoWorseInteractions(Candidate.Points, OriginalPath, OtherPaths))
                         .OrderBy(Candidate => Candidate.Points.Count)
                         .ThenBy(Candidate => Candidate.FlowPriority)
                         .ThenBy(Candidate => Candidate.Length))
            {
                Set(Route, Candidate);
                FlowchartLayoutScore.Result Measured = FlowchartLayoutScore.Measure(
                    Contract, Layout, IncludeArrangementCheck: false);
                int Faults = RejectingFaults(Measured);
                bool Improves = BestScore.HasValue
                    ? Measured.Score.HasValue && Measured.Score.Value < BestScore.Value - 0.001
                    : Measured.Score.HasValue || Faults < BestFaults;
                if (Improves)
                {
                    Best = Candidate;
                    BestFaults = Faults;
                    BestScore = Measured.Score;
                    Restore(Route, Original, OriginalExit, OriginalEntry);
                    break;
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

    private static void RepairRejectedRoutes(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        HashSet<(string SourceId, string TargetId)> Back, FlowchartDirection Direction)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.Where(Route =>
                     Corners(Route.Points) > 2 && !Back.Contains((Route.SourceId, Route.TargetId))).ToList())
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) continue;
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(
                Contract, Layout, IncludeArrangementCheck: false);
            int BestFaults = RejectingFaults(Before), BeforeCrossings = Before.Crossings;
            List<FlowchartLayoutPoint> Original = Route.Points.Select(Copy).ToList();
            string OriginalExit = Route.ExitSide, OriginalEntry = Route.EntrySide;
            Candidate? Best = null;
            foreach (Candidate Candidate in Candidates(Source, Target, Bounds.Values, Direction)
                         .Where(Candidate => Clear(Candidate.Points, Route, Bounds.Values))
                         .OrderBy(Candidate => Candidate.Points.Count)
                         .ThenBy(Candidate => Candidate.FlowPriority)
                         .ThenBy(Candidate => Candidate.Length))
            {
                Set(Route, Candidate);
                FlowchartLayoutScore.Result Measured = FlowchartLayoutScore.Measure(
                    Contract, Layout, IncludeArrangementCheck: false);
                int Faults = RejectingFaults(Measured);
                if (Measured.Overlaps == 0 && Measured.Crossings <= BeforeCrossings && Faults < BestFaults)
                {
                    Best = Candidate;
                    BestFaults = Faults;
                    Restore(Route, Original, OriginalExit, OriginalEntry);
                    break;
                }
                Restore(Route, Original, OriginalExit, OriginalEntry);
            }
            if (Best != null) Set(Route, Best);
        }
    }

    private static IEnumerable<Candidate> Candidates(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, IEnumerable<FlowchartLayoutNodeBounds> Boxes,
        FlowchartDirection Direction, bool RequireFacing = true)
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
            if (RequireFacing && (!OtherEndBeyondSide(Source, Target, Exit)
                || !OtherEndBeyondSide(Target, Source, Entry))) continue;
            foreach (FlowchartLayoutPoint Start in Ports(Source, Exit))
            foreach (FlowchartLayoutPoint End in Ports(Target, Entry))
            {
            if (Math.Abs(Start.X - End.X) < 0.5 || Math.Abs(Start.Y - End.Y) < 0.5)
            {
                List<FlowchartLayoutPoint> Direct = [Start, End];
                if (Directions(Direct, Exit, Entry))
                    yield return New(Direct, Exit, Entry, Direction);
            }
            bool StartHorizontal = Exit is "left" or "right";
            bool EndHorizontal = Entry is "left" or "right";
            if (StartHorizontal != EndHorizontal)
            {
                FlowchartLayoutPoint Bend = StartHorizontal ? Point(End.X, Start.Y) : Point(Start.X, End.Y);
                List<FlowchartLayoutPoint> Points = Collapse([Start, Bend, End]);
                if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
            }
            else if (StartHorizontal)
            {
                foreach (double X in XTracks.Append((Start.X + End.X) / 2.0).Distinct())
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(X, Start.Y), Point(X, End.Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
                }
            }
            else
            {
                foreach (double Y in YTracks.Append((Start.Y + End.Y) / 2.0).Distinct())
                {
                    List<FlowchartLayoutPoint> Points = Collapse([Start, Point(Start.X, Y), Point(End.X, Y), End]);
                    if (Directions(Points, Exit, Entry)) yield return New(Points, Exit, Entry, Direction);
                }
            }
            }
        }
    }

    private static Candidate New(List<FlowchartLayoutPoint> Points, string Exit, string Entry,
        FlowchartDirection Direction) => new(Points, Exit, Entry,
        Exit == DownstreamSide(Direction) ? 0 : Entry == UpstreamSide(Direction) ? 1 : 2,
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
    private static IEnumerable<FlowchartLayoutPoint> Ports(FlowchartLayoutNodeBounds Box, string Side)
    {
        bool Vertical = Side is "left" or "right";
        double Length = Vertical ? Box.Height : Box.Width;
        double[] Fractions = [0.5, 1.0 / 3.0, 2.0 / 3.0, 0.25, 0.75, 0.2, 0.4, 0.6, 0.8];
        foreach (double Fraction in Fractions)
        {
            double Along = -Length / 2.0 + Length * Fraction;
            yield return Side switch
            {
                "left" => Point(Box.CenterX - Box.Width / 2.0, Box.CenterY - Along),
                "right" => Point(Box.CenterX + Box.Width / 2.0, Box.CenterY - Along),
                "top" => Point(Box.CenterX + Along, Box.CenterY + Box.Height / 2.0),
                _ => Point(Box.CenterX + Along, Box.CenterY - Box.Height / 2.0)
            };
        }
    }

    private static bool OtherEndBeyondSide(FlowchartLayoutNodeBounds Box,
        FlowchartLayoutNodeBounds Other, string Side) => Side switch
    {
        "left" => Other.CenterX + Other.Width / 2.0 <= Box.CenterX - Box.Width / 2.0 + 0.5,
        "right" => Other.CenterX - Other.Width / 2.0 >= Box.CenterX + Box.Width / 2.0 - 0.5,
        "top" => Other.CenterY - Other.Height / 2.0 >= Box.CenterY + Box.Height / 2.0 - 0.5,
        _ => Other.CenterY + Other.Height / 2.0 <= Box.CenterY - Box.Height / 2.0 + 0.5
    };

    private static bool CanReassign(string Id, IReadOnlyDictionary<string, NodeContract> Nodes) =>
        !Nodes.TryGetValue(Id, out NodeContract? Node)
        || Node.Shape is null or "" or "rect" or "rounded" or "round" or "stadium";

    private static bool IsGeometricBackEdge(FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => Target.CenterY > Source.CenterY + 0.5,
        FlowchartDirection.BottomToTop => Target.CenterY < Source.CenterY - 0.5,
        FlowchartDirection.LeftToRight => Target.CenterX < Source.CenterX - 0.5,
        FlowchartDirection.RightToLeft => Target.CenterX > Source.CenterX + 0.5,
        _ => false
    };

    private static bool HasMinimumTerminalRuns(IReadOnlyList<FlowchartLayoutPoint> Points) =>
        Points.Count == 2 || Distance(Points[0], Points[1]) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
            && Distance(Points[^2], Points[^1]) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;

    private static double Distance(FlowchartLayoutPoint A, FlowchartLayoutPoint B) =>
        Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);

    private static bool HasLabelRoom(IReadOnlyList<FlowchartLayoutPoint> Points,
        EdgeContract? Edge, DiagramContract Contract)
    {
        if (string.IsNullOrWhiteSpace(Edge?.Label)) return true;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            Edge.Label, Options, Options.EdgeLabelFontSizePx);
        for (int Index = 1; Index < Points.Count; Index++)
        {
            bool Horizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            double Required = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                Dimensions, Options.EdgeLabelFontSizePx, Horizontal);
            double CornerRoom = Points.Count > 2 ? 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
                : DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (Distance(Points[Index - 1], Points[Index]) >= Required + CornerRoom - 0.5) return true;
        }
        return false;
    }

    private static bool PortsAreFree(Candidate Candidate, FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutResult Layout) => Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2)
        .All(Other => !(Other.SourceId == Route.SourceId && Same(Other.Points[0], Candidate.Points[0]))
            && !(Other.TargetId == Route.SourceId && Same(Other.Points[^1], Candidate.Points[0]))
            && !(Other.SourceId == Route.TargetId && Same(Other.Points[0], Candidate.Points[^1]))
            && !(Other.TargetId == Route.TargetId && Same(Other.Points[^1], Candidate.Points[^1])));

    private static bool NoWorseInteractions(IReadOnlyList<FlowchartLayoutPoint> Candidate,
        IReadOnlyList<(double X, double Y)> Original,
        IReadOnlyList<(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Points)> Others)
    {
        int BeforeCrossings = 0, AfterCrossings = 0;
        double BeforeOverlap = 0.0, AfterOverlap = 0.0;
        List<(double X, double Y)> CandidatePath = Tuples(Candidate);
        foreach (var Other in Others)
        {
            var Before = FlowchartBranchRouteOrder.Intersections(Original, Other.Points);
            var After = FlowchartBranchRouteOrder.Intersections(CandidatePath, Other.Points);
            BeforeCrossings += Before.Crossings;
            AfterCrossings += After.Crossings;
            BeforeOverlap += Before.Overlap;
            AfterOverlap += After.Overlap;
        }
        return AfterCrossings <= BeforeCrossings && AfterOverlap <= BeforeOverlap + 0.01;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToList();

    private static bool Same(FlowchartLayoutPoint A, FlowchartLayoutPoint B) =>
        Math.Abs(A.X - B.X) < 0.5 && Math.Abs(A.Y - B.Y) < 0.5;

    private static string DownstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "bottom",
        FlowchartDirection.BottomToTop => "top",
        FlowchartDirection.LeftToRight => "right",
        _ => "left"
    };

    private static string UpstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "top",
        FlowchartDirection.BottomToTop => "bottom",
        FlowchartDirection.LeftToRight => "left",
        _ => "right"
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
