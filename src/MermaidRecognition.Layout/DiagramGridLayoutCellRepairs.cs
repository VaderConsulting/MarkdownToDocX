using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    private static partial class RouteRepairStage
    {
        /// <summary>Scores complete whole-cell endpoint and group moves against the current legal routing option.</summary>
        /// <param name="State">Diagram with cell-bound nodes and the current exact route geometry.</param>
        /// <param name="CandidateLimit">Remaining named candidate-move budget.</param>
        /// <param name="MoveLimit">Remaining named accepted-chain budget.</param>
        /// <param name="Attempted">Cell assignments already examined during this layout.</param>
        /// <returns>Accepted move count and complete candidates examined.</returns>
        private static (int Moved, int Examined) MoveBeforeDetourCells(GridLayoutState State,
            int CandidateLimit, int MoveLimit, HashSet<string> Attempted) =>
            SearchCellAssignments(State, MoveCellOptions(State), CandidateLimit, MoveLimit, Attempted, PreserveBends: true);

        /// <summary>One already validated complete target state; no callback can construct targets while changing live geometry.</summary>
        /// <param name="Key">Deterministic assignment identity.</param>
        /// <param name="Subject">Endpoint or pair being repaired.</param>
        /// <param name="Assignment">Complete immutable regions and gaps, including displaced neighbours.</param>
        private sealed record CellRepairOption(string Key, string Subject, FlowchartCellRepair.Assignment Assignment);

        /// <summary>Generates compass moves of declared regions within the existing chain-depth limit.</summary>
        /// <param name="State">The restored complete layout.</param>
        /// <returns>Whole-cell endpoint or group options, without pixel positions as inputs.</returns>
        private static IEnumerable<CellRepairOption> MoveCellOptions(GridLayoutState State)
        {
            var Units = State.Copy.EdgeRoutes.Where(Route => Route.Points.Count < 2
                    || FlowchartLayoutScore.CountCorners(Route.Points) > 0)
                .SelectMany(Route => new[] { Route.SourceId, Route.TargetId }).Distinct(StringComparer.Ordinal).ToArray();
            using var Existing = ExistingCellMoveOptions(State, Units).GetEnumerator();
            using var Joint = CellJointRouteAssignments(State.Contract, State.Placed, State.Horizontal,
                Units.ToHashSet(StringComparer.Ordinal)).GetEnumerator();
            var Deferred = new List<CellRepairOption>();
            CellRepairOption? CurrentJoint = null;
            bool MoreExisting = true, MoreJoint = true;
            while (MoreExisting || MoreJoint)
            {
                if (MoreExisting = Existing.MoveNext()) yield return Existing.Current;
                if (MoreJoint = NextUsefulJoint()) yield return CurrentJoint!;
            }
            foreach (var Option in Deferred) yield return Option;

            bool NextUsefulJoint()
            {
                while (Joint.MoveNext())
                {
                    var Option = new CellRepairOption("joint|" + Joint.Current.Key, Joint.Current.Subject, Joint.Current.Assignment);
                    if (CanStraightenAllocatedCellRoute(State, Option.Assignment)) { CurrentJoint = Option; return true; }
                    Deferred.Add(Option);
                }
                return false;
            }
        }

        /// <summary>Orders joint options by a concrete straight-line opportunity without deriving or changing any region from pixels.</summary>
        /// <param name="State">Original allocated terminals and complete obstacle geometry.</param>
        /// <param name="Assignment">Unapplied whole-cell target state.</param>
        /// <returns>Whether every changed interface can stay straight and at least one existing bend can be removed.</returns>
        private static bool CanStraightenAllocatedCellRoute(GridLayoutState State, FlowchartCellRepair.Assignment Assignment)
        {
            // Promote a complete interface, not one attractive connector while
            // another previously straight line becomes bent. Other proposals
            // remain available later; this never accepts an estimated score.
            var Before = State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            var After = new Dictionary<string, FlowchartLayoutNodeBounds>(Before, StringComparer.Ordinal);
            foreach (var Node in Assignment.Nodes)
            {
                if (HasHintedContainingGroup(State.Contract, Node.Id) || !string.IsNullOrEmpty(Node.GroupId)) return false;
                After[Node.Id] = Node.CellNode!.Bounds(Assignment.After.Regions[Node.Id], Node.GeometryProvider, Assignment.After);
            }
            bool Gain = false;
            foreach (var Route in State.Copy.EdgeRoutes)
            {
                if (Route.Points.Count < 2 || !Before.TryGetValue(Route.SourceId, out var OldSource)
                    || !Before.TryGetValue(Route.TargetId, out var OldTarget)) return false;
                var NewSource = After[Route.SourceId]; var NewTarget = After[Route.TargetId];
                if (NewSource.Width != OldSource.Width || NewSource.Height != OldSource.Height
                    || NewTarget.Width != OldTarget.Width || NewTarget.Height != OldTarget.Height) return false;
                var SourceDelta = (X: NewSource.CenterX - OldSource.CenterX, Y: NewSource.CenterY - OldSource.CenterY);
                var TargetDelta = (X: NewTarget.CenterX - OldTarget.CenterX, Y: NewTarget.CenterY - OldTarget.CenterY);
                if (SourceDelta == TargetDelta) continue;
                var Start = (X: Route.Points[0].X + SourceDelta.X, Y: Route.Points[0].Y + SourceDelta.Y);
                var End = (X: Route.Points[^1].X + TargetDelta.X, Y: Route.Points[^1].Y + TargetDelta.Y);
                var Candidate = new FlowchartLayoutEdgeRoute { EdgeId = Route.EdgeId, SourceId = Route.SourceId, TargetId = Route.TargetId,
                    ExitSide = Route.ExitSide, EntrySide = Route.EntrySide, ExitNormalX = Route.ExitNormalX, ExitNormalY = Route.ExitNormalY,
                    EntryNormalX = Route.EntryNormalX, EntryNormalY = Route.EntryNormalY };
                Candidate.Points.Add(new() { X = Start.X, Y = Start.Y }); Candidate.Points.Add(new() { X = End.X, Y = End.Y });
                bool Straight = (Math.Abs(Start.X - End.X) <= 0.001 || Math.Abs(Start.Y - End.Y) <= 0.001)
                    && FlowchartAllocatedPortFallback.IsLegal(State.Contract, After, Candidate, new[] { Start, End });
                if (!Straight)
                {
                    string Exit = ModelRouteCompletionStage.Facing(NewSource, NewTarget);
                    string Entry = ModelRouteCompletionStage.Facing(NewTarget, NewSource);
                    var Hint = FindContractEdge(State.Contract, Route)?.RoutingHint;
                    if (Hint?.IsActive == true && (Hint.Travel.HasValue
                        || Hint.Leave.HasValue && HintSide(Hint.Leave.Value) != Exit
                        || Hint.Enter.HasValue && HintSide(Hint.Enter.Value) != Entry)) return false;
                    if (ProposedSideCount(Route.SourceId, Exit) != 1 || ProposedSideCount(Route.TargetId, Entry) != 1) return false;
                    var SourceGeometry = State.Nodes.GetValueOrDefault(Route.SourceId)?.GeometryProvider?.Create(NewSource.Width, NewSource.Height)
                        ?? NodeGeometry.Rectangle(NewSource.Width, NewSource.Height);
                    var TargetGeometry = State.Nodes.GetValueOrDefault(Route.TargetId)?.GeometryProvider?.Create(NewTarget.Width, NewTarget.Height)
                        ?? NodeGeometry.Rectangle(NewTarget.Width, NewTarget.Height);
                    if (!SourceGeometry.ConnectionBoundary.TryPort(Exit, 0.5, out var SourcePort)
                        || !TargetGeometry.ConnectionBoundary.TryPort(Entry, 0.5, out var TargetPort)) return false;
                    Start = (NewSource.CenterX + SourcePort.X, NewSource.CenterY - SourcePort.Y);
                    End = (NewTarget.CenterX + TargetPort.X, NewTarget.CenterY - TargetPort.Y);
                    if (Math.Abs(Start.X - End.X) > 0.001 && Math.Abs(Start.Y - End.Y) > 0.001) return false;
                    Candidate.ExitSide = Exit; Candidate.EntrySide = Entry;
                    Candidate.ExitNormalX = SourcePort.NormalX; Candidate.ExitNormalY = -SourcePort.NormalY;
                    Candidate.EntryNormalX = TargetPort.NormalX; Candidate.EntryNormalY = -TargetPort.NormalY;
                    Candidate.Points.Clear(); Candidate.Points.Add(new() { X = Start.X, Y = Start.Y }); Candidate.Points.Add(new() { X = End.X, Y = End.Y });
                    if (!FlowchartAllocatedPortFallback.IsLegal(State.Contract, After, Candidate, new[] { Start, End })) return false;
                }
                Gain |= FlowchartLayoutScore.CountCorners(Route.Points) > 0;
            }
            return Gain;

            // A spread fan frees centre slots by moving neighbours to other
            // sides. Compare the proposed inventory, not its superseded slots.
            int ProposedSideCount(string Node, string Side) => State.Copy.EdgeRoutes.Sum(Route =>
                (Route.SourceId == Node && After.ContainsKey(Route.TargetId)
                    && ModelRouteCompletionStage.Facing(After[Node], After[Route.TargetId]) == Side ? 1 : 0)
                + (Route.TargetId == Node && After.ContainsKey(Route.SourceId)
                    && ModelRouteCompletionStage.Facing(After[Node], After[Route.SourceId]) == Side ? 1 : 0));
        }
        /// <summary>Retains the existing endpoint, tail, terminal and branch translations alongside joint alternatives.</summary>
        /// <param name="State">Restored source model.</param>
        /// <param name="Units">Endpoints nominated by actual bent or missing routes.</param>
        /// <returns>Previously available complete assignments in their original order.</returns>
        private static IEnumerable<CellRepairOption> ExistingCellMoveOptions(GridLayoutState State, IEnumerable<string> Units)
        {
            foreach (var Candidate in FlowchartJunctionBranchCompaction.CellCandidates(State.Contract, State.Placed, State.Horizontal)
                .Concat(FlowchartTerminalRowPlacement.CellCandidates(State.Contract, State.Placed, State.Horizontal))
                .Concat(CellRouteMoveCandidates(State.Contract, State.Placed, State.Horizontal, Units)))
            {
                var Plan = FlowchartCellRepair.PlanMove(Candidate.Nodes, Candidate.Rows, Candidate.Columns)
                    ?? FlowchartCellRepair.PlanDisplacement(State.Placed, Candidate.Nodes, Candidate.Rows, Candidate.Columns);
                if (Plan != null) yield return new("move|" + Candidate.Key, Candidate.Subject, Plan);
            }
        }
        /// <summary>Offers atomic region swaps for the existing settled side, crossing and shared-track search.</summary>
        /// <param name="State">The complete cell-bound model.</param>
        /// <param name="IncludeSideFaults">Whether facing-side faults nominate nodes.</param>
        /// <param name="IncludeCrossings">Whether crossing faults nominate nodes.</param>
        internal static void SearchCellNodeOrder(GridLayoutState State, bool IncludeSideFaults, bool IncludeCrossings)
        {
            if (State.Contract.Groups.Count != 0 || State.Contract.Edges.Any(Edge => Edge.RoutingHint?.IsActive == true)) return;
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            var Links = Findings.Where(Finding => IncludeSideFaults ? Finding.Rule is "4.11" or "5.8"
                    : IncludeCrossings ? Finding.Rule == "5.9" : Finding.Rule == "5.1")
                .SelectMany(Finding => Finding.Subject.Split(" x ", StringSplitOptions.None)).ToHashSet(StringComparer.Ordinal);
            if (Links.Count == 0 || IncludeCrossings && Links.Count > FlowchartLayoutRules.RepairChainLines) return;
            var Ids = State.Copy.EdgeRoutes.Where(Route => Links.Contains(Route.EdgeId))
                .SelectMany(Route => new[] { Route.SourceId, Route.TargetId }).ToHashSet(StringComparer.Ordinal);
            var Nodes = State.Placed.Where(Node => Ids.Contains(Node.Id)).OrderBy(Node => Node.Id, StringComparer.Ordinal).ToArray();
            SearchCellAssignments(State, SwapCellOptions(Nodes), FlowchartLayoutRules.RepairCandidateMoves,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves, State.MoveBudget.AttemptedMoves);
        }

        /// <summary>Repairs frame intersections with complete whole-cell group translations.</summary>
        /// <param name="State">Cell-bound group members and their derived frames.</param>
        /// <returns>Whether one complete option reduced the measured frame-overlap shortfall.</returns>
        internal static bool RepairCellFrameOverlap(GridLayoutState State) => SearchCellAssignments(State,
            FrameCellOptions(State), FlowchartLayoutRules.RepairCandidateMoves,
            FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves, State.MoveBudget.AttemptedMoves,
            () => CountUnrelatedFrameOverlaps(State.Contract, State.Frames)).Moved > 0;

        /// <summary>Offers measured shared-gap growth and whole-cell moves before retaining an undersized channel.</summary>
        /// <param name="State">The complete cell-bound channel geometry.</param>
        /// <returns>Whether one complete assignment reduced its measured clearance shortfall or total score.</returns>
        internal static bool RepairCellChannelSpacing(GridLayoutState State) => SearchCellAssignments(State,
            GapCellOptions(State).Concat(MoveCellOptions(State)), FlowchartLayoutRules.RepairCandidateMoves,
            FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves, State.MoveBudget.AttemptedMoves,
            () => CellGapShortfalls(State).Sum(Gap => Gap.Missing)).Moved > 0;

        /// <summary>Offers the atomic external-gap fallback after fixed-port track and joint-slot options.</summary>
        /// <param name="State">Final cell model, before quality-only placement spends the remaining candidate budget.</param>
        /// <param name="Bounds">Current complete geometry, refreshed after accepted growth.</param>
        internal static void RepairCellTerminalSpacing(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
        {
            RepairTerminalPortTracks(State, Bounds);
            if (!FlowchartTerminalTrackRepair.Conflicts(State.Copy).Any()) return;
            var Result = SearchCellAssignments(State, GapCellOptions(State), FlowchartLayoutRules.RepairCandidateMoves,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves, State.MoveBudget.AttemptedMoves,
                () => CellGapShortfalls(State).Sum(Gap => Gap.Missing), PreserveBends: true);
            if (Result.Moved == 0) return;
            foreach (var Box in State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds)) Bounds[Box.NodeId] = Box;
            FlowchartTerminalTrackRepair.Observer?.Invoke("gap", "accepted", Result.Moved.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>One genuine shared gap with missing terminal or track room measured on its allocated geometry.</summary>
        /// <param name="Horizontal">True for column gaps, false for row gaps.</param>
        /// <param name="After">Cell index preceding the gap.</param>
        /// <param name="Missing">Additional measured clearance in pixels.</param>
        private sealed record CellGap(bool Horizontal, int After, double Missing);

        /// <summary>Measures actual allocated channel requirements without inferring any node's reservation from its centre.</summary>
        /// <param name="State">Current declared regions and allocated ports.</param>
        /// <returns>Missing clearance grouped by the explicit shared gap it belongs to.</returns>
        private static IEnumerable<CellGap> CellGapShortfalls(GridLayoutState State)
        {
            var Nodes = State.Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            // Off-centre terminals can compete for the same real corridor even
            // though neither endpoint qualifies for the older centre-port rule.
            foreach (var Pair in FlowchartTerminalTrackRepair.Conflicts(State.Copy))
            {
                var A = Pair.First; var B = Pair.Second;
                if (A.Owner == B.Owner || !Nodes.TryGetValue(A.Owner, out var First)
                    || !Nodes.TryGetValue(B.Owner, out var Second)) continue;
                var S = First.CellNode?.Region; var T = Second.CellNode?.Region;
                if (S == null || T == null) continue;
                bool Horizontal = A.Horizontal;
                int End = Horizontal ? S.EndColumn : S.EndRow;
                int Start = Horizontal ? T.StartColumn : T.StartRow;
                int ReverseEnd = Horizontal ? T.EndColumn : T.EndRow;
                int ReverseStart = Horizontal ? S.StartColumn : S.StartRow;
                int After = End <= Start ? End - 1 : ReverseEnd <= ReverseStart ? ReverseEnd - 1 : int.MinValue;
                if (After == int.MinValue || State.Placed.Any(Node =>
                    (Horizontal ? Node.CellNode!.Region.StartColumn : Node.CellNode!.Region.StartRow) <= After
                    && After < (Horizontal ? Node.CellNode!.Region.EndColumn : Node.CellNode!.Region.EndRow) - 1)) continue;
                double Required = 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridMetrics.GRID_UNIT_PX;
                double Available = Math.Abs(A.Port - B.Port);
                if (Available < Required) yield return new(Horizontal, After, Required - Available);
            }
            var Links = State.Copy.EdgeRoutes.Where(Route => Route.Points.Count > 2
                && FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive != true).ToArray();
            var Back = FlowchartBackEdgeHelper.FindBackEdges(State.Contract);
            foreach (var Route in Links)
            {
                if (Back.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                    || !Nodes.TryGetValue(Route.SourceId, out var Source) || !Nodes.TryGetValue(Route.TargetId, out var Target)) continue;
                bool Horizontal = Route.ExitSide is "left" or "right";
                if (Route.EntrySide != (Route.ExitSide switch { "left" => "right", "right" => "left", "top" => "bottom", _ => "top" })) continue;
                if (!AtConnectionCentre(Source, Route.ExitSide, Route.Points[0])
                    || !AtConnectionCentre(Target, Route.EntrySide, Route.Points[^1])) continue;
                var S = Source.CellNode!.Region; var T = Target.CellNode!.Region;
                int FirstEnd = Horizontal ? S.EndColumn : S.EndRow;
                int SecondEnd = Horizontal ? T.EndColumn : T.EndRow;
                int FirstStart = Horizontal ? S.StartColumn : S.StartRow;
                int SecondStart = Horizontal ? T.StartColumn : T.StartRow;
                int After;
                if (FirstEnd <= SecondStart) After = FirstEnd - 1;
                else if (SecondEnd <= FirstStart) After = SecondEnd - 1;
                else continue;
                // Inserting an internal gap enlarges a spanning node but creates no corridor there.
                if (State.Placed.Any(Node =>
                {
                    var Region = Node.CellNode!.Region;
                    return (Horizontal ? Region.StartColumn : Region.StartRow) <= After
                        && After < (Horizontal ? Region.EndColumn : Region.EndRow) - 1;
                })) continue;
                double Available = Horizontal ? Math.Abs(Route.Points[^1].X - Route.Points[0].X)
                    : Math.Abs(Route.Points[^1].Y - Route.Points[0].Y);
                double Required = 3 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridRouteBuilder.ARROWHEAD_LENGTH_PX;
                if (Available < Required) yield return new(Horizontal, After, Required - Available);
            }
        }

        /// <summary>Tries legal joint slot allocation only after bend-track shortening has been exhausted.</summary>
        /// <param name="State">Complete candidate; its ownership and caches are restored on rejection.</param>
        /// <param name="Bounds">Current immutable endpoint geometry.</param>
        /// <param name="OnlyRoutes">Routes allowed to change in an incremental candidate.</param>
        internal static void RepairTerminalPortTracks(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds, IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
        {
            FlowchartTerminalTrackRepair.Apply(State.Contract, State.Copy, State.Placed, Bounds, OnlyRoutes);
            var Pairs = FlowchartTerminalTrackRepair.Conflicts(State.Copy).ToArray();
            if (Pairs.Length == 0 || State.Placed.All(Node => Node.CellNode == null)) return;
            var Owners = Pairs.SelectMany(Pair => new[] { Pair.First.Owner, Pair.Second.Owner }).ToHashSet(StringComparer.Ordinal);
            var Saved = new CellGeometrySnapshot(State);
            var BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            int BeforeBends = RouteBends(State.Copy);
            int BeforeCrossings = TerminalCrossings(State.Copy);
            bool Accepted = false;
            try
            {
                ModelRouteCompletionStage.AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, State.Nodes, Owners);
                var Affected = Saved.ChangedTerminalRoutes().ToHashSet();
                if (Affected.Count == 0 || Affected.Count > FlowchartLayoutRules.RepairChainLines
                    || OnlyRoutes != null && Affected.Any(Route => !OnlyRoutes.Contains(Route))) return;
                DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes,
                    State.Contract, State.Frames, State.Direction, Affected);
                FlowchartTerminalTrackRepair.Apply(State.Contract, State.Copy, State.Placed, Bounds, Affected);
                Accepted = FlowchartTerminalTrackRepair.Conflicts(State.Copy).Count() < Pairs.Length
                    && RouteBends(State.Copy) <= BeforeBends && TerminalCrossings(State.Copy) <= BeforeCrossings
                    && NoNewFindings(BeforeFindings, FlowchartRouteRuleChecker.Check(State.Contract, State.Copy));
                if (Accepted) foreach (var Pair in Pairs)
                    FlowchartTerminalTrackRepair.Observer?.Invoke("slots", Pair.First.Route.EdgeId, Pair.Second.Route.EdgeId);
            }
            finally { if (!Accepted) Saved.Restore(State); }
        }

        /// <summary>Counts crossings on exactly the completed route geometry being scored.</summary>
        /// <param name="Layout">Private completed candidate.</param>
        /// <returns>Geometric crossings, including routes with common graph endpoints.</returns>
        private static int TerminalCrossings(FlowchartLayoutResult Layout)
        {
            var Paths = Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            int Count = 0;
            for (int I = 0; I < Paths.Length; I++) for (int J = I + 1; J < Paths.Length; J++)
                Count += FlowchartBranchRouteOrder.Intersections(Paths[I], Paths[J]).Crossings;
            return Count;
        }

        /// <summary>Checks the renderer's connection-edge centre, including a body inset, rather than the visual box centre.</summary>
        /// <param name="Node">Cell-derived node geometry.</param>
        /// <param name="Side">Allocated side.</param>
        /// <param name="Point">Allocated world-space terminal.</param>
        /// <returns>Whether this is the centre port eligible for gap growth under rule 1.7.</returns>
        private static bool AtConnectionCentre(GridNode Node, string Side, FlowchartLayoutPoint Point)
        {
            var Geometry = Node.GeometryProvider?.Create(Node.Width, Node.Height) ?? NodeGeometry.Rectangle(Node.Width, Node.Height);
            return Geometry.ConnectionBoundary.TryPort(Side, 0.5, out var Port)
                && Math.Abs(Point.X - Node.X - Port.X) < 0.001 && Math.Abs(Node.Y - Point.Y - Port.Y) < 0.001;
        }

        /// <summary>Builds complete gap candidates whose node positions and dimensions are all derived anew from the same cells.</summary>
        /// <param name="State">Original complete routing option.</param>
        /// <returns>Measured gap growth options, deduplicated by the actual gap.</returns>
        private static IEnumerable<CellRepairOption> GapCellOptions(GridLayoutState State)
        {
            var Gaps = CellGapShortfalls(State).GroupBy(Gap => (Gap.Horizontal, Gap.After))
                .Select(Group => new CellGap(Group.Key.Horizontal, Group.Key.After, Group.Max(Gap => Gap.Missing))).ToArray();
            foreach (var Gap in Gaps)
            {
                var Plan = FlowchartCellRepair.PlanAssignment(State.Placed, new Dictionary<string, FlowchartCellRegion>(),
                    [new(Gap.Horizontal, Gap.After, Gap.Missing, DiagramGridMetrics.GRID_UNIT_PX)]);
                if (Plan != null) yield return new("gap|" + Gap.Horizontal + "|" + Gap.After, "gap " + Gap.After, Plan);
            }
        }

        /// <summary>Generates outside placements from group members' declared cell extents, never frame-centre coordinates.</summary>
        /// <param name="State">The original complete group arrangement.</param>
        /// <returns>Atomic complete group translations in each page compass direction.</returns>
        private static IEnumerable<CellRepairOption> FrameCellOptions(GridLayoutState State)
        {
            var Frames = State.Frames.ToArray();
            for (int First = 0; First < Frames.Length; First++)
            for (int Second = 0; Second < Frames.Length; Second++)
            {
                if (First == Second) continue;
                var A = Frames[First]; var B = Frames[Second];
                if (IsInsideGroup(State.Contract, A.Id, B.Id) || IsInsideGroup(State.Contract, B.Id, A.Id)
                    || A.Left >= B.Right || B.Left >= A.Right || A.Bottom >= B.Top || B.Bottom >= A.Top) continue;
                var Moving = State.Placed.Where(Node => IsInsideGroup(State.Contract, Node.Id, A.Id)).ToArray();
                var Fixed = State.Placed.Where(Node => IsInsideGroup(State.Contract, Node.Id, B.Id)).ToArray();
                if (Moving.Length == 0 || Fixed.Length == 0) continue;
                int Top = Moving.Min(Node => Node.CellNode!.Region.StartRow), Bottom = Moving.Max(Node => Node.CellNode!.Region.EndRow);
                int Left = Moving.Min(Node => Node.CellNode!.Region.StartColumn), Right = Moving.Max(Node => Node.CellNode!.Region.EndColumn);
                int OtherTop = Fixed.Min(Node => Node.CellNode!.Region.StartRow), OtherBottom = Fixed.Max(Node => Node.CellNode!.Region.EndRow);
                int OtherLeft = Fixed.Min(Node => Node.CellNode!.Region.StartColumn), OtherRight = Fixed.Max(Node => Node.CellNode!.Region.EndColumn);
                foreach (var Delta in new[] { (Rows: -Math.Max(1, Bottom - OtherTop + 1), Columns: 0),
                    (Rows: Math.Max(1, OtherBottom - Top + 1), Columns: 0),
                    (Rows: 0, Columns: -Math.Max(1, Right - OtherLeft + 1)),
                    (Rows: 0, Columns: Math.Max(1, OtherRight - Left + 1)) })
                {
                    var Plan = FlowchartCellRepair.PlanMove(Moving, Delta.Rows, Delta.Columns)
                        ?? FlowchartCellRepair.PlanDisplacement(State.Placed, Moving, Delta.Rows, Delta.Columns);
                    if (Plan != null) yield return new("frame|" + A.Id + "|" + B.Id + "|" + Delta, A.Id, Plan);
                }
            }
        }

        /// <summary>Exchanges start cells while preserving each node's own spans; shared final cells reject the whole option.</summary>
        /// <param name="Nodes">Nodes nominated by the complete layout's findings.</param>
        /// <returns>Deterministic complete pair assignments.</returns>
        private static IEnumerable<CellRepairOption> SwapCellOptions(GridNode[] Nodes)
        {
            for (int First = 0; First < Nodes.Length; First++)
            for (int Second = First + 1; Second < Nodes.Length; Second++)
            {
                var A = Nodes[First]; var B = Nodes[Second];
                var Pair = new[] { A, B };
                var Ar = A.CellNode!.Region; var Br = B.CellNode!.Region;
                var Plan = FlowchartCellRepair.PlanAssignment(Pair, new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal)
                    {
                        [A.Id] = new(Br.StartRow, Br.StartColumn, Ar.RowSpan, Ar.ColumnSpan),
                        [B.Id] = new(Ar.StartRow, Ar.StartColumn, Br.RowSpan, Br.ColumnSpan)
                    });
                if (Plan != null) yield return new("swap|" + A.Id + "|" + B.Id, A.Id + " x " + B.Id, Plan);
            }
        }

        /// <summary>Builds and scores complete region candidates with one shared legality and exact-rollback path.</summary>
        /// <param name="State">Cell-bound diagram.</param>
        /// <param name="Options">Region operations; no operation reconstructs occupancy from a pixel coordinate.</param>
        /// <param name="CandidateLimit">Remaining named candidate budget.</param>
        /// <param name="MoveLimit">Remaining named chain budget.</param>
        /// <param name="Attempted">Assignments already tried for this exact cell geometry.</param>
        /// <param name="Shortfall">Optional measured mandatory shortfall; reducing it takes precedence over a quality-only score improvement.</param>
        /// <param name="PreserveBends">Whether straightening rejects added bends instead of trading them for a different diagram shape.</param>
        /// <returns>Accepted option count and candidates examined.</returns>
        private static (int Moved, int Examined) SearchCellAssignments(GridLayoutState State,
            IEnumerable<CellRepairOption> Options, int CandidateLimit, int MoveLimit, HashSet<string> Attempted,
            Func<double>? Shortfall = null, bool PreserveBends = false)
            => SearchCellAssignmentsCore(State, Options, CandidateLimit, MoveLimit, Attempted, Shortfall, PreserveBends, false);

        /// <summary>Ranks scoped candidates and recovers the same examined assignments with reference completion when no scoped winner survives.</summary>
        /// <param name="State">Live cell model restored between assignments.</param>
        /// <param name="Options">Immutable assignments, including the exact examined set during recovery.</param>
        /// <param name="CandidateLimit">Existing distinct-assignment budget.</param>
        /// <param name="MoveLimit">Existing accepted-move budget.</param>
        /// <param name="Attempted">Previously examined assignment identities.</param>
        /// <param name="Shortfall">Mandatory clearance cost, if applicable.</param>
        /// <param name="PreserveBends">Whether additional bends disqualify a move.</param>
        /// <param name="ReferenceCompletion">Whether to recheck already charged assignments using the complete reference router.</param>
        /// <returns>The accepted move count and number of distinct examined assignments.</returns>
        private static (int Moved, int Examined) SearchCellAssignmentsCore(GridLayoutState State,
            IEnumerable<CellRepairOption> Options, int CandidateLimit, int MoveLimit, HashSet<string> Attempted,
            Func<double>? Shortfall, bool PreserveBends, bool ReferenceCompletion)
        {
            // Provisional route construction is not the option that reaches
            // the painter. Defer cell quality moves until that final model is
            // available, and never recursively place while scoring a trial.
            if (!State.CellFinalRoutingReady || State.Copy.RenderContext.IsEvaluatingCellRouteCandidate) return (0, 0);
            if (!ReferenceCompletion) CandidateLimit = Math.Min(CandidateLimit,
                FlowchartLayoutRules.RepairCandidateMoves - State.MoveBudget.CandidateMoves);
            MoveLimit = Math.Min(MoveLimit,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves);
            if (CandidateLimit <= 0 || MoveLimit <= 0) return (0, 0);
            using var CostTiming = FlowchartCellCostTiming.Begin("final-layout-scoring");
            var Original = new CellGeometrySnapshot(State);
            // When the whole routing inventory fits the named coupled-line
            // bound, retain its exact joint search. Freezing even one of those
            // few interacting tracks can select a different, worse move.
            bool WholeRouteSet = State.Copy.EdgeRoutes.Count <= FlowchartLayoutRules.RepairChainLines;
            var Before = FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false);
            var IncrementalScore = new FlowchartLayoutScore.CellMovementContext(State.Contract, State.Copy);
            var BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            bool NeedsHardRepair = BeforeFindings.Any(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7");
            int BeforeFrameOverlaps = CountUnrelatedFrameOverlaps(State.Contract, State.Frames);
            double BeforeLength = RouteLength(State.Copy);
            int BeforeBends = RouteBends(State.Copy);
            var BestScore = Before.Cost;
            double BestShortfall = Shortfall?.Invoke() ?? 0;
            double InitialShortfall = BestShortfall;
            CellGeometrySnapshot? Best = null;
            HashSet<FlowchartLayoutEdgeRoute>? BestAffected = null;
            int Examined = 0;
            string? Winner = null;
            string? WinnerKey = null;
            var ExaminedOptions = new List<CellRepairOption>();
            var CellState = FlowchartCellRepair.Capture(State.Placed);
            string Signature = System.Text.Json.JsonSerializer.Serialize(new
            {
                Regions = CellState.Regions.OrderBy(Pair => Pair.Key, StringComparer.Ordinal).Select(Pair => new
                {
                    Pair.Key, Pair.Value.StartRow, Pair.Value.StartColumn,
                    Pair.Value.RowSpan, Pair.Value.ColumnSpan
                }),
                Columns = CellState.ColumnGaps.OrderBy(Pair => Pair.Key), Rows = CellState.RowGaps.OrderBy(Pair => Pair.Key)
            });
            var CandidateGrid = State.Placed[0].CellNode!.Grid;
            int SeenBandRejections = CandidateGrid.RegionConventionRejections;
            foreach (var Option in Options)
            {
                int PreflightSpent = SpendBandPreflightSlots(State, ReferenceCompletion ? "reference-recovery" : "cell-candidate-trials",
                    CandidateGrid.RegionConventionRejections - SeenBandRejections, CandidateLimit - Examined);
                Examined += PreflightSpent;
                if (!ReferenceCompletion) State.MoveBudget.CandidateMoves += PreflightSpent;
                SeenBandRejections = CandidateGrid.RegionConventionRejections;
                if (Examined >= CandidateLimit) break;
                var Participants = Option.Assignment.Nodes;
                if (Participants.Count == 0 || Participants.Any(Node => Node.PlacementHintLocked
                    || HasHintedContainingGroup(State.Contract, Node.Id)))
                { if (Option.Key.StartsWith("gap|", StringComparison.Ordinal)) FlowchartTerminalTrackRepair.Observer?.Invoke("gap-rejected", Option.Key, "hint/empty"); continue; }
                if (HasExternalCellHintDependent(State.Contract, Option.Subject, Participants.ToArray())) continue;
                var Ids = Participants.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                if (State.Copy.EdgeRoutes.Count(Route => Ids.Contains(Route.SourceId) || Ids.Contains(Route.TargetId))
                    > FlowchartLayoutRules.RepairChainLines)
                { if (Option.Key.StartsWith("gap|", StringComparison.Ordinal)) FlowchartTerminalTrackRepair.Observer?.Invoke("gap-rejected", Option.Key,
                    "lines=" + State.Copy.EdgeRoutes.Count(Route => Ids.Contains(Route.SourceId) || Ids.Contains(Route.TargetId))); continue; }
                if (!ReferenceCompletion && !Attempted.Add("cells|" + Signature + "|" + CellAssignmentIdentity(Option.Assignment))) continue;
                Examined++;
                if (NeedsHardRepair) ExaminedOptions.Add(Option);
                if (!ReferenceCompletion) State.MoveBudget.CandidateMoves++;
                if (!ReferenceCompletion && FlowchartRenderContext.OperationCountObserver != null)
                    FlowchartRenderContext.ReportOperationCount("cell-candidate/" + (Option.Key.StartsWith("joint|", StringComparison.Ordinal)
                        ? Option.Key.Split('|')[1] : "existing"), 1);
                try
                {
                    if (!Option.Assignment.TryApply()) continue;
                    State.Frames.Clear();
                    BuildFramesYUp(State.Contract, State.Placed, State.Frames);
                    if (CountUnrelatedFrameOverlaps(State.Contract, State.Frames) > BeforeFrameOverlaps) continue;
                    if (BandCandidateIsIllegal(State))
                    { SpendBandPreflightSlots(State, ReferenceCompletion ? "reference-recovery" : "cell-candidate-trials", 1, 1); continue; }
                    WriteBack(State.Copy, State.Placed, State.Frames);
                    HashSet<FlowchartLayoutEdgeRoute>? Affected;
                    using var RoutingPhase = FlowchartCellCostTiming.ForRoutingPhase(
                        ReferenceCompletion ? "recovery" : "candidate-trials");
                    if (ReferenceCompletion || WholeRouteSet)
                    {
                        CompleteReferenceCellCandidate(State);
                        Affected = State.Copy.EdgeRoutes.ToHashSet();
                    }
                    else Affected = CompleteAffectedCellRoutes(State, Original);
                    if (Affected == null) continue;
                    if (!NeedsHardRepair) ExaminedOptions.Add(Option);
                    var AfterFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                    var After = ReferenceCompletion || WholeRouteSet ? FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false)
                        : IncrementalScore.Measure(Affected);
                    double Remaining = Shortfall?.Invoke() ?? 0;
                    double ExtraLength = RouteLength(State.Copy) - BeforeLength;
                    int ExtraBends = RouteBends(State.Copy) - BeforeBends;
                    int ChangedLines = Original.ChangedLines();
                    var Cost = After.Cost + ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                        + ChangedLines * FlowchartLayoutRules.RepairMovedLineWeight;
                    bool Legal = After.Overlaps <= Before.Overlaps && After.HintsHonoured >= Before.HintsHonoured
                        && NoNewFindings(BeforeFindings, AfterFindings) && CountTitleBandRoutes(State) == 0
                        && !AfterFindings.Any(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7")
                        && (!PreserveBends || ExtraBends <= 0)
                        && ChangedLines <= FlowchartLayoutRules.RepairChainLines;
                    RouteRepairCandidateObserver?.Invoke("cell-move", Option.Subject, Cost, Legal);
                    if (!Legal || Remaining > BestShortfall
                        || Remaining == BestShortfall && Cost >= BestScore - 0.001) continue;
                    BestShortfall = Remaining;
                    BestScore = Cost;
                    Best = new(State);
                    BestAffected = Affected;
                    Winner = Option.Subject;
                    WinnerKey = Option.Key;
                }
                finally { Original.Restore(State); SeenBandRejections = CandidateGrid.RegionConventionRejections; }
            }
            int RemainingBandDrops = SpendBandPreflightSlots(State, ReferenceCompletion ? "reference-recovery" : "cell-candidate-trials",
                CandidateGrid.RegionConventionRejections - SeenBandRejections, CandidateLimit - Examined);
            Examined += RemainingBandDrops;
            if (!ReferenceCompletion) State.MoveBudget.CandidateMoves += RemainingBandDrops;
            (Best ?? Original).Restore(State);
            if (Best == null) return RecoverReferenceCandidates();
            if (ReferenceCompletion || WholeRouteSet)
            {
                State.MoveBudget.AcceptedMoves++;
                ReportAcceptedCellKind(WinnerKey);
                RouteRepairOptionObserver?.Invoke("cell-move", Winner ?? string.Empty);
                return (1, Examined);
            }
            bool PreviousEvaluation = State.Copy.RenderContext.IsEvaluatingCellRouteCandidate;
            State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = true;
            try
            {
                using (FlowchartCellCostTiming.ForRoutingPhase("confirmation"))
                    ModelRouteCompletionStage.Apply(State);

                var Confirmed = FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false);
                var ConfirmedFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                double ExtraLength = RouteLength(State.Copy) - BeforeLength;
                int ExtraBends = RouteBends(State.Copy) - BeforeBends;
                int ChangedLines = Original.ChangedLines();
                var Cost = Confirmed.Cost + ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                    + ChangedLines * FlowchartLayoutRules.RepairMovedLineWeight;
                if (!Original.UnchangedOutside(BestAffected!) || Confirmed.Overlaps > Before.Overlaps || Confirmed.HintsHonoured < Before.HintsHonoured
                    || !NoNewFindings(BeforeFindings, ConfirmedFindings)
                    || ConfirmedFindings.Any(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7")
                    || CountTitleBandRoutes(State) != 0
                    || PreserveBends && ExtraBends > 0
                    || ChangedLines > FlowchartLayoutRules.RepairChainLines
                    || (Shortfall?.Invoke() ?? 0) > InitialShortfall
                    || (Shortfall?.Invoke() ?? 0) == InitialShortfall && Cost >= Before.Cost - 0.001)
                {
                    Original.Restore(State);
                    State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = PreviousEvaluation;
                    return RecoverReferenceCandidates();
                }
            }
            catch { Original.Restore(State); throw; }
            finally { State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = PreviousEvaluation; }
            State.MoveBudget.AcceptedMoves++;
            ReportAcceptedCellKind(WinnerKey);
            RouteRepairOptionObserver?.Invoke("cell-move", Winner ?? string.Empty);
            return (1, Examined);

            // A provisional rejection is not proof that every already examined
            // assignment fails after full completion. Rechecking this exact set
            // spends routing time, but never admits a seventeenth assignment.
            (int Moved, int Examined) RecoverReferenceCandidates() => ReferenceCompletion || WholeRouteSet || ExaminedOptions.Count == 0
                ? (0, Examined)
                : SearchCellAssignmentsCore(State, ExaminedOptions, ExaminedOptions.Count, MoveLimit, Attempted,
                    Shortfall, PreserveBends, true);
        }

        /// <summary>Reports the selected family only when a diagnostic counter consumer is present.</summary>
        /// <param name="Key">Winning immutable option key.</param>
        private static void ReportAcceptedCellKind(string? Key)
        {
            if (Key?.StartsWith("gap|", StringComparison.Ordinal) == true)
                FlowchartTerminalTrackRepair.Observer?.Invoke("gap-winner", Key, "accepted");
            if (FlowchartRenderContext.OperationCountObserver == null || Key == null) return;
            FlowchartRenderContext.ReportOperationCount("cell-winner/" + (Key.StartsWith("joint|", StringComparison.Ordinal)
                ? Key.Split('|')[1] : "existing"), 1);
        }

        /// <summary>Identifies actual complete target changes so equivalent proposals cannot consume the budget twice.</summary>
        /// <param name="Assignment">Validated immutable source and target states.</param>
        /// <returns>Exact ordered region/span and gap deltas, independent of the generating family.</returns>
        private static string CellAssignmentIdentity(FlowchartCellRepair.Assignment Assignment) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Regions = Assignment.After.Regions.Where(Pair => !Assignment.Before.Regions.TryGetValue(Pair.Key, out var Old) || Old != Pair.Value)
                    .OrderBy(Pair => Pair.Key, StringComparer.Ordinal).Select(Pair => new
                    {
                        Pair.Key, Pair.Value.StartRow, Pair.Value.StartColumn, Pair.Value.RowSpan, Pair.Value.ColumnSpan
                    }),
                Columns = Assignment.After.ColumnGaps.OrderBy(Pair => Pair.Key),
                Rows = Assignment.After.RowGaps.OrderBy(Pair => Pair.Key)
            });

        /// <summary>Completes one previously examined assignment with the original routing sequence for safe recovery.</summary>
        /// <param name="State">Applied private candidate; the caller owns exact rollback.</param>
        private static void CompleteReferenceCellCandidate(GridLayoutState State)
        {
            using var Timing = FlowchartCellCostTiming.BeginRouting();
            AssignPortSides(State.Contract, State.Copy, State.Placed, State.Frames, State.Nodes, State.Direction, State.SharedFanSources);
            DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction);
            RefreshHintedRoutes(State);
            bool Previous = State.Copy.RenderContext.IsEvaluatingCellRouteCandidate;
            var PreviousCandidate = RouteRepairCandidateObserver;
            var PreviousOption = RouteRepairOptionObserver;
            State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = true;
            RouteRepairCandidateObserver = null; RouteRepairOptionObserver = null;
            try { ModelRouteCompletionStage.Apply(State); }
            finally
            {
                State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = Previous;
                RouteRepairCandidateObserver = PreviousCandidate; RouteRepairOptionObserver = PreviousOption;
            }
        }

        /// <summary>Owns one complete cell, frame, terminal and hint state for accepted options and exact rollback.</summary>
        private sealed class CellGeometrySnapshot
        {
            private readonly FlowchartLogicalGrid.Snapshot Cells;
            private readonly (GridFrame Frame, int Left, int Right, int Top, int Bottom)[] Frames;
            private readonly FlowchartLayoutNodeBounds[] NodeBounds;
            private readonly FlowchartLayoutNodeBounds[] ClusterBounds;
            private readonly (FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points, string Exit, string Entry,
                double ExitX, double ExitY, double EntryX, double EntryY)[] Routes;
            private readonly FlowchartLayoutQualityFailure[] Violations;
            /// <summary>Painted guides must restore with rejected final-model candidates, not remain attached to a trial route.</summary>
            private readonly List<FlowchartResolvedGuide>? Guides;
            /// <summary>Immutable guide contents prevent in-place edits from corrupting the saved painter state.</summary>
            private readonly (FlowchartResolvedGuide Guide, (double X, double Y)[] Points, FlowchartEdgeRouteKind Kind)[] GuideValues;
            /// <summary>Renderer lookup state must identify the same option as the restored model, including a previously empty cache.</summary>
            private readonly DiagramContract? CachedContract;
            /// <summary>Exact obstacle signature associated with the saved renderer lookup.</summary>
            private readonly string? CachedGeometry;
            /// <summary>Original cached family, restored alongside its key rather than retaining a rejected trial family.</summary>
            private readonly List<FlowchartResolvedGuide>? CachedGuides;
            /// <summary>Immutable contents of mutable cached guide objects.</summary>
            private readonly (FlowchartResolvedGuide Guide, (double X, double Y)[] Points, FlowchartEdgeRouteKind Kind)[] CachedGuideValues;
            /// <summary>Diagnostics for rejected trial geometry must restore with the geometry itself.</summary>
            private readonly string[] Trace;
            private readonly (PlacementHintContract Hint, bool Active, string? Reason)[] PlacementHints;
            private readonly (RoutingHintContract Hint, bool Active, string? Reason)[] RoutingHints;
            private readonly Dictionary<FlowchartLayoutEdgeRoute, GridLayoutState.HintedRoute> HintedRoutes;

            /// <summary>Ensures confirmation did not deactivate or replace the status of any original hint.</summary>
            /// <returns>Whether every saved node, group and routing hint retains its exact original status.</returns>
            internal bool HintsUnchanged() => PlacementHints.All(Item => Item.Hint.IsActive == Item.Active && Item.Hint.UnhonouredReason == Item.Reason)
                && RoutingHints.All(Item => Item.Hint.IsActive == Item.Active && Item.Hint.UnhonouredReason == Item.Reason);

            /// <summary>Captures cell ownership directly; drawing bounds are retained only as derived cached views.</summary>
            /// <param name="State">The complete current candidate.</param>
            internal CellGeometrySnapshot(GridLayoutState State)
            {
                var Clock = LayoutStageTimingObserver == null ? null : System.Diagnostics.Stopwatch.StartNew();
                Cells = FlowchartCellRepair.Capture(State.Placed);
                Frames = State.Frames.Select(Frame => (Frame, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom)).ToArray();
                NodeBounds = State.Copy.NodeBounds.ToArray(); ClusterBounds = State.Copy.ClusterBounds.ToArray();
                Routes = State.Copy.EdgeRoutes.Select(Route => (Route, Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)).ToArray();
                Violations = State.Copy.RenderContext.LayoutStageViolations.ToArray();
                Guides = State.Copy.ResolvedEdgeGuides;
                GuideValues = Guides?.Select(Guide => (Guide, Guide.Points.ToArray(), Guide.RouteKind)).ToArray() ?? [];
                CachedContract = State.Copy.RenderContext.CachedRouteContract;
                CachedGeometry = State.Copy.RenderContext.CachedRouteGeometry;
                CachedGuides = State.Copy.RenderContext.CachedRouteGuides;
                CachedGuideValues = CachedGuides?.Select(Guide => (Guide, Guide.Points.ToArray(), Guide.RouteKind)).ToArray() ?? [];
                Trace = State.Copy.RenderContext.LayoutStageTrace.ToArray();
                PlacementHints = State.Contract.Nodes.Select(Node => Node.PlacementHint).Concat(State.Contract.Groups.Select(Group => Group.PlacementHint))
                    .OfType<PlacementHintContract>().Select(Hint => (Hint, Hint.IsActive, Hint.UnhonouredReason)).ToArray();
                RoutingHints = State.Contract.Edges.Select(Edge => Edge.RoutingHint).OfType<RoutingHintContract>()
                    .Select(Hint => (Hint, Hint.IsActive, Hint.UnhonouredReason)).ToArray();
                HintedRoutes = new(State.HintedRoutes);
                if (Clock != null) LayoutStageTimingObserver?.Invoke("cell-candidate/snapshot", Clock.Elapsed.TotalMilliseconds);
            }

            /// <summary>Restores authoritative cells first, then exact cached frames, route slots, normals and hint accounting.</summary>
            /// <param name="State">The same owning diagram.</param>
            internal void Restore(GridLayoutState State)
            {
                var Clock = LayoutStageTimingObserver == null ? null : System.Diagnostics.Stopwatch.StartNew();
                FlowchartCellRepair.Restore(State.Placed, Cells);
                State.Frames.Clear();
                foreach (var Item in Frames)
                {
                    Item.Frame.Left = Item.Left; Item.Frame.Right = Item.Right; Item.Frame.Top = Item.Top; Item.Frame.Bottom = Item.Bottom;
                    State.Frames.Add(Item.Frame);
                }
                State.Copy.NodeBoundsMutable.Clear(); State.Copy.NodeBoundsMutable.AddRange(NodeBounds);
                State.Copy.ClusterBoundsMutable.Clear(); State.Copy.ClusterBoundsMutable.AddRange(ClusterBounds);
                State.Copy.EdgeRoutesMutable.Clear(); State.Copy.EdgeRoutesMutable.AddRange(Routes.Select(Item => Item.Route));
                foreach (var Item in Routes)
                {
                    Item.Route.Points.Clear();
                    foreach (var Point in Item.Points) Item.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                    Item.Route.ExitSide = Item.Exit; Item.Route.EntrySide = Item.Entry;
                    Item.Route.ExitNormalX = Item.ExitX; Item.Route.ExitNormalY = Item.ExitY;
                    Item.Route.EntryNormalX = Item.EntryX; Item.Route.EntryNormalY = Item.EntryY;
                }
                State.Copy.RenderContext.LayoutStageViolations.Clear(); State.Copy.RenderContext.LayoutStageViolations.AddRange(Violations);
                foreach (var Item in GuideValues)
                {
                    Item.Guide.Points.Clear(); Item.Guide.Points.AddRange(Item.Points);
                    Item.Guide.RouteKind = Item.Kind;
                }
                Guides?.Clear(); Guides?.AddRange(GuideValues.Select(Item => Item.Guide));
                State.Copy.ResolvedEdgeGuides = Guides;
                foreach (var Item in CachedGuideValues)
                {
                    Item.Guide.Points.Clear(); Item.Guide.Points.AddRange(Item.Points);
                    Item.Guide.RouteKind = Item.Kind;
                }
                CachedGuides?.Clear(); CachedGuides?.AddRange(CachedGuideValues.Select(Item => Item.Guide));
                State.Copy.RenderContext.CachedRouteContract = CachedContract;
                State.Copy.RenderContext.CachedRouteGeometry = CachedGeometry;
                State.Copy.RenderContext.CachedRouteGuides = CachedGuides;
                State.Copy.RenderContext.LayoutStageTrace.Clear(); State.Copy.RenderContext.LayoutStageTrace.AddRange(Trace);
                foreach (var Item in PlacementHints) { Item.Hint.IsActive = Item.Active; Item.Hint.UnhonouredReason = Item.Reason; }
                foreach (var Item in RoutingHints) { Item.Hint.IsActive = Item.Active; Item.Hint.UnhonouredReason = Item.Reason; }
                State.HintedRoutes.Clear();
                foreach (var Item in HintedRoutes) State.HintedRoutes.Add(Item.Key, Item.Value);
                if (Clock != null) LayoutStageTimingObserver?.Invoke("cell-candidate/restore", Clock.Elapsed.TotalMilliseconds);
            }

            /// <summary>Counts complete changed routes for the existing repair-chain budget.</summary>
            /// <returns>The number whose terminals, sides or intermediate geometry differ.</returns>
            internal int ChangedLines() => Routes.Count(Item => Item.Route.ExitSide != Item.Exit
                || Item.Route.EntrySide != Item.Entry || !Item.Route.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Item.Points));

            /// <summary>Identifies all changed endpoint and obstacle bounds, including frames derived from moved members or gaps.</summary>
            /// <param name="State">Applied cell candidate.</param>
            /// <returns>Every node or frame whose exact geometry differs.</returns>
            internal HashSet<string> ChangedBoxes(GridLayoutState State)
            {
                var Before = NodeBounds.Concat(ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
                return State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).Where(Box => !Before.TryGetValue(Box.NodeId, out var Old)
                    || Box.CenterX != Old.CenterX || Box.CenterY != Old.CenterY || Box.Width != Old.Width || Box.Height != Old.Height)
                    .Select(Box => Box.NodeId).ToHashSet(StringComparer.Ordinal);
            }

            /// <summary>Finds changed endpoints and corridors near either the old or new obstacle; unaffected paths remain frozen.</summary>
            /// <param name="State">Applied geometry.</param>
            /// <param name="Changed">Changed node and frame ids.</param>
            /// <returns>Conservative routing dependencies.</returns>
            internal HashSet<FlowchartLayoutEdgeRoute> RoutesAffectedBy(GridLayoutState State, IReadOnlySet<string> Changed)
            {
                var OldBounds = NodeBounds.Concat(ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
                var NewBounds = State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
                var Obstacles = NodeBounds.Concat(State.Copy.NodeBounds).Where(Box => Changed.Contains(Box.NodeId)).ToList();
                foreach (var Band in FlowchartFrameTitleBandHelper.Build(State.Contract, OldBounds)
                    .Concat(FlowchartFrameTitleBandHelper.Build(State.Contract, NewBounds)).Where(Band => Changed.Contains(Band.GroupId)))
                    Obstacles.Add(new() { NodeId = Band.GroupId, CenterX = (Band.Left + Band.Right) / 2,
                        CenterY = (Band.Bottom + Band.Top) / 2, Width = Band.Right - Band.Left, Height = Band.Top - Band.Bottom });
                double Margin = DiagramGridRouteBuilder.CORNER_RADIUS_PX;
                return Routes.Where(Item => Changed.Contains(Item.Route.SourceId) || Changed.Contains(Item.Route.TargetId)
                    || Enumerable.Range(1, Math.Max(0, Item.Points.Length - 1)).Any(Index => Obstacles.Any(Box =>
                        Math.Min(Item.Points[Index - 1].X, Item.Points[Index].X) <= Box.CenterX + Box.Width / 2 + Margin
                        && Math.Max(Item.Points[Index - 1].X, Item.Points[Index].X) >= Box.CenterX - Box.Width / 2 - Margin
                        && Math.Min(Item.Points[Index - 1].Y, Item.Points[Index].Y) <= Box.CenterY + Box.Height / 2 + Margin
                        && Math.Max(Item.Points[Index - 1].Y, Item.Points[Index].Y) >= Box.CenterY - Box.Height / 2 - Margin)))
                    .Select(Item => Item.Route).ToHashSet();            }

            /// <summary>Includes unchanged neighbours whose side inventory redistributes their allocated terminal.</summary>
            /// <returns>Routes with changed sides, normals or terminal coordinates.</returns>
            internal IEnumerable<FlowchartLayoutEdgeRoute> ChangedTerminalRoutes() => Routes.Where(Item =>
                Item.Route.ExitSide != Item.Exit || Item.Route.EntrySide != Item.Entry
                || Item.Route.ExitNormalX != Item.ExitX || Item.Route.ExitNormalY != Item.ExitY
                || Item.Route.EntryNormalX != Item.EntryX || Item.Route.EntryNormalY != Item.EntryY
                || Item.Points.Length != 0 && (Item.Route.Points.Count == 0
                    || (Item.Route.Points[0].X, Item.Route.Points[0].Y) != Item.Points[0]
                    || (Item.Route.Points[^1].X, Item.Route.Points[^1].Y) != Item.Points[^1])).Select(Item => Item.Route);

            /// <summary>Checks exact preservation of every route outside the proposed dependency set.</summary>
            /// <param name="Affected">Only routes permitted to change.</param>
            /// <returns>Whether all other sides, normals and points are unchanged.</returns>
            internal bool UnchangedOutside(IReadOnlySet<FlowchartLayoutEdgeRoute> Affected) => !ChangedTerminalRoutes().Any(Route => !Affected.Contains(Route))
                && Routes.Where(Item => !Affected.Contains(Item.Route)).All(Item =>
                    Item.Route.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Item.Points));
        }
    }
}
