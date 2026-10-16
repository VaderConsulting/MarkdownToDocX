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
            Copy.RenderContext = RenderContext ?? LaidOut.RenderContext ?? FlowchartRenderContext.Current;
            MoveBudget = Copy.RenderContext.MoveBeforeDetourBudget;
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
        public HashSet<string> WrappedChainNodes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ObstacleRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PortSideRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> CrossGroupRouteStates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ConvergenceRouteStates { get; } = new(StringComparer.Ordinal);
        public FlowchartRenderContext.RepairSearchBudget MoveBudget { get; }
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
            if (!State.Open)
            {
                foreach (GridNode Node in State.Placed)
                {
                    Node.X = Math.Round(Node.X / DiagramGridMetrics.GRID_UNIT_PX)
                        * DiagramGridMetrics.GRID_UNIT_PX;
                    Node.Y = Math.Round(Node.Y / DiagramGridMetrics.GRID_UNIT_PX)
                        * DiagramGridMetrics.GRID_UNIT_PX;
                }
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
                Step("ChainWrapping", () =>
                {
                    if (FlowchartChainWrapping.Apply(Contract, Placed, Horizontal))
                        State.WrappedChainNodes.UnionWith(Placed.Select(Node => Node.Id));
                });
                Step("FanPacking", () => FlowchartFanPacking.Apply(Contract, Placed));
                Step("ComponentWrapping", () => State.RepairExteriorCrossings = FlowchartComponentWrapping.Apply(Contract, Placed, Horizontal));
                Step("DisconnectedPacking", () => FlowchartDisconnectedPacking.Apply(Contract, Placed));
                Step("GroupMemberPacking", () => FlowchartGroupMemberPacking.Apply(Contract, Placed));
                Step("GroupedChannelPlacement", () => FlowchartGroupedChannelPlacement.Apply(Contract, Placed));
            }

            if (!Open) SnapGridNodeCentres(Placed);
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
                CompactOverlappingRootGroups(Contract, Placed, Frames, Horizontal, State.Copy.RenderContext);
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
            if (!Open)
            {
                SnapGridNodeCentres(Placed);
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            CaptureHintedNodePositions(State);
            FitFrames(Contract, Placed, Frames, Horizontal,
                RefitTriggers.Count == 0 ? "stage settlement" : string.Join(" + ", RefitTriggers.Distinct(StringComparer.Ordinal)));
            if (!Open)
            {
                SnapGridNodeCentres(Placed);
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
            }
            GroupFrameFitPassObserver?.Invoke($"rule 7.8 post-fit unrelated frame overlaps: {CountUnrelatedFrameOverlaps(Contract, Frames)}");
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
            // All later route hints use page compass directions in this Y-up model:
            // North is positive Y and South is negative Y. SVG painting flips Y back to page-down.
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
                FlowchartExteriorCrossingRepair.Apply(State.Copy,
                    BuildPlacedBounds(State.Placed, State.Frames), State.Contract);
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
            // Rule 5.10 gets the first opportunity to remove a blocked straight
            // connection by moving its blocking nodes on the shared grid.
            // Candidate generation is bounded, and every candidate is compared
            // against the complete score and route-fault set before it is kept.
            int moveCandidateLimit = FlowchartLayoutRules.RepairCandidateMoves;
            (int firstMoved, int moveCandidates) = MoveBeforeDetour(State,
                moveCandidateLimit - State.MoveBudget.CandidateMoves,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves,
                State.MoveBudget.AttemptedMoves);
            HashSet<string> SeenStates = new(StringComparer.Ordinal) { RouteState(State.Copy) };
            List<(double X, double Y)[]> BestRoutes = CaptureRoutes(State.Copy);
            List<(double X, double Y)[]> InitialRepairRoutes = CaptureRoutes(State.Copy);
            double InitialRepairRouteLength = RouteLength(State.Copy);
            Dictionary<string, string> InitialOptionRoutes = RouteGeometry(State.Copy);
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> InitialOptionFindings =
                FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(State.Contract, State.Copy,
                IncludeArrangementCheck: false);
            double BeforeRepairScore = Before.OptimizationScore;
            double BestRouteLength = RouteLength(State.Copy);
            int repairChainDepth = 0;
            while (repairChainDepth < FlowchartLayoutRules.RepairChainDepth)
            {
                for (int PassIndex = 0; PassIndex < Passes.Length; PassIndex++)
                {
                    RepairPass Pass = Passes[PassIndex];
                    string Name = $"repair[{PassIndex}] {Pass.RuleIds}";
                    string BeforePassState = RouteState(State.Copy);
                    Action<string, IReadOnlyList<string>, long, double>? Profile = RouteRepairPassObserver;
                    Dictionary<string, string>? BeforeRoutes = Profile == null ? null : RouteGeometry(State.Copy);
                    long BeforeCalls = FlowchartPaddedVisibilityRouter.GridPathCallCount;
                    double BeforeGridMs = FlowchartPaddedVisibilityRouter.GridPathElapsedMilliseconds;
                    Stopwatch? Timer = Profile == null ? null : Stopwatch.StartNew();
                    MeasureLayoutStage(Name, () => PreserveHintedRoutes(State, () => Pass.Apply(State)));
                    if (State.MoveBudget.CandidateMoves < moveCandidateLimit
                        && State.MoveBudget.AcceptedMoves < FlowchartLayoutRules.RepairChainDepth
                        && RouteState(State.Copy) != BeforePassState)
                    {
                        (int moved, int examined) = MoveBeforeDetour(State,
                            moveCandidateLimit - State.MoveBudget.CandidateMoves,
                            FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves,
                            State.MoveBudget.AttemptedMoves);
                    }
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
                int changedLines = ChangedRouteCount(State.Copy, BestRoutes);
                double repairScore = After.OptimizationScore
                    + (((RouteLength(State.Copy) - BestRouteLength) / DiagramGridMetrics.GRID_UNIT_PX)
                        * FlowchartLayoutRules.RepairLengthWeight)
                    + (changedLines * FlowchartLayoutRules.RepairMovedLineWeight);
                if (changedLines > FlowchartLayoutRules.RepairChainLines
                    || RouteLength(State.Copy) - BestRouteLength > FlowchartLayoutRules.RepairExtraLength
                    || After.Bends - Before.Bends > FlowchartLayoutRules.RepairExtraBends
                    || repairScore >= BeforeRepairScore)
                {
                    RestoreRoutes(State.Copy, BestRoutes);
                    break;
                }

                SeenStates.Add(Signature);
                Before = After;
                BeforeRepairScore = repairScore;
                BestRouteLength = RouteLength(State.Copy);
                BestRoutes = CaptureRoutes(State.Copy);
                repairChainDepth++;
            }
            RouteRepairChainObserver?.Invoke(repairChainDepth,
                ChangedRouteCount(State.Copy, InitialRepairRoutes),
                RouteLength(State.Copy) - InitialRepairRouteLength);
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> FinalOptionFindings =
                FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            Dictionary<string, string> FinalOptionRoutes = RouteGeometry(State.Copy);
            foreach (FlowchartRouteRuleChecker.Finding finding in InitialOptionFindings)
            {
                bool remains = FinalOptionFindings.Any(candidate => candidate.Rule == finding.Rule
                    && candidate.Subject == finding.Subject);
                bool Changed(string id) => FinalOptionRoutes.TryGetValue(id, out string? geometry)
                    && (!InitialOptionRoutes.TryGetValue(id, out string? previous) || previous != geometry);
                if (finding.Rule == "5.9")
                {
                    string[] pair = finding.Subject.Split(" x ", StringSplitOptions.TrimEntries
                        | StringSplitOptions.RemoveEmptyEntries);
                    bool first = pair.Length > 0 && Changed(pair[0]);
                    bool second = pair.Length > 1 && Changed(pair[1]);
                    if (first || second)
                        RouteRepairOptionObserver?.Invoke(first && second ? "reroute-chain"
                            : first ? "reroute-this-line" : "reroute-other-line",
                            finding.Rule + ":" + finding.Subject);
                    else if (remains)
                        RouteRepairOptionObserver?.Invoke("hump", finding.Rule + ":" + finding.Subject);
                }
                else if (Changed(finding.Subject))
                    RouteRepairOptionObserver?.Invoke("reroute-this-line", finding.Rule + ":" + finding.Subject);
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
            if (ReallocatedRoutes.Count > 0
                && State.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft)
            {
                FlowchartManyToManyRoutes.Apply(State.Copy, State.Contract,
                    BuildPlacedBounds(State.Placed, State.Frames), State.Direction);
                ObserveCrossings(State, "coupled post-port track search");
            }
        }

        internal static bool ApplyFinalMoveBeforeDetour(GridLayoutState State)
        {
            if (State.MoveBudget.CandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves
                || State.MoveBudget.AcceptedMoves >= FlowchartLayoutRules.RepairChainDepth) return false;
            int remaining = FlowchartLayoutRules.RepairCandidateMoves - State.MoveBudget.CandidateMoves;
            if (TryMoveGroupForUnroutedLink(State, remaining)) return true;
            remaining = FlowchartLayoutRules.RepairCandidateMoves - State.MoveBudget.CandidateMoves;
            if (remaining <= 0) return false;
            (int moved, int examined) = MoveBeforeDetour(State,
                remaining,
                FlowchartLayoutRules.RepairChainDepth - State.MoveBudget.AcceptedMoves,
                State.MoveBudget.AttemptedMoves);
            return moved > 0;
        }

        private static bool TryMoveGroupForUnroutedLink(GridLayoutState state, int candidateBudget)
        {
            if (candidateBudget <= 0) return false;
            List<string> diagnostics = [];
            foreach (FlowchartLayoutQualityFailure failure in state.Copy.RenderContext.LayoutStageViolations
                         .Where(item => item.Kind == "NoLegalRoute" && item.InvolvedIds.Count > 0).ToArray())
            {
                string edgeId = failure.InvolvedIds[0];
                FlowchartLayoutEdgeRoute? route = state.Copy.EdgeRoutes.FirstOrDefault(item => item.EdgeId == edgeId);
                if (route == null) { diagnostics.Add($"NoLegalRoute:{edgeId}:missing-route-object"); continue; }
                FlowchartLayoutPoint routeStart = route.Points.FirstOrDefault() ?? new FlowchartLayoutPoint();
                FlowchartLayoutPoint routeEnd = route.Points.LastOrDefault() ?? new FlowchartLayoutPoint();
                string[] candidateGroups = new[] { route.SourceId, route.TargetId }
                    .Concat(state.Copy.ClusterBounds.Where(box => Contains(box, routeStart.X, routeStart.Y)
                        || Contains(box, routeEnd.X, routeEnd.Y)).Select(box => box.NodeId))
                    .Where(id => state.Frames.Any(frame => frame.Id == id)
                        && !IsInsideGroup(state.Contract, route.SourceId, id)
                        && !IsInsideGroup(state.Contract, route.TargetId, id))
                    .Distinct(StringComparer.Ordinal).ToArray();
                foreach (string groupId in candidateGroups)
                {
                    GroupContract? group = state.Contract.Groups.FirstOrDefault(item => item.Id == groupId);
                    if (group == null) { diagnostics.Add($"NoLegalRoute:{edgeId}:no-contract-group={groupId}"); continue; }
                    GridNode[] members = state.Placed.Where(node => IsInsideGroup(state.Contract, node.Id, groupId)).ToArray();
                    var MemberIds = members.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
                    if (state.Copy.EdgeRoutes.Count(item => MemberIds.Contains(item.SourceId)
                            || MemberIds.Contains(item.TargetId)) > FlowchartLayoutRules.RepairChainLines) continue;
                    GridFrame[] frames = state.Frames.Where(frame => frame.Id == groupId
                        || IsInsideGroup(state.Contract, frame.Id, groupId)).ToArray();
                    PlacementHintContract? hint = group.PlacementHint?.IsActive == true ? group.PlacementHint : null;
                    (double X, double Y) portDirection = groupId == route.SourceId
                        ? route.ExitSide switch
                        {
                            "right" => (1, 0), "left" => (-1, 0), "top" => (0, 1), "bottom" => (0, -1), _ => (1, 0)
                        }
                        : groupId == route.TargetId ? route.EntrySide switch
                        {
                            "right" => (-1, 0), "left" => (1, 0), "top" => (0, -1), "bottom" => (0, 1), _ => (-1, 0)
                        }
                        : hint?.Direction switch
                        {
                            LayoutHintDirection.East => (1, 0), LayoutHintDirection.West => (-1, 0),
                            LayoutHintDirection.North => (0, 1), LayoutHintDirection.South => (0, -1),
                            _ => (1, 0)
                        };
                    (double X, double Y)[] directions = [portDirection, (1, 0), (-1, 0), (0, 1), (0, -1)];
                    GridFrame? movingFrame = frames.FirstOrDefault(frame => frame.Id == groupId);
                    double stepPx = DiagramGridMetrics.GRID_UNIT_PX;
                    foreach ((double dxUnit, double dyUnit) in directions.Distinct())
                    foreach (int step in ClearanceSteps(movingFrame, groupId, dxUnit, dyUnit, state)
                                 .Concat(Enumerable.Range(1, FlowchartLayoutRules.RepairCandidateMoves))
                                 .Distinct().Take(FlowchartLayoutRules.RepairCandidateMoves))
                    {
                        if (candidateBudget-- <= 0)
                        {
                            MoveBeforeDetourObserver?.Invoke(0, diagnostics.Count, diagnostics);
                            return false;
                        }
                        double dx = dxUnit * step * stepPx;
                        double dy = dyUnit * step * stepPx;
                        state.MoveBudget.CandidateMoves++;
                        var oldNodes = members.ToDictionary(node => node, node => (node.X, node.Y));
                        var oldFrames = frames.ToDictionary(frame => frame,
                            frame => (frame.Left, frame.Right, frame.Top, frame.Bottom));
                        var oldRoutes = state.Copy.EdgeRoutes.Select(item => (Route: item,
                            Points: item.Points.Select(point => (point.X, point.Y)).ToArray(),
                            item.ExitSide, item.EntrySide, item.ExitNormalX, item.ExitNormalY,
                            item.EntryNormalX, item.EntryNormalY)).ToArray();
                        FlowchartLayoutQualityFailure[] oldViolations = state.Copy.RenderContext.LayoutStageViolations.ToArray();
                        FlowchartLayoutScore.Result before = FlowchartLayoutScore.Measure(
                            state.Contract, state.Copy, IncludeArrangementCheck: false);
                        List<FlowchartRouteRuleChecker.Finding> beforeFindings =
                            FlowchartRouteRuleChecker.Check(state.Contract, state.Copy).ToList();
                        beforeFindings.AddRange(state.Copy.RenderContext.LayoutStageViolations
                            .Where(item => item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId))
                            .Select(item => new FlowchartRouteRuleChecker.Finding("NoLegalRoute", edgeId, item.Description)));
                        foreach (GridNode node in members) node.SetPositionFromFrameFit(node.X + dx, node.Y + dy);
                        foreach (GridFrame frame in frames)
                        {
                            frame.Left += (int)dx; frame.Right += (int)dx;
                            frame.Top += (int)dy; frame.Bottom += (int)dy;
                        }
                        bool accepted = false;
                        try
                        {
                            int frameOverlaps = CountUnrelatedFrameOverlaps(state.Contract, state.Frames);
                            WriteBack(state.Copy, state.Placed, state.Frames);
                            AssignPortSides(state.Contract, state.Copy, state.Placed, state.Frames,
                                state.Nodes, state.Direction, state.SharedFanSources);
                            DiagramGridRouteBuilder.RebuildAllRoutes(state.Copy, state.Placed, state.Nodes,
                                state.Contract, state.Frames, state.Direction);
                            DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(state.Copy, state.Placed,
                                state.Nodes, state.Contract, state.Frames, state.Direction);
                            FlowchartRoutingHints.Apply(state.Copy, state.Contract,
                                BuildPlacedBounds(state.Placed, state.Frames), state.Nodes, state.Direction,
                                RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true,
                                ClearUnroutedRoutes: false);
                            FlowchartLayoutScore.Result after = FlowchartLayoutScore.Measure(
                                state.Contract, state.Copy, IncludeArrangementCheck: false);
                            List<FlowchartRouteRuleChecker.Finding> afterFindings =
                                FlowchartRouteRuleChecker.Check(state.Contract, state.Copy).ToList();
                            bool routeRestored = !state.Copy.RenderContext.LayoutStageViolations.Any(item =>
                                item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId))
                                && state.Copy.EdgeRoutes.FirstOrDefault(item => item.EdgeId == edgeId)?.Points.Count >= 2;
                            diagnostics.Add($"NoLegalRoute:{edgeId}:group={groupId}:delta=({dx:0},{dy:0}):restored={routeRestored}:overlap={after.Overlaps}:hints={before.HintsHonoured}->{after.HintsHonoured}:score={before.OptimizationScore:0.##}->{after.OptimizationScore:0.##}:newFindings={!NoNewFindings(beforeFindings, afterFindings)}:title={CountTitleBandRoutes(state)}");
                            accepted = routeRestored && !HasNodeOverlap(state.Placed)
                                && frameOverlaps == 0
                                && after.HintsHonoured >= before.HintsHonoured
                                && after.Overlaps <= before.Overlaps
                                && after.OptimizationScore < before.OptimizationScore - 0.001
                                && NoNewFindings(beforeFindings, afterFindings)
                                && CountTitleBandRoutes(state) == 0;
                            if (accepted)
                            {
                                state.MoveBudget.CandidateMoves++;
                                state.MoveBudget.AcceptedMoves++;
                                state.Copy.RenderContext.LayoutStageViolations.RemoveAll(item =>
                                    item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId));
                                MoveBeforeDetourObserver?.Invoke(1, 1,
                                    [$"NoLegalRoute:{edgeId}:moved-group={groupId}:delta=({dx:0},{dy:0})"]);
                                return true;
                            }
                        }
                        finally
                        {
                            if (!accepted)
                            {
                                foreach ((GridNode node, (double x, double y)) in oldNodes)
                                    node.SetPositionFromFrameFit(x, y);
                                foreach ((GridFrame frame, (int left, int right, int top, int bottom)) in oldFrames)
                                {
                                    frame.Left = left; frame.Right = right; frame.Top = top; frame.Bottom = bottom;
                                }
                                RestoreRouteSnapshot(oldRoutes);
                                state.Copy.RenderContext.LayoutStageViolations.Clear();
                                state.Copy.RenderContext.LayoutStageViolations.AddRange(oldViolations);
                                WriteBack(state.Copy, state.Placed, state.Frames);
                            }
                        }
                    }
                }
            }
            if (diagnostics.Count > 0)
                MoveBeforeDetourObserver?.Invoke(0, diagnostics.Count, diagnostics);
            return false;

            static bool Contains(FlowchartLayoutNodeBounds box, double x, double y) =>
                x >= box.CenterX - box.Width / 2 && x <= box.CenterX + box.Width / 2
                && y >= box.CenterY - box.Height / 2 && y <= box.CenterY + box.Height / 2;

            static IEnumerable<int> ClearanceSteps(GridFrame? movingFrame, string groupId,
                double dx, double dy, GridLayoutState current)
            {
                if (movingFrame == null) yield break;
                double step = DiagramGridMetrics.GRID_UNIT_PX;
                FlowchartLayoutNodeBounds[] clusterSnapshot = current.Copy.ClusterBounds.ToArray();
                foreach (FlowchartLayoutNodeBounds obstacle in clusterSnapshot.Where(box =>
                             box.NodeId != groupId && !IsInsideGroup(current.Contract, box.NodeId, groupId)
                             && !IsInsideGroup(current.Contract, groupId, box.NodeId)))
                {
                    double obstacleLeft = obstacle.CenterX - obstacle.Width / 2;
                    double obstacleRight = obstacle.CenterX + obstacle.Width / 2;
                    double obstacleBottom = obstacle.CenterY - obstacle.Height / 2;
                    double obstacleTop = obstacle.CenterY + obstacle.Height / 2;
                    bool intersects = movingFrame.Left < obstacleRight && movingFrame.Right > obstacleLeft
                        && movingFrame.Bottom < obstacleTop && movingFrame.Top > obstacleBottom;
                    if (!intersects) continue;
                    double needed = dx > 0 ? obstacleRight - movingFrame.Left + step
                        : dx < 0 ? movingFrame.Right - obstacleLeft + step
                        : dy > 0 ? obstacleTop - movingFrame.Bottom + step
                        : movingFrame.Top - obstacleBottom + step;
                    needed += 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;
                    int steps = (int)Math.Ceiling(needed / step);
                    if (steps > 0) yield return steps;
                }
            }

            void RestoreRouteSnapshot((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
                string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY,
                double EntryNormalX, double EntryNormalY)[] snapshots)
            {
                foreach (var saved in snapshots)
                {
                    saved.Route.ExitSide = saved.ExitSide; saved.Route.EntrySide = saved.EntrySide;
                    saved.Route.ExitNormalX = saved.ExitNormalX; saved.Route.ExitNormalY = saved.ExitNormalY;
                    saved.Route.EntryNormalX = saved.EntryNormalX; saved.Route.EntryNormalY = saved.EntryNormalY;
                    saved.Route.Points.Clear();
                    foreach ((double x, double y) in saved.Points)
                        saved.Route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
                }
            }
        }

        private static (int Moved, int Examined) MoveBeforeDetour(GridLayoutState State, int maxCandidates,
            int maxMoves,
            HashSet<string> attempted)
        {
            int examined = 0;
            int moved = 0;
            List<string> unresolved = [];
            while (examined < maxCandidates && moved < maxMoves)
            {
                List<FlowchartRouteRuleChecker.Finding> initialFindings =
                    FlowchartRouteRuleChecker.Check(State.Contract, State.Copy).ToList();
                initialFindings.AddRange(State.Copy.RenderContext.LayoutStageViolations
                    .Where(failure => failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Count > 0)
                    .Select(failure => new FlowchartRouteRuleChecker.Finding(
                        "NoLegalRoute", failure.InvolvedIds[0], failure.Description)));
                List<(string Rule, string Subject, FlowchartLayoutEdgeRoute Route, string[] Blockers)> blockedRoutes = [];
                // A legal detour is still an optimisation opportunity. Waiting
                // for a checker fault excluded all offset-but-legal routes from
                // move-before-detour, so their cheaper aligned placements were
                // never offered. Move either endpoint independently; translating
                // both endpoints together cannot straighten their connection.
                foreach (var Route in State.Copy.EdgeRoutes.Where(Route => Route.Points.Count > 2
                             && FlowchartLayoutScore.CountCorners(Route.Points) > 0
                             && FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive != true))
                {
                    initialFindings.Add(new("5.10", Route.EdgeId, "Compare an aligned endpoint placement with this detour."));
                    blockedRoutes.Add(("5.10", Route.EdgeId, Route, [Route.SourceId]));
                    blockedRoutes.Add(("5.10", Route.EdgeId, Route, [Route.TargetId]));
                }
                foreach (FlowchartRouteRuleChecker.Finding finding in initialFindings.Where(finding =>
                             finding.Rule is "3.2/4.7" or "4.2" or "4.9" or "4.11" or "5.8" or "5.9" or "NoLegalRoute"))
                {
                    string[] edgeIds = finding.Rule switch
                    {
                        "5.9" => finding.Subject.Split(" x ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                        "3.2/4.7" => State.Copy.EdgeRoutes.Where(route =>
                                route.SourceId == finding.Subject || route.TargetId == finding.Subject)
                            .Select(route => route.EdgeId).ToArray(),
                        _ => [finding.Subject]
                    };
                    foreach (FlowchartLayoutEdgeRoute route in State.Copy.EdgeRoutes.Where(route =>
                                 edgeIds.Contains(route.EdgeId, StringComparer.Ordinal) && route.Points.Count >= 2))
                    {
                        string[] blockers = RouteBlockers(route, State.Copy.NodeBounds);
                        if (blockers.Length == 0)
                            blockers = [route.SourceId, route.TargetId];
                        blockedRoutes.Add((finding.Rule, finding.Subject, route, blockers));
                    }
                }
                foreach (FlowchartLayoutQualityFailure finding in State.Copy.RenderContext.LayoutStageViolations
                             .Where(finding => finding.Kind == "TitleBandRoute" && finding.InvolvedIds.Count > 0))
                {
                    FlowchartLayoutEdgeRoute? route = State.Copy.EdgeRoutes.FirstOrDefault(route =>
                        route.EdgeId == finding.InvolvedIds[0]);
                    if (route == null || route.Points.Count < 2) continue;
                    string[] blockers = RouteBlockers(route, State.Copy.NodeBounds);
                    if (blockers.Length == 0) blockers = [route.SourceId, route.TargetId];
                    blockedRoutes.Add((finding.Kind, finding.InvolvedIds[0], route, blockers));
                }
                blockedRoutes = blockedRoutes.DistinctBy(item =>
                        item.Rule + "|" + item.Subject + "|" + item.Route.EdgeId + "|" + string.Join(",", item.Blockers))
                    .OrderBy(item => item.Rule == "5.9" ? 0 : 1)
                    .ThenBy(item => item.Rule == "5.9"
                        && ((State.Nodes.TryGetValue(item.Route.SourceId, out NodeContract? sourceNode)
                                && sourceNode.PlacementHint?.Kind == PlacementHintKind.Push
                                && !(State.Nodes.TryGetValue(item.Route.TargetId, out NodeContract? targetNode)
                                    && targetNode.PlacementHint?.IsActive == true))
                            || (State.Nodes.TryGetValue(item.Route.TargetId, out NodeContract? targetNode2)
                                && targetNode2.PlacementHint?.Kind == PlacementHintKind.Push
                                && !(State.Nodes.TryGetValue(item.Route.SourceId, out NodeContract? sourceNode2)
                                    && sourceNode2.PlacementHint?.IsActive == true))) ? 0 : 1)
                    .ThenBy(item => item.Rule, StringComparer.Ordinal)
                    .ThenBy(item => item.Subject, StringComparer.Ordinal)
                    .ThenBy(item => item.Route.EdgeId, StringComparer.Ordinal).ToList();
                if (blockedRoutes.Count == 0) break;
                FlowchartLayoutScore.Result initialScore = FlowchartLayoutScore.Measure(
                    State.Contract, State.Copy, IncludeArrangementCheck: false);
                foreach (FlowchartRouteRuleChecker.Finding finding in initialFindings
                             .DistinctBy(item => item.Rule + "|" + item.Subject, StringComparer.Ordinal))
                    RouteRepairCandidateObserver?.Invoke("hump", finding.Rule + ":" + finding.Subject,
                        initialScore.OptimizationScore, false);
                Dictionary<string, (double X, double Y)> oldPositions = State.Placed.ToDictionary(node => node.Id,
                    node => (node.X, node.Y), StringComparer.Ordinal);
                Dictionary<GridFrame, (int Left, int Right, int Top, int Bottom)> oldFrames = State.Frames
                    .ToDictionary(frame => frame, frame => (frame.Left, frame.Right, frame.Top, frame.Bottom));
                (FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points, string ExitSide,
                    string EntrySide, double ExitNormalX, double ExitNormalY, double EntryNormalX,
                    double EntryNormalY)[] oldRoutes = State.Copy.EdgeRoutes.Select(candidate => (candidate,
                        candidate.Points.Select(point => (point.X, point.Y)).ToArray(), candidate.ExitSide,
                        candidate.EntrySide, candidate.ExitNormalX, candidate.ExitNormalY,
                        candidate.EntryNormalX, candidate.EntryNormalY)).ToArray();
                Dictionary<string, (double X, double Y)>? bestPositions = null;
                Dictionary<GridFrame, (int Left, int Right, int Top, int Bottom)>? bestFrames = null;
                (FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points, string ExitSide,
                    string EntrySide, double ExitNormalX, double ExitNormalY, double EntryNormalX,
                    double EntryNormalY)[]? bestRoutes = null;
                double bestScore = initialScore.OptimizationScore;
                double bestExtraLength = 0;
                int bestExtraBends = 0;
                int bestChangedLines = 0;
                string? bestSubject = null;
                foreach ((string rule, string subject, FlowchartLayoutEdgeRoute route, string[] blockers) in blockedRoutes)
                {
                    if (!initialFindings.Any(item => item.Rule == rule && item.Subject == subject)
                        && rule != "TitleBandRoute") continue;
                    FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
                    bool horizontal = Math.Abs(start.Y - end.Y) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
                    bool vertical = Math.Abs(start.X - end.X) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
                    unresolved.Add($"{rule}:{subject}:{string.Join(",", blockers)}");
                    string[] movableBlockers = blockers.Where(id =>
                        State.Placed.FirstOrDefault(node => node.Id == id)?.PlacementHintLocked != true
                        && !(State.Nodes.TryGetValue(id, out NodeContract? node)
                            && node.PlacementHint?.IsActive == true)
                        && !HasHintedContainingGroup(State.Contract, id)).ToArray();
                    if (movableBlockers.Length == 0) continue;

                    // Moving all nodes that jointly obstruct this straight run as
                    // one rigid candidate preserves their relative group membership.
                    // The move must also keep every member inside its existing frames.
                    List<(double X, double Y)> moveOptions = [];
                    if (rule == "5.10")
                    {
                        foreach (string Id in movableBlockers)
                        {
                            GridNode? Node = State.Placed.FirstOrDefault(Node => Node.Id == Id);
                            string PeerId = Id == route.SourceId ? route.TargetId : route.SourceId;
                            GridNode? Peer = State.Placed.FirstOrDefault(Node => Node.Id == PeerId);
                            if (Node == null || Peer == null) continue;
                            double Dx = Math.Round((Peer.X - Node.X) / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX;
                            double Dy = Math.Round((Peer.Y - Node.Y) / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX;
                            if (Math.Abs(Dx) > 0.1) moveOptions.Add((Dx, 0));
                            if (Math.Abs(Dy) > 0.1) moveOptions.Add((0, Dy));
                        }
                    }
                if (rule == "5.9")
                    {
                        // For an unhinted endpoint that is stranded far from its
                        // hinted peer, test the nearest same-row/column slot beside
                        // that peer before local one-grid nudges. This lets 5.10
                        // resolve long backward links by moving the free endpoint
                        // in the hinted chain's direction.
                        foreach (string movableId in movableBlockers)
                        {
                            GridNode? movable = State.Placed.FirstOrDefault(node => node.Id == movableId);
                            if (movable == null) continue;
                            foreach (FlowchartLayoutEdgeRoute incident in State.Copy.EdgeRoutes.Where(candidate =>
                                         candidate.SourceId == movableId || candidate.TargetId == movableId))
                            {
                                string peerId = incident.SourceId == movableId ? incident.TargetId : incident.SourceId;
                                GridNode? peer = State.Placed.FirstOrDefault(node => node.Id == peerId);
                                if (peer == null) continue;
                                double signX = Math.Sign(peer.X - movable.X);
                                double targetX = peer.X - signX * ((peer.Width + movable.Width) / 2.0
                                    + 2 * DiagramGridMetrics.GRID_UNIT_PX);
                                double targetY = peer.Y;
                                double dx = Math.Round((targetX - movable.X) / DiagramGridMetrics.GRID_UNIT_PX)
                                    * DiagramGridMetrics.GRID_UNIT_PX;
                                double dy = Math.Round((targetY - movable.Y) / DiagramGridMetrics.GRID_UNIT_PX)
                                    * DiagramGridMetrics.GRID_UNIT_PX;
                                if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1) moveOptions.Add((dx, dy));
                            }
                        }
                    }
                    int[][] directions = horizontal
                        ? [[0, 1], [0, -1], [1, 0], [-1, 0]]
                        : vertical
                            ? [[1, 0], [-1, 0], [0, 1], [0, -1]]
                            : [[0, 1], [0, -1], [1, 0], [-1, 0]];
                    moveOptions.AddRange(directions.Select(direction =>
                        ((double)direction[0] * DiagramGridMetrics.GRID_UNIT_PX,
                         (double)direction[1] * DiagramGridMetrics.GRID_UNIT_PX)));
                    foreach ((double dx, double dy) in moveOptions.Distinct())
                    {
                        if (examined >= maxCandidates) break;
                        string moveKey = rule + "|" + subject + "|" + route.EdgeId + "|"
                            + string.Join(",", movableBlockers) + "|" + dx + "," + dy;
                        if (!attempted.Add(moveKey)) continue;
                        examined++;
                        State.MoveBudget.CandidateMoves++;
                        if (TryMoveBlockers(State, rule, subject, movableBlockers, dx, dy,
                                initialScore, initialFindings))
                        {
                            FlowchartLayoutScore.Result candidateScore = FlowchartLayoutScore.Measure(
                                State.Contract, State.Copy, IncludeArrangementCheck: false);
                            double candidateExtraLength = RouteLength(State.Copy) - RouteLength(oldRoutes);
                            int candidateExtraBends = RouteBends(State.Copy) - RouteBends(oldRoutes);
                            int candidateChangedLines = ChangedRouteCount(State.Copy, oldRoutes);
                            double repairScore = candidateScore.OptimizationScore
                                + ((candidateExtraLength / DiagramGridMetrics.GRID_UNIT_PX)
                                    * FlowchartLayoutRules.RepairLengthWeight)
                                + (candidateChangedLines * FlowchartLayoutRules.RepairMovedLineWeight);
                            RouteRepairCandidateObserver?.Invoke("move", rule + ":" + subject,
                                repairScore, true);
                            if (repairScore < bestScore - 0.001)
                            {
                                bestScore = repairScore;
                                bestExtraLength = candidateExtraLength;
                                bestExtraBends = candidateExtraBends;
                                bestChangedLines = candidateChangedLines;
                                bestSubject = rule + ":" + subject;
                                bestPositions = State.Placed.ToDictionary(node => node.Id,
                                    node => (node.X, node.Y), StringComparer.Ordinal);
                                bestFrames = State.Frames.ToDictionary(frame => frame,
                                    frame => (frame.Left, frame.Right, frame.Top, frame.Bottom));
                                bestRoutes = State.Copy.EdgeRoutes.Select(candidate => (candidate,
                                    candidate.Points.Select(point => (point.X, point.Y)).ToArray(),
                                    candidate.ExitSide, candidate.EntrySide, candidate.ExitNormalX,
                                    candidate.ExitNormalY, candidate.EntryNormalX, candidate.EntryNormalY)).ToArray();
                            }
                            RestoreOriginal();
                        }
                    }
                    if (examined >= maxCandidates) break;
                }
                if (bestPositions == null || bestFrames == null || bestRoutes == null) break;

                foreach (GridNode node in State.Placed)
                    if (bestPositions.TryGetValue(node.Id, out (double X, double Y) position))
                        node.SetPositionFromFrameFit(position.X, position.Y);
                State.Frames.Clear();
                foreach ((GridFrame frame, (int left, int right, int top, int bottom)) in bestFrames)
                {
                    frame.Left = left;
                    frame.Right = right;
                    frame.Top = top;
                    frame.Bottom = bottom;
                    State.Frames.Add(frame);
                }
                WriteBack(State.Copy, State.Placed, State.Frames);
                RestoreRouteSnapshot(bestRoutes);
                moved++;
                State.MoveBudget.AcceptedMoves++;
                RouteRepairOptionObserver?.Invoke("move", bestSubject ?? string.Empty);
                if (bestSubject != null)
                    unresolved.Add($"WIN:{bestSubject}:candidate={State.MoveBudget.CandidateMoves}:lines={bestChangedLines}:extraLength={bestExtraLength:0.##}:extraBends={bestExtraBends}");
                // One accepted move is the cheapest option for this fault. Chained
                // repairs are reconsidered after routing changes the dependency set.
                break;

                void RestoreOriginal()
                {
                    foreach (GridNode node in State.Placed)
                        if (oldPositions.TryGetValue(node.Id, out (double X, double Y) position))
                            node.SetPositionFromFrameFit(position.X, position.Y);
                    State.Frames.Clear();
                    foreach ((GridFrame frame, (int left, int right, int top, int bottom)) in oldFrames)
                    {
                        frame.Left = left;
                        frame.Right = right;
                        frame.Top = top;
                        frame.Bottom = bottom;
                        State.Frames.Add(frame);
                    }
                    WriteBack(State.Copy, State.Placed, State.Frames);
                    RestoreRouteSnapshot(oldRoutes);
                }

                void RestoreRouteSnapshot((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
                    string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY,
                    double EntryNormalX, double EntryNormalY)[] snapshot)
                {
                    foreach (var item in snapshot)
                    {
                        item.Route.ExitSide = item.ExitSide;
                        item.Route.EntrySide = item.EntrySide;
                        item.Route.ExitNormalX = item.ExitNormalX;
                        item.Route.ExitNormalY = item.ExitNormalY;
                        item.Route.EntryNormalX = item.EntryNormalX;
                        item.Route.EntryNormalY = item.EntryNormalY;
                        item.Route.Points.Clear();
                        foreach ((double x, double y) in item.Points)
                            item.Route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
                    }
                }
            }
            DiagramGridLayout.MoveBeforeDetourObserver?.Invoke(moved, examined,
                unresolved.Distinct(StringComparer.Ordinal).ToArray());
            if ((examined >= maxCandidates || moved >= maxMoves)
                && State.Copy.EdgeRoutes.Any(route => route.Points.Count >= 3
                && (Math.Abs(route.Points[0].X - route.Points[^1].X) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                    || Math.Abs(route.Points[0].Y - route.Points[^1].Y) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)))
            {
                State.Copy.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                    "MoveBeforeDetourLimit",
                    $"Rule 5.10 reached a configured candidate or chain bound; remaining faults continue to route repair.",
                    [], null));
            }
            return (moved, examined);
        }

        private static double RouteLength(FlowchartLayoutResult layout) => RouteLength(layout.EdgeRoutes.Select(route =>
            (route, route.Points.Select(point => (point.X, point.Y)).ToArray(), route.ExitSide, route.EntrySide,
                route.ExitNormalX, route.ExitNormalY, route.EntryNormalX, route.EntryNormalY)).ToArray());

        private static double RouteLength((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
            string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY,
            double EntryNormalX, double EntryNormalY)[] routes) => routes.Sum(route =>
                Enumerable.Range(1, Math.Max(0, route.Points.Length - 1)).Sum(index =>
                    Math.Abs(route.Points[index].X - route.Points[index - 1].X)
                    + Math.Abs(route.Points[index].Y - route.Points[index - 1].Y)));

        private static int RouteBends(FlowchartLayoutResult layout) => layout.EdgeRoutes.Sum(route =>
            CountRouteBends(route.Points.Select(point => (point.X, point.Y)).ToArray()));

        private static int RouteBends((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
            string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY,
            double EntryNormalX, double EntryNormalY)[] routes) => routes.Sum(route => CountRouteBends(route.Points));

        private static int CountRouteBends((double X, double Y)[] points)
        {
            int bends = 0;
            for (int index = 1; index + 1 < points.Length; index++)
            {
                bool horizontalBefore = Math.Abs(points[index].Y - points[index - 1].Y) <= 0.5;
                bool horizontalAfter = Math.Abs(points[index + 1].Y - points[index].Y) <= 0.5;
                bool verticalBefore = Math.Abs(points[index].X - points[index - 1].X) <= 0.5;
                bool verticalAfter = Math.Abs(points[index + 1].X - points[index].X) <= 0.5;
                if ((horizontalBefore && verticalAfter) || (verticalBefore && horizontalAfter)) bends++;
            }
            return bends;
        }

        private static int ChangedRouteCount(FlowchartLayoutResult layout,
            (FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points, string ExitSide, string EntrySide,
                double ExitNormalX, double ExitNormalY, double EntryNormalX, double EntryNormalY)[] before)
        {
            int changed = 0;
            int count = Math.Min(layout.EdgeRoutes.Count, before.Length);
            for (int index = 0; index < count; index++)
            {
                FlowchartLayoutEdgeRoute route = layout.EdgeRoutes[index];
                var prior = before[index];
                if (route.EdgeId != prior.Route.EdgeId
                    || RouteSignature(prior.Points, prior.ExitSide, prior.EntrySide)
                    != RouteSignature(route.Points.Select(point => (point.X, point.Y)).ToArray(),
                        route.ExitSide, route.EntrySide)) changed++;
            }
            return changed + Math.Abs(layout.EdgeRoutes.Count - before.Length);
        }

        private static int ChangedRouteCount(FlowchartLayoutResult layout, List<(double X, double Y)[]> before)
        {
            int changed = 0;
            int count = Math.Min(layout.EdgeRoutes.Count, before.Count);
            for (int index = 0; index < count; index++)
            {
                IReadOnlyList<FlowchartLayoutPoint> current = layout.EdgeRoutes[index].Points;
                (double X, double Y)[] prior = before[index];
                if (current.Count != prior.Length || current.Where((point, pointIndex) =>
                        pointIndex >= prior.Length || Math.Abs(point.X - prior[pointIndex].X) > 0.001
                            || Math.Abs(point.Y - prior[pointIndex].Y) > 0.001).Any()) changed++;
            }
            return changed + Math.Abs(layout.EdgeRoutes.Count - before.Count);
        }

        private static string RouteSignature((double X, double Y)[] points, string exit, string entry) =>
            exit + ":" + entry + ":" + string.Join(";", points.Select(point =>
                point.X.ToString("R", CultureInfo.InvariantCulture) + "," + point.Y.ToString("R", CultureInfo.InvariantCulture)));

        private static string[] RouteBlockers(FlowchartLayoutEdgeRoute route,
            IReadOnlyList<FlowchartLayoutNodeBounds> nodeBounds) => nodeBounds
            .Where(box => box.NodeId != route.SourceId && box.NodeId != route.TargetId
                && Enumerable.Range(1, Math.Max(0, route.Points.Count - 1)).Any(index =>
                    SegmentHitsNode(route.Points[index - 1], route.Points[index], box)))
            .Select(box => box.NodeId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

        private static bool TryMoveBlockers(GridLayoutState State, string targetRule, string routeSubject, string[] blockerIds,
            double dx, double dy, FlowchartLayoutScore.Result before,
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> beforeFindings)
        {
            blockerIds = blockerIds.Distinct(StringComparer.Ordinal).ToArray();
            Dictionary<string, GridNode> byId = State.Placed.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var Wrapped = blockerIds.Where(State.WrappedChainNodes.Contains).Where(byId.ContainsKey)
                .Select(Id => byId[Id]).ToArray();
            if (Wrapped.Length > 0)
            {
                // Wrapping establishes one shared lattice. A later repair may
                // grow a row or column, but cannot stagger just one of its rows.
                blockerIds = blockerIds.Concat(State.Placed.Where(Node => State.WrappedChainNodes.Contains(Node.Id)
                    && Wrapped.Any(Anchor => Math.Abs(dx) > 0.1 && Math.Abs(Node.X - Anchor.X) < 0.001
                        || Math.Abs(dy) > 0.1 && Math.Abs(Node.Y - Anchor.Y) < 0.001)).Select(Node => Node.Id)).Distinct().ToArray();
            }
            GridNode[] moving = blockerIds.Select(id => byId.GetValueOrDefault(id)).Where(node => node != null)
                .Cast<GridNode>().ToArray();
            if (moving.Length != blockerIds.Length || moving.Length == 0) return false;
            var MovingIds = moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            if (State.Copy.EdgeRoutes.Count(Route => MovingIds.Contains(Route.SourceId)
                    || MovingIds.Contains(Route.TargetId)) > FlowchartLayoutRules.RepairChainLines) return false;
            if (moving.Any(Node => Node.PlacementHintLocked
                    || State.Nodes.GetValueOrDefault(Node.Id)?.PlacementHint?.IsActive == true
                    || HasHintedContainingGroup(State.Contract, Node.Id))) return false;
            Dictionary<GridNode, (double X, double Y)> oldPositions = moving.ToDictionary(node => node, node => (node.X, node.Y));
            var oldFrames = State.Frames.ToArray();
            var oldNodeBounds = State.Copy.NodeBounds.ToArray();
            var oldClusterBounds = State.Copy.ClusterBounds.ToArray();
            int oldFrameOverlaps = CountUnrelatedFrameOverlaps(State.Contract, State.Frames);
            var oldRoutes = State.Copy.EdgeRoutes.Select(route => (Route: route,
                Points: route.Points.Select(point => (point.X, point.Y)).ToArray(),
                route.ExitSide, route.EntrySide, route.ExitNormalX, route.ExitNormalY,
                route.EntryNormalX, route.EntryNormalY)).ToArray();
            FlowchartLayoutQualityFailure[] oldViolations = State.Copy.RenderContext.LayoutStageViolations.ToArray();
            int oldTitleBandRoutes = CountTitleBandRoutes(State);
            foreach (GridNode node in moving) node.SetPositionFromFrameFit(node.X + dx, node.Y + dy);
            State.Frames.Clear();
            BuildFramesYUp(State.Contract, State.Placed, State.Frames);
            bool membershipPreserved = moving.All(node => State.Contract.Groups
                .Where(group => GroupContainsNode(State.Contract, group.Id, node.Id))
                .All(group => State.Frames.FirstOrDefault(frame => frame.Id == group.Id) is not GridFrame frame
                    || NodeInsideFrame(node, frame)));
            if (!membershipPreserved || HasNodeOverlap(State.Placed)
                || CountUnrelatedFrameOverlaps(State.Contract, State.Frames) > oldFrameOverlaps)
            {
                Restore();
                return false;
            }
            WriteBack(State.Copy, State.Placed, State.Frames);
            AssignPortSides(State.Contract, State.Copy, State.Placed, State.Frames,
                State.Nodes, State.Direction, State.SharedFanSources);
            HashSet<string> movedIds = moving.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            IReadOnlyList<FlowchartLayoutNodeBounds> newBounds = BuildPlacedBounds(State.Placed, State.Frames).Values.ToArray();
            HashSet<FlowchartLayoutEdgeRoute> affectedRoutes = State.Copy.EdgeRoutes.Where(route =>
            {
                if (movedIds.Contains(route.SourceId) || movedIds.Contains(route.TargetId)
                    || RouteBlockers(route, newBounds).Any(movedIds.Contains)) return true;
                var oldRoute = oldRoutes.FirstOrDefault(snapshot => ReferenceEquals(snapshot.Route, route));
                return oldRoute.Route != null
                    && (oldRoute.ExitSide != route.ExitSide || oldRoute.EntrySide != route.EntrySide
                        || Math.Abs(oldRoute.ExitNormalX - route.ExitNormalX) > 1e-9
                        || Math.Abs(oldRoute.ExitNormalY - route.ExitNormalY) > 1e-9
                        || Math.Abs(oldRoute.EntryNormalX - route.EntryNormalX) > 1e-9
                        || Math.Abs(oldRoute.EntryNormalY - route.EntryNormalY) > 1e-9);
            }).ToHashSet();
            DiagramGridRouteBuilder.RebuildAllRoutes(State.Copy, State.Placed, State.Nodes,
                State.Contract, State.Frames, State.Direction, affectedRoutes);
            DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(State.Copy, State.Placed, State.Nodes,
                State.Contract, State.Frames, State.Direction, affectedRoutes);
            if (State.Contract.Edges.Any(edge => edge.RoutingHint?.IsActive == true))
                FlowchartRoutingHints.Apply(State.Copy, State.Contract,
                    BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction,
                    RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true,
                    ClearUnroutedRoutes: false);
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> afterFindings =
                FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            FlowchartLayoutScore.Result after = FlowchartLayoutScore.Measure(
                State.Contract, State.Copy, IncludeArrangementCheck: false);
            int newTitleBandRoutes = CountTitleBandRoutes(State);
            double extraLength = RouteLength(State.Copy) - RouteLength(oldRoutes);
            int extraBends = RouteBends(State.Copy) - RouteBends(oldRoutes);
            int changedLines = ChangedRouteCount(State.Copy, oldRoutes);
            double repairScore = after.OptimizationScore
                + ((extraLength / DiagramGridMetrics.GRID_UNIT_PX) * FlowchartLayoutRules.RepairLengthWeight)
                + (changedLines * FlowchartLayoutRules.RepairMovedLineWeight);
            bool targetFaultRemoved = targetRule == "TitleBandRoute"
                ? newTitleBandRoutes < oldTitleBandRoutes
                : targetRule == "NoLegalRoute"
                    ? !State.Copy.RenderContext.LayoutStageViolations.Any(failure =>
                          failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(routeSubject))
                        && !afterFindings.Any(finding => finding.Rule == "3.1" && finding.Subject == routeSubject)
                : !afterFindings.Any(finding => finding.Rule == targetRule && finding.Subject == routeSubject);
            if (targetFaultRemoved && after.Overlaps == 0 && after.Crossings <= before.Crossings
                && repairScore < before.OptimizationScore - 0.001
                && newTitleBandRoutes <= oldTitleBandRoutes
                && NoNewFindings(beforeFindings, afterFindings)) return true;
            Restore();
            return false;

            void Restore()
            {
                foreach ((GridNode node, (double x, double y)) in oldPositions)
                    node.SetPositionFromFrameFit(x, y);
                State.Frames.Clear();
                State.Frames.AddRange(oldFrames);
                State.Copy.NodeBoundsMutable.Clear(); State.Copy.NodeBoundsMutable.AddRange(oldNodeBounds);
                State.Copy.ClusterBoundsMutable.Clear(); State.Copy.ClusterBoundsMutable.AddRange(oldClusterBounds);
                foreach (var snapshot in oldRoutes)
                {
                    snapshot.Route.ExitSide = snapshot.ExitSide;
                    snapshot.Route.EntrySide = snapshot.EntrySide;
                    snapshot.Route.ExitNormalX = snapshot.ExitNormalX;
                    snapshot.Route.ExitNormalY = snapshot.ExitNormalY;
                    snapshot.Route.EntryNormalX = snapshot.EntryNormalX;
                    snapshot.Route.EntryNormalY = snapshot.EntryNormalY;
                    snapshot.Route.Points.Clear();
                    foreach ((double x, double y) in snapshot.Points)
                        snapshot.Route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
                }
                State.Copy.RenderContext.LayoutStageViolations.Clear();
                State.Copy.RenderContext.LayoutStageViolations.AddRange(oldViolations);
            }
        }

        private static int CountTitleBandRoutes(GridLayoutState State)
        {
            Dictionary<string, FlowchartLayoutNodeBounds> bounds = State.Copy.NodeBounds
                .Concat(State.Copy.ClusterBounds).GroupBy(box => box.NodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            IReadOnlyList<FlowchartFrameTitleBandHelper.Band> bands =
                FlowchartFrameTitleBandHelper.Build(State.Contract, bounds);
            return State.Copy.EdgeRoutes.Count(route => route.Points.Count >= 2
                && FlowchartFrameTitleBandHelper.RouteEntersAny(
                    route.Points.Select(point => (point.X, point.Y)).ToArray(), bands,
                    route.SourceId, route.TargetId, State.Contract));
        }


        private static bool NoNewFindings(IReadOnlyList<FlowchartRouteRuleChecker.Finding> before,
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> after)
        {
            Dictionary<string, int> counts = before.GroupBy(item => item.Rule + "|" + item.Subject,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            foreach (IGrouping<string, FlowchartRouteRuleChecker.Finding> group in after.GroupBy(
                         item => item.Rule + "|" + item.Subject, StringComparer.Ordinal))
                if (group.Count() > counts.GetValueOrDefault(group.Key)) return false;
            return true;
        }

        private static bool HasNodeOverlap(IReadOnlyList<GridNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
                if (Math.Abs(nodes[i].X - nodes[j].X) < (nodes[i].Width + nodes[j].Width) / 2.0
                    && Math.Abs(nodes[i].Y - nodes[j].Y) < (nodes[i].Height + nodes[j].Height) / 2.0)
                    return true;
            return false;
        }

        /// <summary>Prevents automatic movement from displacing a member of a group fixed by a placement hint.</summary>
        /// <param name="contract">The group hierarchy and placement hints.</param>
        /// <param name="nodeId">The member being considered for movement.</param>
        /// <returns>True when any containing group has an active placement hint.</returns>
        internal static bool HasHintedContainingGroup(DiagramContract contract, string nodeId) =>
            contract.Groups.Any(group => group.PlacementHint?.IsActive == true
                && GroupContainsNode(contract, group.Id, nodeId));

        private static bool GroupContainsNode(DiagramContract contract, string groupId, string nodeId)
        {
            GroupContract? group = contract.Groups.FirstOrDefault(item => item.Id == groupId);
            return group != null && (group.ChildNodeIds.Contains(nodeId, StringComparer.Ordinal)
                || group.ChildGroupIds.Any(child => GroupContainsNode(contract, child, nodeId)));
        }

        private static bool NodeInsideFrame(GridNode node, GridFrame frame) =>
            node.X - node.Width / 2.0 >= frame.Left + frame.Padding
            && node.X + node.Width / 2.0 <= frame.Right - frame.Padding
            && node.Y - node.Height / 2.0 >= frame.Bottom + frame.Padding
            && node.Y + node.Height / 2.0 <= frame.Top - frame.Padding;

        private static bool SegmentHitsNode(FlowchartLayoutPoint start, FlowchartLayoutPoint end,
            FlowchartLayoutNodeBounds box)
        {
            double left = box.CenterX - box.Width / 2.0, right = box.CenterX + box.Width / 2.0;
            double bottom = box.CenterY - box.Height / 2.0, top = box.CenterY + box.Height / 2.0;
            double tolerance = DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
            if (Math.Abs(start.X - end.X) <= tolerance)
                return start.X > left + tolerance && start.X < right - tolerance
                    && Math.Min(start.Y, end.Y) < top - tolerance && Math.Max(start.Y, end.Y) > bottom + tolerance;
            if (Math.Abs(start.Y - end.Y) <= tolerance)
                return start.Y > bottom + tolerance && start.Y < top - tolerance
                    && Math.Min(start.X, end.X) < right - tolerance && Math.Max(start.X, end.X) > left + tolerance;
            return false;
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
            foreach (var Finding in FlowchartRouteRuleChecker.Check(State.Contract, State.Copy)
                         .Where(Finding => Finding.Rule is "10" or "5.1"))
                State.Copy.RenderContext.LayoutStageViolations.Add(new(
                    Finding.Rule == "10" ? "RouteLegality" : "SharedTrack",
                    Finding.Description, [Finding.Subject], null));
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
