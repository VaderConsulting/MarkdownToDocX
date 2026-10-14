using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Named stages that preserve the existing grid-layout operation order.</summary>
public static partial class DiagramGridLayout
{
    private sealed class GridLayoutState
    {
        public GridLayoutState(
            DiagramContract Contract,
            FlowchartLayoutResult LaidOut,
            FlowchartRenderContext? RenderContext)
        {
            this.Contract = Contract;
            Copy = CopyResult(LaidOut);
            Copy.RenderContext = RenderContext ?? LaidOut.RenderContext;
            IsReady = Contract != null && Copy.NodeBounds.Count > 0;
        }

        public DiagramContract Contract { get; }
        public FlowchartLayoutResult Copy { get; }
        public bool IsReady { get; }
        public bool SkipRootGroupRepacking { get; set; }
        public FlowchartDirection Direction { get; set; }
        public bool Horizontal { get; set; }
        public bool Open { get; set; }
        public int BoxWidth { get; set; }
        public int BoxHeight { get; set; }
        public int DiamondHeight { get; set; }
        public int MainPitch { get; set; }
        public int CrossPitch { get; set; }
        public Dictionary<string, NodeContract> Nodes { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, (double X, double Y)> HintedNodePositions { get; } = new(StringComparer.Ordinal);
        public List<GridNode> Placed { get; } = [];
        public List<FlowchartPartnerRankLayout.Partners> PartnerRanks { get; set; } = [];
        public HashSet<string> SharedFanSources { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> ObstacleRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PortSideRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> CrossGroupRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ConvergenceRouteStates { get; } = new(StringComparer.Ordinal);
        public bool RepairExteriorCrossings { get; set; }
        public List<GridFrame> Frames { get; } = [];
        public Dictionary<FlowchartLayoutEdgeRoute, HintedRoute> HintedRoutes { get; } = [];
        public sealed record HintedRoute(string ExitSide, string EntrySide, (double X, double Y)[] Points);
    }

    private static class GridPreparationStage
    {
        // Rule ids: 1.1–1.7 (measured boxes, grid dimensions and rank coordinates).
        public static void Apply(GridLayoutState State)
        {
            State.Direction = FlowchartDirectionNormalizer.Normalize(State.Contract.Direction);
            State.Horizontal = State.Direction == FlowchartDirection.LeftToRight
                || State.Direction == FlowchartDirection.RightToLeft;
            State.Open = State.Contract.LayoutMode == MermaidRecognition.Config.DiagramLayoutMode.Open;
            (State.BoxWidth, State.BoxHeight) = ResolveStandardBox(State.Contract);
            State.DiamondHeight = State.BoxHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR;
            State.MainPitch = State.Horizontal
                ? State.BoxWidth + Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX)
                : DiagramGridMetrics.PitchY(State.BoxHeight);
            State.CrossPitch = State.Horizontal
                ? DiagramGridMetrics.PitchY(State.BoxHeight)
                : DiagramGridMetrics.PitchX(State.BoxWidth);
            State.Nodes = IndexNodes(State.Contract);

            foreach (FlowchartLayoutNodeBounds Bounds in State.Copy.NodeBounds)
            {
                bool Diamond = State.Nodes.TryGetValue(Bounds.NodeId, out NodeContract? Node)
                    && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
                State.Placed.Add(new GridNode
                {
                    Id = Bounds.NodeId,
                    Shape = State.Nodes.TryGetValue(Bounds.NodeId, out NodeContract? ShapeNode) ? ShapeNode.Shape : null,
                    GroupId = GroupOf(State.Contract, Bounds.NodeId),
                    OldX = Bounds.CenterX,
                    OldY = Bounds.CenterY,
                    X = Bounds.CenterX,
                    Y = Bounds.CenterY,
                    Width = State.Open ? (int)Math.Ceiling(Bounds.Width) : State.BoxWidth,
                    Height = State.Open ? (int)Math.Ceiling(Bounds.Height) : Diamond ? State.DiamondHeight : State.BoxHeight,
                    IsDiamond = Diamond
                });
            }

            AssignRanks(State.Placed, State.Horizontal);
            AssignColumns(State.Placed, State.Horizontal, State.CrossPitch);
            foreach (GridNode Node in State.Placed)
            {
                Node.X = Math.Round((Node.X - Node.Width / 2.0) / DiagramGridMetrics.GRID_UNIT_PX)
                    * DiagramGridMetrics.GRID_UNIT_PX + Node.Width / 2.0;
                Node.Y = Math.Round((Node.Y - Node.Height / 2.0) / DiagramGridMetrics.GRID_UNIT_PX)
                    * DiagramGridMetrics.GRID_UNIT_PX + Node.Height / 2.0;
            }
        }
    }

