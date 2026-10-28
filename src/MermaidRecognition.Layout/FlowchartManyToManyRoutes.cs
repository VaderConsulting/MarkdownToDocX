using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Routes dense adjacent layers from distinct bottom ports to distinct top ports.</summary>
internal static class FlowchartManyToManyRoutes
{
    internal static int MeasureCrossingDelta(FlowchartLayoutResult Layout,
        IReadOnlyCollection<FlowchartLayoutEdgeRoute> Changed,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, IReadOnlyList<(double X, double Y)>> CandidatePaths)
    {
        int Before = MeasureAffectedCrossings(Layout, Changed, CandidatePaths: null);
        int After = MeasureAffectedCrossings(Layout, Changed, CandidatePaths);
        return After - Before;
    }

    private static int MeasureAffectedCrossings(FlowchartLayoutResult Layout,
        IReadOnlyCollection<FlowchartLayoutEdgeRoute> Changed,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, IReadOnlyList<(double X, double Y)>>? CandidatePaths)
    {
        HashSet<FlowchartLayoutEdgeRoute> ChangedSet = [.. Changed];
        Dictionary<FlowchartLayoutEdgeRoute, int> RouteIndices = Layout.EdgeRoutes
            .Select((Route, Index) => (Route, Index)).ToDictionary(Item => Item.Route, Item => Item.Index);
        int Count = 0;
        foreach (FlowchartLayoutEdgeRoute Route in Changed)
        {
            foreach (FlowchartLayoutEdgeRoute Other in Layout.EdgeRoutes)
        {
            if (Route == Other || SharesEndpoint(Route, Other))
                {
                    continue;
                }

                if (ChangedSet.Contains(Other) && RouteIndices[Route] > RouteIndices[Other])
                {
                    continue;
                }

                IReadOnlyList<(double X, double Y)> RoutePath = CandidatePaths != null
                && CandidatePaths.TryGetValue(Route, out IReadOnlyList<(double X, double Y)>? Candidate) ? Candidate
                : Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            IReadOnlyList<(double X, double Y)> OtherPath = CandidatePaths != null
                && CandidatePaths.TryGetValue(Other, out IReadOnlyList<(double X, double Y)>? OtherCandidate) ? OtherCandidate
                : Other.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            Count += RoutePairIntersections(RoutePath, OtherPath).Crossings;
        }
        }

        return Count;
    }

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
        HashSet<(string SourceId, string TargetId)> BackEdges = [.. FlowchartBackEdgeHelper.FindBackEdges(Contract).Select(Edge => (Edge.SourceId, Edge.TargetId))];
        HashSet<(string SourceId, string TargetId)> CompletePairs = CompleteBlockPairs(Contract);
        List<FlowchartLayoutEdgeRoute> Routes = [.. Layout.EdgeRoutes.Where(Route => Bounds.ContainsKey(Route.SourceId)
            && Bounds.ContainsKey(Route.TargetId)
            && !CompletePairs.Contains((Route.SourceId, Route.TargetId))
            && Math.Abs(Bounds[Route.SourceId].CenterY - Bounds[Route.TargetId].CenterY) > 1
            && (Bounds[Route.SourceId].CenterY > Bounds[Route.TargetId].CenterY + 1
                || (HasPlacementHints && Bounds[Route.SourceId].CenterY < Bounds[Route.TargetId].CenterY - 1))
            && !BackEdges.Contains((Route.SourceId, Route.TargetId))
            && ContractEdge(Contract, Route)?.RoutingHint?.IsActive != true)];
        foreach (IGrouping<(double Source, double Target), FlowchartLayoutEdgeRoute> Rank in Routes.GroupBy(Route => (
            Source: Math.Round(Bounds[Route.SourceId].CenterY),
            Target: Math.Round(Bounds[Route.TargetId].CenterY))))
        {
            List<FlowchartLayoutEdgeRoute> Edges = [.. Rank];
            if (Edges.Count < 2)
            {
                continue;
            }

            bool SourceAboveTarget = Rank.Key.Source > Rank.Key.Target;
            bool AllUnlabelled = Edges.All(Route => ContractEdge(Contract, Route) is not EdgeContract Edge
                || string.IsNullOrWhiteSpace(Edge.Label));
            Dictionary<string, List<FlowchartLayoutEdgeRoute>> SourceOrder = Edges.GroupBy(Route => Route.SourceId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.TargetId].CenterX).ThenBy(Route => Route.TargetId).ToList());
            Dictionary<string, List<FlowchartLayoutEdgeRoute>> TargetOrder = Edges.GroupBy(Route => Route.TargetId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.SourceId].CenterX).ThenBy(Route => Route.SourceId).ToList());
            Dictionary<FlowchartLayoutEdgeRoute, (double StartX, double StartY, double EndX, double EndY)> Terminals = Edges.ToDictionary(Route => Route, Route =>
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
            List<FlowchartLayoutEdgeRoute> Bent = [.. Edges.Where(Route =>
                Math.Abs(Terminals[Route].StartX - Terminals[Route].EndX) > 0.5)];
            if (Bent.Count == 0 || (SourceAboveTarget ? NearSource <= NearTarget : NearSource >= NearTarget))
            {
                continue;
            }

            List<FlowchartLayoutEdgeRoute> Order = [.. Bent.OrderBy(Route =>
                    (SourceOrder[Route.SourceId].IndexOf(Route) + TargetOrder[Route.TargetId].IndexOf(Route)) / 2.0)
                .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal)];
            List<(double X, double Y)> PathAt(FlowchartLayoutEdgeRoute Route, int SlotIndex)
            {
                (double StartX, double StartY, double EndX, double EndY) T = Terminals[Route];
                if (Math.Abs(T.StartX - T.EndX) <= 0.5)
                {
                    return [(T.StartX, T.StartY), (T.EndX, T.EndY)];
                }

                double Track = Math.Round(NearSource + ((SlotIndex + 1) * (NearTarget - NearSource) / (Bent.Count + 1)));
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
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> Paths = PathsFor(Order);
            if (Paths.Any(Item => HitsNode(Item.Value, Item.Key, Bounds)))
            {
                continue;
            }

            foreach (FlowchartLayoutEdgeRoute Route in Edges)
            {
                Route.Points.Clear();
                foreach ((double X, double Y) Point in Paths[Route])
                {
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }

                Route.ExitSide = SourceAboveTarget ? "bottom" : "top";
                Route.EntrySide = SourceAboveTarget ? "top" : "bottom";
            }
        }
    }

    private static void ApplyHorizontal(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartDirection Direction)
    {
        HashSet<(string SourceId, string TargetId)> BackEdges = [.. FlowchartBackEdgeHelper.FindBackEdges(Contract).Select(Edge => (Edge.SourceId, Edge.TargetId))];
        HashSet<(string SourceId, string TargetId)> CompletePairs = CompleteBlockPairs(Contract);
        List<FlowchartLayoutEdgeRoute> Routes = [.. Layout.EdgeRoutes.Where(Route =>
            Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && CompletePairs.Contains((Route.SourceId, Route.TargetId))
            && Math.Abs(Bounds[Route.SourceId].CenterX - Bounds[Route.TargetId].CenterX) > 1
            && (Direction == FlowchartDirection.LeftToRight
                ? Bounds[Route.SourceId].CenterX < Bounds[Route.TargetId].CenterX
                : Bounds[Route.SourceId].CenterX > Bounds[Route.TargetId].CenterX)
            && !BackEdges.Contains((Route.SourceId, Route.TargetId))
            && ContractEdge(Contract, Route)?.RoutingHint?.IsActive != true)];
        List<IGrouping<(double Source, double Target), FlowchartLayoutEdgeRoute>> RankGroups = [.. Routes.GroupBy(Route => (Source: Math.Round(Bounds[Route.SourceId].CenterX),
            Target: Math.Round(Bounds[Route.TargetId].CenterX)))];
        foreach (IGrouping<(double Source, double Target), FlowchartLayoutEdgeRoute>? Rank in RankGroups.Where(Group => Group.Count() > 1))
        {
            List<FlowchartLayoutEdgeRoute> Edges = [.. Rank];
            bool IncreasingX = Direction == FlowchartDirection.LeftToRight;
            Dictionary<string, List<FlowchartLayoutEdgeRoute>> SourceOrder = Edges.GroupBy(Route => Route.SourceId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.TargetId].CenterY).ThenBy(Route => Route.TargetId).ToList());
            Dictionary<string, List<FlowchartLayoutEdgeRoute>> TargetOrder = Edges.GroupBy(Route => Route.TargetId).ToDictionary(Group => Group.Key,
                Group => Group.OrderBy(Route => Bounds[Route.SourceId].CenterY).ThenBy(Route => Route.SourceId).ToList());
            Dictionary<FlowchartLayoutEdgeRoute, (double StartX, double StartY, double EndX, double EndY)> Terminals = Edges.ToDictionary(Route => Route, Route =>
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
            List<FlowchartLayoutEdgeRoute> Bent = [.. Edges.Where(Route =>
                Math.Abs(Terminals[Route].StartY - Terminals[Route].EndY) > 0.5)];
            if (Bent.Count == 0 || (IncreasingX ? NearSource >= NearTarget : NearSource <= NearTarget))
            {
                continue;
            }

            Dictionary<FlowchartLayoutEdgeRoute, (int Source, int Target)> EndpointRanks = Bent.ToDictionary(
                Route => Route, Route => (SourceOrder[Route.SourceId].IndexOf(Route),
                    TargetOrder[Route.TargetId].IndexOf(Route)));
            List<FlowchartLayoutEdgeRoute> Order = [.. Bent.OrderBy(Route =>
                    (Terminals[Route].StartY + Terminals[Route].EndY) / 2.0)
                .ThenBy(Route => Terminals[Route].StartY)
                .ThenBy(Route => Terminals[Route].EndY)
                .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal)];
            List<FlowchartLayoutEdgeRoute> MedianSweep(IReadOnlyList<FlowchartLayoutEdgeRoute> Current,
                bool SourceFirst)
            {
                Dictionary<FlowchartLayoutEdgeRoute, int> CurrentPosition = Current
                    .Select((Route, Index) => (Route, Index)).ToDictionary(Item => Item.Route, Item => Item.Index);
                return [.. Bent.OrderBy(Route => SourceFirst
                        ? EndpointRanks[Route].Source : EndpointRanks[Route].Target)
                    .ThenBy(Route => SourceFirst
                        ? EndpointRanks[Route].Target : EndpointRanks[Route].Source)
                    .ThenBy(Route => CurrentPosition[Route])
                    .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal)];
            }
            List<(double X, double Y)> PathAt(FlowchartLayoutEdgeRoute Route, int SlotIndex)
            {
                (double StartX, double StartY, double EndX, double EndY) T = Terminals[Route];
                if (Math.Abs(T.StartY - T.EndY) <= 0.5)
                {
                    return [(T.StartX, T.StartY), (T.EndX, T.EndY)];
                }

                double Track = Math.Round(NearSource + ((SlotIndex + 1.0)
                    * (NearTarget - NearSource) / (Bent.Count + 1.0)));
                return [(T.StartX, T.StartY), (Track, T.StartY), (Track, T.EndY), (T.EndX, T.EndY)];
            }
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> PathsFor(
                IReadOnlyList<FlowchartLayoutEdgeRoute> TrackOrder)
            {
                Dictionary<FlowchartLayoutEdgeRoute, int> Slots = TrackOrder
                    .Select((Route, Index) => (Route, Index))
                    .ToDictionary(Item => Item.Route, Item => Item.Index);
                return Edges.ToDictionary(Route => Route,
                    Route => PathAt(Route, Slots.GetValueOrDefault(Route, -1)));
            }
            double MinTrackX = Edges.Min(Route => Math.Min(Terminals[Route].StartX, Terminals[Route].EndX));
            double MaxTrackX = Edges.Max(Route => Math.Max(Terminals[Route].StartX, Terminals[Route].EndX));
            double MinTrackY = Edges.Min(Route => Math.Min(Terminals[Route].StartY, Terminals[Route].EndY));
            double MaxTrackY = Edges.Max(Route => Math.Max(Terminals[Route].StartY, Terminals[Route].EndY));
            int OverlapPenalty = Edges.Count * Edges.Count;
            HashSet<FlowchartLayoutEdgeRoute> EdgeSet = [.. Edges];
            (FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points)[] ExternalRoutes = [.. Layout.EdgeRoutes.Where(Route => !EdgeSet.Contains(Route)
                && RouteTouchesArea(Route, MinTrackX, MinTrackY, MaxTrackX, MaxTrackY))
                .Select(Route => (Route, Points: Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()))];
            int RouteScore(FlowchartLayoutEdgeRoute Route, int SlotIndex)
            {
                IReadOnlyList<(double X, double Y)> CandidatePath = PathAt(Route, SlotIndex);
                int Score = 0;
                foreach ((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points) Other in ExternalRoutes)
                {
                    if (SharesEndpoint(Route, Other.Route))
                    {
                        continue;
                    }

                    (int Crossings, double Overlap) Interaction = RoutePairIntersections(CandidatePath, Other.Points);
                    Score += Interaction.Crossings + (Interaction.Overlap > 0.5 ? OverlapPenalty : 0);
                }
                return Score;
            }
            Order = ImproveTrackOrder(Edges, Order, PathAt, MedianSweep, RouteScore);
            Dictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>> Paths = PathsFor(Order);
            bool CandidateHit = Paths.Any(Item => HitsNode(Item.Value, Item.Key, Bounds));
            if (CandidateHit)
            {
                continue;
            }

            int AffectedCrossingDelta = MeasureCrossingDelta(Layout, Edges,
                Paths.ToDictionary(Item => Item.Key,
                    Item => (IReadOnlyList<(double X, double Y)>)Item.Value));
            bool RepairsAttachment = !EndpointsOnOutlines(Edges, Bounds, CandidatePaths: null)
                && EndpointsOnOutlines(Edges, Bounds, Paths);
            if (AffectedCrossingDelta >= 0 && !RepairsAttachment)
            {
                continue;
            }

            foreach (FlowchartLayoutEdgeRoute Route in Edges)
            {
                Route.Points.Clear();
                foreach ((double X, double Y) Point in Paths[Route])
                {
                    Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }

                Route.ExitSide = IncreasingX ? "right" : "left";
                Route.EntrySide = IncreasingX ? "left" : "right";
            }
        }
    }

    private static double SlotCross(FlowchartLayoutNodeBounds Box, int Index, int Count) => Math.Round(
        Box.CenterY - (Box.Height / 2.0) + (Box.Height * (Index + 1.0) / (Count + 1.0)));

    private static List<FlowchartLayoutEdgeRoute> ImproveTrackOrder(
        IReadOnlyList<FlowchartLayoutEdgeRoute> Edges,
        List<FlowchartLayoutEdgeRoute> Seed,
        Func<FlowchartLayoutEdgeRoute, int, List<(double X, double Y)>> PathAt,
        Func<IReadOnlyList<FlowchartLayoutEdgeRoute>, bool, List<FlowchartLayoutEdgeRoute>>? MedianSweep = null,
        Func<FlowchartLayoutEdgeRoute, int, int>? SingleRouteScore = null)
    {
        Dictionary<FlowchartLayoutEdgeRoute, int> EdgeIndex = Edges
            .Select((Route, Index) => (Route, Index)).ToDictionary(Item => Item.Route, Item => Item.Index);
        Dictionary<(FlowchartLayoutEdgeRoute Route, int Slot), (double X, double Y)[]> PathCache = [];
        Dictionary<(FlowchartLayoutEdgeRoute Route, int Slot), int> SingleScoreCache = [];
        Dictionary<(int A, int SlotA, int B, int SlotB), int> PairCache = [];

        (double X, double Y)[] Path(FlowchartLayoutEdgeRoute Route, int Slot)
        {
            (FlowchartLayoutEdgeRoute Route, int Slot) Key = (Route, Slot);
            if (!PathCache.TryGetValue(Key, out (double X, double Y)[]? Value))
            {
                PathCache[Key] = Value = [.. PathAt(Route, Slot)];
            }

            return Value;
        }

        int SingleScore(FlowchartLayoutEdgeRoute Route, int Slot)
        {
            (FlowchartLayoutEdgeRoute Route, int Slot) Key = (Route, Slot);
            if (!SingleScoreCache.TryGetValue(Key, out int Score))
            {
                SingleScoreCache[Key] = Score = SingleRouteScore?.Invoke(Route, Slot) ?? 0;
            }

            return Score;
        }

        int PairScore(FlowchartLayoutEdgeRoute A, int SlotA, FlowchartLayoutEdgeRoute B, int SlotB)
        {
            if (SharesEndpoint(A, B))
            {
                return 0;
            }

            int AIndex = EdgeIndex[A], BIndex = EdgeIndex[B];
            if (AIndex > BIndex)
            {
                (AIndex, BIndex) = (BIndex, AIndex);
                (A, B) = (B, A);
                (SlotA, SlotB) = (SlotB, SlotA);
            }
            (int AIndex, int SlotA, int BIndex, int SlotB) Key = (AIndex, SlotA, BIndex, SlotB);
            if (PairCache.TryGetValue(Key, out int Cached))
            {
                return Cached;
            }

            (int Crossings, double Overlap) Interaction = RoutePairIntersections(Path(A, SlotA), Path(B, SlotB));
            int Score = Interaction.Crossings + (Interaction.Overlap > 0.5 ? Edges.Count * Edges.Count : 0);
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
            {
                for (int B = A + 1; B < Edges.Count; B++)
                {
                    Score += PairScore(Edges[A], Slots.GetValueOrDefault(Edges[A], -1),
                    Edges[B], Slots.GetValueOrDefault(Edges[B], -1));
                }
            }

            if (SingleRouteScore != null)
            {
                foreach (FlowchartLayoutEdgeRoute Route in Edges)
                {
                    Score += SingleScore(Route, Slots.GetValueOrDefault(Route, -1));
                }
            }

            return Score;
        }

        int SwapDelta(IReadOnlyList<FlowchartLayoutEdgeRoute> Order,
            IReadOnlyDictionary<FlowchartLayoutEdgeRoute, int> Slots, int Position)
        {
            FlowchartLayoutEdgeRoute A = Order[Position], B = Order[Position + 1];
            int Delta = PairScore(A, Position + 1, B, Position) - PairScore(A, Position, B, Position + 1);
            foreach (FlowchartLayoutEdgeRoute Other in Edges)
            {
                if (Other == A || Other == B)
                {
                    continue;
                }

                int OtherSlot = Slots.GetValueOrDefault(Other, -1);
                Delta += PairScore(A, Position + 1, Other, OtherSlot)
                    + PairScore(B, Position, Other, OtherSlot)
                    - PairScore(A, Position, Other, OtherSlot)
                    - PairScore(B, Position + 1, Other, OtherSlot);
            }
            if (SingleRouteScore != null)
            {
                Delta += SingleScore(A, Position + 1) + SingleScore(B, Position)
                    - SingleScore(A, Position) - SingleScore(B, Position + 1);
            }

            return Delta;
        }

        static string Signature(IEnumerable<FlowchartLayoutEdgeRoute> Order) =>
            string.Join("\u001f", Order.Select(Route => Route.EdgeId));

        List<FlowchartLayoutEdgeRoute> Order = [.. Seed];
        Dictionary<FlowchartLayoutEdgeRoute, int> Slots = BuildSlots(Order);
        int Current = ScoreOrder(Order, Slots);
        HashSet<string> Seen = new(StringComparer.Ordinal) { Signature(Order) };
        while (true)
        {
            bool Improved = false;
            if (MedianSweep != null)
            {
                foreach (bool SourceFirst in new[] { true, false })
                {
                    List<FlowchartLayoutEdgeRoute> Swept = MedianSweep(Order, SourceFirst);
                    Dictionary<FlowchartLayoutEdgeRoute, int> SweptSlots = BuildSlots(Swept);
                    int SweptScore = ScoreOrder(Swept, SweptSlots);
                    if (SweptScore > Current || !Seen.Add(Signature(Swept)))
                    {
                        continue;
                    }

                    bool SweepImproved = SweptScore < Current;
                    Order = Swept;
                    Slots = SweptSlots;
                    Current = SweptScore;
                    Improved |= SweepImproved;
                    bool TransposeImproved;
                    do
                    {
                        TransposeImproved = false;
                        for (int Position = 0; Position + 1 < Order.Count; Position++)
                        {
                            int Delta = SwapDelta(Order, Slots, Position);
                            if (Delta >= 0)
                            {
                                continue;
                            }

                            (Order[Position], Order[Position + 1]) = (Order[Position + 1], Order[Position]);
                            Slots[Order[Position]] = Position;
                            Slots[Order[Position + 1]] = Position + 1;
                            Current += Delta;
                            TransposeImproved = true;
                        }
                    } while (TransposeImproved && Seen.Add(Signature(Order)));
                }
            }
            bool Transposed;
            do
            {
                Transposed = false;
                for (int Position = 0; Position + 1 < Order.Count; Position++)
                {
                    int Delta = SwapDelta(Order, Slots, Position);
                    if (Delta >= 0)
                    {
                        continue;
                    }

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
                List<FlowchartLayoutEdgeRoute> BaseOrder = [.. Order];
                int BestScore = Current;
                List<FlowchartLayoutEdgeRoute>? BestOrder = null;

                List<FlowchartLayoutEdgeRoute> Trial = [.. BaseOrder];
                Dictionary<FlowchartLayoutEdgeRoute, int> TrialSlots = BuildSlots(Trial);
                int TrialScore = Current;
                for (int Position = Origin; Position > 0; Position--)
                {
                    int Delta = SwapDelta(Trial, TrialSlots, Position - 1);
                    (Trial[Position - 1], Trial[Position]) = (Trial[Position], Trial[Position - 1]);
                    TrialSlots[Trial[Position - 1]] = Position - 1;
                    TrialSlots[Trial[Position]] = Position;
                    TrialScore += Delta;
                    if (TrialScore < BestScore) { BestScore = TrialScore; BestOrder = [.. Trial]; }
                }

                Trial = [.. BaseOrder];
                TrialSlots = BuildSlots(Trial);
                TrialScore = Current;
                for (int Position = Origin; Position + 1 < Trial.Count; Position++)
                {
                    int Delta = SwapDelta(Trial, TrialSlots, Position);
                    (Trial[Position], Trial[Position + 1]) = (Trial[Position + 1], Trial[Position]);
                    TrialSlots[Trial[Position]] = Position;
                    TrialSlots[Trial[Position + 1]] = Position + 1;
                    TrialScore += Delta;
                    if (TrialScore < BestScore) { BestScore = TrialScore; BestOrder = [.. Trial]; }
                }

                if (BestOrder == null)
                {
                    continue;
                }

                Order = BestOrder;
                Slots = BuildSlots(Order);
                Current = BestScore;
                Improved = true;
                if (!Seen.Add(Signature(Order)))
                {
                    return Order;
                }
            }

            if (!Improved)
            {
                break;
            }
        }
        return Order;
    }

    private static HashSet<(string SourceId, string TargetId)> CompleteBlockPairs(DiagramContract Contract)
    {
        List<(string SourceId, string[] Targets)> Sources = [.. Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Edge => Edge.SourceId)
            .Select(Group => (Group.Key, Group.Select(Edge => Edge.TargetId).Distinct()
                .OrderBy(Id => Id, StringComparer.Ordinal).ToArray()))];
        return [.. Sources.Where(Item => Item.Targets.Length >= 2)
            .GroupBy(Item => string.Join("\u001f", Item.Targets), StringComparer.Ordinal)
            .Where(Group => Group.Count() >= 2)
            .SelectMany(Group => Group.SelectMany(Item => Item.Targets.Select(Target => (Item.SourceId, Target))))];
    }

    private static double Slot(FlowchartLayoutNodeBounds Box, int Index, int Count) => Math.Round(
        Box.CenterX - (Box.Width / 2.0) + (Box.Width * (Index + 1.0) / (Count + 1.0)));

    private static EdgeContract? ContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route) =>
        Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId)
        ?? Contract.Edges.FirstOrDefault(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId);

    private static bool SharesEndpoint(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B) =>
        A.SourceId == B.SourceId || A.SourceId == B.TargetId
        || A.TargetId == B.SourceId || A.TargetId == B.TargetId;

    private static bool RouteTouchesArea(FlowchartLayoutEdgeRoute Route,
        double Left, double Top, double Right, double Bottom)
    {
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Segment in Route.Points.Zip(Route.Points.Skip(1)))
        {
            FlowchartLayoutPoint A = Segment.First; FlowchartLayoutPoint B = Segment.Second;
            if (Math.Max(Math.Min(A.X, B.X), Left) <= Math.Min(Math.Max(A.X, B.X), Right)
                && Math.Max(Math.Min(A.Y, B.Y), Top) <= Math.Min(Math.Max(A.Y, B.Y), Bottom))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EndpointsOnOutlines(IReadOnlyCollection<FlowchartLayoutEdgeRoute> Routes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, List<(double X, double Y)>>? CandidatePaths)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Routes)
        {
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                return false;
            }

            IReadOnlyList<(double X, double Y)> Points = CandidatePaths != null
                && CandidatePaths.TryGetValue(Route, out List<(double X, double Y)>? Candidate) ? Candidate
                : Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            if (Points.Count < 2 || !OnOutline(Points[0], Source) || !OnOutline(Points[^1], Target))
            {
                return false;
            }
        }
        return true;
    }

    private static bool OnOutline((double X, double Y) Point, FlowchartLayoutNodeBounds Box)
    {
        double Left = Box.CenterX - (Box.Width / 2.0), Right = Box.CenterX + (Box.Width / 2.0);
        double Top = Box.CenterY - (Box.Height / 2.0), Bottom = Box.CenterY + (Box.Height / 2.0);
        bool OnVertical = (Math.Abs(Point.X - Left) < 0.01 || Math.Abs(Point.X - Right) < 0.01)
            && Point.Y >= Top - 0.01 && Point.Y <= Bottom + 0.01;
        bool OnHorizontal = (Math.Abs(Point.Y - Top) < 0.01 || Math.Abs(Point.Y - Bottom) < 0.01)
            && Point.X >= Left - 0.01 && Point.X <= Right + 0.01;
        return OnVertical || OnHorizontal;
    }

    private static (int Crossings, double Overlap) RoutePairIntersections(
        IReadOnlyList<(double X, double Y)> First, IReadOnlyList<(double X, double Y)> Second)
    {
        Span<(double X, double Y)> CrossingPoints = stackalloc (double X, double Y)[
            Math.Max(1, (First.Count - 1) * (Second.Count - 1))];
        int CrossingCount = 0;
        double Overlap = 0;
        for (int I = 1; I < First.Count; I++)
        {
            for (int J = 1; J < Second.Count; J++)
        {
                (double X, double Y) A = First[I - 1]; (double X, double Y) B = First[I]; (double X, double Y) C = Second[J - 1]; (double X, double Y) D = Second[J];
            bool AV = Math.Abs(A.X - B.X) < 0.01; bool CV = Math.Abs(C.X - D.X) < 0.01;
            if (AV == CV)
            {
                if (Math.Abs((AV ? A.X : A.Y) - (CV ? C.X : C.Y)) > 0.01)
                    {
                        continue;
                    }

                    Overlap += Math.Max(0, Math.Min(Math.Max(AV ? A.Y : A.X, AV ? B.Y : B.X),
                        Math.Max(CV ? C.Y : C.X, CV ? D.Y : D.X))
                    - Math.Max(Math.Min(AV ? A.Y : A.X, AV ? B.Y : B.X),
                        Math.Min(CV ? C.Y : C.X, CV ? D.Y : D.X)));
                continue;
            }
                (double X, double Y) V1 = AV ? A : C; (double X, double Y) V2 = AV ? B : D; (double X, double Y) H1 = AV ? C : A; (double X, double Y) H2 = AV ? D : B;
                (double X, double Y) Point = (X: V1.X, Y: H1.Y);
            if (Point.X < Math.Min(H1.X, H2.X) - 0.01 || Point.X > Math.Max(H1.X, H2.X) + 0.01
                || Point.Y < Math.Min(V1.Y, V2.Y) - 0.01 || Point.Y > Math.Max(V1.Y, V2.Y) + 0.01)
                {
                    continue;
                }

                bool EndOfFirst = Near(Point, First[0]) || Near(Point, First[^1]);
            bool EndOfSecond = Near(Point, Second[0]) || Near(Point, Second[^1]);
            if (EndOfFirst && EndOfSecond)
                {
                    continue;
                }

                bool Duplicate = false;
            for (int CrossIndex = 0; CrossIndex < CrossingCount; CrossIndex++)
                {
                    if (Near(CrossingPoints[CrossIndex], Point)) { Duplicate = true; break; }
                }

                if (!Duplicate)
                {
                    CrossingPoints[CrossingCount++] = Point;
                }
            }
        }

        return (CrossingCount, Overlap);
    }

    private static bool Near((double X, double Y) A, (double X, double Y) B) =>
        Math.Abs(A.X - B.X) < 0.01 && Math.Abs(A.Y - B.Y) < 0.01;

    private static bool HitsNode(IReadOnlyList<(double X, double Y)> Points, FlowchartLayoutEdgeRoute Route,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
        {
            if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId)
            {
                continue;
            }

            double Left = Box.CenterX - (Box.Width / 2.0), Right = Box.CenterX + (Box.Width / 2.0);
            double Bottom = Box.CenterY - (Box.Height / 2.0), Top = Box.CenterY + (Box.Height / 2.0);
            foreach (((double X, double Y) First, (double X, double Y) Second) Segment in Points.Zip(Points.Skip(1)))
            {
                (double X, double Y) A = Segment.First; (double X, double Y) B = Segment.Second;
                if (Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Bottom) < Math.Min(Math.Max(A.Y, B.Y), Top))
                {
                    return true;
                }

                if (Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Bottom && A.Y < Top
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
