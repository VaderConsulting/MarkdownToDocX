using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Separates exact terminal tracks without moving their allocated ports or adding corners.</summary>
internal static class FlowchartTerminalTrackRepair
{
    /// <summary>A cardinal terminal ray, its owner, and its first bend in the completed route.</summary>
    internal sealed record Run(FlowchartLayoutEdgeRoute Route, bool Source, bool Horizontal,
        string Owner, double Coordinate, double Port, double Bend)
    {
        /// <summary>Lowest occupied coordinate along the ray.</summary>
        internal double Low => Math.Min(Port, Bend);
        /// <summary>Highest occupied coordinate along the ray.</summary>
        internal double High => Math.Max(Port, Bend);
    }

    /// <summary>Optional external diagnostic observer; never stored in candidate state or a route cache.</summary>
    internal static Action<string, string, string>? Observer;

    /// <summary>Finds overlapping parallel end runs in a common facing gap or on one owner's side.</summary>
    /// <param name="Layout">The exact candidate route geometry.</param>
    /// <returns>Run pairs below the configured track spacing, including fractional allocated ports.</returns>
    internal static IEnumerable<(Run First, Run Second)> Conflicts(FlowchartLayoutResult Layout)
    {
        var Runs = Layout.EdgeRoutes.Where(Route => Route.Points.Count > 2)
            .SelectMany(Route => new[] { Terminal(Route, true), Terminal(Route, false) })
            .OfType<Run>().ToArray();
        for (int I = 0; I < Runs.Length; I++)
        for (int J = I + 1; J < Runs.Length; J++)
        {
            var A = Runs[I]; var B = Runs[J];
            if (A.Route == B.Route || A.Horizontal != B.Horizontal
                || Math.Abs(A.Coordinate - B.Coordinate) >= DiagramGridMetrics.GRID_UNIT_PX - 1e-9
                || Math.Min(A.High, B.High) - Math.Max(A.Low, B.Low) <= 1e-9) continue;
            bool Facing = (B.Port - A.Port) * (A.Bend - A.Port) > 0
                && (A.Port - B.Port) * (B.Bend - B.Port) > 0;
            if (A.Owner == B.Owner || Facing) yield return (A, B);
        }
    }