    private static class PlacementHintStage
    {
        public static void Apply(GridLayoutState State)
        {
            State.Frames.Clear();
            BuildFrames(State.Contract, State.Placed, State.Frames);
            FlowchartPlacementHints.Apply(State.Contract, State.Placed, State.Frames);
            CaptureHintedNodePositions(State);
        }
    }

    private static class NodePlacementStage
    {
        // Rule ids: 2.4–2.21 (automatic and hinted node placement).
        public static void Apply(GridLayoutState State)
        {
            DiagramContract Contract = State.Contract;
            List<GridNode> Placed = State.Placed;
            bool Horizontal = State.Horizontal;
            bool Open = State.Open;
            int CrossPitch = State.CrossPitch;
            int BoxWidth = State.BoxWidth;
            int BoxHeight = State.BoxHeight;
            int MainPitch = State.MainPitch;
            FlowchartDirection Direction = State.Direction;
            FlowchartLayoutResult Copy = State.Copy;
            void Step(string Name, Action Action)
            {
                Action();
            }

            if (!Open)
            {
                Step("ResolveVerticalChordBlockers", () => ResolveVerticalChordBlockers(Contract, Placed));
                if (!ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    Step("AlignForwardMainChainColumn", () => AlignForwardMainChainColumn(Contract, Placed, Horizontal));
                Step("AlignFanInMergeColumn", () => AlignFanInMergeColumn(Contract, Placed, Horizontal));
                if (!ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    Step("AlignForwardMainChainColumn2", () => AlignForwardMainChainColumn(Contract, Placed, Horizontal));
                if (ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    Step("AlignTwoWayDecisionSideTargets", () => AlignTwoWayDecisionSideTargets(Contract, Placed));
                Step("AlignBypassDetourColumns", () => AlignBypassDetourColumns(Contract, Placed, Horizontal));
                Step("AlignDecisionCentreChildColumn", () => AlignDecisionCentreChildColumn(Contract, Placed, Horizontal));
                Step("AlignTerminalDecisionTargets0", () => AlignTerminalDecisionTargets(Contract, Placed, Horizontal, CrossPitch, CoordinatesPlaced: false));
                Step("CentreTerminalFans0", () => CentreTerminalFans(Contract, Placed, Horizontal, CoordinatesPlaced: false));
                Step("AlignSingleTerminalChildren0", () => AlignSingleTerminalChildren(Contract, Placed, Horizontal, CoordinatesPlaced: false));
                Step("CascadeColumns", () => CascadeColumns(Placed));
                Step("PlaceOnGrid", () => PlaceOnGrid(Placed, Horizontal, BoxWidth, BoxHeight, MainPitch, CrossPitch, Copy, Contract));
                if (!Horizontal) Step("ApplySpineColumnAdjustments", () => ApplySpineColumnAdjustments(Contract, Placed, Direction));
                Step("AlignTerminalDecisionTargets1", () => AlignTerminalDecisionTargets(Contract, Placed, Horizontal, CrossPitch, CoordinatesPlaced: true));
                Step("CentreTerminalFans1", () => CentreTerminalFans(Contract, Placed, Horizontal, CoordinatesPlaced: true));
                Step("AlignSingleTerminalChildren1", () => AlignSingleTerminalChildren(Contract, Placed, Horizontal, CoordinatesPlaced: true));
                Step("AlignUnbranchedDecisionInputs", () => AlignUnbranchedDecisionInputs(Contract, Placed, Horizontal));
                Step("AlignHorizontalRetryOutcomes", () => AlignHorizontalRetryOutcomes(Contract, Placed, Horizontal));
                Step("AlignSimpleDecisionMerges", () => AlignSimpleDecisionMerges(Contract, Placed, Horizontal, CrossPitch));
                Step("AlignDecisionDetours", () => AlignDecisionDetours(Contract, Placed, Horizontal, CrossPitch));
                Step("AlignVerticalLoopBranches", () => AlignVerticalLoopBranches(Contract, Placed, Horizontal, CrossPitch));
                State.PartnerRanks = FlowchartPartnerRankLayout.Find(Contract, Placed, Horizontal);
                FlowchartPartnerRankLayout.Prepare(State.PartnerRanks, Horizontal);
                Step("CompactIndependentFanIn", () => CompactIndependentFanIn(Contract, Placed, Horizontal, CrossPitch));
                Step("AlignSharedFanSourcesAndTails", () => State.SharedFanSources = AlignSharedFanSourcesAndTails(Contract, Placed, Horizontal));
                Step("AlignFans", () => FlowchartCircularJunctionLayout.AlignFans(Contract, Placed, Horizontal));
            }
            else
            {
                State.PartnerRanks = FlowchartPartnerRankLayout.Find(Contract, Placed, Horizontal);
                FlowchartPartnerRankLayout.Prepare(State.PartnerRanks, Horizontal, Align: false);
            }

            Step("GrowCrowdedSides", () => GrowCrowdedSides(Contract, Placed, Copy, BoxWidth, BoxHeight));
            Step("GrowParallelRelationshipSides", () => GrowParallelRelationshipSides(Contract, Placed, Horizontal));
            Step("SeparateOverlaps", () => SeparateOverlaps(Placed, Horizontal));
            if (!Open)
            {
                Step("TerminalBranchPlacement", () => FlowchartTerminalBranchPlacement.Apply(Contract, Placed, Horizontal, CrossPitch));
                Step("TerminalInputPlacement", () => FlowchartTerminalInputPlacement.Apply(Contract, Placed, Horizontal, CrossPitch));
                Step("GridCompaction", () => FlowchartGridCompaction.Apply(Placed, Horizontal, Math.Max(CrossPitch, Contract.Groups.Count > 0 ? 60 : DiagramGridMetrics.NODE_GAP_PX)));
                Step("SparseRankCompaction", () => FlowchartSparseRankCompaction.Apply(Contract, Placed, Horizontal));
                Step("JunctionBranchCompaction", () => FlowchartJunctionBranchCompaction.Apply(Contract, Placed, Horizontal));
                Step("TerminalRowPlacement", () => FlowchartTerminalRowPlacement.Apply(Contract, Placed, Horizontal));
                Step("ChainWrapping", () => FlowchartChainWrapping.Apply(Contract, Placed, Horizontal));
                Step("FanPacking", () => FlowchartFanPacking.Apply(Contract, Placed));
                Step("ComponentWrapping", () => State.RepairExteriorCrossings = FlowchartComponentWrapping.Apply(Contract, Placed, Horizontal));
                Step("DisconnectedPacking", () => FlowchartDisconnectedPacking.Apply(Contract, Placed));
                Step("GroupMemberPacking", () => FlowchartGroupMemberPacking.Apply(Contract, Placed));
            }
        }
    }

    private static class GroupFrameStage
    {
        // Rule ids: 2.8–2.21 (group placement, frame fitting and placement hints).
        public static void Apply(GridLayoutState State)
        {
            DiagramContract Contract = State.Contract;
            List<GridNode> Placed = State.Placed;
            bool Horizontal = State.Horizontal;
            bool Open = State.Open;
            List<GridFrame> Frames = State.Frames;
            List<string> RefitTriggers = [];
            string PreviousMembers = GroupMemberPositionSignature(Contract, Placed);
            if (!Open) CenterLoneNestedGroups(Contract, Placed, Horizontal);
            RecordMemberChange("center nested members");
            if (!Open && !State.SkipRootGroupRepacking)
                CompactOverlappingRootGroups(Contract, Placed, Frames, Horizontal);
            RecordMemberChange("compact overlapping root groups");
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames);
            if (FlowchartPartnerRankLayout.GrowGaps(Contract, Placed, Frames, State.PartnerRanks, Horizontal))
            {
                RecordMemberChange("partner-rank gap growth");
            }
            if (!Open && FlowchartGroupedLeafPlacement.Apply(Contract, Placed, Frames, Horizontal))
            {
                RecordMemberChange("grouped leaf placement");
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            if (!Open && FlowchartExternalLeafPlacement.Apply(Contract, Placed, Frames, Horizontal))
            {
                RecordMemberChange("external leaf placement");
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            if (!Open)
            {
                FlowchartIntergroupNodePlacement.Apply(Contract, Placed, Frames, Horizontal);
            }
            if (!Open)
            {
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            if (GrowTerminalSameRankLabelGaps(Contract, Placed, Horizontal))
            {
                RecordMemberChange("same-rank label gap growth");
            }
            ResolveHintedTerminalClearance(State);
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames);
            // Placement hints are relative constraints. Earlier packing and
            // frame fitting can change the referenced frame after the first
            // hint pass, so refresh the relationships against the final frame
            // geometry before locking the hinted positions for route repair.
            foreach (GridNode Node in Placed) Node.PlacementHintLocked = false;
            if (FlowchartPlacementHints.Apply(Contract, Placed, Frames))
            {
                RecordMemberChange("refresh relative placement hints");
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            CaptureHintedNodePositions(State);
            FitFrames(Contract, Placed, Frames, Horizontal,
                RefitTriggers.Count == 0 ? "stage settlement" : string.Join(" + ", RefitTriggers.Distinct(StringComparer.Ordinal)));

            void RecordMemberChange(string Trigger)
            {
                string CurrentMembers = GroupMemberPositionSignature(Contract, Placed);
                if (CurrentMembers == PreviousMembers) return;
                RefitTriggers.Add(Trigger);
                PreviousMembers = CurrentMembers;
            }
        }
    }

    private static class RouteCoordinateStage
    {
        public static void Apply(GridLayoutState State)
        {
            FlipToYUp(State.Placed, State.Frames, State.Copy);
            RestoreContractRouteEndpoints(State.Contract, State.Copy);
            EnsureBackEdgeRoutes(State.Contract, State.Copy, State.Placed);
        }
    }

    private static class JointPortAllocationStage
    {
        // Rule id: 4.7 (jointly choose all endpoint ports before building routes).
        public static void Apply(GridLayoutState State) => AssignPortSides(State.Contract, State.Copy,
            State.Placed, State.Frames, State.Nodes, State.Direction, State.SharedFanSources);
    }

    private static class RouteConstructionStage
    {
        // Rule ids: 3.8, 4.11 and 5.3–5.9 (build rule-specific orthogonal paths).
        public static void Apply(GridLayoutState State)
        {
            DiagramContract Contract = State.Contract;
            FlowchartLayoutResult Copy = State.Copy;
            List<GridNode> Placed = State.Placed;
            List<GridFrame> Frames = State.Frames;
            Dictionary<string, NodeContract> Nodes = State.Nodes;
            FlowchartDirection Direction = State.Direction;
            bool Horizontal = State.Horizontal;
            PreserveHintedRoutes(State, () => DiagramGridRouteBuilder.RebuildAllRoutes(Copy, Placed, Nodes, Contract, Frames, Direction));
            PreserveHintedRoutes(State, () => RebuildDecisionFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction));
            PreserveHintedRoutes(State, () => RebuildSoleForwardDecisionBranchRoutes(Contract, Copy, Placed, Frames, Nodes, Direction));
            PreserveHintedRoutes(State, () => RebuildOrdinaryFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction));
            PreserveHintedRoutes(State, () => FlowchartForwardRouteSimplifier.Apply(Copy, Contract,
                BuildPlacedBounds(Placed, Frames), Nodes, Direction));
            PreserveHintedRoutes(State, () => FlowchartPartnerRankLayout.Route(Contract, Copy, Placed, Frames,
                State.PartnerRanks, Horizontal));
            PreserveHintedRoutes(State, () => RebuildSimpleDecisionMergeRoutes(Contract, Copy, Placed, Frames, Horizontal));
            PreserveHintedRoutes(State, () => RebuildDecisionDetourRoutes(Contract, Copy, Placed, Frames, Horizontal));
            PreserveHintedRoutes(State, () => RebuildLoopBackRoutes(Contract, Copy, Placed, Frames, Nodes, Direction));
            PreserveHintedRoutes(State, () => DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(
                Copy, Placed, Nodes, Contract, Frames, Direction));
            PreserveHintedRoutes(State, () => FlowchartCircularJunctionLayout.RebuildFans(
                Copy, Contract, BuildPlacedBounds(Placed, Frames), Nodes, Direction));
        }
    }

    private static class RouteRepairStage
    {
        // Rule ids: 3, 4 and 5 (route legality repairs and score improvements).
        private sealed record RepairPass(string RuleIds, Action<GridLayoutState> Apply);

        private static readonly RepairPass[] Passes =
        [
            new("4.4", State => SeparateTracks(State.Copy, State.Placed)),
            new("4.4,5.3", State => SeparateParallelReturnTracks(State.Contract, State.Copy,
                BuildPlacedBounds(State.Placed, State.Frames))),
            new("4.4", State => PushPointsOutOfNodes(State.Copy, State.Placed)),
            new("5.3,5.4", State => DiagramGridRouteBuilder.RebuildContainedBackRoutes(
                State.Copy, State.Placed, State.Nodes, State.Contract, State.Frames, State.Direction)),
            new("4.4", State => FlowchartGridTerminalNormalizer.Apply(
                State.Copy, BuildPlacedBounds(State.Placed, State.Frames), State.Nodes)),
            new("1.7,4.2,4.3", ApplyHintAwareGapGrowth),
            new("4.2", State => DiagramGridRouteBuilder.RemoveClearForwardDoglegs(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("2.13,5.9", State => FlowchartBranchRouteOrder.RemoveAvoidableCrossings(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction,
                FlowchartPartnerRankLayout.StraightRoutes(State.Copy, State.PartnerRanks))),
            new("5.8,5.9", State =>
            {
                if (!State.Open && State.Direction is FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop)
                    FlowchartManyToManyRoutes.Apply(State.Copy, State.Contract,
                        BuildPlacedBounds(State.Placed, State.Frames), State.Direction);
            }),
            new("4.4", State => FlowchartObstacleRouteRepair.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames), State.ObstacleRouteStates)),
            new("5.3,5.4", State => FlowchartReturnRouteOrder.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("5.8", State => FlowchartNestedCounterflowTracks.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("4.4,5.8", State => FlowchartCrossGroupRouteSimplifier.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames), State.CrossGroupRouteStates)),
            new("5.8", State => FlowchartAlignedCounterflowRoutes.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("5.8", State => FlowchartTerminalFanBypass.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("5.9", State =>
            {
                if (State.RepairExteriorCrossings)
                    FlowchartExteriorCrossingRepair.Apply(State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("5.8", State =>
            {
                if (!State.Open)
                    FlowchartWrappedChainRoutes.Apply(State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("5.8", State =>
            {
                if (!State.Open)
                    FlowchartWrappedFanRoutes.Apply(State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("4.2", State =>
            {
                if (!State.Open) FlowchartAlignedRouteStraightener.Apply(State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
                else FlowchartAlignedRouteStraightener.ApplyNearlyAlignedFacing(State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("4.2,4.3", State =>
            {
                if (!State.Open)
                    FlowchartTwoCornerRouteRepair.Apply(State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("1.1–1.7", State => WriteBack(State.Copy, State.Placed, State.Frames)),
            new("3.8,4.11", State =>
            {
                if (!State.Open) FlowchartDecisionBranchRouteRepair.Apply(State.Copy, State.Contract);
            }),
            new("4.11", State =>
            {
                if (!State.Open) FlowchartConvergenceRouteRepair.Apply(
                    State.Copy, State.Contract, State.ConvergenceRouteStates);
            }),
            new("4.2,6.1,6.2", State =>
            {
                if (!State.Open) FlowchartSelfLoopLabelRouteRepair.Apply(State.Copy, State.Contract);
            }),
            new("2.13,4.2", State =>
            {
                if (!State.Open)
                    ApplyScoreGuardedTwoCornerRepair(State.Copy, State.Contract,
                        BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("4.2", State =>
            {
                if (!State.Open)
                {
                    FlowchartAlignedRouteStraightener.RepairSameSideDirectRoutes(
                        State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
                    FlowchartAlignedRouteStraightener.ApplyOneCornerCandidates(
                        State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
                    FlowchartAlignedRouteStraightener.ApplyCenteredFacing(
                        State.Copy, BuildPlacedBounds(State.Placed, State.Frames));
                }
            }),
            new("5.8", State => FlowchartCircularJunctionLayout.RebuildFans(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction)),
            new("4.7", State =>
            {
                if (!State.Open) FlowchartPortSideRouteRepair.Apply(State.Copy, State.Contract, State.PortSideRouteStates);
            }),
            new("4.2,4.3", State =>
            {
                // Port-side repair may find a cleaner legal exit/entry pair. Collapse
                // any remaining unhinted doglegs against those final assigned ports.
                if (!State.Open)
                    FlowchartTwoCornerRouteRepair.Apply(State.Copy, State.Contract,
                        BuildPlacedBounds(State.Placed, State.Frames));
            }),
            new("5.8", State => RebuildAlignedSimpleFanRoutes(State.Contract, State.Copy,
                State.Placed, State.Frames, State.Nodes, State.Direction)),
            new("5.8,5.9", State =>
            {
                if (!State.Open && State.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft)
                {
                    FlowchartManyToManyRoutes.Apply(State.Copy, State.Contract,
                        BuildPlacedBounds(State.Placed, State.Frames), State.Direction);
                    ObserveCrossings(State, "horizontal track search");
                    ObserveLayoutStage(State, "horizontal track search");
                }
            })
        ];

        private static void ApplyHintAwareGapGrowth(GridLayoutState State)
        {
            if (!GrowFacingDoglegGaps(State.Copy, State.Contract, State.Placed, State.Frames,
                State.HintedNodePositions.Keys)) return;

            // Gap growth may move automatic nodes to preserve a legal route. Rebuild
            // automatic paths from the new geometry, then reapply explicit line hints
            // so hinted paths continue to follow their requested sides and ports.
            WriteBack(State.Copy, State.Placed, State.Frames);
            AssignPortSides(State.Contract, State.Copy, State.Placed, State.Frames,
                State.Nodes, State.Direction, State.SharedFanSources);
            DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes,
                State.Contract, State.Frames, State.Direction);
            RefreshHintedRoutes(State);
        }

        public static void Apply(GridLayoutState State)
        {
            State.Copy.UsesGridRoutes = true;
            HashSet<string> SeenStates = new(StringComparer.Ordinal) { RouteState(State.Copy) };
            List<(double X, double Y)[]> BestRoutes = CaptureRoutes(State.Copy);
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(State.Contract, State.Copy,
                IncludeArrangementCheck: false);
            while (true)
            {
                for (int PassIndex = 0; PassIndex < Passes.Length; PassIndex++)
                {
                    RepairPass Pass = Passes[PassIndex];
                    string Name = $"repair[{PassIndex}] {Pass.RuleIds}";
                    Action<string, IReadOnlyList<string>, long, double>? Profile = RouteRepairPassObserver;
                    Dictionary<string, string>? BeforeRoutes = Profile == null ? null : RouteGeometry(State.Copy);
                    long BeforeCalls = FlowchartPaddedVisibilityRouter.GridPathCallCount;
                    double BeforeGridMs = FlowchartPaddedVisibilityRouter.GridPathElapsedMilliseconds;
                    Stopwatch? Timer = Profile == null ? null : Stopwatch.StartNew();
                    MeasureLayoutStage(Name, () => PreserveHintedRoutes(State, () => Pass.Apply(State)));
                    if (Profile != null)
                    {
                        Timer!.Stop();
                        Dictionary<string, string> AfterRoutes = RouteGeometry(State.Copy);
                        string[] ChangedRoutes = AfterRoutes.Where(Pair =>
                                !BeforeRoutes!.TryGetValue(Pair.Key, out string? Prior) || Prior != Pair.Value)
                            .Select(Pair => Pair.Key).OrderBy(Id => Id, StringComparer.Ordinal).ToArray();
                        Profile(Name, ChangedRoutes,
                            FlowchartPaddedVisibilityRouter.GridPathCallCount - BeforeCalls,
                            FlowchartPaddedVisibilityRouter.GridPathElapsedMilliseconds - BeforeGridMs);
                    }
                    ObserveCrossings(State, $"repair[{PassIndex}] {Pass.RuleIds}");
                    ObserveLayoutStage(State, $"repair[{PassIndex}] {Pass.RuleIds}");
                }

                string Signature = RouteState(State.Copy);
                if (SeenStates.Contains(Signature))
                {
                    RestoreRoutes(State.Copy, BestRoutes);
                    break;
                }

                FlowchartLayoutScore.Result After = FlowchartLayoutScore.Measure(State.Contract, State.Copy,
                    IncludeArrangementCheck: false);
                if (After.OptimizationScore >= Before.OptimizationScore)
                {
                    RestoreRoutes(State.Copy, BestRoutes);
                    break;
                }

                SeenStates.Add(Signature);
                Before = After;
                BestRoutes = CaptureRoutes(State.Copy);
            }
            HashSet<FlowchartLayoutEdgeRoute> ReallocatedRoutes = RedistributeAllocatedPorts(
                State.Contract, State.Copy, State.Placed, State.Frames, State.Nodes);
            if (ReallocatedRoutes.Count > 0)
            {
                DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes,
                    State.Contract, State.Frames, State.Direction, ReallocatedRoutes);
                RebuildOrdinaryFanOutRoutes(State.Contract, State.Copy, State.Placed,
                    State.Frames, State.Nodes, State.Direction,
                    ReallocatedRoutes.Select(Route => Route.SourceId).ToHashSet(StringComparer.Ordinal));
            }
            if (State.Contract.Edges.Any(Edge => Edge.RoutingHint?.IsActive == true))
            {
                FlowchartRoutingHints.Apply(State.Copy, State.Contract,
                    BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction,
                    RepairAutomaticRoutesEnabled: false);
            }
        }

        private static Dictionary<string, string> RouteGeometry(FlowchartLayoutResult Layout) =>
            Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, Route => string.Join(";", Route.Points.Select(Point =>
                $"{BitConverter.DoubleToInt64Bits(Point.X):X16},{BitConverter.DoubleToInt64Bits(Point.Y):X16}")), StringComparer.Ordinal);

        private static List<(double X, double Y)[]> CaptureRoutes(FlowchartLayoutResult Layout)
        {
            List<(double X, double Y)[]> Routes = new(Layout.EdgeRoutes.Count);
            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
            {
                (double X, double Y)[] Points = new (double X, double Y)[Route.Points.Count];
                for (int PointIndex = 0; PointIndex < Route.Points.Count; PointIndex++)
                    Points[PointIndex] = (Route.Points[PointIndex].X, Route.Points[PointIndex].Y);
                Routes.Add(Points);
            }
            return Routes;
        }

        private static void RestoreRoutes(FlowchartLayoutResult Layout, IReadOnlyList<(double X, double Y)[]> Routes)
        {
            for (int RouteIndex = 0; RouteIndex < Layout.EdgeRoutes.Count; RouteIndex++)
            {
                List<FlowchartLayoutPoint> Points = Layout.EdgeRoutes[RouteIndex].Points;
                Points.Clear();
                foreach ((double X, double Y) in Routes[RouteIndex])
                    Points.Add(new FlowchartLayoutPoint { X = X, Y = Y });
            }
        }

        private static string RouteState(FlowchartLayoutResult Layout)
        {
            StringBuilder Signature = new();
            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
            {
                Signature.Append(Route.EdgeId).Append('|').Append(Route.SourceId).Append('>')
                    .Append(Route.TargetId).Append(':');
                foreach (FlowchartLayoutPoint Point in Route.Points)
                {
                    Signature.Append(BitConverter.DoubleToInt64Bits(Point.X).ToString("X16", CultureInfo.InvariantCulture))
                        .Append(',')
                        .Append(BitConverter.DoubleToInt64Bits(Point.Y).ToString("X16", CultureInfo.InvariantCulture))
                        .Append(';');
                }
                Signature.Append('|');
            }
            return Signature.ToString();
        }
    }

    private static class RoutingHintStage
    {
        // Rule ids: 3.1–3.7 (explicit line endpoints and travel directions).
        public static void Apply(GridLayoutState State)
        {
            FlowchartRoutingHints.Apply(State.Copy, State.Contract,
                BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction,
                RepairAutomaticRoutesEnabled: false);
            State.HintedRoutes.Clear();
            foreach (FlowchartLayoutEdgeRoute Route in State.Copy.EdgeRoutes)
            {
                if (FindContractEdge(State.Contract, Route)?.RoutingHint is not { IsActive: true, UnhonouredReason: null }) continue;
                State.HintedRoutes[Route] = new GridLayoutState.HintedRoute(Route.ExitSide, Route.EntrySide,
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToArray());
            }
        }
    }

    private static void PreserveHintedRoutes(GridLayoutState State, Action Operation)
    {
        Operation();
        RefreshHintedRoutes(State);
    }

    private static void RefreshHintedRoutes(GridLayoutState State)
    {
        if (!State.Contract.Edges.Any(Edge => Edge.RoutingHint?.IsActive == true)) return;
        FlowchartRoutingHints.Apply(State.Copy, State.Contract,
            BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction,
            RepairAutomaticRoutesEnabled: false, PreserveValidSideHintRoutes: true);
        foreach (FlowchartLayoutEdgeRoute Route in State.Copy.EdgeRoutes)
        {
            EdgeContract? Edge = FindContractEdge(State.Contract, Route);
            if (Edge?.RoutingHint?.IsActive != true) continue;
            if (Edge.RoutingHint.UnhonouredReason != null
                && State.HintedRoutes.TryGetValue(Route, out GridLayoutState.HintedRoute? Prior))
            {
                Route.ExitSide = Prior.ExitSide;
                Route.EntrySide = Prior.EntrySide;
                Route.Points.Clear();
                foreach ((double X, double Y) in Prior.Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = X, Y = Y });
                continue;
            }
            if (Edge.RoutingHint.UnhonouredReason == null)
                State.HintedRoutes[Route] = new GridLayoutState.HintedRoute(Route.ExitSide, Route.EntrySide,
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToArray());
        }
    }

    private static void CaptureHintedNodePositions(GridLayoutState State)
    {
        State.HintedNodePositions.Clear();
        HashSet<string> Hinted = State.Contract.Nodes
            .Where(Node => Node.PlacementHint?.IsActive == true)
            .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = State.Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        Stack<string> Pending = new(State.Contract.Groups.Where(Group => Group.PlacementHint?.IsActive == true)
            .Select(Group => Group.Id));
        while (Pending.Count > 0)
        {
            string GroupId = Pending.Pop();
            if (!Groups.TryGetValue(GroupId, out GroupContract? Group)) continue;
            Hinted.UnionWith(Group.ChildNodeIds);
            foreach (string ChildGroupId in Group.ChildGroupIds) Pending.Push(ChildGroupId);
        }
        foreach (PlacementHintContract Hint in State.Contract.Nodes.Select(Node => Node.PlacementHint)
                     .Concat(State.Contract.Groups.Select(Group => Group.PlacementHint))
                     .Where(Hint => Hint?.IsActive == true).Cast<PlacementHintContract>())
        {
            if (Hint.PeerId == null) continue;
            if (Groups.ContainsKey(Hint.PeerId))
            {
                Pending.Push(Hint.PeerId);
                while (Pending.Count > 0)
                {
                    string GroupId = Pending.Pop();
                    if (!Groups.TryGetValue(GroupId, out GroupContract? Group)) continue;
                    Hinted.UnionWith(Group.ChildNodeIds);
                    foreach (string ChildGroupId in Group.ChildGroupIds) Pending.Push(ChildGroupId);
                }
            }
            else if (State.Placed.Any(Node => Node.Id == Hint.PeerId))
            {
                Hinted.Add(Hint.PeerId);
            }
        }
        foreach (GridNode Node in State.Placed.Where(Node => Hinted.Contains(Node.Id)))
        {
            State.HintedNodePositions[Node.Id] = (Node.X, Node.Y);
            Node.PlacementHintLocked = true;
        }
    }

    private static class LegalityValidationStage
    {
        // Rule ids: section 10 (final legality checks after all route decisions).
        public static void Apply(GridLayoutState State)
        {
            DiagramGridQuality.Collect(State.Contract, State.Copy,
                State.Copy.RenderContext.LayoutStageViolations, IncludeArrangementCheck: false);
        }
    }

    private static void ResolveHintedTerminalClearance(GridLayoutState State)
    {
        Dictionary<string, GridNode> ById = State.Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        foreach (EdgeContract Edge in State.Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true)
                     .OrderBy(Edge => Edge.CreationIndex))
        {
            RoutingHintContract Hint = Edge.RoutingHint!;
            if (Hint.Leave.HasValue && ById.TryGetValue(Edge.SourceId, out GridNode? Source))
                ClearRay(Source, HintSide(Hint.Leave.Value), 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX,
                    Edge.SourceId, Edge.TargetId);
            if (Hint.Enter.HasValue && ById.TryGetValue(Edge.TargetId, out GridNode? Target))
                ClearRay(Target, HintSide(Hint.Enter.Value), DiagramGridRouteBuilder.CORNER_RADIUS_PX + 10,
                    Edge.SourceId, Edge.TargetId);
        }

        void ClearRay(GridNode Endpoint, string Side, double Length, string SourceId, string TargetId)
        {
            if (State.HintedNodePositions.ContainsKey(Endpoint.Id)) return;
            (double X1, double Y1, double X2, double Y2) Ray(GridNode Node) => Side switch
            {
                // Clearance runs before FlipToYUp, so north is decreasing grid Y.
                "top" => (Node.X, Node.Y - Node.Height / 2.0, Node.X, Node.Y - Node.Height / 2.0 - Length),
                "bottom" => (Node.X, Node.Y + Node.Height / 2.0, Node.X, Node.Y + Node.Height / 2.0 + Length),
                "right" => (Node.X + Node.Width / 2.0, Node.Y, Node.X + Node.Width / 2.0 + Length, Node.Y),
                _ => (Node.X - Node.Width / 2.0, Node.Y, Node.X - Node.Width / 2.0 - Length, Node.Y)
            };
            bool Blocked(GridNode Obstacle)
            {
                (double X1, double Y1, double X2, double Y2) = Ray(Endpoint);
                double Left = Obstacle.X - Obstacle.Width / 2.0;
                double Right = Obstacle.X + Obstacle.Width / 2.0;
                double Bottom = Obstacle.Y - Obstacle.Height / 2.0;
                double Top = Obstacle.Y + Obstacle.Height / 2.0;
                return Math.Abs(X1 - X2) < 0.5
                    ? X1 > Left + 0.5 && X1 < Right - 0.5 && Math.Max(Math.Min(Y1, Y2), Bottom) < Math.Min(Math.Max(Y1, Y2), Top) - 0.5
                    : Y1 > Bottom + 0.5 && Y1 < Top - 0.5 && Math.Max(Math.Min(X1, X2), Left) < Math.Min(Math.Max(X1, X2), Right) - 0.5;
            }

            for (int Guard = 0; Guard < State.Placed.Count; Guard++)
            {
                GridNode? Obstacle = State.Placed.FirstOrDefault(Node => Node != Endpoint
                    && Node.Id != SourceId && Node.Id != TargetId && Blocked(Node));
                if (Obstacle == null) return;
                if (State.HintedNodePositions.ContainsKey(Obstacle.Id)) return;
                bool VerticalRay = Side is "top" or "bottom";
                double Original = VerticalRay ? Obstacle.X : Obstacle.Y;
                bool Moved = false;
                for (int Step = 1; Step <= 64 && !Moved; Step++)
                foreach (int Sign in new[] { -1, 1 })
                {
                    double Candidate = Original + Sign * Step * DiagramGridMetrics.GRID_UNIT_PX;
                    if (VerticalRay) Obstacle.X = Candidate; else Obstacle.Y = Candidate;
                    bool Overlaps = State.Placed.Any(Other => Other != Obstacle
                        && Math.Abs(Obstacle.X - Other.X) < (Obstacle.Width + Other.Width) / 2.0
                        && Math.Abs(Obstacle.Y - Other.Y) < (Obstacle.Height + Other.Height) / 2.0);
                    if (!Overlaps && !Blocked(Obstacle)) { Moved = true; break; }
                }
                if (!Moved)
                {
                    if (VerticalRay) Obstacle.X = Original; else Obstacle.Y = Original;
                    return;
                }
            }
        }
    }
}
