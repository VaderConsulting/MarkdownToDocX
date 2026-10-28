using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    private sealed record RetainedInteraction(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second, string Rule);

    private sealed partial class RetainedRouteSnapshot
    {
        // Scope has already expanded to both partners. Offer only internal-track
        // translations, preserving allocated terminals and every unrelated path.
        // Accept a strict subset of the demonstrated faults, so trials cannot cycle.
        internal void RepairInteractions(GridLayoutState State, IReadOnlySet<FlowchartLayoutEdgeRoute> Affected)
        {
            var Layout = State.Copy;
            var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            var Prepared = FlowchartAllocatedPortFallback.Prepare(State.Contract, Bounds);
            var Before = AddedInteractions(State, Affected, FrozenOnly: false);
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout).Where(Item => Item.Rule is not ("5.1" or "5.9")).ToHashSet();
            int Budget = FlowchartLayoutRules.RepairCandidateMoves;
            foreach (var Pair in Before.Where(Pair => Affected.Contains(Pair.First) && Affected.Contains(Pair.Second)))
            foreach (var Route in new[] { Pair.First, Pair.Second }.OrderBy(Route => Route.EdgeId, StringComparer.Ordinal))
            {
                if (FindContractEdge(State.Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null }) continue;
                var Other = Route == Pair.First ? Pair.Second : Pair.First;
                var Saved = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                for (int Run = 1; Run < Saved.Length - 2 && Budget > 0; Run++)
                {
                    bool Vertical = Math.Abs(Saved[Run].X - Saved[Run + 1].X) < 0.001;
                    double Current = Vertical ? Saved[Run].X : Saved[Run].Y;
                    var Boxes = new[] { Other.SourceId, Other.TargetId }.Where(Bounds.ContainsKey).Select(Id => Bounds[Id]);
                    double Low = Other.Points.Select(Point => Vertical ? Point.X : Point.Y)
                        .Concat(Boxes.Select(Box => Vertical ? Box.CenterX - Box.Width / 2 : Box.CenterY - Box.Height / 2)).Min();
                    double High = Other.Points.Select(Point => Vertical ? Point.X : Point.Y)
                        .Concat(Boxes.Select(Box => Vertical ? Box.CenterX + Box.Width / 2 : Box.CenterY + Box.Height / 2)).Max();
                    double Step = FlowchartLayoutRules.LineSpacing;
                    foreach (double Offset in new[] { -Step, Step, Math.Floor(Low / Step) * Step - Step - Current,
                        Math.Ceiling(High / Step) * Step + Step - Current }.Distinct().OrderBy(Math.Abs))
                    {
                        if (Budget-- <= 0) return;
                        if (!TryTranslateInternalRun(Route, Run, Vertical, Offset, State.Placed, Layout)) continue;
                        var After = AddedInteractions(State, Affected, FrozenOnly: false);
                        bool Subset = After.All(Item => Before.Any(Old => SamePair(Old, Item)));
                        bool Legal = After.Length < Before.Length && Subset
                            && FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route,
                                Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Prepared)
                            && !FlowchartRouteRuleChecker.Check(State.Contract, Layout).Any(Item => Item.Rule is not ("5.1" or "5.9") && !Findings.Contains(Item));
                        if (Legal) return;
                        Route.Points.Clear();
                        Route.Points.AddRange(Saved.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
                        Layout.BumpGeometryVersion();
                    }
                }
            }
            static bool SamePair(RetainedInteraction A, RetainedInteraction B) => A.Rule == B.Rule
                && ((A.First == B.First && A.Second == B.Second) || (A.First == B.Second && A.Second == B.First));
        }

        // The index is conservative; the checker's exact pair predicates decide
        // whether a candidate introduces a real dependency over the accepted scene.
        internal RetainedInteraction[] AddedInteractions(GridLayoutState State, IReadOnlySet<FlowchartLayoutEdgeRoute> Affected, bool FrozenOnly)
        {
            var Current = State.Copy.EdgeRoutes.ToHashSet();
            var Index = FrozenOnly ? Dependencies : new RetainedRouteDependencyIndex(State.Copy);
            HashSet<(string, string, string)> Seen = [];
            List<RetainedInteraction> Result = [];
            Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> Paths = [];
            (double X, double Y)[] Points(FlowchartLayoutEdgeRoute Route)
            {
                if (!Paths.TryGetValue(Route, out var Value)) Paths[Route] = Value = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                return Value;
            }
            bool Visible(FlowchartLayoutEdgeRoute Route) => FindContractEdge(State.Contract, Route) is { } Edge
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);
            foreach (var Route in Affected.Where(Route => Current.Contains(Route) && Route.Points.Count >= 2 && Visible(Route)))
            {
                var Path = Points(Route);
                double Left = Path.Min(Point => Point.X), Right = Path.Max(Point => Point.X);
                double Bottom = Path.Min(Point => Point.Y), Top = Path.Max(Point => Point.Y);
                var Envelope = new FlowchartLayoutNodeBounds { NodeId = Route.EdgeId, CenterX = (Left + Right) / 2,
                    CenterY = (Bottom + Top) / 2, Width = Right - Left, Height = Top - Bottom };
                foreach (var Other in Index.Near(Envelope, 0))
                {
                    if (Other == Route || !Current.Contains(Other) || Other.Points.Count < 2 || !Visible(Other)
                        || (FrozenOnly && Affected.Contains(Other))) continue;
                    bool Before = ByRoute.TryGetValue(Route, out var Old) && ByRoute.TryGetValue(Other, out _)
                        && VisibleRoutes.Contains(Route) && VisibleRoutes.Contains(Other);
                    ByRoute.TryGetValue(Other, out var OldOther);
                    bool Shared = FlowchartBranchRouteOrder.Intersections(Path, Points(Other)).Overlap > 0.001;
                    bool Crossed = !(Route.SourceId == Other.SourceId && Route.TargetId == Other.TargetId)
                        && FlowchartRouteRuleChecker.HasProperCrossing(Route, Other);
                    if (Shared && (!Before || FlowchartBranchRouteOrder.Intersections(Old!.Points, OldOther!.Points).Overlap <= 0.001)) Add("5.1");
                    if (Crossed && (!Before || (Old!.Source == OldOther!.Source && Old.Target == OldOther.Target)
                        || !FlowchartRouteRuleChecker.HasProperCrossing(Old.CheckRoute(), OldOther!.CheckRoute()))) Add("5.9");
                    void Add(string Rule)
                    {
                        string A = Route.EdgeId, B = Other.EdgeId;
                        if (string.CompareOrdinal(A, B) > 0) (A, B) = (B, A);
                        if (Seen.Add((A, B, Rule))) Result.Add(new(Route, Other, Rule));
                    }
                }
            }
            return Result.OrderBy(Item => Item.First.EdgeId, StringComparer.Ordinal).ThenBy(Item => Item.Second.EdgeId, StringComparer.Ordinal)
                .ThenBy(Item => Item.Rule, StringComparer.Ordinal).ToArray();
        }
    }
}
