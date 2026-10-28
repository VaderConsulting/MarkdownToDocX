using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        internal string? TryAddNode(NodeContract Node, FlowchartCellRegion Region, int RouteLimit,
            out string[] AffectedIds, out string[] RebuiltIds)
        {
            ArgumentNullException.ThrowIfNull(Node); ArgumentNullException.ThrowIfNull(Region);
            AffectedIds = []; RebuiltIds = [];
            if (string.IsNullOrWhiteSpace(Node.Id) || State.Nodes.ContainsKey(Node.Id) || State.Contract.Groups.Any(Group => Group.Id == Node.Id))
            {
                return "The node requires a unique nonempty identity.";
            }
            if (!string.IsNullOrEmpty(Node.ParentGroupId) || State.Contract.Groups.Count != 0)
            {
                return "Adding a grouped node requires a group containment operation.";
            }
            NodeContract Item = CopyRoutingNode(Node);
            bool Diamond = FlowchartDagrePresentation.IsDiamondShape(Item.Shape);
            var Size = Grid.Measure(Region);
            if (!double.IsFinite(Item.LayoutWidth) || !double.IsFinite(Item.LayoutHeight) || Item.LayoutWidth < 0 || Item.LayoutHeight < 0
                || Size.Width > int.MaxValue || Size.Height > int.MaxValue || Size.Width < Item.LayoutWidth || Size.Height < Item.LayoutHeight
                || (Diamond && Size.Height < Grid.Configuration.CellHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR))
            {
                return "The declared cells cannot contain the measured node.";
            }
            RetainedRouteSnapshot Original = new(State);
            var Findings = ExistingFindings();
            if (!Grid.TryPlace(Item.Id, Region)) { return "The node's cells are occupied or violate a placement constraint."; }
            ReplaceInputs();
            GridNode Owner = new() { Id = Item.Id, Shape = Item.Shape, IsDiamond = Diamond, GeometryProvider = Item.GeometryProvider };
            Owner.BindCells(Grid, Layout.RenderContext);
            Owner.CellNode!.UseRoutingCoordinates(State.Placed.FirstOrDefault()?.CellNode?.RoutingOrigin ?? 0);
            State.Placed.Add(Owner); State.Nodes.Add(Item.Id, Item); State.Contract.NodesMutable.Add(Item);
            Layout.NodeBoundsMutable.Add(Owner.CellNode.Bounds(Owner.GeometryProvider));
            Layout.BumpGeometryVersion();
            if (!FlowchartPlacementHints.CellHintsMatch(State.Contract, State.Placed, Grid.Capture().Regions))
            {
                return "The declared cells do not satisfy the node's placement hint.";
            }
            return Complete(Original, Findings, Original.RoutesAffectedBy(State), RouteLimit, out AffectedIds, out RebuiltIds);
        }

        internal string? TryAddRelationship(string Identity, EdgeContract Relationship, int RouteLimit,
            out string[] AffectedIds, out string[] RebuiltIds)
        {
            ArgumentNullException.ThrowIfNull(Relationship);
            AffectedIds = []; RebuiltIds = [];
            if (string.IsNullOrWhiteSpace(Identity) || State.Contract.Edges.Any(Edge => Edge.Id == Identity))
            {
                return "The relationship requires a unique nonempty editor identity.";
            }
            if (!State.Nodes.ContainsKey(Relationship.SourceId) || !State.Nodes.ContainsKey(Relationship.TargetId))
            {
                return "Both relationship endpoints must already be placed.";
            }
            if (Relationship.SourceId == Relationship.TargetId) { return "Incremental self-loop construction is not supported yet."; }
            if (string.Equals(Relationship.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                return "Invisible layout constraints require a separate construction operation.";
            }
            RetainedRouteSnapshot Original = new(State);
            var Findings = ExistingFindings();
            ReplaceInputs();
            EdgeContract Edge = CopyRoutingEdge(Relationship);
            Edge.Id = Identity; Edge.IsUserDefinedId = true;
            State.Contract.EdgesMutable.Add(Edge);
            FlowchartLayoutEdgeRoute Route = new() { EdgeId = Identity, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
            SeedTerminals(Route);
            Layout.EdgeRoutesMutable.Add(Route);
            return Complete(Original, Findings, [Route], RouteLimit, out AffectedIds, out RebuiltIds);
        }

        internal string? TryReconnect(string Identity, string SourceId, string TargetId, int RouteLimit,
            out string[] AffectedIds, out string[] RebuiltIds)
        {
            AffectedIds = []; RebuiltIds = [];
            if (SourceId == TargetId) { return "Incremental self-loop construction is not supported yet."; }
            if (!State.Nodes.ContainsKey(SourceId) || !State.Nodes.ContainsKey(TargetId)) { return "Both reconnect endpoints must already be placed."; }
            var Edge = State.Contract.Edges.SingleOrDefault(Edge => Edge.Id == Identity);
            var Route = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Identity);
            if (Edge == null || Route == null) { return "The relationship identity is unknown."; }
            RetainedRouteSnapshot Original = new(State);
            var Findings = ExistingFindings();
            ReplaceInputs();
            Edge = State.Contract.Edges.Single(Edge => Edge.Id == Identity);
            Edge.SourceId = SourceId; Edge.TargetId = TargetId;
            Route.SourceId = SourceId; Route.TargetId = TargetId;
            SeedTerminals(Route);
            return Complete(Original, Findings, [Route], RouteLimit, out AffectedIds, out RebuiltIds);
        }

        internal string? TryChangePorts(string Identity, RoutingHintContract Ports, int RouteLimit,
            out string[] AffectedIds, out string[] RebuiltIds)
        {
            ArgumentNullException.ThrowIfNull(Ports);
            AffectedIds = []; RebuiltIds = [];
            var Edge = State.Contract.Edges.SingleOrDefault(Edge => Edge.Id == Identity);
            var Route = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Identity);
            if (Edge == null || Route == null) { return "The relationship identity is unknown."; }
            RetainedRouteSnapshot Original = new(State);
            var Findings = ExistingFindings();
            ReplaceInputs();
            Edge = State.Contract.Edges.Single(Edge => Edge.Id == Identity);
            Edge.RoutingHint = CopyRoutingEdge(new() { RoutingHint = Ports }).RoutingHint;
            return Complete(Original, Findings, [Route], RouteLimit, out AffectedIds, out RebuiltIds);
        }

        private HashSet<FlowchartRouteRuleChecker.Finding> ExistingFindings() => [.. FlowchartRouteRuleChecker.Check(State.Contract, Layout)];

        private void ReplaceInputs()
        {
            // Topology helpers cache by contract identity. Never edit a cached contract,
            // even inside a discarded candidate; publish a fresh routing model instead.
            State.Contract = CopyRoutingInputs(State.Contract);
            State.Nodes = State.Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            Layout.BumpGeometryVersion();
        }

        private void SeedTerminals(FlowchartLayoutEdgeRoute Route)
        {
            var Source = Layout.NodeBounds.Single(Box => Box.NodeId == Route.SourceId);
            var Target = Layout.NodeBounds.Single(Box => Box.NodeId == Route.TargetId);
            Route.Points.Clear();
            Route.Points.Add(new() { X = Source.CenterX, Y = Source.CenterY });
            Route.Points.Add(new() { X = Target.CenterX, Y = Target.CenterY });
        }
    }
}
