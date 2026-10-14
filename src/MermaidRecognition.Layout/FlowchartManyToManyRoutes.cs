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

            List<FlowchartLayoutEdgeRoute> Order = Bent.OrderBy(Route =>
                    (SourceOrder[Route.SourceId].IndexOf(Route) + TargetOrder[Route.TargetId].IndexOf(Route)) / 2.0)
                .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToList();
            List<(double X, double Y)> PathAt(FlowchartLayoutEdgeRoute Route, int SlotIndex)
            {
                var T = Terminals[Route];
                if (Math.Abs(T.StartX - T.EndX) <= 0.5)
                    return [(T.StartX, T.StartY), (T.EndX, T.EndY)];
                double Track = Math.Round(NearSource + (SlotIndex + 1) * (NearTarget - NearSource) / (Bent.Count + 1));
                return [(T.StartX, T.StartY), (T.StartX, Track), (T.EndX, Track), (T.EndX, T.EndY)];
            }
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> PathsFor(
                IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                Dictionary<FlowchartLayoutEdgeRoute, int> Slots = TrackOrder.Select((Route, Index) => (Route, Index))
                    .ToDictionary(Item => Item.Route, Item => Item.Index);
                return Edges.ToDictionary(Route => Route, Route => PathAt(Route,
                    Slots.TryGetValue(Route, out int Index) ? Index : -1));
            }
            Order = ImproveTrackOrder(Edges, Order, PathAt);
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
                    if (SharesEndpoint(Edges[A], Edges[B])) continue;
                    var Interaction = FlowchartBranchRouteOrder.Intersections(Paths[Edges[A]], Paths[Edges[B]]);
                    Count += Interaction.Crossings + (Interaction.Overlap > 0.5 ? Edges.Count * Edges.Count : 0);
                }
                return Count;
            }
            int Best = Crossings(Order);
            if (Best == 0) continue;
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

    private static List<FlowchartLayoutEdgeRoute> ImproveTrackOrder(
        IReadOnlyList<FlowchartLayoutEdgeRoute> Edges,
        List<FlowchartLayoutEdgeRoute> Seed,
        Func<FlowchartLayoutEdgeRoute, int, List<(double X, double Y)>> PathAt)
    {
        Dictionary<FlowchartLayoutEdgeRoute, int> EdgeIndex = Edges
            .Select((Route, Index) => (Route, Index)).ToDictionary(Item => Item.Route, Item => Item.Index);
        Dictionary<(FlowchartLayoutEdgeRoute Route, int Slot), (double X, double Y)[]> PathCache = [];
        Dictionary<(int A, int SlotA, int B, int SlotB), int> PairCache = [];
        int OverlapPenalty = Edges.Count * Edges.Count;

        (double X, double Y)[] Path(FlowchartLayoutEdgeRoute Route, int Slot)
        {
            var Key = (Route, Slot);
            if (!PathCache.TryGetValue(Key, out (double X, double Y)[]? Value))
                PathCache[Key] = Value = PathAt(Route, Slot).ToArray();
            return Value;
        }

        int PairScore(FlowchartLayoutEdgeRoute A, int SlotA, FlowchartLayoutEdgeRoute B, int SlotB)
        {
            if (SharesEndpoint(A, B)) return 0;
            int AIndex = EdgeIndex[A], BIndex = EdgeIndex[B];
            if (AIndex > BIndex)
            {
                (AIndex, BIndex) = (BIndex, AIndex);
                (A, B) = (B, A);
                (SlotA, SlotB) = (SlotB, SlotA);
            }
            var Key = (AIndex, SlotA, BIndex, SlotB);
            if (PairCache.TryGetValue(Key, out int Cached)) return Cached;
            var Interaction = FlowchartBranchRouteOrder.Intersections(Path(A, SlotA), Path(B, SlotB));
            int Score = Interaction.Crossings + (Interaction.Overlap > 0.5 ? OverlapPenalty : 0);
            PairCache[Key] = Score;
            return Score;
        }

        Dictionary<FlowchartLayoutEdgeRoute, int> BuildSlots(IReadOnlyList<FlowchartLayoutEdgeRoute> Order) =>
            Order.Select((Route, Index) => (Route, Index)).ToDictionary(Item => Item.Route, Item => Item.Index);

        int ScoreOrder(IReadOnlyList<FlowchartLayoutEdgeRoute> Order,
            IReadOnlyDictionary<FlowchartLayoutEdgeRoute, int> Slots)
        {
            int Score = 0;
            for (int A = 0; A < Edges.Count; A++)
            for (int B = A + 1; B < Edges.Count; B++)
                Score += PairScore(Edges[A], Slots.GetValueOrDefault(Edges[A], -1),
                    Edges[B], Slots.GetValueOrDefault(Edges[B], -1));
            return Score;
        }

        int SwapDelta(IReadOnlyList<FlowchartLayoutEdgeRoute> Order,
            IReadOnlyDictionary<FlowchartLayoutEdgeRoute, int> Slots, int Position)
        {
            FlowchartLayoutEdgeRoute A = Order[Position], B = Order[Position + 1];
            int Delta = PairScore(A, Position + 1, B, Position) - PairScore(A, Position, B, Position + 1);
            foreach (FlowchartLayoutEdgeRoute Other in Edges)
            {
                if (Other == A || Other == B) continue;
                int OtherSlot = Slots.GetValueOrDefault(Other, -1);
                Delta += PairScore(A, Position + 1, Other, OtherSlot)
                    + PairScore(B, Position, Other, OtherSlot)
                    - PairScore(A, Position, Other, OtherSlot)
                    - PairScore(B, Position + 1, Other, OtherSlot);
            }
            return Delta;
        }

        static string Signature(IEnumerable<FlowchartLayoutEdgeRoute> Order) =>
            string.Join("\u001f", Order.Select(Route => Route.EdgeId));

        List<FlowchartLayoutEdgeRoute> Order = Seed.ToList();
        Dictionary<FlowchartLayoutEdgeRoute, int> Slots = BuildSlots(Order);
        int Current = ScoreOrder(Order, Slots);
        HashSet<string> Seen = new(StringComparer.Ordinal) { Signature(Order) };
        while (true)
        {
            bool Improved = false;
            bool Transposed;
            do
            {
                Transposed = false;
                for (int Position = 0; Position + 1 < Order.Count; Position++)
                {
                    int Delta = SwapDelta(Order, Slots, Position);
                    if (Delta >= 0) continue;
                    (Order[Position], Order[Position + 1]) = (Order[Position + 1], Order[Position]);
                    Slots[Order[Position]] = Position;
                    Slots[Order[Position + 1]] = Position + 1;
                    Current += Delta;
                    Transposed = Improved = true;
                }
            } while (Transposed && Seen.Add(Signature(Order)));

            foreach (FlowchartLayoutEdgeRoute Moving in Order.ToArray())
            {
                int Origin = Slots[Moving];
                List<FlowchartLayoutEdgeRoute> BaseOrder = Order.ToList();
                int BestScore = Current;
                List<FlowchartLayoutEdgeRoute>? BestOrder = null;

                List<FlowchartLayoutEdgeRoute> Trial = BaseOrder.ToList();
                Dictionary<FlowchartLayoutEdgeRoute, int> TrialSlots = BuildSlots(Trial);
                int TrialScore = Current;
                for (int Position = Origin; Position > 0; Position--)
                {
                    int Delta = SwapDelta(Trial, TrialSlots, Position - 1);
                    (Trial[Position - 1], Trial[Position]) = (Trial[Position], Trial[Position - 1]);
                    TrialSlots[Trial[Position - 1]] = Position - 1;
                    TrialSlots[Trial[Position]] = Position;
                    TrialScore += Delta;
                    if (TrialScore < BestScore) { BestScore = TrialScore; BestOrder = Trial.ToList(); }
                }

                Trial = BaseOrder.ToList();
                TrialSlots = BuildSlots(Trial);
                TrialScore = Current;
                for (int Position = Origin; Position + 1 < Trial.Count; Position++)
                {
                    int Delta = SwapDelta(Trial, TrialSlots, Position);
                    (Trial[Position], Trial[Position + 1]) = (Trial[Position + 1], Trial[Position]);
                    TrialSlots[Trial[Position]] = Position;
                    TrialSlots[Trial[Position + 1]] = Position + 1;
                    TrialScore += Delta;
                    if (TrialScore < BestScore) { BestScore = TrialScore; BestOrder = Trial.ToList(); }
                }

                if (BestOrder == null) continue;
                Order = BestOrder;
                Slots = BuildSlots(Order);
                Current = BestScore;
                Improved = true;
                if (!Seen.Add(Signature(Order))) return Order;
            }

            if (!Improved) break;
        }
        return Order;
    }

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
