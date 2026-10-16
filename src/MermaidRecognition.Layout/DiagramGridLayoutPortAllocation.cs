using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Port allocation and endpoint-attachment helpers for the port stage.</summary>
public static partial class DiagramGridLayout
{    private static void AssignPortSides(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        HashSet<string> SharedFanSources)
    {
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GridFrame Frame in Frames)
        {
            FrameById[Frame.Id] = Frame;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds = BuildObstacleBounds(Placed, Frames);
        HashSet<string> ReservedCenterPorts = new HashSet<string>(StringComparer.Ordinal);
        List<PortEnd> Ends = new List<PortEnd>();
        List<(FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target)> Pending = [];
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!TryBox(Route.SourceId, ById, FrameById, out BoxRef Source) || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            EdgeContract? ContractEdge = FindContractEdge(Contract, Route);
            if (string.Equals(ContractEdge?.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                // Invisible links are not drawn and must not consume a visible
                // port slot on the same side as a real connection.
                continue;
            }
            RoutingHintContract? RoutingHint = ContractEdge?.RoutingHint?.IsActive == true
                ? ContractEdge.RoutingHint : null;
            bool CircularReturn = BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                && (Nodes.TryGetValue(Route.SourceId, out var CircularSource) && CircularSource.Shape == "circle"
                    || Nodes.TryGetValue(Route.TargetId, out var CircularTarget) && CircularTarget.Shape == "circle");
            string StraightExit = string.Empty, StraightEntry = string.Empty;
            bool HasStraightCandidate = RoutingHint == null && !CircularReturn
                && TryAssignStraightPortSides(Source, Target, Direction,
                    out StraightExit, out StraightEntry);
            if (HasStraightCandidate && StraightChordIsClear(
                    Contract,
                    Source,
                    Target,
                    StraightExit,
                    StraightEntry,
                    ObstacleBounds,
                    Route.SourceId,
                    Route.TargetId))
            {
                Route.ExitSide = StraightExit;
                Route.EntrySide = StraightEntry;
                ReservedCenterPorts.Add(Source.Id + "|" + StraightExit);
                ReservedCenterPorts.Add(Target.Id + "|" + StraightEntry);
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = true,
                    KeepsCentre = true,
                    Side = StraightExit,
                    Box = Source,
                    Other = Target
                });
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = false,
                    KeepsCentre = true,
                    Side = StraightEntry,
                    Box = Target,
                    Other = Source
                });
                continue;
            }
            Pending.Add((Route, Source, Target));
        }

        Dictionary<string, FlowchartLayoutNodeBounds> FanOutBoundsById = BuildPlacedBounds(Placed, Frames);
        foreach ((FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target) Item in Pending)
        {
            string Exit;
            string Entry;
            EdgeContract? ContractEdge = FindContractEdge(Contract, Item.Route);
            RoutingHintContract? RoutingHint = ContractEdge?.RoutingHint?.IsActive == true
                ? ContractEdge.RoutingHint : null;
            if (RoutingHint != null)
            {
                Exit = RoutingHint.Leave.HasValue
                    ? HintSide(RoutingHint.Leave.Value)
                    : FacingSide(Item.Source, Item.Target);
                Entry = RoutingHint.Enter.HasValue
                    ? HintSide(RoutingHint.Enter.Value)
                    : FacingSide(Item.Target, Item.Source);
            }
            else
            {
                // Geometry owns the first side choice for every ordinary edge,
                // including fans and junctions. Direction, old Dagre sides and
                // track occupancy affect the path between endpoints, never the
                // endpoint sides. This prevents 4.7 repair becoming a late
                // substitute for allocating sides from the placed node geometry.
                Exit = FacingSide(Item.Source, Item.Target);
                Entry = FacingSide(Item.Target, Item.Source);
            }
            Item.Route.ExitSide = Exit;
            Item.Route.EntrySide = Entry;
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = true,
                IsHinted = RoutingHint != null,
                Side = Exit,
                Box = Item.Source,
                Other = Item.Target
            });
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = false,
                IsHinted = RoutingHint != null,
                Side = Entry,
                Box = Item.Target,
                Other = Item.Source
            });
        }

        Dictionary<string, List<PortEnd>> Groups = new Dictionary<string, List<PortEnd>>(StringComparer.Ordinal);
        foreach (PortEnd End in Ends)
        {
            bool Diamond = !End.Box.IsFrame
                && Nodes.TryGetValue(End.Box.Id, out NodeContract? PortNode)
                && FlowchartDagrePresentation.IsDiamondShape(PortNode.Shape);
            // A diamond's four vertices and sloping-edge ports form one shared
            // inventory. Allocating each nominal rectangle side separately can
            // give two lines the same vertex.
            string Key = End.Box.Id + (Diamond ? "|diamond" : "|" + End.Side);
            if (!Groups.TryGetValue(Key, out List<PortEnd>? Group))
            {
                Group = new List<PortEnd>();
                Groups[Key] = Group;
            }

            Group.Add(End);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Group = Pair.Value;
            bool Diamond = Pair.Key.EndsWith("|diamond", StringComparison.Ordinal);
            if (!Diamond) Group.Sort(delegate(PortEnd Left, PortEnd Right)
            {
                int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
                if (HintOrder != 0) return HintOrder;
                bool VerticalSide = Left.Side == "left" || Left.Side == "right";
                if (VerticalSide)
                {
                    return Right.Other.CenterY.CompareTo(Left.Other.CenterY);
                }

                return Left.Other.CenterX.CompareTo(Right.Other.CenterX);
            });

            ApplySlots(Group, Nodes);
        }

        var SideTimer = Stopwatch.StartNew();
        ChooseFreeSidesByRouteCost(Ends, Nodes, FanOutBoundsById, Contract, Copy.RenderContext);
        LayoutStageTimingObserver?.Invoke("allocation/free-side-comparison", SideTimer.Elapsed.TotalMilliseconds);

    }

    /// <summary>Compares free sides before accepting a shared rectangular side, including the displaced neighbouring ports.</summary>
    /// <param name="Ends">All visible ends participating in this joint allocation.</param>
    /// <param name="Nodes">Shape definitions used by outline and normal calculations.</param>
    /// <param name="Bounds">Placed endpoint and obstacle bounds.</param>
    /// <param name="Contract">Declared links, frames and honoured hints.</param>
    /// <param name="Context">The owning layout's search caches.</param>
    private static void ChooseFreeSidesByRouteCost(List<PortEnd> Ends,
        Dictionary<string, NodeContract> Nodes, Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
        DiagramContract Contract, FlowchartRenderContext Context)
    {
        var ByNode = Ends.GroupBy(End => End.Box.Id).ToDictionary(Group => Group.Key, Group => Group.ToList());
        string Obstacles = string.Join(";", Bounds.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
            .Select(Pair => FormattableString.Invariant($"{Pair.Key}:{Pair.Value.CenterX:R},{Pair.Value.CenterY:R},{Pair.Value.Width:R},{Pair.Value.Height:R}")));
        string Geometry = Obstacles + "|" + string.Join(";", Ends.Select(End => $"{End.Route.EdgeId}:{End.IsSource}:{End.Side}:{End.IsHinted}"));
        if (!Context.TerminalCandidateCache.TryGetValue((Contract, Obstacles), out var TerminalCache))
            Context.TerminalCandidateCache[(Contract, Obstacles)] = TerminalCache = new(StringComparer.Ordinal);
        var Cache = Context.PortSideCache;
        if (Cache.TryGetValue((Contract, Geometry), out string[]? Cached))
        {
            var ChangedNodes = new HashSet<string>(StringComparer.Ordinal);
            for (int Index = 0; Index < Ends.Count; Index++)
            {
                if (Ends[Index].Side != Cached[Index]) ChangedNodes.Add(Ends[Index].Box.Id);
                Ends[Index].Side = Cached[Index];
                if (Ends[Index].IsSource) Ends[Index].Route.ExitSide = Cached[Index];
                else Ends[Index].Route.EntrySide = Cached[Index];
            }
            foreach (var NodeEnds in ByNode.Where(Pair => ChangedNodes.Contains(Pair.Key)).Select(Pair => Pair.Value))
            foreach (var SideEnds in NodeEnds.GroupBy(End => End.Side))
                ApplySlots(SideEnds.OrderBy(End => End.Side is "left" or "right"
                    ? -End.Other.CenterY : End.Other.CenterX).ToList(), Nodes);
            FlowchartRenderContext.ReportOperationCount("port-side-assignment-cache-hits", 1);
            return;
        }
        var ByRoute = Ends.GroupBy(End => End.Route).Where(Group => Group.Count() == 2)
            .ToDictionary(Group => Group.Key, Group => Group.OrderByDescending(End => End.IsSource).ToArray());
        var SharedNeighbours = ByRoute.Keys.ToDictionary(Route => Route, _ => new HashSet<FlowchartLayoutEdgeRoute>());
        foreach (var Side in Ends.GroupBy(End => (End.Box.Id, End.Side)))
        foreach (var End in Side)
            SharedNeighbours[End.Route].UnionWith(Side.Select(Other => Other.Route));
        var CohortByRoute = new Dictionary<FlowchartLayoutEdgeRoute, HashSet<FlowchartLayoutEdgeRoute>>();
        foreach (var Route in ByRoute.Keys)
        {
            if (CohortByRoute.ContainsKey(Route)) continue;
            var Cohort = new HashSet<FlowchartLayoutEdgeRoute>();
            var Pending = new Queue<FlowchartLayoutEdgeRoute>(); Pending.Enqueue(Route);
            while (Pending.TryDequeue(out var Next))
                if (Cohort.Add(Next)) foreach (var Other in SharedNeighbours[Next]) Pending.Enqueue(Other);
            foreach (var Member in Cohort) CohortByRoute[Member] = Cohort;
        }
        var PathCache = new Dictionary<FlowchartLayoutEdgeRoute,
            ((double Sx, double Sy, double Tx, double Ty, string Exit, string Entry) Key, List<(double X, double Y)>? Path)>();
        foreach (var Pair in ByRoute)
        {
            var Cohort = CohortByRoute[Pair.Key];
            if (Cohort.Count > FlowchartLayoutRules.RepairChainLines) continue;
            int Examined = 0;
            PortEnd Source = Pair.Value[0], Target = Pair.Value[1];
            if (Source.IsHinted || Target.IsHinted || Source.Box.Id == Target.Box.Id
                || !IsBox(Source) || !IsBox(Target)) continue;
            bool Shared(PortEnd End) => ByNode[End.Box.Id].Count(Other => Other.Side == End.Side) > 1;
            if (!Shared(Source) && !Shared(Target)) continue;
            var Affected = ByNode[Source.Box.Id].Concat(ByNode[Target.Box.Id]).Select(End => End.Route).Distinct().ToArray();
            if (Affected.Length > FlowchartLayoutRules.RepairChainLines) continue;
            double Left = Affected.Min(Route => Route.Points.Min(Point => Point.X));
            double Right = Affected.Max(Route => Route.Points.Max(Point => Point.X));
            double Bottom = Affected.Min(Route => Route.Points.Min(Point => Point.Y));
            double Top = Affected.Max(Route => Route.Points.Max(Point => Point.Y));
            var Nearby = ByRoute.Keys.Where(Route => Route.Points.Max(Point => Point.X) >= Left
                && Route.Points.Min(Point => Point.X) <= Right && Route.Points.Max(Point => Point.Y) >= Bottom
                && Route.Points.Min(Point => Point.Y) <= Top).ToArray();
            // Side selection is a bounded coupled search. Do not route an entire
            // dense catalogue graph provisionally for each local side option.
            if (Nearby.Length > FlowchartLayoutRules.RepairChainLines) continue;
            var UnchangedPaths = Nearby.Except(Affected).Select(Route =>
            {
                var Key = (Route.Points[0].X, Route.Points[0].Y, Route.Points[^1].X, Route.Points[^1].Y, Route.ExitSide, Route.EntrySide);
                if (PathCache.TryGetValue(Route, out var Cached) && Cached.Key == Key) return Cached.Path;
                var Path = TerminalPaths(Route)
                    .OrderBy(Candidate => PortCandidateCost(Candidate)).FirstOrDefault();
                PathCache[Route] = (Key, Path);
                return Path;
            })
                .Where(Path => Path != null).ToArray();
            var Saved = Affected.Select(Route => (Route,
                Start: (Route.Points[0].X, Route.Points[0].Y), End: (Route.Points[^1].X, Route.Points[^1].Y),
                Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)).ToArray();
            string OriginalExit = Source.Side, OriginalEntry = Target.Side;
            string BestExit = OriginalExit, BestEntry = OriginalEntry;
            var Best = Cost();
            Examined++;
            int MaximumCrossings = Best.Crossings;
            // An empty low-bend family is not permission to introduce an
            // aligned-port detour: the obstacle router may still have a legal
            // baseline route outside this provisional family.
            int MaximumAlignedDetours = Best.AlignedDetours == int.MaxValue ? 0 : Best.AlignedDetours;
            // Keep the original facing pair first so an exact tie is stable.
            foreach (string Exit in new[] { OriginalExit, "bottom", "top", "left", "right" }.Distinct())
            foreach (string Entry in new[] { OriginalEntry, "top", "bottom", "left", "right" }.Distinct())
            {
                if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) continue;
                if (Exit == OriginalExit && Entry == OriginalEntry) continue;
                // Sharing can be necessary; compare it with every available side
                // instead of consuming an occupied centre without a comparison.
                if (Exit != OriginalExit && ByNode[Source.Box.Id].Any(End => End != Source && End.Side == Exit)
                    || Entry != OriginalEntry && ByNode[Target.Box.Id].Any(End => End != Target && End.Side == Entry)) continue;
                Restore();
                Source.Side = Exit; Target.Side = Entry;
                Source.Route.ExitSide = Exit; Target.Route.EntrySide = Entry;
                Allocate(Source.Box.Id); Allocate(Target.Box.Id);
                Examined++;
                FlowchartRenderContext.ReportOperationCount("port-side-assignment-candidates", 1);
                var Candidate = Cost();
                if (Candidate.Crossings <= MaximumCrossings && Candidate.AlignedDetours <= MaximumAlignedDetours
                    && Candidate.CompareTo(Best) < 0)
                {
                    Best = Candidate; BestExit = Exit; BestEntry = Entry;
                }
            }
            Restore();
            Source.Side = BestExit; Target.Side = BestEntry;
            Source.Route.ExitSide = BestExit; Target.Route.EntrySide = BestEntry;
            Allocate(Source.Box.Id); Allocate(Target.Box.Id);

            // Only endpoint geometry is committed here. Route construction will
            // build the selected paths after all sides and slots are allocated.
            void Restore()
            {
                Source.Side = OriginalExit; Target.Side = OriginalEntry;
                foreach (var Item in Saved)
                {
                    Item.Route.Points[0] = new() { X = Item.Start.X, Y = Item.Start.Y };
                    Item.Route.Points[^1] = new() { X = Item.End.X, Y = Item.End.Y };
                    Item.Route.ExitSide = Item.ExitSide; Item.Route.EntrySide = Item.EntrySide;
                    Item.Route.ExitNormalX = Item.ExitNormalX; Item.Route.ExitNormalY = Item.ExitNormalY;
                    Item.Route.EntryNormalX = Item.EntryNormalX; Item.Route.EntryNormalY = Item.EntryNormalY;
                }
            }

            void Allocate(string NodeId)
            {
                foreach (var Group in ByNode[NodeId].GroupBy(End => End.Side))
                {
                    var Ordered = Group.OrderBy(End => End.Side is "left" or "right"
                        ? -End.Other.CenterY : End.Other.CenterX).ToList();
                    ApplySlots(Ordered, Nodes);
                }
            }

            (double Overlap, int Bends, int SharedSides, double Length, int Crossings, int AlignedDetours) Cost()
            {
                int Bends = 0, Crossings = 0, AlignedDetours = 0;
                double Length = 0, Overlap = 0;
                var ChosenPaths = new List<List<(double X, double Y)>>();
                foreach (var Route in Affected)
                {
                    if (!Bounds.TryGetValue(Route.SourceId, out var From) || !Bounds.TryGetValue(Route.TargetId, out var To))
                        return (double.PositiveInfinity, int.MaxValue, int.MaxValue, double.PositiveInfinity, int.MaxValue, int.MaxValue);
                    var BestRoute = (Overlap: double.PositiveInfinity, Bends: int.MaxValue, Length: double.PositiveInfinity, Crossings: int.MaxValue);
                    List<(double X, double Y)>? BestPath = null;
                    foreach (var Path in TerminalPaths(Route))
                    {
                        int Turns = 0;
                        double Distance = 0;
                        for (int Index = 1; Index < Path.Count; Index++)
                        {
                            double Dx = Path[Index].X - Path[Index - 1].X, Dy = Path[Index].Y - Path[Index - 1].Y;
                            Distance += Math.Sqrt(Dx * Dx + Dy * Dy);
                            if (Index + 1 < Path.Count && Math.Abs(Dx * (Path[Index + 1].Y - Path[Index].Y)
                                - Dy * (Path[Index + 1].X - Path[Index].X)) > 0.001) Turns++;
                        }
                        double SharedLength = 0; int CrossingCount = 0;
                        foreach (var Other in UnchangedPaths.Concat(ChosenPaths))
                        {
                            var Interaction = FlowchartBranchRouteOrder.Intersections(Path, Other!);
                            SharedLength += Interaction.Overlap; CrossingCount += Interaction.Crossings;
                        }
                        var Candidate = (Overlap: Math.Round(SharedLength, 3), Bends: Turns, Length: Distance, Crossings: CrossingCount);
                        if (Candidate.CompareTo(BestRoute) < 0) { BestRoute = Candidate; BestPath = Path; }
                    }
                    if (BestPath == null) return (double.PositiveInfinity, int.MaxValue, int.MaxValue, double.PositiveInfinity, int.MaxValue, int.MaxValue);
                    if (BestPath.Count > 2 && (Math.Abs(BestPath[0].X - BestPath[^1].X) <= 1
                            || Math.Abs(BestPath[0].Y - BestPath[^1].Y) <= 1)) AlignedDetours++;
                    ChosenPaths.Add(BestPath);
                    Overlap += BestRoute.Overlap; Bends += BestRoute.Bends; Length += BestRoute.Length; Crossings += BestRoute.Crossings;
                }
                int SharedSides = ByNode[Source.Box.Id].Concat(ByNode[Target.Box.Id])
                    .GroupBy(End => (End.Box.Id, End.Side)).Count(Group => Group.Count() > 1);
                return (Overlap, Bends, SharedSides, Length, Crossings, AlignedDetours);
            }
        }

        Cache[(Contract, Geometry)] = Ends.Select(End => End.Side).ToArray();

        // <summary>Reuses the exact legal terminal family when the complete obstacle geometry and allocated ports agree.</summary>
        // <param name="Route">The provisional side and slot assignment to enumerate.</param>
        // <returns>Candidate paths owned by the render cache and treated as immutable.</returns>
        List<List<(double X, double Y)>> TerminalPaths(FlowchartLayoutEdgeRoute Route)
        {
            string Key = FormattableString.Invariant($"{Route.SourceId}:{Route.TargetId}:{Nodes.GetValueOrDefault(Route.SourceId)?.Shape}:{Nodes.GetValueOrDefault(Route.TargetId)?.Shape}:{Route.ExitSide}:{Route.EntrySide}:{Route.Points[0].X:R},{Route.Points[0].Y:R},{Route.Points[^1].X:R},{Route.Points[^1].Y:R}");
            if (TerminalCache.TryGetValue(Key, out var Paths)) return Paths;
            Paths = DiagramGridRouteBuilder.MinimumBendTerminalCandidates(Route.Points[0], Route.Points[^1],
                Bounds[Route.SourceId], Bounds[Route.TargetId], Route.ExitSide, Route.EntrySide, Nodes, Bounds, Contract).ToList();
            TerminalCache[Key] = Paths;
            return Paths;
        }

        bool IsBox(PortEnd End) => End.Box.IsFrame || Nodes.TryGetValue(End.Box.Id, out var Node)
            && IsRectangular(Node.Shape);
    }

    /// <summary>Measures a provisional allocated-port route without rounding away short runs.</summary>
    /// <param name="Path">The complete candidate, including terminal normals.</param>
    /// <returns>Bends followed by travelled length, for deterministic minimum-candidate ordering.</returns>
    private static (int Bends, double Length) PortCandidateCost(IReadOnlyList<(double X, double Y)> Path)
    {
        int Bends = 0; double Length = 0;
        for (int Index = 1; Index < Path.Count; Index++)
        {
            double Dx = Path[Index].X - Path[Index - 1].X, Dy = Path[Index].Y - Path[Index - 1].Y;
            Length += Math.Sqrt(Dx * Dx + Dy * Dy);
            if (Index + 1 < Path.Count && Math.Abs(Dx * (Path[Index + 1].Y - Path[Index].Y)
                    - Dy * (Path[Index + 1].X - Path[Index].X)) > 0.001) Bends++;
        }
        return (Bends, Length);
    }

    internal static EdgeContract? FindContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route)
    {
        EdgeContract? ById = Contract.Edges.FirstOrDefault(Edge =>
            !string.IsNullOrEmpty(Edge.Id) && string.Equals(Edge.Id, Route.EdgeId, StringComparison.Ordinal));
        if (ById != null) return ById;
        return Contract.Edges.Where(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
            .OrderBy(Edge => Edge.CreationIndex).FirstOrDefault();
    }

    private static HashSet<FlowchartLayoutEdgeRoute> RedistributeAllocatedPorts(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = Frames.ToDictionary(Frame => Frame.Id, StringComparer.Ordinal);
        Dictionary<string, List<PortEnd>> Groups = new(StringComparer.Ordinal);
        Dictionary<FlowchartLayoutEdgeRoute, ((double X, double Y) Start, (double X, double Y) End)> OriginalEndpoints = [];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2
                || string.IsNullOrEmpty(Route.ExitSide)
                || string.IsNullOrEmpty(Route.EntrySide)
                || !TryBox(Route.SourceId, ById, FrameById, out BoxRef Source)
                || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            bool IsHinted = FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true;
            if (IsHinted) continue;
            // Hinted routes still occupy their symmetric slot. Excluding them
            // here must be compensated by the final hint allocator, which
            // allocates hinted and automatic endpoints on each affected side as
            // one group. Keep that allocator as the single source of truth.
            OriginalEndpoints[Route] = ((Route.Points[0].X, Route.Points[0].Y),
                (Route.Points[^1].X, Route.Points[^1].Y));
            Add(Source, Target, Route, true, Route.ExitSide);
            Add(Target, Source, Route, false, Route.EntrySide);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Ends = Pair.Value;
            bool Diamond = Pair.Key.EndsWith("|diamond", StringComparison.Ordinal);
            if (!Diamond)
            {
                Ends.Sort((Left, Right) =>
                {
                    int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
                    if (HintOrder != 0) return HintOrder;
                    bool VerticalSide = Left.Side is "left" or "right";
                    return VerticalSide
                        ? Right.Other.CenterY.CompareTo(Left.Other.CenterY)
                        : Left.Other.CenterX.CompareTo(Right.Other.CenterX);
                });
            }
            if (!Diamond)
                ApplySlots(Ends, Nodes);
        }

        HashSet<FlowchartLayoutEdgeRoute> Changed = [];
        foreach (KeyValuePair<FlowchartLayoutEdgeRoute, ((double X, double Y) Start, (double X, double Y) End)> Pair in OriginalEndpoints)
        {
            FlowchartLayoutEdgeRoute Route = Pair.Key;
            if (Pair.Value.Start != (Route.Points[0].X, Route.Points[0].Y)
                || Pair.Value.End != (Route.Points[^1].X, Route.Points[^1].Y))
            {
                Changed.Add(Route);
            }
        }
        return Changed;

        void Add(BoxRef Box, BoxRef Other, FlowchartLayoutEdgeRoute Route,
            bool IsSource, string Side)
        {
            bool Diamond = !Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? Node)
                && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            string Key = Box.Id + (Diamond ? "|diamond" : "|" + Side);
            if (!Groups.TryGetValue(Key, out List<PortEnd>? Ends))
                Groups[Key] = Ends = [];
            Ends.Add(new PortEnd
            {
                Route = Route,
                IsSource = IsSource,
                IsHinted = false,
                Side = Side,
                Box = Box,
                Other = Other
            });
        }
    }

    internal static string HintSide(LayoutHintDirection Direction) => Direction switch
    {
        LayoutHintDirection.North => "top",
        LayoutHintDirection.South => "bottom",
        LayoutHintDirection.East => "right",
        _ => "left"
    };

    private static void ApplySlots(List<PortEnd> Group, Dictionary<string, NodeContract> Nodes)
    {
        if (Group.Count == 0)
        {
            return;
        }

        BoxRef Box = Group[0].Box;
        string Side = Group[0].Side;
        bool Diamond = Nodes.TryGetValue(Box.Id, out NodeContract? Node) && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
        if (Diamond && !Box.IsFrame)
        {
            List<(double X, double Y)> Destinations = new List<(double X, double Y)>();
            foreach (PortEnd End in Group)
            {
                Destinations.Add((End.Other.CenterX - Box.CenterX, End.Other.CenterY - Box.CenterY));
            }

            if (FlowchartDiamondPorts.TryAssign((int)Box.Width, (int)Box.Height, Destinations, out IReadOnlyList<FlowchartDiamondPort> Ports)
                && Ports.Count == Group.Count)
            {
                for (int Index = 0; Index < Group.Count; Index++)
                {
                    FlowchartDiamondPort Port = Ports[Index];
                    double X = Box.CenterX + Port.OffsetX;
                    double Y = Box.CenterY + Port.OffsetY;
                    SetEnd(Group[Index], X, Y, Port.DepartureX, Port.DepartureY);
                }

                return;
            }
        }

        bool VerticalSide = Side == "left" || Side == "right";
        int Length = (int)Math.Round(VerticalSide ? Box.Height : Box.Width);
        bool Circle = !Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? CircleNode)
            && IsCircleShape(CircleNode.Shape);
        if (Circle)
        {
            double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
            for (int Index = 0; Index < Group.Count; Index++)
            {
                double Fraction = (Index + 1.0) / (Group.Count + 1.0);
                double Angle = Side switch
                {
                    "top" => Math.PI * (1.0 - Fraction),
                    "bottom" => Math.PI * (1.0 + Fraction),
                    "left" => (Math.PI / 2.0) + (Math.PI * Fraction),
                    _ => (Math.PI / 2.0) - (Math.PI * Fraction)
                };
                double NormalX = Math.Cos(Angle), NormalY = Math.Sin(Angle);
                double X = Box.CenterX + (Radius * NormalX);
                double Y = Box.CenterY + (Radius * NormalY);
                SetEnd(Group[Index], X, Y, NormalX, NormalY);
            }

            return;
        }

        // Rounded rectangles still allocate over the complete declared edge.
        // Reserving a corner inset here moves ports away from k/(n+1) and makes
        // the slot pattern depend on the renderer's corner radius.
        double AvailableLength = Length;
        for (int Index = 0; Index < Group.Count; Index++)
        {
            double Along = ((Index + 1.0) / (Group.Count + 1.0)) * AvailableLength;
            double X;
            double Y;
            int OutX = 0;
            int OutY = 0;
            if (Side == "top")
            {
                X = Box.Left + Along;
                Y = Box.Top;
                OutY = 1;
            }
            else if (Side == "bottom")
            {
                X = Box.Left + Along;
                Y = Box.Bottom;
                OutY = -1;
            }
            else if (Side == "left")
            {
                X = Box.Left;
                Y = Box.Top - Along;
                OutX = -1;
            }
            else
            {
                X = Box.Right;
                Y = Box.Top - Along;
                OutX = 1;
            }

            bool HorizontalBracketEdge = Side is "top" or "bottom"
                && Nodes.TryGetValue(Box.Id, out NodeContract? BracketNode)
                && FlowchartTrapezoidBracketPathAnalyser.IsTrapezoidBracketShape(BracketNode.Shape);
            if (!Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? ShapeNode)
                && !IsRectangular(ShapeNode.Shape) && !Diamond && !HorizontalBracketEdge)
            {
                (X, Y) = ProjectToOutline(Box, ShapeNode.Shape,
                    (int)Math.Round(X), (int)Math.Round(Y), OutX, OutY);
            }

            SetEnd(Group[Index], X, Y, OutX, OutY);
        }
    }

    private static (int X, int Y) ProjectToOutline(BoxRef Box, string? Shape, int SlotX, int SlotY, int OutX, int OutY)
    {
        if (string.Equals(Shape, "ellipse", StringComparison.OrdinalIgnoreCase))
        {
            double Rx = Box.Width / 2.0, Ry = Box.Height / 2.0;
            if (OutX != 0)
            {
                double Dy = (SlotY - Box.CenterY) / Ry;
                double Offset = Rx * Math.Sqrt(Math.Max(0, 1 - Dy * Dy));
                return ((int)Math.Round(Box.CenterX + OutX * Offset), SlotY);
            }
            double Dx = (SlotX - Box.CenterX) / Rx;
            double VerticalOffset = Ry * Math.Sqrt(Math.Max(0, 1 - Dx * Dx));
            return (SlotX, (int)Math.Round(Box.CenterY + OutY * VerticalOffset));
        }

        if (string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase))
        {
            double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
            double Dx = SlotX - Box.CenterX;
            double Dy = SlotY - Box.CenterY;
            if (OutX != 0)
            {
                double Inside = (Radius * Radius) - (Dy * Dy);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int X = (int)Math.Round(Box.CenterX + (OutX > 0 ? Offset : -Offset));
                    return (X, SlotY);
                }
            }

            if (OutY != 0)
            {
                double Inside = (Radius * Radius) - (Dx * Dx);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int Y = (int)Math.Round(Box.CenterY + (OutY > 0 ? Offset : -Offset));
                    return (SlotX, Y);
                }
            }
        }

        FlowchartLayoutNodeBounds Bounds = new FlowchartLayoutNodeBounds
        {
            NodeId = Box.Id,
            CenterX = Box.CenterX,
            CenterY = Box.CenterY,
            Width = Box.Width,
            Height = Box.Height
        };
        (double AttachX, double AttachY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            Shape,
            SlotX + (OutX * 4.0),
            SlotY + (OutY * 4.0),
            EdgeEndpointMarker.None);
        return ((int)Math.Round(AttachX), (int)Math.Round(AttachY));
    }

    private static bool IsRectangular(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rectangle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "round", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase)
            // Sequence-diagram participants render as rectangular lifelines' heads.
            // Treating this non-flowchart shape name as a generic ray-clipped shape
            // pulled its edge ports toward the centre and broke k/(n+1) allocation.
            || string.Equals(Shape, "participant", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCircleShape(string? Shape) =>
        string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase);

    private static void SetEnd(PortEnd End, double X, double Y, double OutX, double OutY)
    {
        End.OutX = OutX;
        End.OutY = OutY;
        if (End.IsSource)
        {
            End.Route.ExitNormalX = OutX;
            End.Route.ExitNormalY = OutY;
        }
        else
        {
            End.Route.EntryNormalX = OutX;
            End.Route.EntryNormalY = OutY;
        }
        FlowchartLayoutPoint Point = End.IsSource ? End.Route.Points[0] : End.Route.Points[End.Route.Points.Count - 1];
        Point.X = X;
        Point.Y = Y;
        End.Route.Points[End.IsSource ? 0 : End.Route.Points.Count - 1] = new FlowchartLayoutPoint
        {
            X = X,
            Y = Y
        };
    }

    private static void EnsureStub(FlowchartLayoutEdgeRoute Route, bool AtStart)
    {
        if (Route.Points.Count < 2)
        {
            return;
        }

        int EndIndex = AtStart ? 0 : Route.Points.Count - 1;
        int NextIndex = AtStart ? 1 : Route.Points.Count - 2;
        FlowchartLayoutPoint End = Route.Points[EndIndex];
        FlowchartLayoutPoint Next = Route.Points[NextIndex];
        double Dx = Next.X - End.X;
        double Dy = Next.Y - End.Y;
        bool Perpendicular = Math.Abs(Dx) <= 0.5 || Math.Abs(Dy) <= 0.5;
        if (Perpendicular && ((Dx * Dx) + (Dy * Dy)) >= (STUB_PX * STUB_PX))
        {
            return;
        }

        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        double DirX = Length < 1e-6 ? 0.0 : Dx / Length;
        double DirY = Length < 1e-6 ? 1.0 : Dy / Length;
        if (Math.Abs(DirX) >= Math.Abs(DirY))
        {
            DirY = 0.0;
            DirX = DirX >= 0 ? 1.0 : -1.0;
        }
        else
        {
            DirX = 0.0;
            DirY = DirY >= 0 ? 1.0 : -1.0;
        }

        FlowchartLayoutPoint Stub = new FlowchartLayoutPoint
        {
            X = Math.Round(End.X + (DirX * STUB_PX)),
            Y = Math.Round(End.Y + (DirY * STUB_PX))
        };
        if (AtStart)
        {
            Route.Points.Insert(1, Stub);
        }
        else
        {
            Route.Points.Insert(Route.Points.Count - 1, Stub);
        }
    }

}
