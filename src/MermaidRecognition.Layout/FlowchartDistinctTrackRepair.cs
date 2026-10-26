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
        if (OnlyRoutes is { Count: 0 })
        {
            return false;
        }

        FlowchartLayoutEdgeRoute[] AllRoutes = Layout.EdgeRoutes.ToArray();
        Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> PathCache = new Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]>();
        foreach (FlowchartLayoutEdgeRoute Route in AllRoutes)
        {
            PathCache[Route] = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        }

        bool Changed = false;
        int GlobalShared = FlowchartRouteRuleChecker.Check(Contract, Layout).Count(Finding => Finding.Rule == "5.1");
        foreach (FlowchartLayoutEdgeRoute Route in AllRoutes.Where(Route => Route.Points.Count >= 4 && (OnlyRoutes == null || OnlyRoutes.Contains(Route))))
        {
            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null })
            {
                continue;
            }

            FlowchartLayoutEdgeRoute[] Others = AllRoutes.Where(Other => Other != Route && Other.Points.Count >= 2).ToArray();
            (double X, double Y)[][] OtherPaths = Others.Select(Other => PathCache[Other]).ToArray();
            FlowchartRouteSegmentSpatialIndex SegmentIndex = new FlowchartRouteSegmentSpatialIndex(OtherPaths);
            (double X, double Y)[] Original = PathCache[Route];
            (int Shared, int Crossings, double Length) Before = Measure(Route, Others, PathCache, SegmentIndex);
            if (Before.Shared == 0)
            {
                continue;
            }

            (int Shared, int Crossings, double Length) Best = Before;
            FlowchartLayoutScore.ComparisonCost BestCompleteCost = FlowchartLayoutScore.ComparisonCost.PositiveInfinity;
            (double X, double Y)[]? BestPoints = null;
            int Examined = 0;
            int[] Runs = Enumerable.Range(1, Original.Length - 3)
                .Where(Index => SegmentIndex.SegmentOverlapsAnyPath(Original[Index].X, Original[Index].Y, Original[Index + 1].X, Original[Index + 1].Y))
                .ToArray();
            if (Runs.Length == 0)
            {
                continue;
            }

            for (int Distance = 1; Examined < FlowchartLayoutRules.RepairCandidateMoves; Distance++)
            {
                foreach (int Index in Runs)
                {
                    bool Vertical = Math.Abs(Original[Index].X - Original[Index + 1].X) < 0.5;
                    foreach (int Sign in new[] { -1, 1 })
                    {
                        if (Examined++ >= FlowchartLayoutRules.RepairCandidateMoves)
                        {
                            break;
                        }

                        Restore(Route, Original);
                        double Step = Math.Max(DiagramGridMetrics.GRID_UNIT_PX, FlowchartLayoutRules.LineSpacing);
                        if (!DiagramGridLayout.TryTranslateInternalRun(Route, Index, Vertical, Sign * Distance * Step, Placed, Layout))
                        {
                            continue;
                        }

                        PathCache[Route] = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                        (int Shared, int Crossings, double Length) Candidate = Measure(Route, Others, PathCache, SegmentIndex);
                        bool CompletesSeparation = Candidate.Shared == 0 && GlobalShared == Before.Shared;
                        if (Candidate.Shared > Before.Shared || Candidate.Crossings > Before.Crossings && !CompletesSeparation)
                        {
                            continue;
                        }

                        FlowchartLayoutScore.ComparisonCost CompleteCost = CompletesSeparation
                            ? FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false).Cost
                                + RouteLength(Route) / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                            : FlowchartLayoutScore.ComparisonCost.PositiveInfinity;
                        if (CompletesSeparation ? CompleteCost >= BestCompleteCost : Candidate.CompareTo(Best) >= 0)
                        {
                            continue;
                        }

                        FlowchartLayoutResult Trial = new FlowchartLayoutResult();
                        Trial.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                        Trial.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                        Trial.EdgeRoutesMutable.Add(Route);
                        if (FlowchartRouteRuleChecker.Check(Contract, Trial).Any(Finding =>
                                Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1"))
                        {
                            continue;
                        }

                        Best = Candidate;
                        if (CompletesSeparation)
                        {
                            BestCompleteCost = CompleteCost;
                        }

                        BestPoints = PathCache[Route];
                    }
                }
            }

            Restore(Route, BestPoints ?? Original);
            PathCache[Route] = BestPoints ?? Original;
            if (BestPoints != null)
            {
                GlobalShared += Best.Shared - Before.Shared;
            }

            Changed |= BestPoints != null;
        }

        return Changed;
    }

    private static double RouteLength(FlowchartLayoutEdgeRoute Route) => Route.Points.Zip(Route.Points.Skip(1))
        .Sum(Pair => Math.Sqrt(Math.Pow(Pair.Second.X - Pair.First.X, 2) + Math.Pow(Pair.Second.Y - Pair.First.Y, 2)));

    private static (int Shared, int Crossings, double Length) Measure(FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutEdgeRoute[] Others, Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> PathCache,
        FlowchartRouteSegmentSpatialIndex SegmentIndex)
    {
        (double X, double Y)[] Path = PathCache[Route];
        int Shared = 0;
        int Crossings = 0;
        double Length = 0;
        HashSet<int> Relevant = new HashSet<int>(SegmentIndex.PathsOverlapping(Path));
        bool ScanAll = Relevant.Count == 0 && Path.Length >= 2;
        for (int Index = 0; Index < Others.Length; Index++)
        {
            if (!ScanAll && !Relevant.Contains(Index))
            {
                continue;
            }

            (double X, double Y)[] OtherPath = PathCache[Others[Index]];
            double Overlap = FlowchartBranchRouteOrder.Intersections(Path, OtherPath).Overlap;
            if (Overlap > 0.001)
            {
                Shared++;
            }

            Length += Overlap;
            if ((Route.SourceId != Others[Index].SourceId || Route.TargetId != Others[Index].TargetId)
                && FlowchartRouteRuleChecker.HasProperCrossing(Route, Others[Index]))
            {
                Crossings++;
            }
        }

        return (Shared, Crossings, Length);
    }

    private static void Restore(FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points)
    {
        Route.Points.Clear();
        foreach ((double X, double Y) Point in Points)
        {
            Route.Points.Add(new FlowchartLayoutPoint { X = Point.X, Y = Point.Y });
        }
    }
}
