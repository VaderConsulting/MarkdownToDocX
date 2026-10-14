using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Replaces convergence S-jogs with the one-corner route required by rule 4.11.
/// Fan and complete adjacent-rank block routes retain their higher-precedence routing.
/// Every accepted change is measured on the complete routed diagram.
/// </summary>
internal static class FlowchartConvergenceRouteRepair
{
    private const double EPSILON = 0.5;
    private static readonly double MinimumRun = DiagramGridRouteBuilder.CORNER_RADIUS_PX
        + DiagramGridMetrics.GRID_UNIT_PX;

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract)
    {
        if (Layout.EdgeRoutes.Count == 0) return;
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        HashSet<(string Source, string Target)> BlockPairs = CompleteAdjacentRankBlockPairs(Contract, Bounds, Direction);
        FlowchartRouteSpatialIndex Spatial = new(Layout);
        var ScoreContext = new FlowchartLayoutScore.IncrementalContext(
            Contract, Layout, IncludeArrangementCheck: false);

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.ToList())
        {
            if (Route.Points.Count < 3 || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)
                || IncomingCount(Contract, Route.TargetId) < 2
                || IsHinted(Contract, Route) || BlockPairs.Contains((Route.SourceId, Route.TargetId))
                || IsFan(Contract, Bounds, Route, Direction)
                || Contract.Nodes.Any(Node => (Node.Id == Route.SourceId || Node.Id == Route.TargetId)
                    && FlowchartDagrePresentation.IsDiamondShape(Node.Shape))) continue;

            (double SourceCross, double SourceFlow, double SourceHalfCross, double SourceHalfFlow) = Axes(Source, Direction);
            (double TargetCross, double TargetFlow, double TargetHalfCross, double TargetHalfFlow) = Axes(Target, Direction);
            if (Math.Abs(SourceCross - TargetCross) <= EPSILON) continue;

            (double X, double Y) TopCentre = Point(TargetCross, TargetFlow - TargetHalfFlow, Direction);
            bool TopFree = !Layout.EdgeRoutes.Any(Other => Other != Route && Other.TargetId == Route.TargetId
                && Other.Points.Count > 0 && Near(Other.Points[^1], TopCentre));
            bool SourceIsLeft = SourceCross < TargetCross;
            (double X, double Y) Start = TopFree
                ? Point(SourceCross + (SourceIsLeft ? SourceHalfCross : -SourceHalfCross), SourceFlow, Direction)
                : Point(SourceCross, SourceFlow + SourceHalfFlow, Direction);
            (double X, double Y) Corner = TopFree
                ? Point(TargetCross, SourceFlow, Direction)
                : Point(SourceCross, TargetFlow, Direction);
            (double X, double Y) End = TopFree ? TopCentre : Point(
                TargetCross + (SourceIsLeft ? -TargetHalfCross : TargetHalfCross), TargetFlow, Direction);
            List<FlowchartLayoutPoint> Candidate = [New(Start), New(Corner), New(End)];
            if (!Legal(Candidate, Route, Layout, Contract, Bounds, Spatial)) continue;

            List<FlowchartLayoutPoint> Original = Route.Points.Select(Copy).ToList();
            string OriginalExit = Route.ExitSide;
            string OriginalEntry = Route.EntrySide;
            IReadOnlyCollection<FlowchartLayoutEdgeRoute> BeforeInteractions =
                Spatial.QueryRoutes(Original, Route);
            FlowchartLayoutScore.CandidateMeasurement Before =
                ScoreContext.MeasureCandidateSummary(Route, BeforeInteractions);
            Route.Points.Clear(); Route.Points.AddRange(Candidate.Select(Copy));
            Route.ExitSide = TopFree ? CrossSide(!SourceIsLeft, Direction) : DownstreamSide(Direction);
            Route.EntrySide = TopFree ? UpstreamSide(Direction) : CrossSide(SourceIsLeft, Direction);
            HashSet<FlowchartLayoutEdgeRoute> CandidateInteractions =
                Spatial.QueryRoutes(Candidate, Route).ToHashSet();
            CandidateInteractions.UnionWith(BeforeInteractions);
            FlowchartLayoutScore.CandidateMeasurement After =
                ScoreContext.MeasureCandidateSummary(Route, CandidateInteractions);
            bool Keep = After.Overlaps == 0 && After.Crossings <= Before.Crossings
                && (Before.Score.HasValue
                    ? After.Score.HasValue && After.Score.Value < Before.Score.Value - 0.001
                    : After.RejectingFaults < Before.RejectingFaults
                        || After.RejectingFaults == Before.RejectingFaults
                            && Corners(Candidate) < Corners(Original));
            if (!Keep)
            {
                Route.Points.Clear(); Route.Points.AddRange(Original);
                Route.ExitSide = OriginalExit; Route.EntrySide = OriginalEntry;
            }
            else
            {
                HashSet<FlowchartLayoutEdgeRoute> Touched = Spatial.QueryRoutes(Original, Route).ToHashSet();
                Touched.UnionWith(CandidateInteractions);
                ScoreContext.CommitCurrent(Route, Touched);
                Spatial.ReplaceRoute(Route);
            }
        }
    }

    private static int RejectingFaults(FlowchartLayoutScore.Result Result) =>
        Result.Violations.Count(Failure => Failure.Kind is not "ArrangementKept" and not "NodeOverlap");

    private static int Corners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Count = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
            if ((Math.Abs(Points[Index].Y - Points[Index - 1].Y) < EPSILON)
                != (Math.Abs(Points[Index + 1].Y - Points[Index].Y) < EPSILON)) Count++;
        return Count;
    }

    private static bool Legal(IReadOnlyList<FlowchartLayoutPoint> Points, FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartRouteSpatialIndex Spatial)
    {
        if (Points.Count != 3 || Distance(Points[0], Points[1]) + EPSILON < MinimumRun
            || Distance(Points[1], Points[2]) + EPSILON < MinimumRun) return false;
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId
                || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.SourceId)
                || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.TargetId)) continue;
            for (int Index = 1; Index < Points.Count; Index++)
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y, Box)) return false;
        }
        List<(double X, double Y)> Candidate = Points.Select(Point => (Point.X, Point.Y)).ToList();
        List<(double X, double Y)> Original = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
        foreach (FlowchartLayoutEdgeRoute Other in Spatial.QueryRoutes(Points, Route))
        {
            List<(double X, double Y)> OtherPoints = Other.Points.Select(Point => (Point.X, Point.Y)).ToList();
            var Before = FlowchartBranchRouteOrder.Intersections(Original, OtherPoints);
            var After = FlowchartBranchRouteOrder.Intersections(Candidate, OtherPoints);
            if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + EPSILON) return false;
        }
        return true;
    }

    private static bool IsHinted(DiagramContract Contract, FlowchartLayoutEdgeRoute Route) =>
        Contract.Edges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId
            && Edge.RoutingHint?.IsActive == true);

    private static int IncomingCount(DiagramContract Contract, string Target) => Contract.Edges.Count(Edge =>
        Edge.TargetId == Target && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase));

    private static bool IsFan(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route, FlowchartDirection Direction)
    {
        List<FlowchartLayoutNodeBounds> Out = Contract.Edges.Where(Visible)
            .Where(Edge => Edge.SourceId == Route.SourceId && Bounds.ContainsKey(Edge.TargetId))
            .Select(Edge => Bounds[Edge.TargetId]).ToList();
        // A same-rank peer set is structurally a fan. Preserve it here even
        // when its current placement has not yet reached the expected side;
        // otherwise fixing convergence merely exposes a new fan-side fault.
        if (Out.Count >= 2 && SameRank(Out, Direction)) return true;
        List<FlowchartLayoutNodeBounds> In = Contract.Edges.Where(Visible)
            .Where(Edge => Edge.TargetId == Route.TargetId && Bounds.ContainsKey(Edge.SourceId))
            .Select(Edge => Bounds[Edge.SourceId]).ToList();
        return In.Count >= 2 && SameRank(In, Direction);
    }

    private static HashSet<(string Source, string Target)> CompleteAdjacentRankBlockPairs(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartDirection Direction)
    {
        var Result = new HashSet<(string, string)>();
        var Edges = Contract.Edges.Where(Visible)
            .Where(Edge => Bounds.ContainsKey(Edge.SourceId) && Bounds.ContainsKey(Edge.TargetId)).ToList();
        List<double> Ranks = Bounds.Values.Select(Box => Math.Round(Axes(Box, Direction).Flow, 3))
            .Distinct().Order().ToList();
        var RankIndex = Ranks.Select((Rank, Index) => (Rank, Index)).ToDictionary(Item => Item.Rank, Item => Item.Index);
        foreach (var RankPair in Edges.GroupBy(Edge =>
                 (Source: Math.Round(Axes(Bounds[Edge.SourceId], Direction).Flow, 3),
                  Target: Math.Round(Axes(Bounds[Edge.TargetId], Direction).Flow, 3))))
        {
            if (Math.Abs(RankIndex[RankPair.Key.Source] - RankIndex[RankPair.Key.Target]) != 1) continue;
            List<(string Source, string Target)> Pairs = RankPair.Select(Edge => (Edge.SourceId, Edge.TargetId)).Distinct().ToList();
            var RemainingSources = Pairs.Select(Pair => Pair.Source).ToHashSet(StringComparer.Ordinal);
            while (RemainingSources.Count > 0)
            {
                var Sources = new HashSet<string>(StringComparer.Ordinal) { RemainingSources.Order(StringComparer.Ordinal).First() };
                var Targets = new HashSet<string>(StringComparer.Ordinal);
                bool Changed;
                do
                {
                    Changed = false;
                    foreach ((string Source, string Target) in Pairs)
                    {
                        if (Sources.Contains(Source) && Targets.Add(Target)) Changed = true;
                        if (Targets.Contains(Target) && Sources.Add(Source)) Changed = true;
                    }
                } while (Changed);
                RemainingSources.ExceptWith(Sources);
                int Present = Pairs.Count(Pair => Sources.Contains(Pair.Source) && Targets.Contains(Pair.Target));
                if (Sources.Count >= 2 && Targets.Count >= 2 && Present == Sources.Count * Targets.Count)
                    foreach (var Pair in Pairs.Where(Pair => Sources.Contains(Pair.Source) && Targets.Contains(Pair.Target)))
                        Result.Add(Pair);
            }
        }
        return Result;
    }

    private static bool Visible(EdgeContract Edge) =>
        !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
    private static bool SameRank(IReadOnlyList<FlowchartLayoutNodeBounds> Boxes, FlowchartDirection Direction) =>
        Boxes.Select(Box => Math.Round(Axes(Box, Direction).Flow, 3)).Distinct().Count() == 1;
    private static (double Cross, double Flow, double HalfCross, double HalfFlow) Axes(
        FlowchartLayoutNodeBounds Box, FlowchartDirection Direction)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        (double Cross, double Flow) = Axes(Box.CenterX, Box.CenterY, Direction);
        return (Cross, Flow, Horizontal ? Box.Height / 2 : Box.Width / 2,
            Horizontal ? Box.Width / 2 : Box.Height / 2);
    }
    private static (double Cross, double Flow) Axes(double X, double Y, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => (X, -Y), FlowchartDirection.BottomToTop => (X, Y),
        FlowchartDirection.LeftToRight => (Y, X), FlowchartDirection.RightToLeft => (Y, -X), _ => (X, -Y)
    };
    private static (double X, double Y) Point(double Cross, double Flow, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => (Cross, -Flow), FlowchartDirection.BottomToTop => (Cross, Flow),
        FlowchartDirection.LeftToRight => (Flow, Cross), FlowchartDirection.RightToLeft => (-Flow, Cross), _ => (Cross, -Flow)
    };
    private static string UpstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "top", FlowchartDirection.BottomToTop => "bottom",
        FlowchartDirection.LeftToRight => "left", _ => "right"
    };
    private static string DownstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "bottom", FlowchartDirection.BottomToTop => "top",
        FlowchartDirection.LeftToRight => "right", _ => "left"
    };
    private static string CrossSide(bool Negative, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop => Negative ? "left" : "right",
        _ => Negative ? "bottom" : "top"
    };
    private static FlowchartLayoutPoint New((double X, double Y) Point) => new() { X = Math.Round(Point.X), Y = Math.Round(Point.Y) };
    private static FlowchartLayoutPoint Copy(FlowchartLayoutPoint Point) => new() { X = Point.X, Y = Point.Y };
    private static double Distance(FlowchartLayoutPoint A, FlowchartLayoutPoint B) => Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    private static bool Near(FlowchartLayoutPoint Point, (double X, double Y) Other) =>
        Math.Abs(Point.X - Other.X) <= EPSILON && Math.Abs(Point.Y - Other.Y) <= EPSILON;
}
