using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Separates internal tracks while preserving allocated terminals and scoring complete legal separation.</summary>
internal static class FlowchartDistinctTrackRepair
{
    /// <summary>Offers bounded grid translations for shared internal runs, including routes with terminal-only hints.</summary>
    /// <param name="Contract">Link declarations and explicit travel constraints.</param>
    /// <param name="Layout">The authoritative final routes and frame bounds.</param>
    /// <param name="Placed">Node bodies used by the internal-run translator.</param>
    /// <param name="OnlyRoutes">Affected lines eligible for repair, or null for every line.</param>
    /// <returns>Whether an accepted translation reduced shared tracks or shared length; only complete separation may add crossings.</returns>
    internal static bool Apply(DiagramContract Contract, FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Placed, IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
    {
        if (OnlyRoutes is { Count: 0 }) return false;
        bool Changed = false;
        int GlobalShared = FlowchartRouteRuleChecker.Check(Contract, Layout).Count(Finding => Finding.Rule == "5.1");
        foreach (var Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 4 && (OnlyRoutes == null || OnlyRoutes.Contains(Route))))
        {
            // Entry/exit hints constrain the unchanged terminals. A travel hint
            // also constrains the interior, so that family is left untouched.
            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null }) continue;
            var Others = Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2).ToArray();
            var Paths = Others.Select(Other => Other.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            var Original = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            var Before = Measure(Route, Others, Paths);
            if (Before.Shared == 0) continue;
            var Best = Before;
            var BestCompleteCost = FlowchartLayoutScore.ComparisonCost.PositiveInfinity;
            (double X, double Y)[]? BestPoints = null;
            int Examined = 0;
            var Runs = Enumerable.Range(1, Original.Length - 3).Where(Index => Paths.Any(Path =>
                FlowchartBranchRouteOrder.Intersections(new[] { Original[Index], Original[Index + 1] }, Path).Overlap > 0.001)).ToArray();
            if (Runs.Length == 0) continue;
            for (int Distance = 1; Examined < FlowchartLayoutRules.RepairCandidateMoves; Distance++)
            foreach (int Index in Runs)
            {
                bool Vertical = Math.Abs(Original[Index].X - Original[Index + 1].X) < 0.5;
                foreach (int Sign in new[] { -1, 1 })
                {
                    if (Examined++ >= FlowchartLayoutRules.RepairCandidateMoves) break;
                    Restore(Route, Original);
                    if (!DiagramGridLayout.TryTranslateInternalRun(Route, Index, Vertical,
                            Sign * Distance * DiagramGridMetrics.GRID_UNIT_PX, Placed)) continue;
                    var Candidate = Measure(Route, Others, Paths);
                    bool CompletesSeparation = Candidate.Shared == 0 && GlobalShared == Before.Shared;
                    if (Candidate.Shared > Before.Shared || Candidate.Crossings > Before.Crossings && !CompletesSeparation) continue;
                    var CompleteCost = CompletesSeparation
                        ? FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false).Cost
                            + RouteLength(Route) / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                        : FlowchartLayoutScore.ComparisonCost.PositiveInfinity;
                    // Shared tracks are a legality failure. Once all are gone,
                    // compare complete candidates with the existing whole-layout
                    // score and repair-length weight, including any added crossing.
                    if (CompletesSeparation ? CompleteCost >= BestCompleteCost : Candidate.CompareTo(Best) >= 0) continue;
                    var Trial = new FlowchartLayoutResult();
                    Trial.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                    Trial.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                    Trial.EdgeRoutesMutable.Add(Route);
                    if (FlowchartRouteRuleChecker.Check(Contract, Trial).Any(Finding =>
                            Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1")) continue;
                    Best = Candidate;
                    if (CompletesSeparation) BestCompleteCost = CompleteCost;
                    BestPoints = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                }
            }
            Restore(Route, BestPoints ?? Original);
            if (BestPoints != null) GlobalShared += Best.Shared - Before.Shared;
            Changed |= BestPoints != null;
        }
        return Changed;
    }

    /// <summary>Measures the changed connector's travelled length for the existing repair-length score.</summary>
    /// <param name="Route">The complete candidate, including its unchanged allocated terminals.</param>
    /// <returns>The polyline length in pixels.</returns>
    private static double RouteLength(FlowchartLayoutEdgeRoute Route) => Route.Points.Zip(Route.Points.Skip(1))
        .Sum(Pair => Math.Sqrt(Math.Pow(Pair.Second.X - Pair.First.X, 2) + Math.Pow(Pair.Second.Y - Pair.First.Y, 2)));

    /// <summary>Measures the same positive shared segments and proper crossing pairs used by the audit.</summary>
    /// <param name="Route">The changed route currently installed in the layout.</param>
    /// <param name="Others">Unchanged routes paired with the candidate.</param>
    /// <param name="Paths">Their cached immutable geometry.</param>
    /// <returns>Pair counts followed by shared length for a stable improvement among equal pair counts.</returns>
    private static (int Shared, int Crossings, double Length) Measure(FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutEdgeRoute[] Others, (double X, double Y)[][] Paths)
    {
        var Path = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        int Shared = 0, Crossings = 0;
        double Length = 0;
        for (int Index = 0; Index < Others.Length; Index++)
        {
            double Overlap = FlowchartBranchRouteOrder.Intersections(Path, Paths[Index]).Overlap;
            if (Overlap > 0.001) Shared++;
            Length += Overlap;
            if ((Route.SourceId != Others[Index].SourceId || Route.TargetId != Others[Index].TargetId)
                && FlowchartRouteRuleChecker.HasProperCrossing(Route, Others[Index])) Crossings++;
        }
        return (Shared, Crossings, Length);
    }

    /// <summary>Restores exact coordinates without rounding fractional port slots.</summary>
    /// <param name="Route">The mutable route whose exact geometry is restored.</param>
    /// <param name="Points">The saved baseline or complete winning candidate.</param>
    private static void Restore(FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points)
    {
        Route.Points.Clear();
        foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
    }
}
