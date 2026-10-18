using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    private static partial class RouteRepairStage
    {
        /// <summary>Completes only routes affected by changed endpoint geometry, obstacles or redistributed peer slots.</summary>
        /// <param name="State">Applied complete cell candidate.</param>
        /// <param name="Original">Exact restored search base.</param>
        /// <returns>The complete affected route set, or null when it exceeds the existing line limit or cannot be routed legally.</returns>
        private static HashSet<FlowchartLayoutEdgeRoute>? CompleteAffectedCellRoutes(GridLayoutState State, CellGeometrySnapshot Original)
        {
            using var CostTiming = FlowchartCellCostTiming.Begin("routing");
            var Bounds = State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            var ChangedBoxes = Original.ChangedBoxes(State);
            var Affected = Original.RoutesAffectedBy(State, ChangedBoxes);
            AssignPortSidesScoped(State.Contract, State.Copy, State.Placed, State.Frames, State.Nodes,
                State.Direction, State.SharedFanSources, Affected);
            var PortBoxes = Affected.SelectMany(Route => new[] { Route.SourceId, Route.TargetId }).ToHashSet(StringComparer.Ordinal);
            ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, State.Nodes, PortBoxes);
            Affected.UnionWith(Original.ChangedTerminalRoutes());
            if (Affected.Count > FlowchartLayoutRules.RepairChainLines) return null;
            DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction, Affected);
            FlowchartRoutingHints.Apply(State.Copy, State.Contract, Bounds, State.Nodes, State.Direction,
                RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true, ClearUnroutedRoutes: false, OnlyRoutes: Affected);
            ModelRouteCompletionStage.RefineCellRouteCandidate(State, Bounds, Affected);
            ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, State.Nodes, PortBoxes);
            Affected.UnionWith(Original.ChangedTerminalRoutes());
            if (Affected.Count > FlowchartLayoutRules.RepairChainLines) return null;
            foreach (var Route in Affected)
            {
                if (FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route, Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())) continue;
                if (FindContractEdge(State.Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null }) return null;
                var Path = FlowchartAllocatedPortFallback.Build(State.Contract, Bounds, State.Nodes, Route, true);
                Route.Points.Clear(); foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                if (!FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route, Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())) return null;
            }
            if (!Original.UnchangedOutside(Affected)) return null;
            // Publish a candidate guide view, preserving every untouched path. Full
            // completion of the winner remains the final acceptance authority.
            State.Copy.ResolvedEdgeGuides = State.Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).Select(Edge =>
            {
                if (!State.Copy.TryCopyDrawnRoutePoints(Edge, out var Points)) return null;
                return new FlowchartResolvedGuide(Edge, Points, Points.Count == 2 ? FlowchartEdgeRouteKind.Straight : FlowchartEdgeRouteKind.Grid);
            }).OfType<FlowchartResolvedGuide>().ToList();
            State.Copy.RenderContext.CachedRouteContract = State.Contract;
            State.Copy.RenderContext.CachedRouteGeometry = string.Join(";", Bounds.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
                .Select(Pair => FormattableString.Invariant($"{Pair.Key}:{Pair.Value.CenterX:R},{Pair.Value.CenterY:R},{Pair.Value.Width:R},{Pair.Value.Height:R}")));
            State.Copy.RenderContext.CachedRouteGuides = State.Copy.ResolvedEdgeGuides;
            return Affected;

        }
    }
}
