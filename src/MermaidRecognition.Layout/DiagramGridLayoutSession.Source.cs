using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        // Classification has excluded unsupported shape, style and containment changes.
        // Replace all supported inputs before routing so a mixed source edit has one closure.
        internal string? TrySourceUpdate(DiagramContract Model, IReadOnlyCollection<string> ChangedHints,
            IReadOnlyCollection<string> ChangedEdges, int RouteLimit, out string[] AffectedIds, out string[] RebuiltIds,
            bool CapturePlacementOrigins = false)
        {
            AffectedIds = []; RebuiltIds = [];
            using IDisposable Scope = FlowchartRenderContext.Activate(Layout.RenderContext);
            RetainedRouteSnapshot Original = CaptureRouting();
            var OriginalContract = State.Contract;
            var Findings = ExistingFindings();
            string? RemovalFailure = RemoveSourceNodes(Model);
            if (RemovalFailure != null) return RemovalFailure;
            bool AddedNodes = Model.Nodes.Any(Node => !State.Nodes.ContainsKey(Node.Id));
            string? AdditionFailure = AddSourceNodes(Model);
            if (AdditionFailure != null) return AdditionFailure;
            var Before = Grid.Capture();
            string? SizeFailure = PlanSourceNodeSizes(Model, Before.Regions, out var SizedRegions);
            if (SizeFailure != null) return SizeFailure;
            var Placed = State.Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            var Restored = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
            foreach (string Id in ChangedHints)
            {
                var Hint = Model.Nodes.Single(Node => Node.Id == Id).PlacementHint;
                if (!State.Nodes.ContainsKey(Id))
                {
                    if (Hint?.IsActive != true && UnpinnedOrigins.Remove(Id, out var RemovedOrigin)) Restored.Add(Id, RemovedOrigin);
                    continue;
                }
                if (Hint?.IsActive == true && ((CapturePlacementOrigins && State.Nodes[Id].PlacementHint?.IsActive != true)
                    || (Hint.Kind == PlacementHintKind.At && State.Nodes[Id].PlacementHint is not { IsActive: true, Kind: PlacementHintKind.At })))
                    UnpinnedOrigins.TryAdd(Id, Before.Regions[Id]);
                else if (Hint?.IsActive != true && UnpinnedOrigins.Remove(Id, out var Origin))
                    Restored.Add(Id, Origin);
            }
            State.Contract = CopyRoutingInputs(Model);
            State.Nodes = State.Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            string? Failure = DiagramSourcePlacementHints.Plan(State.Contract, SizedRegions, ChangedHints, out var Targets, Restored);
            if (Failure != null) { return Failure; }
            RelaxConventionForMovedNodes(Targets, Before.Regions);
            if (!Grid.TryPrepare(Targets, [], out var After)
                || !FlowchartPlacementHints.CellHintsMatch(State.Contract, State.Placed, After!.Regions))
            {
                return "The source placement hints violate cell ownership or mandatory constraints.";
            }
            if (!Grid.TryCommit(Before, After!)) { return "The candidate cell state was superseded."; }
            if (!Enterprise.EnterpriseBandPlacement.RetainedAssignmentIsLegal(State.Contract, After!.Regions))
                return "The changed nodes violate the retained semantic layer constraints.";
            if (AddedNodes)
            {
                var PriorConvention = Grid.RegionConvention;
                var FrozenInputs = State.Contract;
                Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> Convention = Regions => PriorConvention?.Invoke(Regions) != false
                    && Enterprise.EnterpriseBandPlacement.RetainedAssignmentIsLegal(FrozenInputs, Regions);
                Grid.RegionConvention = Convention;
                Grid.RegionConventionSnapshot = () => Convention;
            }
            for (int Index = 0; Index < Layout.NodeBounds.Count; Index++)
            {
                var Box = Layout.NodeBounds[Index];
                GridNode Node = Placed[Box.NodeId];
                Node.PlacementHintLocked = State.Nodes[Node.Id].PlacementHint?.IsActive == true;
                if (Before.Regions[Box.NodeId] != After!.Regions[Box.NodeId])
                {
                    Layout.NodeBoundsMutable[Index] = Node.CellNode!.Bounds(Node.GeometryProvider);
                }
            }
            string? Containment = ValidateFrozenContainment(Targets.Where(Target => Before.Regions[Target.Key] != Target.Value).Select(Target => Target.Key));
            if (Containment != null) return Containment;
            HashSet<FlowchartLayoutEdgeRoute> Changed = [];
            foreach (string Id in ChangedEdges)
            {
                EdgeContract? Edge = State.Contract.Edges.SingleOrDefault(Edge => Edge.Id == Id);
                if (Edge == null)
                {
                    var Removed = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Id);
                    if (Removed == null) return "The removed relationship identity is unknown.";
                    Layout.EdgeRoutesMutable.Remove(Removed);
                    continue;
                }
                if (!State.Nodes.ContainsKey(Edge.SourceId) || !State.Nodes.ContainsKey(Edge.TargetId))
                {
                    return "Both relationship endpoints must already be placed nodes.";
                }
                FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Id);
                if (Route == null)
                {
                    Route = new() { EdgeId = Id, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
                    SeedTerminals(Route);
                    Layout.EdgeRoutesMutable.Add(Route);
                }
                else if (Route.SourceId != Edge.SourceId || Route.TargetId != Edge.TargetId)
                {
                    Route.SourceId = Edge.SourceId; Route.TargetId = Edge.TargetId;
                    SeedTerminals(Route);
                }
                Changed.Add(Route);
            }
            Layout.BumpGeometryVersion();
            Changed.UnionWith(Original.RoutesAffectedBy(State));
            Changed.UnionWith(ChangedTopologyRoutes(OriginalContract));
            return Complete(Original, Findings, Changed, RouteLimit, out AffectedIds, out RebuiltIds);
        }

        // Metadata/paint-only updates do not enter route completion or initial placement.
        internal void ReplaceSourceMetadata(DiagramContract Model)
        {
            State.Contract = CopyRoutingInputs(Model);
            State.Nodes = State.Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            PublishGuides(State, Layout.ResolvedEdgeGuides);
        }

        private sealed record UserMoveConvention(Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool>? Inner,
            IReadOnlyDictionary<string, FlowchartCellRegion> Baseline);
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool>, UserMoveConvention> UserMoveConventions = new();

        // The initial layout's convention (fixed band placement, shared level rows, partner alignment) stops repairs from
        // moving nodes. It must not veto an explicit source-hint move, so moved nodes are judged at their baseline
        // position by it and by their actual position against the semantic layer bands. Unmoved nodes stay fully protected.
        private void RelaxConventionForMovedNodes(IReadOnlyDictionary<string, FlowchartCellRegion> Targets,
            IReadOnlyDictionary<string, FlowchartCellRegion> Before)
        {
            Dictionary<string, FlowchartCellRegion> Baseline = Targets.Where(Pair => Before.TryGetValue(Pair.Key, out var Old) && Old != Pair.Value)
                .ToDictionary(Pair => Pair.Key, Pair => Before[Pair.Key], StringComparer.Ordinal);
            if (Baseline.Count == 0) return;
            var Current = Grid.RegionConvention;
            var Inner = Current;
            if (Current != null && UserMoveConventions.TryGetValue(Current, out var Existing))
            {
                Inner = Existing.Inner;
                foreach (var Pair in Existing.Baseline) Baseline[Pair.Key] = Pair.Value;
            }
            var FrozenInputs = State.Contract;
            Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> Composite = Regions =>
            {
                if (!Enterprise.EnterpriseBandPlacement.RetainedAssignmentIsLegal(FrozenInputs, Regions)) return false;
                if (Inner == null) return true;
                var Substituted = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
                foreach (var Pair in Baseline) if (Substituted.ContainsKey(Pair.Key)) Substituted[Pair.Key] = Pair.Value;
                return Inner(Substituted);
            };
            UserMoveConventions.Add(Composite, new(Inner, Baseline));
            Grid.RegionConvention = Composite;
            Grid.RegionConventionSnapshot = () => Composite;
        }
    }
}
