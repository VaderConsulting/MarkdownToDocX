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
        {
            // Provisional route construction is not the option that reaches
            // the painter. Defer cell quality moves until that final model is
            // available, and never recursively place while scoring a trial.
            if (!State.CellFinalRoutingReady || State.Copy.RenderContext.IsEvaluatingCellRouteCandidate) return (0, 0);
            CandidateLimit = Math.Min(CandidateLimit,
                FlowchartLayoutRules.RepairCandidateMoves - State.MoveBudget.CandidateMoves);
            MoveLimit = Math.Min(MoveLimit,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves);
            if (CandidateLimit <= 0 || MoveLimit <= 0) return (0, 0);
            var Original = new CellGeometrySnapshot(State);
            var Before = FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false);
            var BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            int BeforeFrameOverlaps = CountUnrelatedFrameOverlaps(State.Contract, State.Frames);
            double BeforeLength = RouteLength(State.Copy);
            int BeforeBends = RouteBends(State.Copy);
            double BestScore = Before.OptimizationScore;
            double BestShortfall = Shortfall?.Invoke() ?? 0;
            CellGeometrySnapshot? Best = null;
            int Examined = 0;
            string? Winner = null;
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
            foreach (var Option in Options)
            {
                if (Examined >= CandidateLimit) break;
                var Participants = Option.Assignment.Nodes;
                if (Participants.Count == 0 || Participants.Any(Node => Node.PlacementHintLocked
                    || HasHintedContainingGroup(State.Contract, Node.Id))) continue;
                if (HasExternalCellHintDependent(State.Contract, Option.Subject, Participants.ToArray())) continue;
                var Ids = Participants.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                if (State.Copy.EdgeRoutes.Count(Route => Ids.Contains(Route.SourceId) || Ids.Contains(Route.TargetId))
                    > FlowchartLayoutRules.RepairChainLines) continue;
                if (!Attempted.Add("cells|" + Signature + "|" + Option.Key)) continue;
                Examined++;
                State.MoveBudget.CandidateMoves++;
                try
                {
                    if (!Option.Assignment.TryApply()) continue;
                    State.Frames.Clear();
                    BuildFramesYUp(State.Contract, State.Placed, State.Frames);
                    if (CountUnrelatedFrameOverlaps(State.Contract, State.Frames) > BeforeFrameOverlaps) continue;
                    WriteBack(State.Copy, State.Placed, State.Frames);
                    AssignPortSides(State.Contract, State.Copy, State.Placed, State.Frames, State.Nodes, State.Direction, State.SharedFanSources);
                    DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction);
                    RefreshHintedRoutes(State);
                    bool PreviousEvaluation = State.Copy.RenderContext.IsEvaluatingCellRouteCandidate;
                    var PreviousCandidateObserver = RouteRepairCandidateObserver;
                    var PreviousOptionObserver = RouteRepairOptionObserver;
                    State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = true;
                    // Nested route options belong to an unpublished trial.
                    // Reporting them as actual wins also makes a non-profiled
                    // catalogue pay for thousands of discarded trace strings.
                    RouteRepairCandidateObserver = null;
                    RouteRepairOptionObserver = null;
                    try { ModelRouteCompletionStage.Apply(State); }
                    finally
                    {
                        State.Copy.RenderContext.IsEvaluatingCellRouteCandidate = PreviousEvaluation;
                        RouteRepairCandidateObserver = PreviousCandidateObserver;
                        RouteRepairOptionObserver = PreviousOptionObserver;
                    }
                    var AfterFindings = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                    var After = FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false);
                    double Remaining = Shortfall?.Invoke() ?? 0;
                    double ExtraLength = RouteLength(State.Copy) - BeforeLength;
                    int ExtraBends = RouteBends(State.Copy) - BeforeBends;
                    int ChangedLines = Original.ChangedLines();
                    double Cost = After.OptimizationScore + ExtraLength / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
                        + ChangedLines * FlowchartLayoutRules.RepairMovedLineWeight;
                    bool Legal = After.Overlaps <= Before.Overlaps && After.HintsHonoured >= Before.HintsHonoured
                        && NoNewFindings(BeforeFindings, AfterFindings) && CountTitleBandRoutes(State) == 0
                        && ExtraLength <= FlowchartLayoutRules.RepairExtraLength && ExtraBends <= FlowchartLayoutRules.RepairExtraBends
                        && (!PreserveBends || ExtraBends <= 0)
                        && ChangedLines <= FlowchartLayoutRules.RepairChainLines;
                    RouteRepairCandidateObserver?.Invoke("cell-move", Option.Subject, Cost, Legal);
                    if (!Legal || Remaining > BestShortfall
                        || Remaining == BestShortfall && Cost >= BestScore - 0.001) continue;
                    BestShortfall = Remaining;
                    BestScore = Cost;
                    Best = new(State);
                    Winner = Option.Subject;
                }
                finally { Original.Restore(State); }
            }
            (Best ?? Original).Restore(State);
            if (Best == null) return (0, Examined);
            State.MoveBudget.AcceptedMoves++;
            RouteRepairOptionObserver?.Invoke("cell-move", Winner ?? string.Empty);
            return (1, Examined);
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
        }
    }
}
