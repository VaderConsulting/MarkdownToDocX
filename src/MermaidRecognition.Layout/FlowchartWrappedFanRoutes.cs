using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Routes inner rows of a wrapped fan on distinct tracks between leaf columns.</summary>
internal static class FlowchartWrappedFanRoutes
{
    private const int MinimumFan = DiagramGridMetrics.TRACKS_PER_GAP + 1;

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        var Visible = Contract.Edges.Where(Edge => Bounds.ContainsKey(Edge.SourceId)
            && Bounds.ContainsKey(Edge.TargetId)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (FlowchartLayoutNodeBounds Hub in Bounds.Values)
        {
            List<FlowchartLayoutNodeBounds> Out = Visible.Where(Edge => Edge.SourceId == Hub.NodeId)
                .Select(Edge => Bounds[Edge.TargetId]).Distinct().ToList();
            List<FlowchartLayoutNodeBounds> In = Visible.Where(Edge => Edge.TargetId == Hub.NodeId)
                .Select(Edge => Bounds[Edge.SourceId]).Distinct().ToList();
            bool FanOut = Out.Count >= MinimumFan && In.Count < MinimumFan;
            List<FlowchartLayoutNodeBounds> Leaves = FanOut ? Out : In;
            if (Leaves.Count < MinimumFan) continue;

            double Main(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterX : Box.CenterY;
            double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
            double MainHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Width : Box.Height) / 2.0;
            double CrossHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Height : Box.Width) / 2.0;
            List<IGrouping<double, FlowchartLayoutNodeBounds>> Ranks = Leaves
                .Where(Box => Math.Abs(Main(Box) - Main(Hub)) > 0.5)
                .GroupBy(Box => Main(Box)).Where(Group => Group.Count() > 1)
                .OrderBy(Group => Math.Abs(Group.Key - Main(Hub))).ToList();
            if (Ranks.Count < 2) continue;
            double Sign = Math.Sign(Ranks[0].Key - Main(Hub));
            if (Sign == 0.0) continue;
            var WrappedLeaves = Ranks.SelectMany(Group => Group).ToList();
            var Columns = WrappedLeaves.GroupBy(Box => Cross(Box)).OrderBy(Group => Group.Key).ToList();
            if (Columns.Count < 2) continue;
            var Gaps = new List<(double Low, double High, int Used, List<FlowchartLayoutNodeBounds> Leaves)>();
            for (int Index = 0; Index < Columns.Count - 1; Index++)
            {
                double Low = Columns[Index].Max(Box => Cross(Box) + CrossHalf(Box));
                double High = Columns[Index + 1].Min(Box => Cross(Box) - CrossHalf(Box));
                if (High - Low >= DiagramGridMetrics.GRID_UNIT_PX - 0.5)
                    Gaps.Add((Low, High, 0, []));
            }
            if (Gaps.Count == 0) continue;
            var Candidates = new Dictionary<FlowchartLayoutEdgeRoute, Candidate>();

            foreach (FlowchartLayoutNodeBounds Leaf in Ranks[0].OrderBy(Cross))
            {
                FlowchartLayoutEdgeRoute? Route = FindRoute(Layout, Hub, Leaf, FanOut);
                if (Route == null) continue;
                double HubSide = Main(Hub) + Sign * MainHalf(Hub);
                double LeafSide = Main(Leaf) - Sign * MainHalf(Leaf);
                var Points = new List<FlowchartLayoutPoint>
                {
                    Point(HubSide, Cross(Leaf), Horizontal), Point(LeafSide, Cross(Leaf), Horizontal)
                };
                AddCandidate(Candidates, Route, Points, FanOut,
                    MainSide(Horizontal, Sign), MainSide(Horizontal, -Sign));
            }

            List<FlowchartLayoutNodeBounds> Inner = Ranks.Skip(1).SelectMany(Group => Group)
                .OrderBy(Box => Math.Abs(Main(Box) - Main(Hub))).ThenBy(Cross).ToList();
            foreach (FlowchartLayoutNodeBounds Leaf in Inner)
            {
                var Choices = Gaps.Select((Gap, Index) => (Gap, Index))
                    .Where(Item => Item.Gap.High <= Cross(Leaf) + 0.5 || Item.Gap.Low >= Cross(Leaf) - 0.5)
                    .OrderBy(Item => Distance(Item.Gap, Cross(Leaf)))
                    .ThenBy(Item => Item.Gap.Leaves.Count).ThenBy(Item => Item.Index).ToList();
                if (Choices.Count == 0) continue;
                int Choice = Choices[0].Index;
                Gaps[Choice].Leaves.Add(Leaf);
            }

            double HubCrossLow = Cross(Hub) - CrossHalf(Hub), HubCrossHigh = Cross(Hub) + CrossHalf(Hub);
            foreach (var Gap in Gaps)
            {
                int[] Offsets = DiagramGridMetrics.TrackOffsets(Gap.Leaves.Count,
                    (int)Math.Round(Gap.High - Gap.Low));
                List<FlowchartLayoutNodeBounds> Ordered = Gap.Leaves.OrderBy(Box => Math.Abs(Main(Box) - Main(Hub)))
                    .ThenBy(Box => Cross(Box)).ToList();
                for (int Index = 0; Index < Ordered.Count; Index++)
                {
                    FlowchartLayoutNodeBounds Leaf = Ordered[Index];
                    double Track = Gap.Low + Offsets[Index];
                    if (Track <= HubCrossLow + 0.5 || Track >= HubCrossHigh - 0.5) continue;
                    FlowchartLayoutEdgeRoute? Route = FindRoute(Layout, Hub, Leaf, FanOut);
                    if (Route == null) continue;
                    double HubSide = Main(Hub) + Sign * MainHalf(Hub);
                    double LeafSide = Cross(Leaf) + Math.Sign(Track - Cross(Leaf)) * CrossHalf(Leaf);
                    var Points = new List<FlowchartLayoutPoint>
                    {
                        Point(HubSide, Track, Horizontal),
                        Point(Main(Leaf), Track, Horizontal),
                        Point(Main(Leaf), LeafSide, Horizontal)
                    };
                    string HubSideName = MainSide(Horizontal, Sign);
                    string LeafSideName = CrossSide(Horizontal, Math.Sign(Track - Cross(Leaf)));
                    AddCandidate(Candidates, Route, Points, FanOut, HubSideName, LeafSideName);
                }
            }
            TryReplaceFan(Layout, Bounds, Candidates);
        }
    }

    private static double Distance((double Low, double High, int Used, List<FlowchartLayoutNodeBounds> Leaves) Gap,
        double Cross) => Cross < Gap.Low ? Gap.Low - Cross : Cross > Gap.High ? Cross - Gap.High : 0.0;

    private static FlowchartLayoutEdgeRoute? FindRoute(FlowchartLayoutResult Layout,
        FlowchartLayoutNodeBounds Hub, FlowchartLayoutNodeBounds Leaf, bool FanOut) =>
        Layout.EdgeRoutes.FirstOrDefault(Edge => FanOut
            ? Edge.SourceId == Hub.NodeId && Edge.TargetId == Leaf.NodeId
            : Edge.SourceId == Leaf.NodeId && Edge.TargetId == Hub.NodeId);

    private sealed record Candidate(List<FlowchartLayoutPoint> Points, string ExitSide, string EntrySide);

    private static void AddCandidate(IDictionary<FlowchartLayoutEdgeRoute, Candidate> Candidates,
        FlowchartLayoutEdgeRoute Route, List<FlowchartLayoutPoint> Points, bool FanOut,
        string HubSide, string LeafSide)
    {
        if (!FanOut) Points.Reverse();
        Candidates[Route] = new(Points, FanOut ? HubSide : LeafSide, FanOut ? LeafSide : HubSide);
    }

    private static void TryReplaceFan(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, Candidate> Candidates)
    {
        var Safe = Candidates.Where(Item => !HitsBox(Item.Value.Points, Item.Key, Bounds))
            .ToDictionary(Item => Item.Key, Item => Item.Value);
        if (Safe.Count == 0) return;
        int BeforeCrossings = 0, AfterCrossings = 0;
        double BeforeOverlap = 0.0, AfterOverlap = 0.0;
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
        {
            FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes[First], B = Layout.EdgeRoutes[Second];
            var Before = FlowchartBranchRouteOrder.Intersections(Tuples(A.Points), Tuples(B.Points));
            var After = FlowchartBranchRouteOrder.Intersections(
                Tuples(Safe.TryGetValue(A, out Candidate? NewA) ? NewA.Points : A.Points),
                Tuples(Safe.TryGetValue(B, out Candidate? NewB) ? NewB.Points : B.Points));
            BeforeCrossings += Before.Crossings; AfterCrossings += After.Crossings;
            BeforeOverlap += Before.Overlap; AfterOverlap += After.Overlap;
        }
        if (AfterCrossings > BeforeCrossings || AfterOverlap > BeforeOverlap + 0.01) return;
        foreach ((FlowchartLayoutEdgeRoute Route, Candidate Candidate) in Safe)
        {
            Route.Points.Clear();
            Route.Points.AddRange(Candidate.Points);
            Route.ExitSide = Candidate.ExitSide;
            Route.EntrySide = Candidate.EntrySide;
        }
    }

    private static bool HitsBox(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
            double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                FlowchartLayoutPoint A = Points[Index - 1], B = Points[Index];
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return true;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return true;
            }
        }
        return false;
    }

    private static List<(double X, double Y)> Tuples(IEnumerable<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToList();

    private static FlowchartLayoutPoint Point(double Main, double Cross, bool Horizontal) => Horizontal
        ? new FlowchartLayoutPoint { X = Main, Y = Cross }
        : new FlowchartLayoutPoint { X = Cross, Y = Main };

    private static string MainSide(bool Horizontal, double Sign) => Horizontal
        ? Sign > 0 ? "right" : "left"
        : Sign > 0 ? "top" : "bottom";

    private static string CrossSide(bool Horizontal, double Sign) => Horizontal
        ? Sign > 0 ? "top" : "bottom"
        : Sign > 0 ? "right" : "left";

}
