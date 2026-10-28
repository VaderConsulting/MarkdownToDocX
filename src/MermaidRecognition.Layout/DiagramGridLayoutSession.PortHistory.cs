using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        // Candidate forks share immutable checkpoints. A failed candidate cannot change
        // accepted undo state. Only terminals on the edited endpoint owners are stored.
        private ImmutableDictionary<string, ImmutableDictionary<(string Edge, bool Source), RetainedPortPreference>>
            PortAllocationHistory = ImmutableDictionary<string, ImmutableDictionary<(string Edge, bool Source), RetainedPortPreference>>.Empty;

        private static string RoutingRequest(EdgeContract Edge) =>
            $"{Edge.Id}:{Edge.SourceId}>{Edge.TargetId}:{Edge.Stroke}:{Edge.MinLength}:" +
            (Edge.RoutingHint is { IsActive: true } Hint
                ? $"{Hint.Leave},{Hint.Enter},{Hint.Travel},{Hint.LeavePoint},{Hint.EnterPoint}" : ",,,,");

        private static string AllocationKey(DiagramContract Contract,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, HashSet<string> Owners) =>
            string.Join(";", Owners.OrderBy(Id => Id, StringComparer.Ordinal).Select(Id => Bounds.TryGetValue(Id, out var Box)
                ? FormattableString.Invariant($"{Id}:{Box.CenterX:R},{Box.CenterY:R},{Box.Width:R},{Box.Height:R}") : Id))
            + "|" + string.Join(";", Contract.Edges.Where(Edge => Owners.Contains(Edge.SourceId) || Owners.Contains(Edge.TargetId))
                .OrderBy(Edge => Edge.Id, StringComparer.Ordinal).Select(RoutingRequest));

        private void RestorePortAllocations(RetainedRouteSnapshot Original, HashSet<FlowchartLayoutEdgeRoute> Affected,
            Dictionary<(string Edge, bool Source), RetainedPortPreference> Preferences)
        {
            var Before = Original.Contract.Edges.ToDictionary(Edge => Edge.Id!, StringComparer.Ordinal);
            var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            var OldPorts = Original.PreferredPorts(new HashSet<FlowchartLayoutEdgeRoute>());
            foreach (var Edge in State.Contract.Edges.OrderBy(Edge => Edge.Id, StringComparer.Ordinal))
            {
                if (!Before.TryGetValue(Edge.Id!, out var Old) || RoutingRequest(Old) == RoutingRequest(Edge)) continue;
                HashSet<string> Owners = [Old.SourceId, Old.TargetId, Edge.SourceId, Edge.TargetId];
                string OldKey = AllocationKey(Original.Contract, Original.Bounds, Owners);
                string NewKey = AllocationKey(State.Contract, Bounds, Owners);
                PortAllocationHistory = PortAllocationHistory.SetItem(OldKey,
                    OldPorts.Where(Pair => Owners.Contains(Pair.Value.Node)).ToImmutableDictionary());
                if (!PortAllocationHistory.TryGetValue(NewKey, out var Restored)) continue;
                foreach (var Pair in Restored)
                {
                    var Route = Layout.EdgeRoutes.FirstOrDefault(Route => Route.EdgeId == Pair.Key.Edge);
                    if (Route == null || (Pair.Key.Source ? Route.SourceId : Route.TargetId) != Pair.Value.Node) continue;
                    Preferences[Pair.Key] = Pair.Value;
                    var Point = Route.Points.Count == 0 ? null : Pair.Key.Source ? Route.Points[0] : Route.Points[^1];
                    if (Point == null || Point.X != Pair.Value.X || Point.Y != Pair.Value.Y
                        || (Pair.Key.Source ? Route.ExitSide : Route.EntrySide) != Pair.Value.Side) Affected.Add(Route);
                }
            }
        }
    }
}
