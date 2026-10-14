using System;
using System.Collections.Generic;
using System.Globalization;
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
        public FlowchartDirection Direction { get; set; }
        public bool Horizontal { get; set; }
        public bool Open { get; set; }
        public int BoxWidth { get; set; }
        public int BoxHeight { get; set; }
        public int DiamondHeight { get; set; }
        public int MainPitch { get; set; }
        public int CrossPitch { get; set; }
        public Dictionary<string, NodeContract> Nodes { get; set; } = new(StringComparer.Ordinal);
        public List<GridNode> Placed { get; } = [];
        public List<FlowchartPartnerRankLayout.Partners> PartnerRanks { get; set; } = [];
        public HashSet<string> SharedFanSources { get; set; } = new(StringComparer.Ordinal);
        public bool RepairExteriorCrossings { get; set; }
        public List<GridFrame> Frames { get; } = [];
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

            if (!Open)
            {
                ResolveVerticalChordBlockers(Contract, Placed);
                if (!ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    AlignForwardMainChainColumn(Contract, Placed, Horizontal);
                AlignFanInMergeColumn(Contract, Placed, Horizontal);
                if (!ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    AlignForwardMainChainColumn(Contract, Placed, Horizontal);
                if (ShouldUseSpineColumnLayout(Contract, Placed, Direction))
                    AlignTwoWayDecisionSideTargets(Contract, Placed);
                AlignBypassDetourColumns(Contract, Placed, Horizontal);
                AlignDecisionCentreChildColumn(Contract, Placed, Horizontal);
                AlignTerminalDecisionTargets(Contract, Placed, Horizontal, CrossPitch, CoordinatesPlaced: false);
                CentreTerminalFans(Contract, Placed, Horizontal, CoordinatesPlaced: false);
                AlignSingleTerminalChildren(Contract, Placed, Horizontal, CoordinatesPlaced: false);
                CascadeColumns(Placed);
                PlaceOnGrid(Placed, Horizontal, BoxWidth, BoxHeight, MainPitch, CrossPitch, Copy, Contract);
                if (!Horizontal) ApplySpineColumnAdjustments(Contract, Placed, Direction);
                AlignTerminalDecisionTargets(Contract, Placed, Horizontal, CrossPitch, CoordinatesPlaced: true);
                CentreTerminalFans(Contract, Placed, Horizontal, CoordinatesPlaced: true);
                AlignSingleTerminalChildren(Contract, Placed, Horizontal, CoordinatesPlaced: true);
                AlignUnbranchedDecisionInputs(Contract, Placed, Horizontal);
                AlignHorizontalRetryOutcomes(Contract, Placed, Horizontal);
                AlignSimpleDecisionMerges(Contract, Placed, Horizontal, CrossPitch);
                AlignDecisionDetours(Contract, Placed, Horizontal, CrossPitch);
                AlignVerticalLoopBranches(Contract, Placed, Horizontal, CrossPitch);
                State.PartnerRanks = FlowchartPartnerRankLayout.Find(Contract, Placed, Horizontal);
                FlowchartPartnerRankLayout.Prepare(State.PartnerRanks, Horizontal);
                CompactIndependentFanIn(Contract, Placed, Horizontal, CrossPitch);
                State.SharedFanSources = AlignSharedFanSourcesAndTails(Contract, Placed, Horizontal);
                FlowchartCircularJunctionLayout.AlignFans(Contract, Placed, Horizontal);
            }
            else
            {
                State.PartnerRanks = FlowchartPartnerRankLayout.Find(Contract, Placed, Horizontal);
                FlowchartPartnerRankLayout.Prepare(State.PartnerRanks, Horizontal, Align: false);
            }

            GrowCrowdedSides(Contract, Placed, Copy, BoxWidth, BoxHeight);
            GrowParallelRelationshipSides(Contract, Placed, Horizontal);
            SeparateOverlaps(Placed, Horizontal);
            if (!Open)
            {
                FlowchartTerminalBranchPlacement.Apply(Contract, Placed, Horizontal, CrossPitch);
                FlowchartTerminalInputPlacement.Apply(Contract, Placed, Horizontal, CrossPitch);
                FlowchartGridCompaction.Apply(Placed, Horizontal, Math.Max(CrossPitch, Contract.Groups.Count > 0 ? 60 : DiagramGridMetrics.NODE_GAP_PX));
                FlowchartSparseRankCompaction.Apply(Contract, Placed, Horizontal);
                FlowchartJunctionBranchCompaction.Apply(Contract, Placed, Horizontal);
                FlowchartTerminalRowPlacement.Apply(Contract, Placed, Horizontal);
                FlowchartChainWrapping.Apply(Contract, Placed, Horizontal);
                FlowchartFanPacking.Apply(Contract, Placed);
                State.RepairExteriorCrossings = FlowchartComponentWrapping.Apply(Contract, Placed, Horizontal);
                FlowchartDisconnectedPacking.Apply(Contract, Placed);
                FlowchartGroupMemberPacking.Apply(Contract, Placed);
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
            if (!Open) CenterLoneNestedGroups(Contract, Placed, Horizontal);
            if (!Open) CompactOverlappingRootGroups(Contract, Placed, Frames, Horizontal);
            FitFrames(Contract, Placed, Frames, Horizontal);
            if (FlowchartPartnerRankLayout.GrowGaps(Contract, Placed, Frames, State.PartnerRanks, Horizontal))
                FitFrames(Contract, Placed, Frames, Horizontal);
            if (!Open && FlowchartGroupedLeafPlacement.Apply(Contract, Placed, Frames, Horizontal))
                FitFrames(Contract, Placed, Frames, Horizontal);
            if (!Open && FlowchartExternalLeafPlacement.Apply(Contract, Placed, Frames, Horizontal))
                FitFrames(Contract, Placed, Frames, Horizontal);
            if (!Open) FlowchartIntergroupNodePlacement.Apply(Contract, Placed, Frames, Horizontal);
            if (!Open)
            {
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
                if (FlowchartPlacementHints.Apply(Contract, Placed, Frames))
                {
                    Frames.Clear();
                    BuildFrames(Contract, Placed, Frames);
                }
            }
            if (GrowTerminalSameRankLabelGaps(Contract, Placed, Horizontal))
                FitFrames(Contract, Placed, Frames, Horizontal);
        }
    }

    private static class RouteConstructionStage
    {
        // Rule ids: 3.8, 4.7, 4.11, 5.3–5.9 (ports and initial route construction).
        public static void Apply(GridLayoutState State)
        {
            DiagramContract Contract = State.Contract;
            FlowchartLayoutResult Copy = State.Copy;
            List<GridNode> Placed = State.Placed;
            List<GridFrame> Frames = State.Frames;
            Dictionary<string, NodeContract> Nodes = State.Nodes;
            FlowchartDirection Direction = State.Direction;
            bool Horizontal = State.Horizontal;
            FlipToYUp(Placed, Frames, Copy);
            RestoreContractRouteEndpoints(Contract, Copy);
            EnsureBackEdgeRoutes(Contract, Copy, Placed);
            AssignPortSides(Contract, Copy, Placed, Frames, Nodes, Direction, State.SharedFanSources);
            DiagramGridRouteBuilder.RebuildAllRoutes(Copy, Placed, Nodes, Contract, Frames, Direction);
            RebuildDecisionFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
            RebuildSoleForwardDecisionBranchRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
            RebuildOrdinaryFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
            FlowchartForwardRouteSimplifier.Apply(Copy, Contract, BuildPlacedBounds(Placed, Frames), Nodes, Direction);
            FlowchartPartnerRankLayout.Route(Contract, Copy, Placed, Frames, State.PartnerRanks, Horizontal);
            RebuildSimpleDecisionMergeRoutes(Contract, Copy, Placed, Frames, Horizontal);
            RebuildDecisionDetourRoutes(Contract, Copy, Placed, Frames, Horizontal);
            RebuildLoopBackRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
            DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(Copy, Placed, Nodes, Contract, Frames, Direction);
            FlowchartCircularJunctionLayout.RebuildFans(Copy, Contract, BuildPlacedBounds(Placed, Frames), Nodes, Direction);
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
            new("4.2,4.3", State => GrowFacingDoglegGaps(State.Copy, State.Contract, State.Placed, State.Frames)),
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
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("5.3,5.4", State => FlowchartReturnRouteOrder.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("5.8", State => FlowchartNestedCounterflowTracks.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
            new("4.4,5.8", State => FlowchartCrossGroupRouteSimplifier.Apply(
                State.Copy, State.Contract, BuildPlacedBounds(State.Placed, State.Frames))),
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
            new("3.1–3.7", State => FlowchartRoutingHints.Apply(State.Copy, State.Contract,
                BuildPlacedBounds(State.Placed, State.Frames), State.Nodes, State.Direction)),
            new("1.1–1.7", State => WriteBack(State.Copy, State.Placed, State.Frames)),
            new("3.8,4.11", State =>
            {
                if (!State.Open) FlowchartDecisionBranchRouteRepair.Apply(State.Copy, State.Contract);
            }),
            new("4.11", State =>
            {
                if (!State.Open) FlowchartConvergenceRouteRepair.Apply(State.Copy, State.Contract);
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
                if (!State.Open) FlowchartPortSideRouteRepair.Apply(State.Copy, State.Contract);
            }),
            new("5.8,5.9", State =>
            {
                if (!State.Open && State.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft)
                    FlowchartManyToManyRoutes.Apply(State.Copy, State.Contract,
                        BuildPlacedBounds(State.Placed, State.Frames), State.Direction);
            }),
            new("5.8", State => RebuildAlignedSimpleFanRoutes(State.Contract, State.Copy,
                State.Placed, State.Frames, State.Nodes, State.Direction))
        ];

        public static void Apply(GridLayoutState State)
        {
            State.Copy.UsesGridRoutes = true;
            HashSet<string> SeenStates = new(StringComparer.Ordinal) { RouteState(State.Copy) };
            List<(double X, double Y)[]> BestRoutes = CaptureRoutes(State.Copy);
            FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(State.Contract, State.Copy,
                IncludeArrangementCheck: false);
            while (true)
            {
                foreach (RepairPass Pass in Passes)
                    Pass.Apply(State);

                FlowchartLayoutScore.Result After = FlowchartLayoutScore.Measure(State.Contract, State.Copy,
                    IncludeArrangementCheck: false);
                string Signature = RouteState(State.Copy);
                if (!SeenStates.Add(Signature)
                    || After.OptimizationScore >= Before.OptimizationScore)
                {
                    RestoreRoutes(State.Copy, BestRoutes);
                    break;
                }

                Before = After;
                BestRoutes = CaptureRoutes(State.Copy);
            }
        }

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

    private static class FinalHintStage
    {
        // Rule ids: 2.21 and 3.1–3.7 (preserve line-hint endpoints after repairs).
        public static void Apply(GridLayoutState State) => FlowchartRoutingHints.Apply(
            State.Copy,
            State.Contract,
            BuildPlacedBounds(State.Placed, State.Frames),
            State.Nodes,
            State.Direction,
            RepairAutomaticRoutesEnabled: false);
    }
}
