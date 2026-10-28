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
{
    /// <summary>Observes side inventories at allocation boundaries without changing production routing.</summary>
    internal static Action<string>? CorridorPortOrderObserver;
    /// <summary>Allocates the complete visible port inventory using the existing deterministic joint policy.</summary>
    /// <param name="Contract">Original visible links, groups and routing hints.</param>
    /// <param name="Copy">Model whose allocated terminals are updated.</param>
    /// <param name="Placed">Current authoritative node projections.</param>
    /// <param name="Frames">Current fitted frame projections.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <param name="Direction">Diagram flow direction.</param>
    /// <param name="SharedFanSources">Previously classified shared roots.</param>
    private static void AssignPortSides(DiagramContract Contract, FlowchartLayoutResult Copy, List<GridNode> Placed,
        List<GridFrame> Frames, Dictionary<string, NodeContract> Nodes, FlowchartDirection Direction,
        HashSet<string> SharedFanSources) => AssignPortSidesScoped(Contract, Copy, Placed, Frames, Nodes, Direction, SharedFanSources, null);

    /// <summary>Reconsiders affected sides while retaining other sides in the joint slot inventory.</summary>
    /// <param name="Contract">Original visible links, groups and routing hints.</param>
    /// <param name="Copy">Model whose allocated terminals are updated.</param>
    /// <param name="Placed">Current authoritative node projections.</param>
    /// <param name="Frames">Current fitted frame projections.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <param name="Direction">Diagram flow direction.</param>
    /// <param name="SharedFanSources">Previously classified shared roots.</param>
    /// <param name="OnlyRoutes">Affected lines, or null for the unchanged complete allocation policy.</param>
    private static void AssignPortSidesScoped(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        HashSet<string> SharedFanSources, IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes)
    {
        IReadOnlySet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
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
        List<PortEnd> Ends = [];
        List<(FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target)> Pending = [];
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!TryBox(Route.SourceId, ById, FrameById, out BoxRef Source) || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            if (OnlyRoutes != null && !OnlyRoutes.Contains(Route))
            {
                Ends.Add(new PortEnd { Route = Route, IsSource = true, Side = Route.ExitSide, Box = Source, Other = Target });
                Ends.Add(new PortEnd { Route = Route, IsSource = false, Side = Route.EntrySide, Box = Target, Other = Source });
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
                && ((Nodes.TryGetValue(Route.SourceId, out NodeContract? CircularSource) && CircularSource.Shape == "circle")
                    || (Nodes.TryGetValue(Route.TargetId, out NodeContract? CircularTarget) && CircularTarget.Shape == "circle"));
            string StraightExit = string.Empty, StraightEntry = string.Empty;
            bool HasStraightCandidate = RoutingHint == null && !CircularReturn
                && (ContractEdge == null || !BackEdges.Contains(ContractEdge))
                && TryAssignStraightPortSides(Source, Target, Direction,
                    out StraightExit, out StraightEntry)
                && SupportsNextGeometrySide(Source, StraightExit, Ends, Nodes)
                && SupportsNextGeometrySide(Target, StraightEntry, Ends, Nodes);
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
            else if (ContractEdge != null && BackEdges.Contains(ContractEdge)
                && Item.Source.Id != Item.Target.Id)
            {
                string ReturnSide = ReturnTrackSide(Item.Source, Item.Target, Horizontal, Placed);
                string Downstream = Direction switch
                {
                    FlowchartDirection.BottomToTop => "top",
                    FlowchartDirection.LeftToRight => "right",
                    FlowchartDirection.RightToLeft => "left",
                    _ => "bottom"
                };
                bool DownstreamUsed = Contract.Edges.Any(Other => !ReferenceEquals(Other, ContractEdge)
                    && Other.SourceId == Item.Source.Id && Other.Stroke != "invisible"
                    && TryBox(Other.TargetId, ById, FrameById, out BoxRef OtherTarget)
                    && FacingSide(Item.Source, OtherTarget) == Downstream);
                Exit = DownstreamUsed ? ReturnSide : Downstream;
                Entry = ReturnSide;
            }
            else if (Horizontal && ById.TryGetValue(Item.Source.Id, out GridNode? WrapSource)
                && ById.TryGetValue(Item.Target.Id, out GridNode? WrapTarget)
                && WrapTarget.Rank > WrapSource.Rank && WrapTarget.Y != WrapSource.Y
                && (WrapTarget.X - WrapSource.X) * (Direction == FlowchartDirection.RightToLeft ? -1 : 1) < 0
                && Contract.Edges.Count(Edge => Edge.SourceId == Item.Source.Id && Edge.Stroke != "invisible") == 1
                && Contract.Edges.Count(Edge => Edge.TargetId == Item.Target.Id && Edge.Stroke != "invisible") == 1)
            {
                Exit = WrapTarget.Y < WrapSource.Y ? "bottom" : "top";
                Entry = WrapTarget.Y < WrapSource.Y ? "top" : "bottom";
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
            if (RoutingHint == null)
            {
                Exit = EligibleGeometrySide(Item.Source, Item.Target, Exit, Ends, Nodes);
                Entry = EligibleGeometrySide(Item.Target, Item.Source, Entry, Ends, Nodes);
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
                Group = [];
                Groups[Key] = Group;
            }

            Group.Add(End);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Group = Pair.Value;
            bool Diamond = Pair.Key.EndsWith("|diamond", StringComparison.Ordinal);
            bool CorridorNesting = NeedsCorridorPortNesting(Group, Copy.RenderContext);
            if (!Diamond)
            {
                Group.Sort(delegate(PortEnd Left, PortEnd Right)
            {
                if (CorridorNesting)
                {
                    return CompareCorridorPortEnds(Left, Right);
                }

                int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
                if (HintOrder != 0)
                {
                    return HintOrder;
                }

                bool VerticalSide = Left.Side == "left" || Left.Side == "right";
                int PeerOrder = VerticalSide ? Right.Other.CenterY.CompareTo(Left.Other.CenterY)
                    : Left.Other.CenterX.CompareTo(Right.Other.CenterX);
                if (PeerOrder == 0 && CorridorSlotIsOnFlatSide(Left) && CorridorSlotIsOnFlatSide(Right)
                    && FlowchartFallbackCorridorTracks.HasReservedCapacity(Copy.RenderContext))
                {
                    return CompareExistingCorridorSlots(Left, Right);
                }

                if (VerticalSide)
                {
                    return Right.Other.CenterY.CompareTo(Left.Other.CenterY);
                }

                return Left.Other.CenterX.CompareTo(Right.Other.CenterX);
            });
            }

            ApplySlots(Group, Nodes, Contract, Direction);
            CorridorPortOrderObserver?.Invoke("initial|" + Copy.RenderContext.ActiveLayoutStage + "|" + Pair.Key + "|nested=" + CorridorNesting + "|"
                + string.Join(";", Group.Select(End => End.Route.EdgeId + ":" + (End.IsSource ? End.Route.Points[0].Y : End.Route.Points[^1].Y))));
        }

        Stopwatch SideTimer = Stopwatch.StartNew();
        ChooseFreeSidesByRouteCost(Ends, Nodes, FanOutBoundsById, Contract, Direction, Copy.RenderContext);
        LayoutStageTimingObserver?.Invoke("allocation/free-side-comparison", SideTimer.Elapsed.TotalMilliseconds);

    }

    /// <summary>Keeps equal-rank fan leaves nested when corridor growth changes route enumeration order.</summary>
    /// <param name="Left">First side endpoint.</param>
    /// <param name="Right">Second endpoint on the same side.</param>
    /// <returns>Ordinary hinted and cross-axis order, then the far-leaf outer-slot order for equal cross coordinates.</returns>
    private static int CompareCorridorPortEnds(PortEnd Left, PortEnd Right)
    {
        int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
        if (HintOrder != 0)
        {
            return HintOrder;
        }

        bool Vertical = Left.Side is "left" or "right";
        int CrossOrder = Vertical ? Right.Other.CenterY.CompareTo(Left.Other.CenterY)
            : Left.Other.CenterX.CompareTo(Right.Other.CenterX);
        if (CrossOrder != 0)
        {
            return CrossOrder;
        }

        double FirstDistance = Vertical ? Math.Abs(Left.Other.CenterX - Left.Box.CenterX) : Math.Abs(Left.Other.CenterY - Left.Box.CenterY);
        double SecondDistance = Vertical ? Math.Abs(Right.Other.CenterX - Right.Box.CenterX) : Math.Abs(Right.Other.CenterY - Right.Box.CenterY);
        bool FarFirst = Vertical ? Left.Other.CenterY < Left.Box.CenterY : Left.Other.CenterX > Left.Box.CenterX;
        return FarFirst ? SecondDistance.CompareTo(FirstDistance) : FirstDistance.CompareTo(SecondDistance);
    }

    /// <summary>Identifies crossed or inverted off-axis equal-rank fan arms before changing an otherwise valid side order.</summary>
    /// <param name="Ends">One node side's complete endpoint inventory.</param>
    /// <param name="Context">Current construction boundary and reserved gaps.</param>
    /// <returns>True only for a crossed or geometrically inverted flat-side fan; aligned straight terminals and honoured hints keep their order.</returns>
    private static bool NeedsCorridorPortNesting(IReadOnlyList<PortEnd> Ends, FlowchartRenderContext Context)
    {
        CorridorPortOrderObserver?.Invoke("check|" + Context.ActiveLayoutStage + "|has=" + FlowchartFallbackCorridorTracks.HasReservedCapacity(Context)
            + "|" + string.Join(";", Ends.Select(End => End.Box.Id + "/" + End.Side + "/" + End.Route.EdgeId + "/src=" + End.IsSource
                + "/hint=" + End.IsHinted + "/peer=" + End.Other.CenterX + "," + End.Other.CenterY
                + "/remote=" + (End.IsSource ? End.Route.EntrySide : End.Route.ExitSide))));
        if (!FlowchartFallbackCorridorTracks.IsConstructionStage(Context) || !Ends.All(CorridorSlotIsOnFlatSide))
        {
            return false;
        }

        for (int I = 0; I < Ends.Count; I++)
        {
            for (int J = I + 1; J < Ends.Count; J++)
        {
                PortEnd A = Ends[I]; PortEnd B = Ends[J];
            if (A.IsHinted || B.IsHinted || A.IsSource != B.IsSource)
                {
                    continue;
                }

                bool VerticalSide = A.Side is "left" or "right";
            if (VerticalSide ? A.Other.CenterY != B.Other.CenterY : A.Other.CenterX != B.Other.CenterX)
                {
                    continue;
                }
                // Final facing-side choice can replace a provisional left/right
                // entry with top/bottom after slot allocation. Off-axis equal-rank
                // leaves already define the nesting order; aligned straight peers
                // do not, and keep their settled centre-port allocation.
                if (VerticalSide ? A.Other.CenterY == A.Box.CenterY : A.Other.CenterX == A.Box.CenterX)
                {
                    continue;
                }

                int RequiredOrder = CompareCorridorPortEnds(A, B);
            int CurrentOrder = CompareExistingCorridorSlots(A, B);
            // A port replay can invert the terminal slots before its paths are
            // rebuilt. The inversion is already geometric evidence: normal
            // perpendicular arms to equal-rank leaves would necessarily cross.
            if (RequiredOrder != 0 && CurrentOrder != 0 && Math.Sign(RequiredOrder) != Math.Sign(CurrentOrder))
                {
                    return true;
                }

                if (FlowchartBranchRouteOrder.Intersections(
                    [.. A.Route.Points.Select(Point => (Point.X, Point.Y))],
                    [.. B.Route.Points.Select(Point => (Point.X, Point.Y))]).Crossings > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Preserves a settled equal-rank allocation instead of letting a later sort undo a validated nested fan.</summary>
    /// <param name="Left">First endpoint on the node side.</param>
    /// <param name="Right">Second endpoint on that side.</param>
    /// <returns>Current along-side slot order; unallocated coincident anchors retain the original tie.</returns>
    private static int CompareExistingCorridorSlots(PortEnd Left, PortEnd Right)
    {
        FlowchartLayoutPoint A = Left.IsSource ? Left.Route.Points[0] : Left.Route.Points[^1];
        FlowchartLayoutPoint B = Right.IsSource ? Right.Route.Points[0] : Right.Route.Points[^1];
        return Left.Side is "left" or "right" ? B.Y.CompareTo(A.Y) : A.X.CompareTo(B.X);
    }

    /// <summary>Limits ordinary corridor ordering to allocated flat faces, leaving curved and sloped connection inventories on their existing geometry policy.</summary>
    /// <param name="End">Allocated terminal and its actual envelope.</param>
    /// <returns>Whether the terminal lies on the named axis-aligned face.</returns>
    private static bool CorridorSlotIsOnFlatSide(PortEnd End)
    {
        FlowchartLayoutPoint Point = End.IsSource ? End.Route.Points[0] : End.Route.Points[^1];
        double Difference = End.Side switch { "left" => Point.X - End.Box.Left, "right" => Point.X - End.Box.Right,
            "top" => Point.Y - End.Box.Top, _ => Point.Y - End.Box.Bottom };
        return Math.Abs(Difference) < 0.001;
    }

    /// <summary>Retains the preferred side when its renderer-owned boundary has capacity, otherwise compares eligible sides.</summary>
    /// <param name="Box">Node or frame receiving a port.</param>
    /// <param name="Other">The neighbour used to compare terminal distance.</param>
    /// <param name="Preferred">The ordinary geometry or return-channel side.</param>
    /// <param name="Ends">Already allocated connection ends.</param>
    /// <param name="Nodes">Renderer geometry definitions.</param>
    /// <returns>An eligible side, or the original side when no legal allocation is available.</returns>
    private static string EligibleGeometrySide(BoxRef Box, BoxRef Other, string Preferred,
        List<PortEnd> Ends, Dictionary<string, NodeContract> Nodes)
    {
        Nodes.TryGetValue(Box.Id, out NodeContract? Node);
        int Count(string Side) => Ends.Count(End => End.Box.Id == Box.Id && End.Side == Side) + 1;
        if (SupportsGeometrySide(Box, Preferred, Count(Preferred), Nodes))
        {
            return Preferred;
        }

        NodeGeometry? Geometry = Node?.GeometryProvider?.Create(Box.Width, Box.Height);
        return new[] { "top", "bottom", "left", "right" }
            .Where(Side => SupportsGeometrySide(Box, Side, Count(Side), Nodes))
            .OrderBy(Side =>
            {
                double X = Side == "left" ? Box.Left : Side == "right" ? Box.Right : Box.CenterX;
                double Y = Side == "top" ? Box.Top : Side == "bottom" ? Box.Bottom : Box.CenterY;
                if (Geometry != null && Geometry.ConnectionBoundary.TryPort(Side, 0.5, out NodeGeometryPort Port))
                { X = Box.CenterX + Port.X; Y = Box.CenterY - Port.Y; }
                return Math.Abs(X - Other.CenterX) + Math.Abs(Y - Other.CenterY);
            }).FirstOrDefault() ?? Preferred;
    }

    /// <summary>Checks an additional renderer-owned port without scanning ordinary nodes' accumulated ends.</summary>
    /// <param name="Box">Endpoint being allocated.</param>
    /// <param name="Side">Proposed compass side.</param>
    /// <param name="Ends">Connection ends already assigned in this pass.</param>
    /// <param name="Nodes">Renderer-owned geometry definitions.</param>
    /// <returns>Whether this side can accept the next allocated connection end.</returns>
    private static bool SupportsNextGeometrySide(BoxRef Box, string Side, List<PortEnd> Ends, Dictionary<string, NodeContract> Nodes) =>
        SupportsGeometrySide(Box, Side, Ends.Count(End => End.Box.Id == Box.Id && End.Side == Side) + 1, Nodes);

    /// <summary>Checks the actual connection boundary rather than assuming every visual rectangle edge receives ports.</summary>
    /// <param name="Box">Measured endpoint bounds.</param>
    /// <param name="Side">Requested compass side.</param>
    /// <param name="Count">Number of jointly allocated ports.</param>
    /// <param name="Nodes">Renderer-owned definitions.</param>
    /// <returns>Whether every allocated position belongs to an eligible connection segment.</returns>
    private static bool SupportsGeometrySide(BoxRef Box, string Side, int Count, Dictionary<string, NodeContract> Nodes)
    {
        Nodes.TryGetValue(Box.Id, out NodeContract? Node);
        if (Node != null && (FlowchartDagrePresentation.IsDiamondShape(Node.Shape) || IsCircleShape(Node.Shape)))
        {
            return true;
        }

        double Length = Side is "left" or "right" ? Box.Height : Box.Width;
        INodeConnectionBoundary? Boundary = Node?.GeometryProvider?.Create(Box.Width, Box.Height).ConnectionBoundary;
        // A rectangular side can take a line only when that line lands on the fixed centre-out grid (rule 3.2).
        return ConnectionPointGrid.TryCentreOutFractions(Boundary, Side, Length, Count, out _);
    }

    /// <summary>Chooses the outside return channel from source position and occupied branch space.</summary>
    /// <param name="Source">The later node.</param>
    /// <param name="Target">The earlier node.</param>
    /// <param name="Horizontal">Whether the main flow is horizontal.</param>
    /// <param name="Placed">Measured node positions used to compare free sides.</param>
    /// <returns>A side perpendicular to the main flow, defaulting to page-right in vertical flow.</returns>
    private static string ReturnTrackSide(BoxRef Source, BoxRef Target, bool Horizontal, IReadOnlyList<GridNode> Placed)
    {
        double SourceCross = Horizontal ? Source.CenterY : Source.CenterX;
        double TargetCross = Horizontal ? Target.CenterY : Target.CenterX;
        string Positive = Horizontal ? "top" : "right", Negative = Horizontal ? "bottom" : "left";
        if (SourceCross > TargetCross)
        {
            return Positive;
        }

        if (SourceCross < TargetCross)
        {
            return Negative;
        }

        int PositiveNodes = Placed.Count(Node => (Horizontal ? Node.Y : Node.X) > SourceCross);
        int NegativeNodes = Placed.Count(Node => (Horizontal ? Node.Y : Node.X) < SourceCross);
        if (PositiveNodes != NegativeNodes)
        {
            return PositiveNodes < NegativeNodes ? Positive : Negative;
        }

        return Horizontal ? Negative : Positive;
    }

    /// <summary>Compares free sides before accepting a shared rectangular side, including the displaced neighbouring ports.</summary>
    /// <param name="Ends">All visible ends participating in this joint allocation.</param>
    /// <param name="Nodes">Shape definitions used by outline and normal calculations.</param>
    /// <param name="Bounds">Placed endpoint and obstacle bounds.</param>
    /// <param name="Contract">Declared links, frames and honoured hints.</param>
    /// <param name="Direction"></param>
    /// <param name="Context">The owning layout's search caches.</param>
    private static void ChooseFreeSidesByRouteCost(List<PortEnd> Ends,
        Dictionary<string, NodeContract> Nodes, Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
        DiagramContract Contract, FlowchartDirection Direction, FlowchartRenderContext Context)
    {
        Dictionary<string, List<PortEnd>> ByNode = Ends.GroupBy(End => End.Box.Id).ToDictionary(Group => Group.Key, Group => Group.ToList());
        string Obstacles = string.Join(";", Bounds.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
            .Select(Pair => FormattableString.Invariant($"{Pair.Key}:{Pair.Value.CenterX:R},{Pair.Value.CenterY:R},{Pair.Value.Width:R},{Pair.Value.Height:R}")));
        string Geometry = Obstacles + "|" + string.Join(";", Ends.Select(End => $"{End.Route.EdgeId}:{End.IsSource}:{End.Side}:{End.IsHinted}"));
        if (!Context.TerminalCandidateCache.TryGetValue((Contract, Obstacles), out Dictionary<string, List<List<(double X, double Y)>>>? TerminalCache))
        {
            Context.TerminalCandidateCache[(Contract, Obstacles)] = TerminalCache = new(StringComparer.Ordinal);
        }

        Dictionary<(DiagramContract Contract, string Geometry), string[]> Cache = Context.PortSideCache;
        if (Cache.TryGetValue((Contract, Geometry), out string[]? Cached))
        {
            HashSet<string> ChangedNodes = new HashSet<string>(StringComparer.Ordinal);
            for (int Index = 0; Index < Ends.Count; Index++)
            {
                if (Ends[Index].Side != Cached[Index])
                {
                    ChangedNodes.Add(Ends[Index].Box.Id);
                }

                Ends[Index].Side = Cached[Index];
                if (Ends[Index].IsSource)
                {
                    Ends[Index].Route.ExitSide = Cached[Index];
                }
                else
                {
                    Ends[Index].Route.EntrySide = Cached[Index];
                }
            }
            foreach (List<PortEnd>? NodeEnds in ByNode.Where(Pair => ChangedNodes.Contains(Pair.Key)).Select(Pair => Pair.Value))
            {
                foreach (IGrouping<string, PortEnd> SideEnds in NodeEnds.GroupBy(End => End.Side))
                {
                    ApplySlots([.. SideEnds.OrderBy(End => End.Side is "left" or "right"
                    ? -End.Other.CenterY : End.Other.CenterX)], Nodes, Contract, Direction, UseCompassOverrides: false);
                }
            }

            FlowchartRenderContext.ReportOperationCount("port-side-assignment-cache-hits", 1);
            return;
        }
        Dictionary<FlowchartLayoutEdgeRoute, PortEnd[]> ByRoute = Ends.GroupBy(End => End.Route).Where(Group => Group.Count() == 2)
            .ToDictionary(Group => Group.Key, Group => Group.OrderByDescending(End => End.IsSource).ToArray());
        Dictionary<FlowchartLayoutEdgeRoute, HashSet<FlowchartLayoutEdgeRoute>> SharedNeighbours = ByRoute.Keys.ToDictionary(Route => Route, _ => new HashSet<FlowchartLayoutEdgeRoute>());
        foreach (IGrouping<(string Id, string Side), PortEnd> Side in Ends.GroupBy(End => (End.Box.Id, End.Side)))
        {
            foreach (PortEnd? End in Side)
            {
                SharedNeighbours[End.Route].UnionWith(Side.Select(Other => Other.Route));
            }
        }

        Dictionary<FlowchartLayoutEdgeRoute, HashSet<FlowchartLayoutEdgeRoute>> CohortByRoute = [];
        foreach (FlowchartLayoutEdgeRoute Route in ByRoute.Keys)
        {
            if (CohortByRoute.ContainsKey(Route))
            {
                continue;
            }

            HashSet<FlowchartLayoutEdgeRoute> Cohort = [];
            Queue<FlowchartLayoutEdgeRoute> Pending = new Queue<FlowchartLayoutEdgeRoute>(); Pending.Enqueue(Route);
            while (Pending.TryDequeue(out FlowchartLayoutEdgeRoute? Next))
            {
                if (Cohort.Add(Next))
                {
                    foreach (FlowchartLayoutEdgeRoute? Other in SharedNeighbours[Next])
                    {
                        Pending.Enqueue(Other);
                    }
                }
            }

            foreach (FlowchartLayoutEdgeRoute Member in Cohort)
            {
                CohortByRoute[Member] = Cohort;
            }
        }
        Dictionary<FlowchartLayoutEdgeRoute, ((double Sx, double Sy, double Tx, double Ty, string Exit, string Entry) Key, List<(double X, double Y)>? Path)> PathCache = [];
        // Terminal families are immutable for this obstacle geometry. The same
        // complete path participates in many side options, so measure its cost
        // and each ordered path pair once rather than per incident link option.
        Dictionary<List<(double X, double Y)>, (int Bends, double Length)> PathCosts = [];
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> TerminalObstacles = DiagramGridRouteBuilder.PrepareTerminalObstacles(Contract, Bounds);
        Dictionary<(List<(double X, double Y)> A, List<(double X, double Y)> B), (int Crossings, double Overlap)> Interactions = [];
        foreach (KeyValuePair<FlowchartLayoutEdgeRoute, PortEnd[]> Pair in ByRoute)
        {
            HashSet<FlowchartLayoutEdgeRoute> Cohort = CohortByRoute[Pair.Key];
            if (Cohort.Count > FlowchartLayoutRules.RepairChainLines)
            {
                continue;
            }

            int Examined = 0;
            PortEnd Source = Pair.Value[0], Target = Pair.Value[1];
            if (Source.IsHinted || Target.IsHinted || Source.Box.Id == Target.Box.Id
                || !IsBox(Source) || !IsBox(Target))
            {
                continue;
            }

            bool Shared(PortEnd End) => ByNode[End.Box.Id].Count(Other => Other.Side == End.Side) > 1;
            if (!Shared(Source) && !Shared(Target))
            {
                continue;
            }

            FlowchartLayoutEdgeRoute[] Affected = [.. ByNode[Source.Box.Id].Concat(ByNode[Target.Box.Id])
                .Select(End => End.Route).Distinct()];
            if (Affected.Length > FlowchartLayoutRules.RepairChainLines)
            {
                continue;
            }

            double Left = Affected.Min<FlowchartLayoutEdgeRoute, double>((FlowchartLayoutEdgeRoute Route) => Route.Points.Min((FlowchartLayoutPoint Point) => Point.X));
            double Right = Affected.Max<FlowchartLayoutEdgeRoute, double>((FlowchartLayoutEdgeRoute Route) => Route.Points.Max((FlowchartLayoutPoint Point) => Point.X));
            double Bottom = Affected.Min<FlowchartLayoutEdgeRoute, double>((FlowchartLayoutEdgeRoute Route) => Route.Points.Min((FlowchartLayoutPoint Point) => Point.Y));
            double Top = Affected.Max<FlowchartLayoutEdgeRoute, double>((FlowchartLayoutEdgeRoute Route) => Route.Points.Max((FlowchartLayoutPoint Point) => Point.Y));
            FlowchartLayoutEdgeRoute[] Nearby = [.. ByRoute.Keys.Where(Route => Route.Points.Max(Point => Point.X) >= Left
                && Route.Points.Min(Point => Point.X) <= Right
                && Route.Points.Max(Point => Point.Y) >= Bottom
                && Route.Points.Min(Point => Point.Y) <= Top)];
            // Side selection is a bounded coupled search. Do not route an entire
            // dense catalogue graph provisionally for each local side option.
            if (Nearby.Length > FlowchartLayoutRules.RepairChainLines)
            {
                continue;
            }

            List<(double X, double Y)>?[] UnchangedPaths = [.. Nearby.Except(Affected).Select(Route => {
                (double, double, double, double, string ExitSide, string EntrySide) Key = (Route.Points[0].X, Route.Points[0].Y, Route.Points[^1].X, Route.Points[^1].Y, Route.ExitSide, Route.EntrySide);
                if (PathCache.TryGetValue(Route, out ((double Sx, double Sy, double Tx, double Ty, string Exit, string Entry) Key, List<(double X, double Y)>? Path) Cached) && Cached.Key == Key) { return Cached.Path; } List<(double X, double Y)>? Path = TerminalPaths(Route)
                    .OrderBy(Candidate => PortCandidateCost(Candidate)).FirstOrDefault();
                PathCache[Route] = (Key, Path);
                return Path;
            })
                .Where(Path => Path != null)];
            (FlowchartLayoutEdgeRoute Route, (double X, double Y) Start, (double X, double Y) End, string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY, double EntryNormalX, double EntryNormalY)[] Saved = [.. Affected.Select(Route => (Route,
                Start: (Route.Points[0].X, Route.Points[0].Y), End: (Route.Points[^1].X, Route.Points[^1].Y),
                Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY))];
            string OriginalExit = Source.Side, OriginalEntry = Target.Side;
            string BestExit = OriginalExit, BestEntry = OriginalEntry;
            (double Overlap, int Bends, int SharedSides, double Length, int Crossings, int AlignedDetours) Best = Cost();
            Examined++;
            int MaximumCrossings = Best.Crossings;
            // An empty low-bend family is not permission to introduce an
            // aligned-port detour: the obstacle router may still have a legal
            // baseline route outside this provisional family.
            int MaximumAlignedDetours = Best.AlignedDetours == int.MaxValue ? 0 : Best.AlignedDetours;
            // Keep the original facing pair first so an exact tie is stable.
            foreach (string Exit in new[] { OriginalExit, "bottom", "top", "left", "right" }.Distinct())
            {
                foreach (string Entry in new[] { OriginalEntry, "top", "bottom", "left", "right" }.Distinct())
            {
                if (!SupportsGeometrySide(Source.Box, Exit,
                        ByNode[Source.Box.Id].Count(End => !ReferenceEquals(End, Source) && End.Side == Exit) + 1, Nodes)
                    || !SupportsGeometrySide(Target.Box, Entry,
                        ByNode[Target.Box.Id].Count(End => !ReferenceEquals(End, Target) && End.Side == Entry) + 1, Nodes))
                    {
                        continue;
                    }

                    if (Examined >= FlowchartLayoutRules.RepairCandidateMoves)
                    {
                        continue;
                    }

                    if (Exit == OriginalExit && Entry == OriginalEntry)
                    {
                        continue;
                    }
                    // A line may take a port another line already uses when that swap
                    // lowers the bend count. The points stay on the centre-out grid.
                    bool SwapsOntoOccupiedSide = (Exit != OriginalExit && ByNode[Source.Box.Id].Any(End => End != Source && End.Side == Exit))
                    || (Entry != OriginalEntry && ByNode[Target.Box.Id].Any(End => End != Target && End.Side == Entry));
                Restore();
                Source.Side = Exit; Target.Side = Entry;
                Source.Route.ExitSide = Exit; Target.Route.EntrySide = Entry;
                Allocate(Source.Box.Id); Allocate(Target.Box.Id);
                Examined++;
                FlowchartRenderContext.ReportOperationCount("port-side-assignment-candidates", 1);
                    (double Overlap, int Bends, int SharedSides, double Length, int Crossings, int AlignedDetours) Candidate = Cost();
                bool FewerBends = Candidate.Bends < Best.Bends;
                bool NoNewFault = Candidate.Crossings <= MaximumCrossings && Candidate.AlignedDetours <= MaximumAlignedDetours;
                if (NoNewFault && (SwapsOntoOccupiedSide ? FewerBends : Candidate.CompareTo(Best) < 0))
                {
                    Best = Candidate; BestExit = Exit; BestEntry = Entry;
                }
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
                foreach ((FlowchartLayoutEdgeRoute Route, (double X, double Y) Start, (double X, double Y) End, string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY, double EntryNormalX, double EntryNormalY) Item in Saved)
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
                foreach (IGrouping<string, PortEnd> Group in ByNode[NodeId].GroupBy(End => End.Side))
                {
                    List<PortEnd> Ordered = [.. Group.OrderBy(End => End.Side is "left" or "right"
                        ? -End.Other.CenterY : End.Other.CenterX)];
                    ApplySlots(Ordered, Nodes, Contract, Direction, UseCompassOverrides: false);
                }
            }

            (double Overlap, int Bends, int SharedSides, double Length, int Crossings, int AlignedDetours) Cost()
            {
                int Bends = 0, Crossings = 0, AlignedDetours = 0;
                double Length = 0, Overlap = 0;
                List<List<(double X, double Y)>> ChosenPaths = [];
                foreach (FlowchartLayoutEdgeRoute? Route in Affected)
                {
                    if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? From) || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? To))
                    {
                        return (double.PositiveInfinity, int.MaxValue, int.MaxValue, double.PositiveInfinity, int.MaxValue, int.MaxValue);
                    }

                    (double Overlap, int Bends, double Length, int Crossings) BestRoute = (Overlap: double.PositiveInfinity, Bends: int.MaxValue, Length: double.PositiveInfinity, Crossings: int.MaxValue);
                    List<(double X, double Y)>? BestPath = null;
                    foreach (List<(double X, double Y)> Path in TerminalPaths(Route))
                    {
                        if (!PathCosts.TryGetValue(Path, out (int Bends, double Length) PathCost))
                        {
                            PathCost = PortCandidateCost(Path);
                            PathCosts.Add(Path, PathCost);
                        }
                        double SharedLength = 0; int CrossingCount = 0;
                        foreach (List<(double X, double Y)>? Other in UnchangedPaths.Concat(ChosenPaths))
                        {
                            if (!Interactions.TryGetValue((Path, Other!), out (int Crossings, double Overlap) Interaction))
                            {
                                Interaction = FlowchartBranchRouteOrder.Intersections(Path, Other!);
                                Interactions.Add((Path, Other!), Interaction);
                            }
                            SharedLength += Interaction.Overlap; CrossingCount += Interaction.Crossings;
                        }
                        (double Overlap, int Bends, double Length, int Crossings) Candidate = (Overlap: Math.Round(SharedLength, 3), Bends: PathCost.Bends, Length: PathCost.Length, Crossings: CrossingCount);
                        if (Candidate.CompareTo(BestRoute) < 0) { BestRoute = Candidate; BestPath = Path; }
                    }
                    if (BestPath == null)
                    {
                        return (double.PositiveInfinity, int.MaxValue, int.MaxValue, double.PositiveInfinity, int.MaxValue, int.MaxValue);
                    }

                    if (BestPath.Count > 2 && (Math.Abs(BestPath[0].X - BestPath[^1].X) <= 1
                            || Math.Abs(BestPath[0].Y - BestPath[^1].Y) <= 1))
                    {
                        AlignedDetours++;
                    }

                    ChosenPaths.Add(BestPath);
                    Overlap += BestRoute.Overlap; Bends += BestRoute.Bends; Length += BestRoute.Length; Crossings += BestRoute.Crossings;
                }
                int SharedSides = ByNode[Source.Box.Id].Concat(ByNode[Target.Box.Id])
                    .GroupBy(End => (End.Box.Id, End.Side)).Count(Group => Group.Count() > 1);
                return (Overlap, Bends, SharedSides, Length, Crossings, AlignedDetours);
            }
        }

        Cache[(Contract, Geometry)] = [.. Ends.Select(End => End.Side)];

        // <summary>Reuses the exact legal terminal family when the complete obstacle geometry and allocated ports agree.</summary>
        // <param name="Route">The provisional side and slot assignment to enumerate.</param>
        // <returns>Candidate paths owned by the render cache and treated as immutable.</returns>
        List<List<(double X, double Y)>> TerminalPaths(FlowchartLayoutEdgeRoute Route)
        {
            string Key = FormattableString.Invariant($"{Route.SourceId}:{Route.TargetId}:{Nodes.GetValueOrDefault(Route.SourceId)?.Shape}:{Nodes.GetValueOrDefault(Route.TargetId)?.Shape}:{Route.ExitSide}:{Route.EntrySide}:{Route.Points[0].X:R},{Route.Points[0].Y:R},{Route.Points[^1].X:R},{Route.Points[^1].Y:R}");
            if (TerminalCache.TryGetValue(Key, out List<List<(double X, double Y)>>? Paths))
            {
                return Paths;
            }

            Paths = [.. DiagramGridRouteBuilder.MinimumBendTerminalCandidates(Route.Points[0], Route.Points[^1],
                Bounds[Route.SourceId], Bounds[Route.TargetId], Route.ExitSide, Route.EntrySide, Nodes, Bounds, Contract,
                TerminalObstacles)];
            TerminalCache[Key] = Paths;
            return Paths;
        }

        bool IsBox(PortEnd End) => End.Box.IsFrame || (Nodes.TryGetValue(End.Box.Id, out NodeContract? Node)
            && IsRectangular(Node.Shape));
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
            Length += Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Index + 1 < Path.Count && Math.Abs((Dx * (Path[Index + 1].Y - Path[Index].Y))
                    - (Dy * (Path[Index + 1].X - Path[Index].X))) > 0.001)
            {
                Bends++;
            }
        }
        return (Bends, Length);
    }

    internal static EdgeContract? FindContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route)
    {
        EdgeContract? ById = Contract.Edges.FirstOrDefault(Edge =>
            !string.IsNullOrEmpty(Edge.Id) && string.Equals(Edge.Id, Route.EdgeId, StringComparison.Ordinal));
        if (ById != null)
        {
            return ById;
        }

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
            if (IsHinted)
            {
                continue;
            }
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
                    if (HintOrder != 0)
                    {
                        return HintOrder;
                    }

                    bool VerticalSide = Left.Side is "left" or "right";
                    return VerticalSide
                        ? Right.Other.CenterY.CompareTo(Left.Other.CenterY)
                        : Left.Other.CenterX.CompareTo(Right.Other.CenterX);
                });
            }
            if (!Diamond)
            {
                ApplySlots(Ends, Nodes, Contract);
            }
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
            {
                Groups[Key] = Ends = [];
            }

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

    private static bool TryApplyDiamondCompassSourcePorts(
        List<PortEnd> Group,
        DiagramContract Contract,
        FlowchartDirection Direction,
        HashSet<int> FixedIndices)
    {
        if (Group.Count == 0)
        {
            return false;
        }

        bool HasLabeledYesNoSource = false;
        foreach (PortEnd End in Group)
        {
            if (!End.IsSource)
            {
                continue;
            }

            EdgeContract? Edge = FindContractEdge(Contract, End.Route);
            if (Edge == null)
            {
                continue;
            }

            if (FlowchartDecisionBranchLabelHelper.IsYesBranchLabel(Edge.Label)
                || FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Edge.Label))
            {
                HasLabeledYesNoSource = true;
                break;
            }
        }

        if (!HasLabeledYesNoSource)
        {
            return false;
        }

        string HubId = Group[0].Box.Id;
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBoundsDictionary(Group);
        if (!FlowchartDecisionPortAllocator.TryResolveCompassFanOutPortsForHub(
                Contract,
                Direction,
                HubId,
                BoundsById,
                out Dictionary<string, (double X, double Y, double NormalX, double NormalY)> PortsByTarget))
        {
            return false;
        }

        for (int Index = 0; Index < Group.Count; Index++)
        {
            PortEnd End = Group[Index];
            if (!End.IsSource)
            {
                continue;
            }

            if (!PortsByTarget.TryGetValue(End.Route.TargetId, out (double X, double Y, double NormalX, double NormalY) Attach))
            {
                continue;
            }

            SetEnd(End, Attach.X, Attach.Y, Attach.NormalX, Attach.NormalY);
            FixedIndices.Add(Index);
        }

        return FixedIndices.Count > 0;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildBoundsDictionary(List<PortEnd> Group)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (PortEnd End in Group)
        {
            BoundsById[End.Box.Id] = ToLayoutBounds(End.Box);
            BoundsById[End.Other.Id] = ToLayoutBounds(End.Other);
        }

        return BoundsById;
    }

    private static FlowchartLayoutNodeBounds ToLayoutBounds(BoxRef Box) => new()
    {
        NodeId = Box.Id,
        CenterX = Box.CenterX,
        CenterY = Box.CenterY,
        Width = Box.Width,
        Height = Box.Height
    };

    internal static string HintSide(LayoutHintDirection Direction) => Direction switch
    {
        LayoutHintDirection.North => "top",
        LayoutHintDirection.South => "bottom",
        LayoutHintDirection.East => "right",
        _ => "left"
    };

    private static void ApplySlots(
        List<PortEnd> Group,
        Dictionary<string, NodeContract> Nodes,
        DiagramContract? Contract = null,
        FlowchartDirection? Direction = null,
        bool UseCompassOverrides = true)
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
            FlowchartDirection FlowDirection = Direction
                ?? (Contract != null
                    ? FlowchartDirectionNormalizer.Normalize(Contract.Direction)
                    : FlowchartDirection.TopToBottom);
            HashSet<int> CompassFixedIndices = [];
            if (Contract != null
                && UseCompassOverrides
                && TryApplyDiamondCompassSourcePorts(Group, Contract, FlowDirection, CompassFixedIndices))
            {
                if (CompassFixedIndices.Count == Group.Count)
                {
                    return;
                }
            }

            List<int> RemainingIndices = CompassFixedIndices.Count == 0
                ? [.. Enumerable.Range(0, Group.Count)]
                : [.. Enumerable.Range(0, Group.Count).Where(Index => !CompassFixedIndices.Contains(Index))];
            List<(double X, double Y)> Destinations = [];
            foreach (int Index in RemainingIndices)
            {
                PortEnd End = Group[Index];
                Destinations.Add((End.Other.CenterX - Box.CenterX, End.Other.CenterY - Box.CenterY));
            }

            if (Destinations.Count == 0)
            {
                return;
            }

            HashSet<int> Reservations = [.. Enumerable.Range(0, RemainingIndices.Count).Where(SubIndex =>
            {
                PortEnd End = Group[RemainingIndices[SubIndex]];
                if (End.KeepsCentre) { return true; } if (End.Route.Points.Count != 2) { return false; } FlowchartLayoutPoint A = End.Route.Points[0];
                FlowchartLayoutPoint B = End.Route.Points[^1];
                return (Math.Abs(A.X - B.X) < 0.001 && Math.Abs(End.Box.CenterX - End.Other.CenterX) < 0.001)
                    || (Math.Abs(A.Y - B.Y) < 0.001 && Math.Abs(End.Box.CenterY - End.Other.CenterY) < 0.001);
            })];
            if (FlowchartDiamondPorts.TryAssign((int)Box.Width, (int)Box.Height, Destinations, out IReadOnlyList<FlowchartDiamondPort> Ports, Reservations)
                && Ports.Count == RemainingIndices.Count)
            {
                for (int SubIndex = 0; SubIndex < RemainingIndices.Count; SubIndex++)
                {
                    int Index = RemainingIndices[SubIndex];
                    FlowchartDiamondPort Port = Ports[SubIndex];
                    double X = Box.CenterX + Port.OffsetX;
                    double Y = Box.CenterY + Port.OffsetY;
                    SetEnd(Group[Index], X, Y, Port.DepartureX, Port.DepartureY);
                }

                return;
            }
        }

        bool VerticalSide = Side == "left" || Side == "right";
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
        // Reserving a corner inset here moves the grid away from the side centre
        // and makes the points depend on the renderer's corner radius.
        double SideLength = VerticalSide ? Box.Height : Box.Width;
        double[]? GridFractions = PlanGridFractions(Group, Nodes, Contract);
        INodeConnectionBoundary? Boundary = Nodes.TryGetValue(Box.Id, out NodeContract? GridNode) && GridNode.GeometryProvider != null
            ? GridNode.GeometryProvider.Create(Box.Width, Box.Height).ConnectionBoundary
            : null;
        double[] Fallback = GridFractions == null
            ? ConnectionPointGrid.FractionsOnGrid(Boundary, Side, SideLength, Group.Count)
            : [];
        for (int Index = 0; Index < Group.Count; Index++)
        {
            PortEnd End = Group[Index];
            double Fraction;
            double Along;
            if (GridFractions != null)
            {
                Fraction = GridFractions[Index];
                Along = Fraction * SideLength;
            }
            else
            {
                // The fixed grid is shorter than the line count. Stay on those points.
                // An even share of the side would sit somewhere new whenever the count changed.
                Fraction = Fallback.Length == Group.Count ? Fallback[Index] : 0.5;
                Along = Fraction * SideLength;
                if (End.KeepsCentre && TrySetCenterPort(End, Nodes, Side, Box))
                {
                    continue;
                }
            }

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

            if (Nodes.TryGetValue(Box.Id, out NodeContract? GeometryNode) && GeometryNode.GeometryProvider is { } Provider)
            {
                if (Provider.Create(Box.Width, Box.Height).ConnectionBoundary.TryPort(
                    Side, Fraction, out NodeGeometryPort Port))
                {
                    SetEnd(Group[Index], Box.CenterX + Port.X, Box.CenterY - Port.Y, Port.NormalX, -Port.NormalY);
                }

                continue;
            }

            bool HorizontalBracketEdge = Side is "top" or "bottom"
                && Nodes.TryGetValue(Box.Id, out NodeContract? BracketNode)
                && FlowchartTrapezoidBracketPathAnalyser.IsTrapezoidBracketShape(BracketNode.Shape);
            if (!Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? ShapeNode)
                && !IsRectangular(ShapeNode.Shape) && !Diamond && !HorizontalBracketEdge)
            {
                (X, Y) = ProjectToOutline(Box, ShapeNode.Shape,
                    (int)Math.Round(X), (int)Math.Round(Y), OutX, OutY);
                // Projection uses pixel coordinates for the outline. A lone
                // cardinal port still owns the exact side centre, including an
                // open layout's fractional centre; rounding its along-side
                // coordinate would turn a straight neighbour link into a jog.
                if (Group.Count == 1 && Math.Abs(Fraction - 0.5) < 0.000001)
                {
                    if (VerticalSide)
                    {
                        Y = Box.CenterY;
                    }
                    else
                    {
                        X = Box.CenterX;
                    }
                }
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
                double Offset = Rx * Math.Sqrt(Math.Max(0, 1 - (Dy * Dy)));
                return ((int)Math.Round(Box.CenterX + (OutX * Offset)), SlotY);
            }
            double Dx = (SlotX - Box.CenterX) / Rx;
            double VerticalOffset = Ry * Math.Sqrt(Math.Max(0, 1 - (Dx * Dx)));
            return (SlotX, (int)Math.Round(Box.CenterY + (OutY * VerticalOffset)));
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
        // A rejected geometry candidate may have cleared the provisional path.
        // Allocation still owns both anchors of that declared relationship.
        while (End.Route.Points.Count < 2)
        {
            End.Route.Points.Add(new FlowchartLayoutPoint());
        }

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

    private static bool StraightCenterPortFits(PortEnd End)
    {
        if (End.Route.Points.Count != 2)
        {
            return false;
        }

        FlowchartLayoutPoint A = End.Route.Points[0];
        FlowchartLayoutPoint B = End.Route.Points[^1];
        bool HorizontalChord = Math.Abs(A.Y - B.Y) < 0.5;
        bool VerticalChord = Math.Abs(A.X - B.X) < 0.5;
        return End.Side switch
        {
            "left" or "right" => HorizontalChord,
            "top" or "bottom" => VerticalChord,
            _ => false
        };
    }

    private static bool TrySetCenterPort(
        PortEnd End,
        Dictionary<string, NodeContract> Nodes,
        string Side,
        BoxRef Box)
    {
        if (Nodes.TryGetValue(Box.Id, out NodeContract? GeometryNode) && GeometryNode.GeometryProvider is { } Provider)
        {
            if (!Provider.Create(Box.Width, Box.Height).ConnectionBoundary.TryPort(Side, 0.5, out NodeGeometryPort Port))
            {
                return false;
            }

            SetEnd(End, Box.CenterX + Port.X, Box.CenterY - Port.Y, Port.NormalX, -Port.NormalY);
            return true;
        }

        int OutX = 0;
        int OutY = 0;
        double X;
        double Y;
        if (Side == "top")
        {
            X = Box.CenterX;
            Y = Box.Top;
            OutY = 1;
        }
        else if (Side == "bottom")
        {
            X = Box.CenterX;
            Y = Box.Bottom;
            OutY = -1;
        }
        else if (Side == "left")
        {
            X = Box.Left;
            Y = Box.CenterY;
            OutX = -1;
        }
        else
        {
            X = Box.Right;
            Y = Box.CenterY;
            OutX = 1;
        }

        SetEnd(End, X, Y, OutX, OutY);
        return true;
    }

}
