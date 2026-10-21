using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Allocates free tracks while a fallback route is constructed, before it is validated or published.</summary>
internal static class FlowchartFallbackCorridorTracks
{
    /// <summary>Restricts occupancy allocation to construction from final ports, so exploratory repairs retain their established inputs and order.</summary>
    /// <param name="Context">Current layout candidate's state.</param>
    /// <returns>Whether a grown initial corridor is being routed in final model construction.</returns>
    internal static bool IsConstructionStage(FlowchartRenderContext Context) =>
        Context.ActiveLayoutStage == "model-route-completion-and-rule-checks"
        && HasReservedCapacity(Context);

    /// <summary>Identifies layouts whose initial producer reserved corridor tracks, without activating routing in an earlier repair.</summary>
    /// <param name="Context">Owning immutable-capacity evidence.</param>
    /// <returns>Whether this layout has a grown initial routing gap.</returns>
    internal static bool HasReservedCapacity(FlowchartRenderContext Context) =>
        Context.HasInitialCorridorCapacity && !Context.SuppressReservedCorridorAssignment;

    /// <summary>Uses reserved free strips instead of independently choosing the same shortest corridor rail for every fallback.</summary>
    /// <param name="Contract">Declared membership and protected titles.</param>
    /// <param name="Bounds">Fixed node and frame geometry.</param>
    /// <param name="Route">Exact allocated ports and normals, never reassigned here.</param>
    /// <param name="Path">Unpublished normal-preserving visibility result.</param>
    /// <param name="Existing">Already constructed paths in this complete layout candidate.</param>
    /// <param name="Prepared">Optional candidate-scoped legality inventory.</param>
    /// <returns>A legal distinct-track path with no additional corners or crossings, or the original path when no such allocation fits.</returns>
    internal static List<(double X, double Y)> Allocate(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartLayoutEdgeRoute Route,
        List<(double X, double Y)> Path, IReadOnlyList<FlowchartLayoutEdgeRoute> Existing,
        FlowchartAllocatedPortFallback.PreparedGeometry? Prepared)
    {
        var Others = Existing.Where(Other => Other != Route && Other.EdgeId != Route.EdgeId && Other.Points.Count >= 2)
            .Select(Other => Other.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
        var Before = Measure(Path, Others);
        var Previous = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
        if (Path.Count >= 2 && Previous.Count >= 2 && Previous[0] == Path[0] && Previous[^1] == Path[^1]
            && FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Previous, Prepared))
        {
            var Prior = Measure(Previous, Others);
            if (Prior.Shared <= Before.Shared && Prior.Crossings <= Before.Crossings
                && (Prior.Shared < Before.Shared || Prior.Crossings < Before.Crossings)
                && FlowchartLayoutScore.CountCorners(Route.Points) <= FlowchartLayoutScore.CountCorners(
                    Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray()))
            {
                // A geometry-only cache is a route proposal, not permission to
                // throw away an already legal, less-interfering construction.
                Path = Previous; Before = Prior;
            }
        }
        if (Before.Shared == 0 && Before.Crossings == 0 && Before.Close == 0) return Path;
        var Groups = Contract.Groups.Select(Group => Group.Id).ToHashSet(StringComparer.Ordinal);
        var Bodies = Bounds.Values.Where(Box => !Groups.Contains(Box.NodeId)).ToArray();
        int Budget = FlowchartLayoutRules.RepairCandidateMoves;
        int Bends = FlowchartLayoutScore.CountCorners(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray());
        bool Changed = false;
        for (int Depth = 0; Depth < FlowchartLayoutRules.RepairChainDepth && Budget > 0; Depth++)
        {
            List<(double X, double Y)>? Best = null;
            var BestMeasure = Before;
            for (int Index = 1; Index < Path.Count - 2 && Budget > 0; Index++)
            {
                var A = Path[Index]; var B = Path[Index + 1];
                bool Horizontal = Math.Abs(A.Y - B.Y) < 0.001;
                if (!Horizontal && Math.Abs(A.X - B.X) >= 0.001) continue;
                double Coordinate = Horizontal ? A.Y : A.X;
                var Intervals = Bodies.Select(Box => (Low: Horizontal ? Box.CenterY - Box.Height / 2 : Box.CenterX - Box.Width / 2,
                    High: Horizontal ? Box.CenterY + Box.Height / 2 : Box.CenterX + Box.Width / 2)).ToArray();
                if (Intervals.Any(Interval => Coordinate > Interval.Low + 0.001 && Coordinate < Interval.High - 0.001)) continue;
                double Low = Intervals.Where(Interval => Interval.High <= Coordinate + 0.001).Select(Interval => Interval.High).DefaultIfEmpty(double.NaN).Max();
                double High = Intervals.Where(Interval => Interval.Low >= Coordinate - 0.001).Select(Interval => Interval.Low).DefaultIfEmpty(double.NaN).Min();
                if (!double.IsFinite(Low) || !double.IsFinite(High) || High <= Low) continue;
                double Step = FlowchartLayoutRules.LineSpacing;
                double First = Math.Ceiling((Low + DiagramGridRouteBuilder.CORNER_RADIUS_PX) / Step) * Step;
                double Last = Math.Floor((High - DiagramGridRouteBuilder.CORNER_RADIUS_PX) / Step) * Step;
                int Count = (int)Math.Floor((Last - First) / Step) + 1;
                if (Count < 1) continue;
                var Options = Enumerable.Range(0, Count).Select(Slot => First + Slot * Step)
                    .Where(Track => Math.Abs(Track - Coordinate) > 0.001).Select(Track =>
                    {
                        var Candidate = Path.ToList();
                        Candidate[Index] = Horizontal ? (A.X, Track) : (Track, A.Y);
                        Candidate[Index + 1] = Horizontal ? (B.X, Track) : (Track, B.Y);
                        return (Track, Candidate, Measure: Measure(Candidate, Others));
                    }).Where(Option => RunHasClearance(Option.Candidate[Index], Option.Candidate[Index + 1], Others, Step)
                        && Option.Measure.Shared <= Before.Shared && Option.Measure.Crossings <= Before.Crossings
                        && (Option.Measure.Shared < Before.Shared || Option.Measure.Crossings < Before.Crossings || Option.Measure.Close < Before.Close))
                    .Where(Option => Option.Measure.Close <= Before.Close)
                    .OrderBy(Option => Option.Measure.Close).ThenBy(Option => Option.Measure.Shared).ThenBy(Option => Option.Measure.Crossings)
                    .ThenBy(Option => Math.Abs(Option.Track - Coordinate));
                foreach (var Option in Options)
                {
                    if (Budget-- <= 0) break;
                    if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Option.Candidate, Prepared)
                        || FlowchartLayoutScore.CountCorners(Option.Candidate.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray()) > Bends) continue;
                    if (Option.Measure.Shared > BestMeasure.Shared || Option.Measure.Crossings > BestMeasure.Crossings || Option.Measure.Close > BestMeasure.Close) continue;
                    Best = Option.Candidate; BestMeasure = Option.Measure;
                    break;
                }
            }
            if (Best == null) break;
            Path = Best; Before = BestMeasure; Changed = true;
        }
        if (Changed) FlowchartRenderContext.Current.BandSpaceDiagnostics.Add($"FALLBACK_CORRIDOR_TRACK|{Route.EdgeId}|shared={Before.Shared}|crossings={Before.Crossings}");
        return Path;
    }

    /// <summary>Reserves the full configured separation for the changed run wherever another parallel run has overlapping extent.</summary>
    /// <param name="A">Changed run's first point.</param>
    /// <param name="B">Changed run's second point.</param>
    /// <param name="Others">Other complete drawn polylines.</param>
    /// <param name="Separation">Existing track spacing.</param>
    /// <returns>False for a shared or too-close parallel run, without modifying either route.</returns>
    private static bool RunHasClearance((double X, double Y) A, (double X, double Y) B,
        IReadOnlyList<(double X, double Y)[]> Others, double Separation)
    {
        bool Horizontal = Math.Abs(A.Y - B.Y) < 0.001;
        foreach (var Other in Others)
        for (int Index = 1; Index < Other.Length; Index++)
        {
            var C = Other[Index - 1]; var D = Other[Index];
            if (Horizontal ? Math.Abs(C.Y - D.Y) >= 0.001 : Math.Abs(C.X - D.X) >= 0.001) continue;
            double Distance = Horizontal ? Math.Abs(A.Y - C.Y) : Math.Abs(A.X - C.X);
            if (Distance >= Separation - 0.001) continue;
            double Low = Horizontal ? Math.Max(Math.Min(A.X, B.X), Math.Min(C.X, D.X)) : Math.Max(Math.Min(A.Y, B.Y), Math.Min(C.Y, D.Y));
            double High = Horizontal ? Math.Min(Math.Max(A.X, B.X), Math.Max(C.X, D.X)) : Math.Min(Math.Max(A.Y, B.Y), Math.Max(C.Y, D.Y));
            if (High - Low > 0.001) return false;
        }
        return true;
    }

    /// <summary>Measures interactions with the other complete paths without changing routes, ports or diagnostics.</summary>
    /// <param name="Path">Unpublished path.</param>
    /// <param name="Others">Other exact drawn polylines.</param>
    /// <returns>Number of overlapping line pairs, physical crossings and under-spaced parallel segment pairs.</returns>
    private static (int Shared, int Crossings, int Close) Measure(IReadOnlyList<(double X, double Y)> Path,
        IReadOnlyList<(double X, double Y)[]> Others)
    {
        int Shared = 0, Crossings = 0, Close = 0;
        foreach (var Other in Others)
        {
            var Intersection = FlowchartBranchRouteOrder.Intersections(Path, Other);
            if (Intersection.Overlap > 0.001) Shared++;
            Crossings += Intersection.Crossings;
            Close += CloseParallelSegments(Path, Other);
        }
        return (Shared, Crossings, Close);
    }

    /// <summary>Measures every parallel overlapping run using exact drawn coordinates, including fractional terminal tracks.</summary>
    /// <param name="Path">Candidate polyline.</param>
    /// <param name="Other">Unchanged neighbouring polyline.</param>
    /// <returns>The number of segment pairs closer than the configured track spacing.</returns>
    internal static int CloseParallelSegments(IReadOnlyList<(double X, double Y)> Path,
        IReadOnlyList<(double X, double Y)> Other)
    {
        int Count = 0;
        for (int I = 1; I < Path.Count; I++)
        {
            var A = Path[I - 1]; var B = Path[I];
            bool H = Math.Abs(A.Y - B.Y) < 0.001;
            if (H ? Math.Abs(A.X - B.X) < 0.001 : Math.Abs(A.X - B.X) >= 0.001) continue;
            for (int J = 1; J < Other.Count; J++)
            {
                var C = Other[J - 1]; var D = Other[J];
                if (H ? Math.Abs(C.Y - D.Y) >= 0.001 || Math.Abs(C.X - D.X) < 0.001
                    : Math.Abs(C.X - D.X) >= 0.001 || Math.Abs(C.Y - D.Y) < 0.001) continue;
                double Distance = H ? Math.Abs(A.Y - C.Y) : Math.Abs(A.X - C.X);
                double Low = H ? Math.Max(Math.Min(A.X, B.X), Math.Min(C.X, D.X)) : Math.Max(Math.Min(A.Y, B.Y), Math.Min(C.Y, D.Y));
                double High = H ? Math.Min(Math.Max(A.X, B.X), Math.Max(C.X, D.X)) : Math.Min(Math.Max(A.Y, B.Y), Math.Max(C.Y, D.Y));
                if (Distance < FlowchartLayoutRules.LineSpacing - 0.001 && High > Low + 0.001) Count++;
            }
        }
        return Count;
    }
}
