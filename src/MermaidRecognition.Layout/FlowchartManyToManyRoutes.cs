using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Routes dense adjacent layers from distinct bottom ports to distinct top ports.</summary>
internal static class FlowchartManyToManyRoutes
{
    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartDirection Direction = FlowchartDirection.TopToBottom)
    {
        if (Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft)
        {
            ApplyHorizontal(Layout, Contract, Bounds, Direction);
            return;
        }
        bool HasPlacementHints = Contract.Nodes.Any(Node => Node.PlacementHint?.IsActive == true)
            || Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true);
        HashSet<(string SourceId, string TargetId)> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        HashSet<(string SourceId, string TargetId)> CompletePairs = CompleteBlockPairs(Contract);
        var Routes = Layout.EdgeRoutes.Where(Route => Bounds.ContainsKey(Route.SourceId)
            && Bounds.ContainsKey(Route.TargetId)
            && !CompletePairs.Contains((Route.SourceId, Route.TargetId))
            && Math.Abs(Bounds[Route.SourceId].CenterY - Bounds[Route.TargetId].CenterY) > 1
            && (Bounds[Route.SourceId].CenterY > Bounds[Route.TargetId].CenterY + 1
                || HasPlacementHints && Bounds[Route.SourceId].CenterY < Bounds[Route.TargetId].CenterY - 1)
            && !BackEdges.Contains((Route.SourceId, Route.TargetId))
            && ContractEdge(Contract, Route)?.RoutingHint?.IsActive != true).ToList();
        foreach (var Rank in Routes.GroupBy(Route => (
            Source: Math.Round(Bounds[Route.SourceId].CenterY),
            Target: Math.Round(Bounds[Route.TargetId].CenterY))))
        {
            List<FlowchartLayoutEdgeRoute> Edges = Rank.ToList();
            if (Edges.Count < 2) continue;
            bool SourceAboveTarget = Rank.Key.Source > Rank.Key.Target;
            bool AllUnlabelled = Edges.All(Route => ContractEdge(Contract, Route) is not EdgeContract Edge
                || string.IsNullOrWhiteSpace(Edge.Label));
            var SourceOrder = Edges.GroupBy(Route => Route.SourceId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.TargetId].CenterX).ThenBy(Route => Route.TargetId).ToList());
            var TargetOrder = Edges.GroupBy(Route => Route.TargetId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.SourceId].CenterX).ThenBy(Route => Route.SourceId).ToList());
            var Terminals = Edges.ToDictionary(Route => Route, Route =>
            {
                FlowchartLayoutNodeBounds Source = Bounds[Route.SourceId], Target = Bounds[Route.TargetId];
                List<FlowchartLayoutEdgeRoute> From = SourceOrder[Route.SourceId], Into = TargetOrder[Route.TargetId];
                double StartX = Slot(Source, From.IndexOf(Route), From.Count);
                double EndX = Slot(Target, Into.IndexOf(Route), Into.Count);
                return (StartX,
                    StartY: Source.CenterY + (SourceAboveTarget ? -Source.Height / 2.0 : Source.Height / 2.0),
                    EndX,
                    EndY: Target.CenterY + (SourceAboveTarget ? Target.Height / 2.0 : -Target.Height / 2.0));
            });
            double NearSource = SourceAboveTarget
                ? Edges.Min(Route => Terminals[Route].StartY)
                : Edges.Max(Route => Terminals[Route].StartY);
            double NearTarget = SourceAboveTarget
                ? Edges.Max(Route => Terminals[Route].EndY)
                : Edges.Min(Route => Terminals[Route].EndY);
            List<FlowchartLayoutEdgeRoute> Bent = Edges.Where(Route =>
                Math.Abs(Terminals[Route].StartX - Terminals[Route].EndX) > 0.5).ToList();
            if (Bent.Count == 0 || (SourceAboveTarget ? NearSource <= NearTarget : NearSource >= NearTarget)) continue;

            List<FlowchartLayoutEdgeRoute> Order = Bent.OrderBy(Route => Bounds[Route.SourceId].CenterX)
                .ThenBy(Route => Bounds[Route.TargetId].CenterX).ToList();
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> PathsFor(
                IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Tracks = TrackOrder.Select((Route, Index) => (Route,
                    Y: Math.Round(NearSource + (Index + 1) * (NearTarget - NearSource) / (TrackOrder.Count + 1))))
                    .ToDictionary(Item => Item.Route, Item => Item.Y);
                return Edges.ToDictionary(Route => Route, Route =>
                {
                    var T = Terminals[Route];
                    return Math.Abs(T.StartX - T.EndX) <= 0.5
                        ? new List<(double X, double Y)> { (T.StartX, T.StartY), (T.EndX, T.EndY) }
                        : [(T.StartX, T.StartY), (T.StartX, Tracks[Route]),
                            (T.EndX, Tracks[Route]), (T.EndX, T.EndY)];
                });
            }
            int Crossings(IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Paths = PathsFor(TrackOrder);
                int Count = 0;
                for (int A = 0; A < Edges.Count; A++)
                for (int B = A + 1; B < Edges.Count; B++)
                {
                    if (!AllUnlabelled && SharesEndpoint(Edges[A], Edges[B])) continue;
                    var Interaction = FlowchartBranchRouteOrder.Intersections(Paths[Edges[A]], Paths[Edges[B]]);
                    Count += Interaction.Crossings + (Interaction.Overlap > 0.5 ? Edges.Count * Edges.Count : 0);
                }
                return Count;
            }
            int Best = Crossings(Order);
            while (true)
            {
                bool Improved = false;
                for (int First = 0; First < Order.Count && !Improved; First++)
                for (int Second = First + 1; Second < Order.Count; Second++)
                {
                    (Order[First], Order[Second]) = (Order[Second], Order[First]);
                    int Candidate = Crossings(Order);
                    if (Candidate < Best) { Best = Candidate; Improved = true; break; }
                    (Order[First], Order[Second]) = (Order[Second], Order[First]);
                }
                if (!Improved) break;
            }
            // A track can be trapped in a swap-local minimum: moving it past
            // several neighbours may improve the fan although no single swap
            // does. Try every stable insertion and keep only strict decreases.
            while (AllUnlabelled)
            {
                List<FlowchartLayoutEdgeRoute>? BetterOrder = null;
                int Better = Best;
                for (int From = 0; From < Order.Count; From++)
                for (int To = 0; To < Order.Count; To++)
                {
                    if (From == To) continue;
                    List<FlowchartLayoutEdgeRoute> Candidate = Order.ToList();
                    FlowchartLayoutEdgeRoute Moving = Candidate[From];
                    Candidate.RemoveAt(From);
                    Candidate.Insert(To, Moving);
                    int CandidateCrossings = Crossings(Candidate);
                    if (CandidateCrossings < Better)
                    {
                        Better = CandidateCrossings;
                        BetterOrder = Candidate;
                    }
                }
                if (BetterOrder == null) break;
                Order = BetterOrder;
                Best = Better;
            }
            var Paths = PathsFor(Order);
            if (Paths.Any(Item => HitsNode(Item.Value, Item.Key, Bounds))) continue;
            foreach (FlowchartLayoutEdgeRoute Route in Edges)
            {
                Route.Points.Clear();
                foreach ((double X, double Y) Point in Paths[Route])
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                Route.ExitSide = SourceAboveTarget ? "bottom" : "top";
                Route.EntrySide = SourceAboveTarget ? "top" : "bottom";
            }
        }
    }

    private static void ApplyHorizontal(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartDirection Direction)
    {
        HashSet<(string SourceId, string TargetId)> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        HashSet<(string SourceId, string TargetId)> CompletePairs = CompleteBlockPairs(Contract);
        List<FlowchartLayoutEdgeRoute> Routes = Layout.EdgeRoutes.Where(Route =>
            Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && CompletePairs.Contains((Route.SourceId, Route.TargetId))
            && Math.Abs(Bounds[Route.SourceId].CenterX - Bounds[Route.TargetId].CenterX) > 1
            && (Direction == FlowchartDirection.LeftToRight
                ? Bounds[Route.SourceId].CenterX < Bounds[Route.TargetId].CenterX
                : Bounds[Route.SourceId].CenterX > Bounds[Route.TargetId].CenterX)
            && !BackEdges.Contains((Route.SourceId, Route.TargetId))
            && ContractEdge(Contract, Route)?.RoutingHint?.IsActive != true).ToList();
        foreach (var Rank in Routes.GroupBy(Route => (Source: Math.Round(Bounds[Route.SourceId].CenterX),
                     Target: Math.Round(Bounds[Route.TargetId].CenterX)))
                     .Where(Group => Group.Count() > 1))
        {
            List<FlowchartLayoutEdgeRoute> Edges = Rank.ToList();
            bool IncreasingX = Direction == FlowchartDirection.LeftToRight;
            var SourceOrder = Edges.GroupBy(Route => Route.SourceId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.TargetId].CenterY).ThenBy(Route => Route.TargetId).ToList());
            var TargetOrder = Edges.GroupBy(Route => Route.TargetId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.SourceId].CenterY).ThenBy(Route => Route.SourceId).ToList());
            var Terminals = Edges.ToDictionary(Route => Route, Route =>
            {
                FlowchartLayoutNodeBounds Source = Bounds[Route.SourceId], Target = Bounds[Route.TargetId];
                List<FlowchartLayoutEdgeRoute> From = SourceOrder[Route.SourceId], Into = TargetOrder[Route.TargetId];
                double StartY = SlotCross(Source, From.IndexOf(Route), From.Count);
                double EndY = SlotCross(Target, Into.IndexOf(Route), Into.Count);
                return (StartX: Source.CenterX + (IncreasingX ? Source.Width / 2 : -Source.Width / 2), StartY,
                    EndX: Target.CenterX + (IncreasingX ? -Target.Width / 2 : Target.Width / 2), EndY);
            });
            double NearSource = IncreasingX ? Edges.Max(Route => Terminals[Route].StartX)
                : Edges.Min(Route => Terminals[Route].StartX);
            double NearTarget = IncreasingX ? Edges.Min(Route => Terminals[Route].EndX)
                : Edges.Max(Route => Terminals[Route].EndX);
            List<FlowchartLayoutEdgeRoute> Bent = Edges.Where(Route =>
                Math.Abs(Terminals[Route].StartY - Terminals[Route].EndY) > 0.5).ToList();
            if (Bent.Count == 0 || (IncreasingX ? NearSource >= NearTarget : NearSource <= NearTarget)) continue;
            List<FlowchartLayoutEdgeRoute> Order = Bent.OrderBy(Route => Bounds[Route.SourceId].CenterY)
                .ThenBy(Route => Bounds[Route.TargetId].CenterY).ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToList();
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> PathsFor(
                IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Tracks = TrackOrder.Select((Route, Index) => (Route,
                    X: Math.Round(NearSource + (Index + 1.0) * (NearTarget - NearSource) / (TrackOrder.Count + 1.0))))
                    .ToDictionary(Item => Item.Route, Item => Item.X);
                return Edges.ToDictionary(Route => Route, Route =>
                {
                    var T = Terminals[Route];
                    if (Math.Abs(T.StartY - T.EndY) <= 0.5)
                        return new List<(double X, double Y)> { (T.StartX, T.StartY), (T.EndX, T.EndY) };
                    double Track = Tracks[Route];
                    return new List<(double X, double Y)> { (T.StartX, T.StartY), (Track, T.StartY),
                        (Track, T.EndY), (T.EndX, T.EndY) };
                });
            }
            int Crossings(IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                var Paths = PathsFor(TrackOrder);
                int Count = 0;
                for (int A = 0; A < Edges.Count; A++)
                for (int B = A + 1; B < Edges.Count; B++)
                {
                    var Interaction = FlowchartBranchRouteOrder.Intersections(Paths[Edges[A]], Paths[Edges[B]]);
                    Count += Interaction.Crossings + (Interaction.Overlap > 0.5 ? Edges.Count * Edges.Count : 0);
                }
                return Count;
            }
            int Best = Crossings(Order);
            while (true)
            {
                List<FlowchartLayoutEdgeRoute>? BetterOrder = null;
                int Better = Best;
                for (int From = 0; From < Order.Count; From++)
                for (int To = 0; To < Order.Count; To++)
                {
                    if (From == To) continue;
                    List<FlowchartLayoutEdgeRoute> Candidate = Order.ToList();
                    FlowchartLayoutEdgeRoute Moving = Candidate[From];
                    Candidate.RemoveAt(From);
                    Candidate.Insert(To, Moving);
                    int CandidateCrossings = Crossings(Candidate);
                    if (CandidateCrossings < Better) { Better = CandidateCrossings; BetterOrder = Candidate; }
                }
                if (BetterOrder == null) break;
                Order = BetterOrder;
                Best = Better;
            }
            var Paths = PathsFor(Order);
            if (Paths.Any(Item => HitsNode(Item.Value, Item.Key, Bounds))) continue;
            foreach (FlowchartLayoutEdgeRoute Route in Edges)
            {
                Route.Points.Clear();
                foreach ((double X, double Y) Point in Paths[Route])
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                Route.ExitSide = IncreasingX ? "right" : "left";
                Route.EntrySide = IncreasingX ? "left" : "right";
            }
        }
    }

    private static double SlotCross(FlowchartLayoutNodeBounds Box, int Index, int Count) => Math.Round(
        Box.CenterY - Box.Height / 2.0 + Box.Height * (Index + 1.0) / (Count + 1.0));

    private static HashSet<(string SourceId, string TargetId)> CompleteBlockPairs(DiagramContract Contract)
    {
        List<(string SourceId, string[] Targets)> Sources = Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Edge => Edge.SourceId)
            .Select(Group => (Group.Key, Group.Select(Edge => Edge.TargetId).Distinct()
                .OrderBy(Id => Id, StringComparer.Ordinal).ToArray())).ToList();
        return Sources.Where(Item => Item.Targets.Length >= 2)
            .GroupBy(Item => string.Join("\u001f", Item.Targets), StringComparer.Ordinal)
            .Where(Group => Group.Count() >= 2)
            .SelectMany(Group => Group.SelectMany(Item => Item.Targets.Select(Target => (Item.SourceId, Target))))
            .ToHashSet();
    }

    private static double Slot(FlowchartLayoutNodeBounds Box, int Index, int Count) => Math.Round(
        Box.CenterX - Box.Width / 2.0 + Box.Width * (Index + 1.0) / (Count + 1.0));

    private static EdgeContract? ContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route) =>
        Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId)
        ?? Contract.Edges.FirstOrDefault(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId);

    private static bool SharesEndpoint(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B) =>
        A.SourceId == B.SourceId || A.SourceId == B.TargetId || A.TargetId == B.SourceId || A.TargetId == B.TargetId;

    private static bool HitsNode(IReadOnlyList<(double X, double Y)> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId) continue;
            double Left = Box.CenterX - Box.Width / 2.0, Right = Box.CenterX + Box.Width / 2.0;
            double Bottom = Box.CenterY - Box.Height / 2.0, Top = Box.CenterY + Box.Height / 2.0;
            foreach (var Segment in Points.Zip(Points.Skip(1)))
            {
                var A = Segment.First; var B = Segment.Second;
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top)) return true;
                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right)) return true;
            }
        }
        return false;
    }
}