    /// <summary>Tries existing ordered gap tracks before any slot reassignment or atomic gap growth.</summary>
    /// <param name="Contract">Declared links and honoured route hints.</param>
    /// <param name="Layout">Private completed candidate, or the final model before scoring.</param>
    /// <param name="Placed">Current authoritative node regions.</param>
    /// <param name="Bounds">Current node and frame geometry for independent legality checks.</param>
    /// <param name="OnlyRoutes">Candidate dependency set; untouched routes may not be modified.</param>
    /// <returns>The number of accepted first-bend track changes.</returns>
    internal static int Apply(DiagramContract Contract, FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Placed, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
    {
        var Pairs = Conflicts(Layout).ToArray();
        if (Pairs.Length == 0) return 0;
        int Kept = 0;
        FlowchartAllocatedPortFallback.PreparedGeometry? Prepared = null;
        foreach (var Pair in Pairs)
        {
            Observer?.Invoke("conflict", Key(Pair.First), Key(Pair.Second));
            foreach (var Run in new[] { Pair.First, Pair.Second })
            {
                int Examined = 0;
                var Route = Run.Route;
                if (Route.Points.Count < 4 || OnlyRoutes != null && !OnlyRoutes.Contains(Route)
                    || DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true) continue;
                var Original = Points(Route);
                var Others = Layout.EdgeRoutes.Where(Other => Other != Route).Select(Points).ToArray();
                var Before = Measure(Original, Others);
                int BeforeConflicts = Conflicts(Layout).Count();
                int BeforeBends = FlowchartLayoutScore.CountCorners(Route.Points);
                var OtherRun = Run == Pair.First ? Pair.Second : Pair.First;
                double Sign = Math.Sign(Run.Bend - Run.Port);
                double Separation = DiagramGridMetrics.GRID_UNIT_PX;
                // Preserve gap-track order: turn before the other occupied extent.
                // Existing tracks are preferred to opening a new lane.
                var Tracks = Layout.EdgeRoutes.SelectMany(Other => Other.Points.Skip(1).SkipLast(1))
                    .Select(Point => Run.Horizontal ? Point.X : Point.Y)
                    .Append(Sign > 0 ? OtherRun.Low - DiagramGridRouteBuilder.CORNER_RADIUS_PX
                        : OtherRun.High + DiagramGridRouteBuilder.CORNER_RADIUS_PX)
                    .Append(Run.Port + Sign * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
                    .Distinct().Where(Track => (Track - Run.Port) * Sign > 0
                        && (Run.Bend - Track) * Sign > 1e-9
                        && (Sign > 0 ? OtherRun.Low - Track : Track - OtherRun.High) > 1e-9)
                    .OrderBy(Track => Math.Abs(Track - Run.Bend));
                foreach (double Track in Tracks)
                {
                    if (Examined++ >= FlowchartLayoutRules.RepairCandidateMoves) break;
                    Observer?.Invoke("try-track", Key(Run), Track.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Restore(Route, Original);
                    int Index = Run.Source ? 1 : Route.Points.Count - 3;
                    if (!DiagramGridLayout.TryTranslateInternalRun(Route, Index, Run.Horizontal,
                            Track - Run.Bend, Placed)) { Observer?.Invoke("blocked-track", Key(Run), "geometry"); continue; }
                    var Candidate = Points(Route);
                    var After = Measure(Candidate, Others);
                    if (After.Crossings > Before.Crossings || After.Shared > Before.Shared
                        || Conflicts(Layout).Count() >= BeforeConflicts
                        || FlowchartLayoutScore.CountCorners(Route.Points) > BeforeBends) { Observer?.Invoke("blocked-track", Key(Run), $"score {Before} -> {After}"); continue; }
                    Prepared ??= FlowchartAllocatedPortFallback.Prepare(Contract, Bounds);
                    if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Candidate, Prepared)) { Observer?.Invoke("blocked-track", Key(Run), "normal/body/title"); continue; }
                    // Compare the final geometry, never a rounded proxy. The caller
                    // still validates/scorers the complete finished layout.
                    var Trial = new FlowchartLayoutResult();
                    Trial.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                    Trial.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                    Trial.EdgeRoutesMutable.Add(Route);
                    if (FlowchartRouteRuleChecker.Check(Contract, Trial).Any(Finding => Finding.Subject == Route.EdgeId
                        && Finding.Rule is "10" or "3.1" or "4.1" or "4.4")) { Observer?.Invoke("blocked-track", Key(Run), "rule"); continue; }
                    Original = Candidate;
                    Kept++;
                    Observer?.Invoke("track", Key(Pair.First), Key(Pair.Second));
                    break;
                }
                Restore(Route, Original);
            }
        }
        return Kept;
    }

    /// <summary>Builds a cardinal end-run descriptor without inferring a port from pixel rounding.</summary>
    /// <param name="Route">Completed route.</param>
    /// <param name="Source">Whether to inspect its source end.</param>
    /// <returns>A ray descriptor, or null for a diagonal normal.</returns>
    private static Run? Terminal(FlowchartLayoutEdgeRoute Route, bool Source)
    {
        var Port = Source ? Route.Points[0] : Route.Points[^1];
        var Bend = Source ? Route.Points[1] : Route.Points[^2];
        bool Horizontal = Math.Abs(Port.Y - Bend.Y) < 1e-9;
        if (!Horizontal && Math.Abs(Port.X - Bend.X) >= 1e-9) return null;
        return new(Route, Source, Horizontal, Source ? Route.SourceId : Route.TargetId,
            Horizontal ? Port.Y : Port.X, Horizontal ? Port.X : Port.Y, Horizontal ? Bend.X : Bend.Y);
    }

    /// <summary>Identifies a terminal consistently across candidate completion and rollback.</summary>
    private static string Key(Run Run) => Run.Route.EdgeId + (Run.Source ? ":source" : ":target");
    /// <summary>Copies route coordinates so rejected trials cannot mutate a saved baseline.</summary>
    private static (double X, double Y)[] Points(FlowchartLayoutEdgeRoute Route) => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
    /// <summary>Restores exact saved ports and bend coordinates, without quantisation or cache writes.</summary>
    private static void Restore(FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Saved)
    {
        Route.Points.Clear();
        foreach (var Point in Saved) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
    }
    /// <summary>Scores the complete changed path against every unchanged route using the drawn geometry.</summary>
    private static (int Crossings, double Shared) Measure((double X, double Y)[] Path, (double X, double Y)[][] Others)
    {
        int Crossings = 0; double Shared = 0;
        foreach (var Other in Others)
        {
            var Pair = FlowchartBranchRouteOrder.Intersections(Path, Other);
            Crossings += Pair.Crossings; Shared += Pair.Overlap;
        }
        return (Crossings, Shared);
    }
}
