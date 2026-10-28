using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>An ownership boundary around routing inputs, cells, ports, guides and per-candidate caches.</summary>
    internal sealed partial class RetainedRoutingState
    {
        // A newly introduced absolute pin must undo to its pre-pin reservation,
        // even when the original source had no placement hint to reapply.
        private readonly Dictionary<string, FlowchartCellRegion> UnpinnedOrigins = new(StringComparer.Ordinal);
        private readonly GridLayoutState State;
        private readonly FlowchartLogicalGrid Grid;
        private double RoutingOrigin;
        private RetainedRoutingState(GridLayoutState State, FlowchartLogicalGrid Grid) { this.State = State; this.Grid = Grid; }
        internal FlowchartLayoutResult Layout => State.Copy;
        internal DiagramContract Contract => State.Contract;
        internal DiagramLayoutOptions Options => Layout.RenderContext.LayoutOptions;

        internal static RetainedRoutingState Capture(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLogicalGrid? EmptyGrid = null)
        {
            ArgumentNullException.ThrowIfNull(Contract);
            ArgumentNullException.ThrowIfNull(Layout);
            if (!Layout.UsesGridRoutes || Layout.CellNodes == null
                || Layout.CellNodes.Any(Node => Node.CellNode == null))
            {
                throw new ArgumentException("A retained session requires completed, authoritative cell geometry.", nameof(Layout));
            }
            if (Layout.EdgeRoutes.Any(Route => string.IsNullOrEmpty(Route.EdgeId))
                || Layout.EdgeRoutes.Select(Route => Route.EdgeId).Distinct(StringComparer.Ordinal).Count() != Layout.EdgeRoutes.Count)
            {
                throw new ArgumentException("Retained routes require unique nonempty IDs within the accepted model.", nameof(Layout));
            }

            DiagramContract Inputs = CopyRoutingInputs(Contract);
            FlowchartRenderContext Context = new()
            {
                LayoutOptions = Layout.RenderContext.LayoutOptions, UseCellRegions = true,
                HasInitialCorridorCapacity = Layout.RenderContext.HasInitialCorridorCapacity,
                SuppressReservedCorridorAssignment = Layout.RenderContext.SuppressReservedCorridorAssignment,
                FinishedRoutePassApplied = true
            };
            GridLayoutState Copy = new(Inputs, Layout, Context)
            {
                Direction = FlowchartDirectionNormalizer.Normalize(Inputs.Direction),
                Nodes = Inputs.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal),
                CellFinalRoutingReady = true
            };
            Copy.Horizontal = Copy.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
            Copy.Copy.UsesGridRoutes = true;
            // Bounds and renderer geometry are immutable values; retain the exact group
            // geometry, including any renderer-owned decorations omitted by legacy copies.
            Copy.Copy.ClusterBoundsMutable.Clear();
            Copy.Copy.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
            Dictionary<FlowchartLogicalGrid, FlowchartLogicalGrid> Grids = [];
            foreach (GridNode Node in Layout.CellNodes)
            {
                FlowchartLogicalGrid Original = Node.CellNode!.Grid;
                if (!Grids.TryGetValue(Original, out FlowchartLogicalGrid? Grid))
                {
                    var Convention = Original.RegionConventionSnapshot?.Invoke() ?? Original.RegionConvention;
                    Grid = new(Original.Configuration) { RegionConvention = Convention, RegionConventionSnapshot = () => Convention };
                    FlowchartLogicalGrid.Snapshot Cells = Original.Capture();
                    Grid.Restore(new(Grid, Cells.Regions, Cells.ColumnGaps, Cells.RowGaps));
                    Grids.Add(Original, Grid);
                }
                GridNode Owner = new()
                {
                    Id = Node.Id, Shape = Node.Shape, GroupId = Node.GroupId, IsDiamond = Node.IsDiamond,
                    GeometryProvider = Node.GeometryProvider, Rank = Node.Rank, Column = Node.Column,
                    OldX = Node.OldX, OldY = Node.OldY, PlacementHintLocked = Node.PlacementHintLocked
                };
                Owner.BindCells(Grid, Context);
                if (Node.CellNode.RoutingOrigin is { } Origin) { Owner.CellNode!.UseRoutingCoordinates(Origin); }
                Copy.Placed.Add(Owner);
            }
            if (Grids.Count > 1) { throw new ArgumentException("A retained routing session requires one shared cell grid.", nameof(Layout)); }
            Copy.Copy.CellNodes = Copy.Placed;
            foreach (FlowchartLayoutNodeBounds Box in Layout.ClusterBounds)
            {
                Copy.Frames.Add(new()
                {
                    Id = Box.NodeId, Left = checked((int)(Box.CenterX - Box.Width / 2)),
                    Right = checked((int)(Box.CenterX + Box.Width / 2)),
                    Bottom = checked((int)(Box.CenterY - Box.Height / 2)), Top = checked((int)(Box.CenterY + Box.Height / 2))
                });
            }
            // Read paths directly from the accepted model. Guide resolution can invoke routing.
            PublishGuides(Copy, Layout.ResolvedEdgeGuides);
            var CapturedGrid = Grids.Values.SingleOrDefault() ?? new(EmptyGrid?.Configuration ?? FlowchartLogicalGridConfiguration.FromRules());
            if (Grids.Count == 0 && EmptyGrid != null)
            {
                var Cells = EmptyGrid.Capture();
                CapturedGrid.Restore(new(CapturedGrid, Cells.Regions, Cells.ColumnGaps, Cells.RowGaps));
                CapturedGrid.RegionConvention = EmptyGrid.RegionConventionSnapshot?.Invoke() ?? EmptyGrid.RegionConvention;
                var Convention = CapturedGrid.RegionConvention;
                CapturedGrid.RegionConventionSnapshot = () => Convention;
            }
            return new(Copy, CapturedGrid) { RoutingOrigin = Copy.Placed.FirstOrDefault()?.CellNode?.RoutingOrigin ?? 0 };
        }

        internal RetainedRoutingState Fork()
        {
            var Copy = Capture(State.Contract, Layout, Grid);
            foreach (var Pair in UnpinnedOrigins) Copy.UnpinnedOrigins.Add(Pair.Key, Pair.Value);
            Copy.PortAllocationHistory = PortAllocationHistory;
            Copy.RemovedNodeRegions = RemovedNodeRegions;
            Copy.RoutingOrigin = RoutingOrigin;
            return Copy;
        }

        internal string? TryMove(IReadOnlyDictionary<string, FlowchartCellRegion> Targets, int RouteLimit,
            out string[] AffectedIds, out string[] RebuiltIds)
        {
            AffectedIds = []; RebuiltIds = [];
            using IDisposable Scope = FlowchartRenderContext.Activate(Layout.RenderContext);
            FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
            foreach (var Target in Targets)
            {
                if (!Before.Regions.TryGetValue(Target.Key, out FlowchartCellRegion? Region)) { return "The move names an unknown node."; }
                if (Region.RowSpan != Target.Value.RowSpan || Region.ColumnSpan != Target.Value.ColumnSpan) { return "Cell resizing requires a separate operation."; }
                GridNode Node = State.Placed.Single(Node => Node.Id == Target.Key);
                if (Node.PlacementHintLocked && Region != Target.Value) { return "The move would change a locked placement hint."; }
                if (!string.IsNullOrEmpty(Node.GroupId) && Region != Target.Value) { return "Moving a grouped node requires a group containment operation."; }
            }
            if (!Grid.TryPrepare(Targets, [], out FlowchartLogicalGrid.Snapshot? After)
                || !FlowchartPlacementHints.CellHintsMatch(State.Contract, State.Placed, After!.Regions))
            {
                return "The move violates cell ownership, placement hints or semantic constraints.";
            }

            RetainedRouteSnapshot Original = new(State);
            HashSet<FlowchartRouteRuleChecker.Finding> ExistingFindings = [.. FlowchartRouteRuleChecker.Check(State.Contract, Layout)];
            if (!Grid.TryCommit(Before, After!)) { return "The candidate cell state was superseded."; }
            // Project only node geometry. Frozen group boxes and the model coordinate frame stay exact.
            for (int Index = 0; Index < Layout.NodeBounds.Count; Index++)
            {
                var Box = Layout.NodeBounds[Index];
                if (Targets.TryGetValue(Box.NodeId, out var Target) && Before.Regions[Box.NodeId] != Target)
                {
                    GridNode Node = State.Placed.Single(Node => Node.Id == Box.NodeId);
                    Layout.NodeBoundsMutable[Index] = Node.CellNode!.Bounds(Node.GeometryProvider);
                }
            }
            Layout.BumpGeometryVersion();
            HashSet<FlowchartLayoutEdgeRoute> Affected = Original.RoutesAffectedBy(State);
            return Complete(Original, ExistingFindings, Affected, RouteLimit, out AffectedIds, out RebuiltIds);
        }

        private string? Complete(RetainedRouteSnapshot Original, HashSet<FlowchartRouteRuleChecker.Finding> ExistingFindings,
            HashSet<FlowchartLayoutEdgeRoute> Affected, int RouteLimit, out string[] AffectedIds, out string[] RebuiltIds)
        {
            AffectedIds = []; RebuiltIds = [];
            using IDisposable Scope = FlowchartRenderContext.Activate(Layout.RenderContext);
            HashSet<string> Rebuilt = new(StringComparer.Ordinal);
            var PreviousPreferences = Layout.RenderContext.PreferredPorts;
            var Preferences = Original.PreferredPorts(Affected);
            RestorePortAllocations(Original, Affected, Preferences);
            Layout.RenderContext.PreferredPorts = Preferences;
            try
            {
                string? PointFailure = FlowchartConnectionPoints.Validate(State.Contract, Layout);
                if (PointFailure != null) return PointFailure;
                // A growing, finite set reaches a fixed point or the explicit route budget.
                // A peer whose terminal changed on the final refinement pass must itself be rebuilt.
                for (int Pass = 0; Pass <= Layout.EdgeRoutes.Count; Pass++)
                {
                    AffectedIds = Ids(Affected);
                    if (Affected.Count > RouteLimit) { return "The routing dependency set exceeds the local route limit."; }
                    if (Affected.Count == 0) { break; }
                    AssignPortSidesScoped(State.Contract, Layout, State.Placed, State.Frames, State.Nodes,
                        State.Direction, State.SharedFanSources, Affected);
                    Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds)
                        .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
                    HashSet<string> PortBoxes = Affected.SelectMany(Route => new[] { Route.SourceId, Route.TargetId })
                        .Concat(Original.OldEndpoints(Affected)).ToHashSet(StringComparer.Ordinal);
                    ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes, PortBoxes);
                    Affected.UnionWith(Original.ChangedTerminalRoutes());
                    AffectedIds = Ids(Affected);
                    if (Affected.Count > RouteLimit) { return "Changed terminals exceed the local route limit."; }
                    Rebuilt.UnionWith(Affected.Select(Route => Route.EdgeId));
                    DiagramGridRouteBuilder.RebuildAllRoutes(Layout, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction, Affected);
                    FlowchartRoutingHints.Apply(Layout, State.Contract, Bounds, State.Nodes, State.Direction,
                        RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true, ClearUnroutedRoutes: false, OnlyRoutes: Affected);
                    ModelRouteCompletionStage.RefineCellRouteCandidate(State, Bounds, Affected);
                    ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes, PortBoxes);
                    HashSet<FlowchartLayoutEdgeRoute> Expanded = [.. Original.ChangedTerminalRoutes()];
                    Expanded.ExceptWith(Affected);
                    if (Expanded.Count != 0) { Affected.UnionWith(Expanded); continue; }

                    FlowchartAllocatedPortFallback.PreparedGeometry Prepared = FlowchartAllocatedPortFallback.Prepare(State.Contract, Bounds);
                    foreach (FlowchartLayoutEdgeRoute Route in Affected)
                    {
                        if (Legal(Route)) { continue; }
                        if (FindContractEdge(State.Contract, Route)?.RoutingHint is { IsActive: true, Travel: not null })
                        {
                            return "A mandatory routing hint cannot be satisfied locally.";
                        }
                        var Path = FlowchartAllocatedPortFallback.Build(State.Contract, Bounds, State.Nodes, Route, true, Prepared, Layout.EdgeRoutes);
                        Route.Points.Clear();
                        Route.Points.AddRange(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
                        if (!Legal(Route)) { return "The affected relationship cannot be routed legally with fixed geometry."; }
                    }
                    break;
                    bool Legal(FlowchartLayoutEdgeRoute Route) => FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route,
                        Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Prepared);
                }
                if (!Original.UnchangedOutside(Affected)) { return "Routing changed geometry outside its dependency set."; }
                PointFailure = FlowchartConnectionPoints.Validate(State.Contract, Layout, CheckRoutes: true);
                if (PointFailure != null) return PointFailure;
                foreach (FlowchartLayoutEdgeRoute Route in Affected)
                {
                    if (FindContractEdge(State.Contract, Route)?.RoutingHint is not { IsActive: true } Hint) { continue; }
                    if (Hint.UnhonouredReason != null
                        || (Hint.Leave.HasValue && Route.ExitSide != HintSide(Hint.Leave.Value))
                        || (Hint.Enter.HasValue && Route.EntrySide != HintSide(Hint.Enter.Value))
                        || (Hint.Travel.HasValue && !DiagramGridRouteBuilder.HasTravelDirection(
                            Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Hint.Travel.Value)))
                    {
                        return "A mandatory routing hint cannot be satisfied locally.";
                    }
                }
                Layout.BumpGeometryVersion();
                var NewFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout).Where(Finding => !ExistingFindings.Contains(Finding)).ToArray();
                if (NewFindings.Length > 0) { return "Local validation rejected the move: " + NewFindings[0].Description; }
                var Boxes = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToArray();
                var Points = Layout.EdgeRoutes.SelectMany(Route => Route.Points).ToArray();
                double[] Xs = Boxes.SelectMany(Box => new[] { Box.CenterX - Box.Width / 2, Box.CenterX + Box.Width / 2 })
                    .Concat(Points.Select(Point => Point.X)).ToArray();
                double[] Ys = Boxes.SelectMany(Box => new[] { Box.CenterY - Box.Height / 2, Box.CenterY + Box.Height / 2 })
                    .Concat(Points.Select(Point => Point.Y)).ToArray();
                Layout.SetGraphSize(Xs.Length == 0 ? 0 : Xs.Max() - Xs.Min(), Ys.Length == 0 ? 0 : Ys.Max() - Ys.Min());
                PublishGuides(State, Layout.ResolvedEdgeGuides);
                return null;
            }
            finally
            {
                Layout.RenderContext.PreferredPorts = PreviousPreferences;
                AffectedIds = Ids(Affected); RebuiltIds = Rebuilt.OrderBy(Id => Id, StringComparer.Ordinal).ToArray();
            }
        }

        private static string[] Ids(IEnumerable<FlowchartLayoutEdgeRoute> Routes) => Routes.Select(Route => Route.EdgeId).OrderBy(Id => Id, StringComparer.Ordinal).ToArray();

        private static void PublishGuides(GridLayoutState State, IReadOnlyList<FlowchartResolvedGuide>? Previous)
        {
            State.Copy.ResolvedEdgeGuides = [.. State.Contract.Edges
                .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                .Select(Edge =>
                {
                    if (!State.Copy.TryCopyDrawnRoutePoints(Edge, out var Points)) { return null; }
                    var Old = Previous?.FirstOrDefault(Guide => Guide.Edge.Id == Edge.Id && Guide.Points.SequenceEqual(Points));
                    return new FlowchartResolvedGuide(Edge, Points, Old?.RouteKind
                        ?? (Points.Count == 2 ? FlowchartEdgeRouteKind.Straight : FlowchartEdgeRouteKind.Grid));
                })
                .OfType<FlowchartResolvedGuide>()];
            State.Copy.RenderContext.CachedRouteContract = State.Contract;
            State.Copy.RenderContext.CachedRouteGuides = State.Copy.ResolvedEdgeGuides;
            State.Copy.RenderContext.CachedRouteGeometry = string.Join(";", State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds)
                .OrderBy(Box => Box.NodeId, StringComparer.Ordinal)
                .Select(Box => FormattableString.Invariant($"{Box.NodeId}:{Box.CenterX:R},{Box.CenterY:R},{Box.Width:R},{Box.Height:R}")));
        }
    }
}
