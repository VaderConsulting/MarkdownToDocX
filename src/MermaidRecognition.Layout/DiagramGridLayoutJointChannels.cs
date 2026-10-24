using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Bounded searches that compare a channel's placement and connector assignments atomically.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>A complete connector assignment retained independently of mutable search geometry.</summary>
    /// <param name="Route">The declared connector being assigned.</param>
    /// <param name="Points">The complete unrounded polyline.</param>
    /// <param name="Exit">The source side.</param>
    /// <param name="Entry">The target side.</param>
    private sealed record JointChannelPath(FlowchartLayoutEdgeRoute Route,
        List<(double X, double Y)> Points, string Exit, string Entry);

    /// <summary>Allocates distinct grid tracks after reserving the same terminal clearance as route construction.</summary>
    /// <param name="NearSource">The furthest source port in increasing channel coordinates.</param>
    /// <param name="NearTarget">The nearest target port in the same channel coordinates.</param>
    /// <param name="Count">The number of bent paths needing independent tracks.</param>
    /// <param name="Tracks">Receives ordered grid coordinates, including negative channel coordinates.</param>
    /// <returns>False when the channel needs growth before this assignment can be offered.</returns>
    internal static bool TryAllocateChannelTracks(double NearSource, double NearTarget, int Count, out double[] Tracks)
    {
        Tracks = [];
        if (Count == 0) return true;
        double Step = DiagramGridMetrics.GRID_UNIT_PX;
        double Clearance = 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        double First = Math.Ceiling((NearSource + Clearance) / Step) * Step;
        double Last = Math.Floor((NearTarget - Clearance) / Step) * Step;
        int Available = (int)Math.Floor((Last - First) / Step) + 1;
        if (Available < Count) return false;
        Tracks = Enumerable.Range(0, Count).Select(Index => First + Step * (Count == 1
            ? Math.Floor((Available - 1) / 2.0)
            : Math.Round(Index * (Available - 1.0) / (Count - 1)))).ToArray();
        return true;
    }

    /// <summary>Orders internal tracks so coincident departure and arrival runs meet without sharing a positive segment.</summary>
    /// <param name="Seed">The deterministic median order used for otherwise unconstrained tracks.</param>
    /// <param name="Starts">The complete allocated departure slots.</param>
    /// <param name="Ends">The complete allocated arrival slots.</param>
    /// <param name="Horizontal">Whether the channel's main direction is horizontal.</param>
    /// <returns>A stable topological order, or the original order when two-bend constraints are cyclic.</returns>
    private static FlowchartLayoutEdgeRoute[] OrderNonoverlappingChannelTracks(FlowchartLayoutEdgeRoute[] Seed,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, (double X, double Y)> Starts,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, (double X, double Y)> Ends, bool Horizontal)
    {
        var Remaining = Seed.ToList();
        var Ordered = new List<FlowchartLayoutEdgeRoute>();
        while (Remaining.Count > 0)
        {
            var Next = Remaining.FirstOrDefault(Route => !Remaining.Any(Other => Other != Route
                && Math.Abs((Horizontal ? Starts[Other].Y : Starts[Other].X)
                    - (Horizontal ? Ends[Route].Y : Ends[Route].X)) < 0.001));
            if (Next == null) return Seed;
            Ordered.Add(Next); Remaining.Remove(Next);
        }
        return Ordered.ToArray();
    }

    /// <summary>Compares complete side, slot and track assignments for a shared rank channel.</summary>
    /// <param name="State">Final geometry and declarations, including honoured hints.</param>
    /// <param name="Bounds">Authoritative endpoint and obstacle geometry.</param>
    /// <param name="IncludeNodeOrder">Whether to offer placement candidates before track assignments; false inside a placement candidate.</param>
    private static void SearchJointChannels(GridLayoutState State,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds, bool IncludeNodeOrder = true)
    {
        var Layout = State.Copy;
        if (IncludeNodeOrder) SearchJointNodeOrder(State, Bounds);
        int RemainingCandidates = FlowchartLayoutRules.RepairCandidateMoves;
        var BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
        var Eligible = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
            && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && State.Nodes.TryGetValue(Route.SourceId, out var Source) && IsRectangular(Source.Shape)
            && State.Nodes.TryGetValue(Route.TargetId, out var Target) && IsRectangular(Target.Shape)
            && FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive != true).ToArray();
        // Each orientation offers an entire channel at once. Neither an earlier
        // line nor its accidental track becomes a fixed obstacle for later lines.
        foreach (bool Horizontal in new[] { false, true })
        {
            var Groups = Eligible.GroupBy(Route => (
                Source: Horizontal ? Bounds[Route.SourceId].CenterX : Bounds[Route.SourceId].CenterY,
                Target: Horizontal ? Bounds[Route.TargetId].CenterX : Bounds[Route.TargetId].CenterY));
            foreach (var Group in Groups.Where(Group => Group.Count() > 1
                         && Group.Count() <= FlowchartLayoutRules.RepairChainLines))
            {
                if (RemainingCandidates <= 0) return;
                var Routes = Group.ToArray();
                if (!BeforeFindings.Any(Finding => Finding.Rule is "5.1" or "4.11" or "5.8"
                        && Routes.Any(Route => Finding.Subject.Split(" x ", StringSplitOptions.None).Contains(Route.EdgeId)))) continue;
                double Sign = Math.Sign(Group.Key.Target - Group.Key.Source);
                if (Sign == 0) continue;
                string Exit = Horizontal ? Sign > 0 ? "right" : "left" : Sign > 0 ? "top" : "bottom";
                string Entry = Horizontal ? Sign > 0 ? "left" : "right" : Sign > 0 ? "bottom" : "top";
                // A side used by an outside link needs a larger coupled cohort;
                // never silently displace a port whose route is outside this option.
                if (Layout.EdgeRoutes.Except(Routes).Any(Other =>
                        Routes.Any(Route => Other.SourceId == Route.SourceId && Other.ExitSide == Exit
                            || Other.TargetId == Route.SourceId && Other.EntrySide == Exit
                            || Other.SourceId == Route.TargetId && Other.ExitSide == Entry
                            || Other.TargetId == Route.TargetId && Other.EntrySide == Entry))) continue;
                var Saved = Routes.Select(Route => new JointChannelPath(Route,
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
                var Normals = Routes.Select(Route => (Route, Route.ExitNormalX, Route.ExitNormalY,
                    Route.EntryNormalX, Route.EntryNormalY)).ToArray();
                var BestScore = FlowchartLayoutScore.ComparisonCost.PositiveInfinity;
                JointChannelPath[]? Best = null;
                int Examined = 0;
                foreach (bool ReverseSources in new[] { false, true })
                foreach (bool ReverseTargets in new[] { false, true })
                foreach (bool ConstrainTracks in new[] { true, false })
                foreach (bool ReverseTracks in new[] { false, true })
                {
                    if (Examined++ >= FlowchartLayoutRules.RepairCandidateMoves || RemainingCandidates-- <= 0) break;
                    var Starts = Slots(true, ReverseSources);
                    var Ends = Slots(false, ReverseTargets);
                    var Bent = Routes.Where(Route => Math.Abs(Cross(Starts[Route]) - Cross(Ends[Route])) > 0.001)
                        .OrderBy(Route => Cross(Starts[Route]) + Cross(Ends[Route])).ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToArray();
                    if (ConstrainTracks) Bent = OrderNonoverlappingChannelTracks(Bent, Starts, Ends, Horizontal);
                    if (ReverseTracks) Array.Reverse(Bent);
                    double NearSource = Routes.Max(Route => Main(Starts[Route]) * Sign);
                    double NearTarget = Routes.Min(Route => Main(Ends[Route]) * Sign);
                    double Gap = NearTarget - NearSource;
                    if (Gap <= 0) continue;
                    if (!TryAllocateChannelTracks(NearSource, NearTarget, Bent.Length, out var Tracks)) continue;
                    var Candidate = Routes.Select(Route =>
                    {
                        var A = Starts[Route]; var B = Ends[Route];
                        int Track = Array.IndexOf(Bent, Route);
                        double Along = Track < 0 ? 0 : Tracks[Track] * Sign;
                        var Points = Track < 0 ? new List<(double X, double Y)> { A, B }
                            : Horizontal ? new() { A, (Along, A.Y), (Along, B.Y), B }
                            : new List<(double X, double Y)> { A, (A.X, Along), (B.X, Along), B };
                        return new JointChannelPath(Route, Points, Exit, Entry);
                    }).ToArray();
                    Install(Candidate);
                    // Reject an invalid changed path before checking every pair
                    // in a dense diagram. This uses the same exact outline and
                    // title-band checker; surviving options still receive the
                    // complete assignment check below.
                    var ChangedPaths = new FlowchartLayoutResult();
                    ChangedPaths.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                    ChangedPaths.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                    ChangedPaths.EdgeRoutesMutable.AddRange(Routes);
                    if (FlowchartRouteRuleChecker.Check(State.Contract, ChangedPaths)
                            .Any(Finding => Finding.Rule is "10" or "3.1" or "4.1")) continue;
                    var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                    bool Legal = !Findings.Any(Finding => Finding.Rule is "10" or "5.1"
                        && Routes.Any(Route => Finding.Subject == Route.EdgeId
                            || Finding.Subject.Split(" x ", StringSplitOptions.None).Contains(Route.EdgeId)))
                        && new[] { "3.1", "4.1", "4.2", "4.9", "3.2/4.7", "3.3/5.2", "4.11", "5.8" }.All(Rule =>
                            Findings.Count(Finding => Finding.Rule == Rule) <= BeforeFindings.Count(Finding => Finding.Rule == Rule))
                        // Rule 5.1 is legality, so complete separation outranks
                        // the crossing cost of its illegal overlapping baseline.
                        // Among complete legal assignments use the unchanged
                        // whole-layout score below; partial repairs retain the
                        // nonincreasing crossing admission.
                        && (Findings.All(Finding => Finding.Rule != "5.1")
                            || Findings.Count(Finding => Finding.Rule == "5.9") <= BeforeFindings.Count(Finding => Finding.Rule == "5.9"));
                    FlowchartRenderContext.ReportOperationCount("joint-channel-assignments", 1);
                    RouteRepairCandidateObserver?.Invoke("joint-channel",
                        $"lines={Routes.Length};horizontal={Horizontal};gap={Gap};bent={Bent.Length};illegal={Findings.Count(Finding => Finding.Rule == "10")};shared={Findings.Count(Finding => Finding.Rule == "5.1")}", 0, Legal);
                    if (!Legal) continue;
                    var Score = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false);
                    if (Score.Cost < BestScore)
                    {
                        BestScore = Score.Cost;
                        Best = Candidate;
                    }
                }
                Install(Best ?? Saved);
                if (Best != null) BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                if (Best == null)
                    foreach (var Original in Normals)
                    {
                        Original.Route.ExitNormalX = Original.ExitNormalX; Original.Route.ExitNormalY = Original.ExitNormalY;
                        Original.Route.EntryNormalX = Original.EntryNormalX; Original.Route.EntryNormalY = Original.EntryNormalY;
                    }

                // <summary>Allocates every end on the selected side before any track is scored.</summary>
                // <param name="Source">Whether to allocate departures or arrivals.</param>
                // <param name="Reverse">Whether to offer the opposite slot order.</param>
                // <returns>One continuous k/(n+1) outline point per connector.</returns>
                Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)> Slots(bool Source, bool Reverse)
                {
                    var Result = new Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)>();
                    foreach (var NodeGroup in Routes.GroupBy(Route => Source ? Route.SourceId : Route.TargetId))
                    {
                        var Box = Bounds[NodeGroup.Key];
                        var Order = NodeGroup.OrderBy(Route => Horizontal
                            ? Bounds[Source ? Route.TargetId : Route.SourceId].CenterY
                            : Bounds[Source ? Route.TargetId : Route.SourceId].CenterX)
                            .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToArray();
                        if (Reverse) Array.Reverse(Order);
                        for (int Index = 0; Index < Order.Length; Index++)
                        {
                            double Fraction = (Index + 1.0) / (Order.Length + 1.0);
                            double Outward = Source ? Sign : -Sign;
                            Result[Order[Index]] = Horizontal
                                ? (Box.CenterX + Outward * Box.Width / 2, Box.CenterY - Box.Height / 2 + Fraction * Box.Height)
                                : (Box.CenterX - Box.Width / 2 + Fraction * Box.Width, Box.CenterY + Outward * Box.Height / 2);
                        }
                    }
                    return Result;
                }

                // <summary>Projects a point across the channel.</summary>
                // <param name="Point">A terminal in final model coordinates.</param>
                // <returns>The coordinate used to order slots and tracks.</returns>
                double Cross((double X, double Y) Point) => Horizontal ? Point.Y : Point.X;
                // <summary>Projects a point along the channel.</summary>
                // <param name="Point">A terminal in final model coordinates.</param>
                // <returns>The coordinate used to measure available track space.</returns>
                double Main((double X, double Y) Point) => Horizontal ? Point.X : Point.Y;
            }
        }
    }

    /// <summary>Offers bounded whole-grid permutations before fixing the sides of a small unhinted channel.</summary>
    /// <param name="State">The complete model whose node centres are the available grid positions.</param>
    /// <param name="Bounds">The obstacle index updated atomically with a winning assignment.</param>
    /// <param name="IncludeSideFaults">Whether settled fan and convergence faults also nominate movable endpoints.</param>
    /// <param name="IncludeCrossings">Whether a small settled crossing component nominates endpoints for cost reduction.</param>
    private static void SearchJointNodeOrder(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
        bool IncludeSideFaults = false, bool IncludeCrossings = false)
    {
        if (State.Placed.Any(Node => Node.CellNode != null))
        {
            RouteRepairStage.SearchCellNodeOrder(State, IncludeSideFaults, IncludeCrossings);
            foreach (var Box in State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds)) Bounds[Box.NodeId] = Box;
            return;
        }
        var Layout = State.Copy;
        if (State.Contract.Groups.Count != 0 || State.Contract.Edges.Any(Edge => Edge.RoutingHint?.IsActive == true)) return;
        var Before = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
        if (!Before.Any(Finding => IncludeSideFaults ? Finding.Rule is "4.11" or "5.8"
                : IncludeCrossings ? Finding.Rule == "5.9" : Finding.Rule == "5.1")) return;
        var SharedLinks = Before.Where(Finding => Finding.Rule == "5.1"
                || IncludeSideFaults && Finding.Rule is "4.11" or "5.8"
                || IncludeCrossings && Finding.Rule == "5.9")
            .SelectMany(Finding => Finding.Subject.Split(" x ", StringSplitOptions.None)).ToHashSet(StringComparer.Ordinal);
        if (IncludeCrossings && SharedLinks.Count > FlowchartLayoutRules.RepairChainLines) return;
        var InvolvedNodes = Layout.EdgeRoutes.Where(Route => SharedLinks.Contains(Route.EdgeId))
            .SelectMany(Route => new[] { Route.SourceId, Route.TargetId }).ToHashSet(StringComparer.Ordinal);
        var OriginalBounds = Layout.NodeBounds.OrderBy(Box => InvolvedNodes.Contains(Box.NodeId) ? 1 : 0).ToArray();
        var OriginalPaths = Layout.EdgeRoutes.Select(Route => new JointChannelPath(Route,
            Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
        var OriginalNormals = Layout.EdgeRoutes.Select(Route => (Route, Route.ExitNormalX, Route.ExitNormalY,
            Route.EntryNormalX, Route.EntryNormalY)).ToArray();
        var Positions = OriginalBounds.Select(Box => (Box.CenterX, Box.CenterY)).ToArray();
        int[] Order = Enumerable.Range(0, Positions.Length).ToArray();
        var BestBounds = OriginalBounds;
        var BestPaths = OriginalPaths;
        var BestNormals = OriginalNormals;
        var BestScore = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false).Cost;
        int BestSideFaults = IncludeSideFaults ? Before.Count(Finding => Finding.Rule is "4.11" or "5.8") : 0;
        int Examined = 0;
        // Offer straightening moves against the settled ports before spending
        // the bounded permutation budget on unrelated existing grid positions.
        int Alignments = 0;
        foreach (var Route in Layout.EdgeRoutes.Where(Route => SharedLinks.Contains(Route.EdgeId)))
        foreach (bool Source in new[] { true, false })
        {
            if (Alignments++ >= FlowchartLayoutRules.RepairCandidateMoves) break;
            string Id = Source ? Route.SourceId : Route.TargetId;
            string Peer = Source ? Route.TargetId : Route.SourceId;
            int Index = Array.FindIndex(OriginalBounds, Box => Box.NodeId == Id);
            if (Index < 0 || !Bounds.TryGetValue(Peer, out var PeerBox)) continue;
            var Candidate = Positions.ToArray();
            string Side = Source ? Route.ExitSide : Route.EntrySide;
            Candidate[Index] = Side is "left" or "right"
                ? (Candidate[Index].CenterX, Math.Round(PeerBox.CenterY / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX)
                : (Math.Round(PeerBox.CenterX / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX, Candidate[Index].CenterY);
            Evaluate(Candidate);
        }
        Visit(0);
        int Nudges = 0;
        foreach (int Distance in Enumerable.Range(1, FlowchartLayoutRules.RepairChainDepth))
        foreach (int Position in Enumerable.Range(0, OriginalBounds.Length).Where(Position => InvolvedNodes.Contains(OriginalBounds[Position].NodeId)))
        foreach (var Direction in new[] { (X: 0, Y: 1), (X: 0, Y: -1), (X: 1, Y: 0), (X: -1, Y: 0) })
        {
            if (Nudges++ >= FlowchartLayoutRules.RepairCandidateMoves) break;
            var Candidate = Positions.ToArray();
            Candidate[Position] = (Candidate[Position].CenterX + Direction.X * Distance * DiagramGridMetrics.GRID_UNIT_PX,
                Candidate[Position].CenterY + Direction.Y * Distance * DiagramGridMetrics.GRID_UNIT_PX);
            Evaluate(Candidate);
        }
        Layout.NodeBoundsMutable.Clear(); Layout.NodeBoundsMutable.AddRange(BestBounds);
        foreach (var Box in BestBounds)
        {
            Bounds[Box.NodeId] = Box;
            State.Placed.Single(Node => Node.Id == Box.NodeId).SetPositionFromFrameFit(Box.CenterX, Box.CenterY);
        }
        Install(BestPaths);
        foreach (var Saved in BestNormals)
            {
                Saved.Route.ExitNormalX = Saved.ExitNormalX; Saved.Route.ExitNormalY = Saved.ExitNormalY;
                Saved.Route.EntryNormalX = Saved.EntryNormalX; Saved.Route.EntryNormalY = Saved.EntryNormalY;
            }

        // A changed node order is an entire placement and routing option. The
        // existing chain depth bounds how many nodes may change position; the
        // candidate limit bounds complete options, rather than inner loop work.
        // <summary>Enumerates complete position permutations within the named move and chain limits.</summary>
        // <param name="Index">The next node whose existing grid position is assigned.</param>
        void Visit(int Index)
        {
            if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) return;
            if (Index < Order.Length)
            {
                for (int Other = Index; Other < Order.Length; Other++)
                {
                    (Order[Index], Order[Other]) = (Order[Other], Order[Index]);
                    int Moved = Enumerable.Range(0, Index + 1).Count(Position => Order[Position] != Position);
                    if (Moved <= FlowchartLayoutRules.RepairChainDepth) Visit(Index + 1);
                    (Order[Index], Order[Other]) = (Order[Other], Order[Index]);
                    if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) break;
                }
                return;
            }
            Examined++;
            Evaluate(Order.Select(Position => Positions[Position]).ToArray());
        }

        // <summary>Builds, validates and scores all connectors for one complete placement option.</summary>
        // <param name="Centres">One proposed whole-grid centre per node.</param>
        void Evaluate((double CenterX, double CenterY)[] Centres)
        {
            var MovedIds = Enumerable.Range(0, Centres.Length).Where(Index => Centres[Index] != Positions[Index])
                .Select(Index => OriginalBounds[Index].NodeId).ToHashSet(StringComparer.Ordinal);
            if (MovedIds.Count > FlowchartLayoutRules.RepairChainDepth) return;
            if (MovedIds.Any(Id => State.Placed.Single(Node => Node.Id == Id).PlacementHintLocked
                    || State.Nodes[Id].PlacementHint?.IsActive == true
                    || RouteRepairStage.HasHintedContainingGroup(State.Contract, Id))) return;
            foreach (int Index in Enumerable.Range(0, Centres.Length).Where(Index => MovedIds.Contains(OriginalBounds[Index].NodeId)))
            {
                var Box = OriginalBounds[Index]; var Centre = Centres[Index];
                for (int Other = 0; Other < Centres.Length; Other++)
                {
                    if (Other == Index) continue;
                    double GapX = Math.Abs(Centre.CenterX - Centres[Other].CenterX) - (Box.Width + OriginalBounds[Other].Width) / 2;
                    double GapY = Math.Abs(Centre.CenterY - Centres[Other].CenterY) - (Box.Height + OriginalBounds[Other].Height) / 2;
                    if (GapX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX < DiagramGridMetrics.NODE_GAP_PX
                        && GapY + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX < DiagramGridMetrics.NODE_GAP_PX) return;
                }
            }
            Install(OriginalPaths);
            Layout.NodeBoundsMutable.Clear();
            for (int Position = 0; Position < Order.Length; Position++)
            {
                var Original = OriginalBounds[Position]; var Centre = Centres[Position];
                var Box = new FlowchartLayoutNodeBounds { NodeId = Original.NodeId, CenterX = Centre.CenterX,
                    CenterY = Centre.CenterY, Width = Original.Width, Height = Original.Height };
                Layout.NodeBoundsMutable.Add(Box); Bounds[Box.NodeId] = Box;
            }
            var Ends = new List<PortEnd>();
            foreach (var Route in Layout.EdgeRoutes)
            {
                var Source = AsBox(Bounds[Route.SourceId]); var Target = AsBox(Bounds[Route.TargetId]);
                bool Reassign = MovedIds.Count == 0 ? SharedLinks.Contains(Route.EdgeId)
                    : MovedIds.Contains(Route.SourceId) || MovedIds.Contains(Route.TargetId);
                string Exit = Reassign ? FacingSide(Source, Target) : Route.ExitSide;
                string Entry = Reassign ? FacingSide(Target, Source) : Route.EntrySide;
                Route.ExitSide = Exit; Route.EntrySide = Entry;
                Ends.Add(new() { Route = Route, IsSource = true, Side = Exit, Box = Source, Other = Target });
                Ends.Add(new() { Route = Route, IsSource = false, Side = Entry, Box = Target, Other = Source });
            }
            foreach (var Side in Ends.GroupBy(End => (End.Box.Id,
                         Side: State.Nodes.TryGetValue(End.Box.Id, out var Node) && FlowchartDiamondAttachHelper.IsDiamondShape(Node.Shape)
                             ? "diamond" : End.Side)))
                ApplySlots(Side.OrderBy(End => End.Side is "left" or "right" ? -End.Other.CenterY : End.Other.CenterX).ToList(),
                    State.Nodes, State.Contract, State.Direction);
            var ChangedRoutes = Layout.EdgeRoutes.Where(Route =>
            {
                var Saved = OriginalPaths.First(Path => Path.Route == Route);
                return MovedIds.Contains(Route.SourceId) || MovedIds.Contains(Route.TargetId)
                    || Route.ExitSide != Saved.Exit || Route.EntrySide != Saved.Entry
                    || Math.Abs(Route.Points[0].X - Saved.Points[0].X) > 0.001
                    || Math.Abs(Route.Points[0].Y - Saved.Points[0].Y) > 0.001
                    || Math.Abs(Route.Points[^1].X - Saved.Points[^1].X) > 0.001
                    || Math.Abs(Route.Points[^1].Y - Saved.Points[^1].Y) > 0.001;
            }).ToArray();
            if (ChangedRoutes.Length > FlowchartLayoutRules.RepairChainLines) return;
            foreach (var Route in ChangedRoutes)
            {
                var Points = DiagramGridRouteBuilder.BuildRoute(Route.Points[0].X, Route.Points[0].Y,
                    Bounds[Route.SourceId], State.Nodes[Route.SourceId].Shape, Route.ExitSide,
                    Route.Points[^1].X, Route.Points[^1].Y, Bounds[Route.TargetId], State.Nodes[Route.TargetId].Shape,
                    Route.EntrySide, Bounds, Route.SourceId, Route.TargetId, State.Contract, State.Direction, State.Horizontal,
                    ExitNormal: (Route.ExitNormalX, Route.ExitNormalY), EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
                Route.Points.Clear();
                foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
            // Evaluate the complete built assignment, including its track order,
            // rather than rejecting a placement because generic independent
            // route construction happened to choose one shared track.
            SearchJointChannels(State, Bounds, IncludeNodeOrder: false);
            var TrialPlaced = State.Placed.Select(Node => new GridNode { Id = Node.Id, Shape = Node.Shape,
                GeometryProvider = Node.GeometryProvider,
                GroupId = Node.GroupId, X = Bounds[Node.Id].CenterX, Y = Bounds[Node.Id].CenterY,
                Width = Node.Width, Height = Node.Height }).ToList();
            FlowchartDistinctTrackRepair.Apply(State.Contract, Layout, TrialPlaced);
            if (OriginalPaths.Count(Path => Path.Route.ExitSide != Path.Exit || Path.Route.EntrySide != Path.Entry
                    || !Path.Route.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Path.Points))
                > FlowchartLayoutRules.RepairChainLines) return;
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
            bool Legal = !Findings.Any(Finding => Finding.Rule is "10" or "5.1")
                && new[] { "3.1", "4.1", "4.2", "4.9", "3.2/4.7", "3.3/5.2", "4.11", "5.8" }.All(Rule =>
                    Findings.Count(Finding => Finding.Rule == Rule) <= Before.Count(Finding => Finding.Rule == Rule))
                && (Findings.Count(Finding => Finding.Rule == "5.9") <= Before.Count(Finding => Finding.Rule == "5.9")
                    || IncludeSideFaults && Findings.Count(Finding => Finding.Rule is "4.11" or "5.8") < Before.Count(Finding => Finding.Rule is "4.11" or "5.8"));
            FlowchartRenderContext.ReportOperationCount("joint-placement-assignments", 1);
            RouteRepairCandidateObserver?.Invoke("joint-placement",
                $"nodes={Centres.Length};illegal={Findings.Count(Finding => Finding.Rule == "10")};shared={Findings.Count(Finding => Finding.Rule == "5.1")}", 0, Legal);
            if (!Legal) return;
            var Score = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false);
            int SideFaults = IncludeSideFaults ? Findings.Count(Finding => Finding.Rule is "4.11" or "5.8") : 0;
            if (Score.Overlaps != 0 || SideFaults > BestSideFaults
                || SideFaults == BestSideFaults && Score.Cost >= BestScore) return;
            BestSideFaults = SideFaults;
            BestScore = Score.Cost;
            BestBounds = Layout.NodeBounds.ToArray();
            BestPaths = Layout.EdgeRoutes.Select(Route => new JointChannelPath(Route,
                Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
            BestNormals = Layout.EdgeRoutes.Select(Route => (Route, Route.ExitNormalX, Route.ExitNormalY,
                Route.EntryNormalX, Route.EntryNormalY)).ToArray();
        }

        // <summary>Exposes a final node's four sides to the shared port allocator.</summary>
        // <param name="Bounds">The proposed node rectangle.</param>
        // <returns>An upward-Y outline with unchanged node identity and dimensions.</returns>
        BoxRef AsBox(FlowchartLayoutNodeBounds Bounds) => new() { Id = Bounds.NodeId, CenterX = Bounds.CenterX,
            CenterY = Bounds.CenterY, Width = Bounds.Width, Height = Bounds.Height, Left = Bounds.CenterX - Bounds.Width / 2,
            Right = Bounds.CenterX + Bounds.Width / 2, Bottom = Bounds.CenterY - Bounds.Height / 2, Top = Bounds.CenterY + Bounds.Height / 2 };
    }

    /// <summary>Installs an entire channel option before either legality or cost is evaluated.</summary>
    /// <param name="Paths">All routes belonging to this atomic assignment.</param>
    private static void Install(IEnumerable<JointChannelPath> Paths)
    {
        foreach (var Path in Paths)
        {
            Path.Route.Points.Clear();
            foreach (var Point in Path.Points) Path.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            Path.Route.ExitSide = Path.Exit; Path.Route.EntrySide = Path.Entry;
            Path.Route.ExitNormalX = Path.Exit == "right" ? 1 : Path.Exit == "left" ? -1 : 0;
            Path.Route.ExitNormalY = Path.Exit == "top" ? 1 : Path.Exit == "bottom" ? -1 : 0;
            Path.Route.EntryNormalX = Path.Entry == "right" ? 1 : Path.Entry == "left" ? -1 : 0;
            Path.Route.EntryNormalY = Path.Entry == "top" ? 1 : Path.Entry == "bottom" ? -1 : 0;
        }
    }
}
