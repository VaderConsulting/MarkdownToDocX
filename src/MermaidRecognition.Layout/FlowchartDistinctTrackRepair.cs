using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Separates internal tracks while preserving both audited pair counts and allocated terminals.</summary>
internal static class FlowchartDistinctTrackRepair
{
    /// <summary>Offers bounded grid translations for shared internal runs, including routes with terminal-only hints.</summary>
    /// <param name="Contract">Link declarations and explicit travel constraints.</param>
    /// <param name="Layout">The authoritative final routes and frame bounds.</param>
    /// <param name="Placed">Node bodies used by the internal-run translator.</param>
    /// <returns>Whether an accepted translation reduced shared tracks or shared length without adding crossings.</returns>
    internal static bool Apply(DiagramContract Contract, FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Placed)
    {
        bool Changed = false;
        foreach (var Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 4))
        {
            // Entry/exit hints constrain the unchanged terminals. A travel hint
            // also constrains the interior, so that family is left untouched.
            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null }) continue;
            var Others = Layout.EdgeRoutes.Where(Other => Other != Route && Other.Points.Count >= 2).ToArray();
            var Paths = Others.Select(Other => Other.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            var Original = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            var Before = Measure();
            if (Before.Shared == 0) continue;
            var Best = Before;
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
                    Restore(Original);
                    if (!DiagramGridLayout.TryTranslateInternalRun(Route, Index, Vertical,
                            Sign * Distance * DiagramGridMetrics.GRID_UNIT_PX, Placed)) continue;
                    var Candidate = Measure();
                    if (Candidate.Shared > Before.Shared || Candidate.Crossings > Before.Crossings
                        || Candidate.CompareTo(Best) >= 0) continue;
                    var Trial = new FlowchartLayoutResult();
                    Trial.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                    Trial.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                    Trial.EdgeRoutesMutable.Add(Route);
                    if (FlowchartRouteRuleChecker.Check(Contract, Trial).Any(Finding =>
                            Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1")) continue;
                    Best = Candidate;
                    BestPoints = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                }
            }
            Restore(BestPoints ?? Original);
            Changed |= BestPoints != null;

            /// <summary>Measures the same positive shared segments and proper crossing pairs used by the audit.</summary>
            /// <returns>Pair counts followed by shared length for a stable improvement among equal pair counts.</returns>
            (int Shared, int Crossings, double Length) Measure()
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
            /// <param name="Points">The saved baseline or complete winning candidate.</param>
            void Restore((double X, double Y)[] Points)
            {
                Route.Points.Clear();
                foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
        }
        return Changed;
    }
}
