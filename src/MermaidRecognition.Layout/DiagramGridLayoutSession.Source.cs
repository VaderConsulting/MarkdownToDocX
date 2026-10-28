using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        // Classification has already excluded changes to measured sizes, groups and removal.
        // Replace all supported inputs before routing so a mixed source edit has one closure.
        internal string? TrySourceUpdate(DiagramContract Model, IReadOnlyCollection<string> ChangedHints,
            IReadOnlyCollection<string> ChangedEdges, int RouteLimit, out string[] AffectedIds, out string[] RebuiltIds,
            bool CapturePlacementOrigins = false)
        {
            AffectedIds = []; RebuiltIds = [];
            using IDisposable Scope = FlowchartRenderContext.Activate(Layout.RenderContext);
            RetainedRouteSnapshot Original = new(State);
            var Findings = ExistingFindings();
            var Before = Grid.Capture();
            var Placed = State.Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            var Restored = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
            foreach (string Id in ChangedHints)
            {
                var Hint = Model.Nodes.Single(Node => Node.Id == Id).PlacementHint;
                if (Hint?.IsActive == true && ((CapturePlacementOrigins && State.Nodes[Id].PlacementHint?.IsActive != true)
                    || (Hint.Kind == PlacementHintKind.At && State.Nodes[Id].PlacementHint is not { IsActive: true, Kind: PlacementHintKind.At })))
                    UnpinnedOrigins.TryAdd(Id, Before.Regions[Id]);
                else if (Hint?.IsActive != true && UnpinnedOrigins.Remove(Id, out var Origin))
                    Restored.Add(Id, Origin);
            }
            State.Contract = CopyRoutingInputs(Model);
            State.Nodes = State.Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            string? Failure = DiagramSourcePlacementHints.Plan(State.Contract, Before.Regions, ChangedHints, out var Targets, Restored);
            if (Failure != null) { return Failure; }
            foreach (var Pair in Targets)
            {
                GridNode Node = Placed[Pair.Key];
                if (!string.IsNullOrEmpty(Node.GroupId) && Before.Regions[Pair.Key] != Pair.Value)
                {
                    return "Moving a grouped node requires a group containment operation.";
                }
            }
            if (!Grid.TryPrepare(Targets, [], out var After)
                || !FlowchartPlacementHints.CellHintsMatch(State.Contract, State.Placed, After!.Regions))
            {
                return "The source placement hints violate cell ownership or mandatory constraints.";
            }
            if (!Grid.TryCommit(Before, After!)) { return "The candidate cell state was superseded."; }
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
            HashSet<FlowchartLayoutEdgeRoute> Changed = [];
            foreach (string Id in ChangedEdges)
            {
                EdgeContract Edge = State.Contract.Edges.Single(Edge => Edge.Id == Id);
                if (Edge.SourceId == Edge.TargetId) { return "Incremental self-loop construction is not supported yet."; }
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
            return Complete(Original, Findings, Changed, RouteLimit, out AffectedIds, out RebuiltIds);
        }

        // Metadata/paint-only updates do not enter route completion or initial placement.
        internal void ReplaceSourceMetadata(DiagramContract Model)
        {
            State.Contract = CopyRoutingInputs(Model);
            State.Nodes = State.Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            PublishGuides(State, Layout.ResolvedEdgeGuides);
        }
    }
}
