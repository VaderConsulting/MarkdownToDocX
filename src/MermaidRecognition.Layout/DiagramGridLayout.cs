using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Tidies a Mermaid dagre placement onto the shared grid.
/// Group membership is preserved; placement and ports follow the grid routing rules.
/// </summary>
public static partial class DiagramGridLayout
{
    private const int STUB_PX = 15;
    internal static Action<string, int>? CrossingCountObserver { get; set; }
    internal static Action<string, FlowchartLayoutResult>? LayoutStageObserver { get; set; }
    /// <summary>Observes the selected complete layout after discarded wrap trials, without changing publication.</summary>
    internal static Action<DiagramContract, FlowchartLayoutResult>? PublishedLayoutObserver { get; set; }
    internal static Action<string, double>? LayoutStageTimingObserver { get; set; }
    internal static Action<string>? GroupFrameRefitObserver { get; set; }
    internal static Action<string>? GroupFrameFitPassObserver { get; set; }
    internal static Action<string>? GroupFrameCandidateObserver { get; set; }
    internal static Action<string, IReadOnlyList<string>, long, double>? RouteRepairPassObserver { get; set; }
    internal static Action<int, int, IReadOnlyList<string>>? MoveBeforeDetourObserver { get; set; }
    internal static Action<int, int, double>? RouteRepairChainObserver { get; set; }
    internal static Action<string, string>? RouteRepairOptionObserver { get; set; }
    internal static Action<string, string, double, bool>? RouteRepairCandidateObserver { get; set; }
    internal static Action<string, int>? FrameOverlapStageObserver { get; set; }
    internal static Action<string>? OrthogonalRouteCleanupObserver { get; set; }

    private static void ObserveCrossings(GridLayoutState State, string Stage)
    {
        Action<string, int>? Observer = CrossingCountObserver;
        if (Observer == null) return;
        Observer(Stage, FlowchartLayoutScore.Measure(State.Contract, State.Copy,
            IncludeArrangementCheck: false).Crossings);
    }

    private static void ObserveLayoutStage(GridLayoutState State, string Stage)
    {
        LayoutStageObserver?.Invoke(Stage, State.Copy);
    }

    internal static void MeasureLayoutStage(string Stage, Action Operation)
    {
        Action<string, double>? Observer = LayoutStageTimingObserver;
        if (Observer == null)
        {
            Operation();
            return;
        }

        Observer(Stage, -1);
        Stopwatch Timer = Stopwatch.StartNew();
        try { Operation(); }
        finally
        {
            Timer.Stop();
            Observer(Stage, Timer.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Returns a Y-up copy of <paramref name="LaidOut"/> with shared box sizes, grid positions,
    /// group frames that include a title strip, and orthogonal routes from the new ports.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <param name="LaidOut">Mermaid dagre placement in Y-down coordinates.</param>
    /// <param name="RenderContext">Mutable state owned by this layout and render.</param>
    /// <returns>The gridded layout.</returns>
    public static FlowchartLayoutResult Apply(
        DiagramContract Contract,
        FlowchartLayoutResult LaidOut,
        FlowchartRenderContext? RenderContext = null)
    {
        (RenderContext ?? LaidOut.RenderContext).HasInitialCorridorCapacity = false;
        (RenderContext ?? LaidOut.RenderContext).SuppressReservedCorridorAssignment = false;
        var Result = ApplyWithRoutedBands(Contract, LaidOut, RenderContext);
        CompletePublishedRoutes(Contract, Result);
        PublishedLayoutObserver?.Invoke(Contract, Result);
        return Result;
    }

    /// <summary>Completes the existing final route pass once, before candidate scoring or final publication.</summary>
    /// <param name="Contract">Original declarations and honoured hints.</param>
    /// <param name="Result">Fully placed layout owning its independent render context.</param>
    private static void CompletePublishedRoutes(DiagramContract Contract, FlowchartLayoutResult Result)
    {
        if (Result.RenderContext.FinishedRoutePassApplied) return;
        var Context = Result.RenderContext;
        if (Context.UseCellRegions && !Context.DisableExpandedBendRepairsForTests && !Context.DisableFinishedBendRepairsForTests)
        {
            // Wrap selection is complete. Nothing after this pass may choose
            // another placement or reset an accepted line's allocated ports.
            const string Stage = "finished-route-interior-and-facing";
            Context.LayoutStageTrace.Add(Stage);
            string? Previous = Context.ActiveLayoutStage;
            Context.ActiveLayoutStage = Stage;
            using var Active = FlowchartRenderContext.Activate(Context);
            try
            {
                MeasureLayoutStage(Stage, () =>
                {
                    if (FlowchartFinishedLayoutBendRepair.ApplyRoutesOnly(Contract, Result) > 0)
                        RouteRepairStage.PublishFinal(Contract, Result);
                });
            }
            finally { Context.ActiveLayoutStage = Previous; }
        }
        Result.RenderContext.FinishedRoutePassApplied = true;
    }

    internal static FlowchartLayoutResult ApplyForGroupReflow(DiagramContract Contract,
        FlowchartLayoutResult LaidOut, FlowchartRenderContext RenderContext) =>
        ApplyCore(Contract, LaidOut, RenderContext, SkipRootGroupRepacking: true);

    /// <summary>Completes placement and routing, reusing only a private wrap trial with exactly identical initial inputs.</summary>
    /// <param name="Contract">Unchanged source, containment and hint declarations.</param>
    /// <param name="LaidOut">Original Dagre seed, copied into this trial's ownership.</param>
    /// <param name="RenderContext">Owning production or private trial context.</param>
    /// <param name="SkipRootGroupRepacking">Whether a nested reflow must preserve its caller's root packing.</param>
    /// <param name="CompletedBandTrials">Optional completed-input cache shared only by fresh wrap trial contexts.</param>
    /// <returns>The completed layout, with the selected trial's exact routes and hint outcomes.</returns>
    private static FlowchartLayoutResult ApplyCore(
        DiagramContract Contract,
        FlowchartLayoutResult LaidOut,
        FlowchartRenderContext? RenderContext,
        bool SkipRootGroupRepacking,
        Dictionary<string, CompletedBandTrial>? CompletedBandTrials = null)
    {
        GridLayoutState State = new(Contract, LaidOut, RenderContext) { SkipRootGroupRepacking = SkipRootGroupRepacking };
        using IDisposable CoverageScope = FlowchartCellPixelWriteGuard.ActivateCoverage(State.CellWriteCoverage);
        if (!State.IsReady) return State.Copy;
        FlowchartRenderContext Context = State.Copy.RenderContext;
        Context.FinishedRoutePassApplied = false;
        Context.LayoutStageTrace.Clear();
        Context.LayoutStageViolations.Clear();
        Run("grid-and-measured-geometry", GridPreparationStage.Apply);
        // Wrap width is consumed by the initial cell producer. From this point
        // onward the exact ordered regions, gaps, bodies, frames, ports, routes
        // and hint inputs determine the remaining deterministic pipeline.
        string? BandTrialIdentity = CompletedBandTrials == null ? null : InitialBandTrialIdentity(State);
        if (BandTrialIdentity != null && CompletedBandTrials!.TryGetValue(BandTrialIdentity, out var Completed))
        {
            Completed.Hints.Restore();
            Context.InitialBandCapacityNeeded = Completed.CapacityNeeded;
            FlowchartRenderContext.ReportOperationCount("completed-band-trial-cache-hits", 1);
            return Completed.Layout;
        }
        Run("placement-hints-2.21", PlacementHintStage.Apply);
        Run("automatic-node-placement-2.x", NodePlacementStage.Apply);
        Run("group-frame-fitting", GroupFrameStage.Apply);
        Run("route-coordinate-preparation", RouteCoordinateStage.Apply);
        Run("joint-port-allocation-4.7", JointPortAllocationStage.Apply);
        Run("line-routing-hints-3.1-3.7", RoutingHintStage.Apply);
        Run("rule-specific-route-construction", RouteConstructionStage.Apply);
        Run("coupled-route-repair", RouteRepairStage.Apply);
        Run("model-route-completion-and-rule-checks", ModelRouteCompletionStage.Apply);
        Run("final-legality-validation-section-10", LegalityValidationStage.Apply);
        Run("final-complete-layout-repair", RouteRepairStage.ApplyFinalCompleteRepair);
        Run("spine-and-decision-branch-reinforcement", Current =>
        {
            bool HorizontalFlow = Current.Direction
                is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
            if (HorizontalFlow || !ShouldUseSpineColumnLayout(Current.Contract, Current.Placed, Current.Direction))
            {
                return;
            }

            ApplySpineColumnAdjustments(Current.Contract, Current.Placed, Current.Direction);
            int CrossPitch = Math.Max(
                DiagramGridMetrics.PITCH_X_PX,
                (int)Math.Round(FlowchartRenderOptions.FromContract(Current.Contract).NodeSpacingPx));
            AlignDecisionBranchTargetRow(Current.Contract, Current.Placed, HorizontalFlow, CrossPitch);
            if (Current.Placed.Any(Node => Node.CellNode != null))
            {
                ReplaceCellFrames(Current.Contract, Current.Placed, Current.Frames);
            }

            WriteBack(Current.Copy, Current.Placed, Current.Frames);
            RouteRepairStage.PublishFinal(Current);
        });
        Run("bend-removing-port-reassignment-4.12", Current =>
        {
            Context.BendReassignmentRemoved = 0;
            if (!Context.UseCellRegions || Context.DisableExpandedBendRepairsForTests) return;
            Context.BendReassignmentRemoved = FlowchartBendRemovingPortRepair.Apply(Contract, Current.Copy);
            if (Context.BendReassignmentRemoved > 0) RouteRepairStage.PublishFinal(Current);
        });
        if (BandTrialIdentity != null)
            CompletedBandTrials!.Add(BandTrialIdentity, new(State.Copy, new BandHintState(Contract), Context.InitialBandCapacityNeeded));
        return State.Copy;

        void Run(string Name, Action<GridLayoutState> Stage)
        {
            Context.LayoutStageTrace.Add(Name);
            string? PreviousStage = Context.ActiveLayoutStage;
            Context.ActiveLayoutStage = Name;
            try { MeasureLayoutStage(Name, () => Stage(State)); }
            finally { Context.ActiveLayoutStage = PreviousStage; }
            if (FrameOverlapStageObserver != null)
                FrameOverlapStageObserver(Name, CountUnrelatedFrameOverlaps(State.Contract, State.Frames));
            ObserveCrossings(State, Name);
            ObserveLayoutStage(State, Name);
        }
    }

    /// <summary>Reuses the established joint allocator for one node during a private rule 4.12 side trial.</summary>
    /// <param name="Contract">Original declarations.</param>
    /// <param name="Layout">Private candidate routes.</param>
    /// <param name="Bounds">Fixed complete geometry.</param>
    /// <param name="Nodes">Renderer geometry providers.</param>
    /// <param name="Id">Only node whose side inventory can change.</param>
    internal static void ReallocateBendRepairPorts(DiagramContract Contract, FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds, Dictionary<string, NodeContract> Nodes, string Id) =>
        ModelRouteCompletionStage.AllocateFinalPortSlots(Contract, Layout, Bounds, Nodes, new HashSet<string> { Id });
    private static void RestoreContractRouteEndpoints(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, EdgeContract> ById = Contract.Edges
            .Where(Edge => !string.IsNullOrEmpty(Edge.Id))
            .GroupBy(Edge => Edge.Id!, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!ById.TryGetValue(Route.EdgeId, out EdgeContract? Edge))
            {
                continue;
            }

            Route.SourceId = Edge.SourceId;
            Route.TargetId = Edge.TargetId;
        }
    }

    private static string PositionSignature(IEnumerable<GridNode> Nodes) => string.Join("|",
        Nodes.OrderBy(Node => Node.Id, StringComparer.Ordinal).Select(Node =>
            $"{Node.Id}:{BitConverter.DoubleToInt64Bits(Node.X):X16},{BitConverter.DoubleToInt64Bits(Node.Y):X16},{Node.Width},{Node.Height}"));

    /// <summary>Snaps legacy unbound centres; whole-cell reservations already own their exact derived centres.</summary>
    /// <param name="Nodes">Nodes crossing a placement or frame-fit boundary.</param>
    private static void SnapGridNodeCentres(IEnumerable<GridNode> Nodes)
    {
        double Unit = DiagramGridMetrics.GRID_UNIT_PX;
        foreach (GridNode Node in Nodes)
        {
            // A half-cell centre is valid when a node spans an odd number of
            // cells. Pixel snapping must never replace its authoritative region.
            if (Node.CellNode != null) continue;
            double X = Math.Round(Node.X / Unit, MidpointRounding.AwayFromZero) * Unit;
            double Y = Math.Round(Node.Y / Unit, MidpointRounding.AwayFromZero) * Unit;
            if (X != Node.X || Y != Node.Y) Node.SetPositionFromFrameFit(X, Y);
        }
    }

    /// <summary>
    /// Gives a labelled straight cross-axis connection enough measured room without
    /// disturbing the rest of its rank. Only a terminal target moves; connected
    /// structures continue to be handled by the scored placement passes.
    /// </summary>
    private static bool GrowTerminalSameRankLabelGaps(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool HorizontalFlow)
    {
        if (Placed.Any(Node => Node.CellNode != null)) return GrowCellTerminalSameRankLabelGaps(Contract, Placed, HorizontalFlow);
        bool Changed = false;
        Dictionary<string, GridNode> ById = Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (EdgeContract Edge in Contract.Edges.OrderBy(Edge => Edge.CreationIndex))
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || !ById.TryGetValue(Edge.SourceId, out GridNode? Source)
                || !ById.TryGetValue(Edge.TargetId, out GridNode? Target)
                || Contract.Edges.Any(Other => Other.SourceId == Target.Id
                    && !string.Equals(Other.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            double SourceMain = HorizontalFlow ? Source.X : Source.Y;
            double TargetMain = HorizontalFlow ? Target.X : Target.Y;
            if (Math.Abs(SourceMain - TargetMain) > 0.5)
            {
                continue;
            }

            double SourceCross = HorizontalFlow ? Source.Y : Source.X;
            double TargetCross = HorizontalFlow ? Target.Y : Target.X;
            if (Math.Abs(TargetCross - SourceCross) < 0.5)
            {
                continue;
            }

            double SourceExtent = HorizontalFlow ? Source.Height : Source.Width;
            double TargetExtent = HorizontalFlow ? Target.Height : Target.Width;
            double ClearGap = Math.Abs(TargetCross - SourceCross) - (SourceExtent + TargetExtent) / 2.0;
            FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label, Options, Options.EdgeLabelFontSizePx);
            double RequiredGap = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Dimensions, Options.EdgeLabelFontSizePx, Horizontal: !HorizontalFlow)
                + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (ClearGap + 0.5 >= RequiredGap)
            {
                continue;
            }

            double Delta = DiagramGridMetrics.CeilToGrid(RequiredGap - ClearGap);
            double CandidateCross = TargetCross + Math.Sign(TargetCross - SourceCross) * Delta;
            double CandidateX = HorizontalFlow ? Target.X : CandidateCross;
            double CandidateY = HorizontalFlow ? CandidateCross : Target.Y;
            bool Overlaps = Placed.Any(Other => Other != Target
                && Math.Abs(CandidateX - Other.X) < (Target.Width + Other.Width) / 2.0
                && Math.Abs(CandidateY - Other.Y) < (Target.Height + Other.Height) / 2.0);
            if (Overlaps)
            {
                continue;
            }

            if (HorizontalFlow) Target.Y = CandidateCross;
            else Target.X = CandidateCross;
            Changed = true;
        }
        return Changed;
    }

    private static void ApplyScoreGuardedTwoCornerRepair(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        // Sloping outlines need their terminal normals rebuilt with the route.
        // This pass deliberately preserves those specialised attachments.
        if (Contract.Nodes.Any(Node => FlowchartDiamondAttachHelper.IsDiamondShape(Node.Shape))) return;
        FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(Contract, Layout);
        var Snapshot = Layout.EdgeRoutes.Select(Route => (Route, Route.ExitSide, Route.EntrySide,
            Points: Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToList())).ToList();
        FlowchartTwoCornerRouteShortener.Apply(Layout, Contract, Bounds, IncludeTwoCornerRoutes: true);
        FlowchartLayoutScore.Result After = FlowchartLayoutScore.Measure(Contract, Layout);
        bool Keep = Before.Score.HasValue
            ? After.Score.HasValue && After.Cost < Before.Cost - 0.001
            : After.Overlaps <= Before.Overlaps && After.Crossings <= Before.Crossings;
        if (Keep) return;
        foreach (var Saved in Snapshot)
        {
            Saved.Route.ExitSide = Saved.ExitSide;
            Saved.Route.EntrySide = Saved.EntrySide;
            Saved.Route.Points.Clear();
            Saved.Route.Points.AddRange(Saved.Points);
        }
    }

    internal static bool GrowFacingDoglegGaps(FlowchartLayoutResult Layout, DiagramContract Contract,
        List<GridNode> Placed, List<GridFrame> Frames, IEnumerable<string>? ExcludedNodeIds = null)
    {
        HashSet<string> PinnedEndpoints = Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true)
            .SelectMany(Edge => new[] { Edge.SourceId, Edge.TargetId }).ToHashSet(StringComparer.Ordinal);
        if (ExcludedNodeIds != null) PinnedEndpoints.UnionWith(ExcludedNodeIds);
        HashSet<string> HintedEdgeIds = Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true)
            .Select(Edge => Edge.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Changed = false;
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 6 || HintedEdgeIds.Contains(Route.EdgeId)
                || BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)) continue;
            var Source = Placed.Find(Node => Node.Id == Route.SourceId);
            var Target = Placed.Find(Node => Node.Id == Route.TargetId);
            if (Source == null || Target == null || Source.IsDiamond || Target.IsDiamond) continue;
            var Start = Route.Points[0]; var Next = Route.Points[1];
            var End = Route.Points[^1]; var Previous = Route.Points[^2];
            bool Horizontal = Math.Abs(Start.Y - Next.Y) < 0.5 && Math.Abs(Previous.Y - End.Y) < 0.5;
            bool Vertical = Math.Abs(Start.X - Next.X) < 0.5 && Math.Abs(Previous.X - End.X) < 0.5;
            if (!Horizontal && !Vertical) continue;
            double Along(FlowchartLayoutPoint Point) => Horizontal ? Point.X : Point.Y;
            double Across(FlowchartLayoutPoint Point) => Horizontal ? Point.Y : Point.X;
            double Sign = Math.Sign(Along(Next) - Along(Start));
            double Gap = (Along(End) - Along(Start)) * Sign;
            double Needed = 3 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridRouteBuilder.ARROWHEAD_LENGTH_PX;
            if (Sign == 0 || Math.Sign(Along(End) - Along(Previous)) != Sign || Gap <= 0 || Gap >= Needed
                || Math.Abs(Across(End) - Across(Start)) < 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) continue;
            double Cut = (Along(Start) + Along(End)) / 2;
            // Only open a genuine shared gap: never split a node body.
            if (Placed.Any(Node => (Horizontal ? Node.X - Node.Width / 2.0 : Node.Y - Node.Height / 2.0) < Cut
                && (Horizontal ? Node.X + Node.Width / 2.0 : Node.Y + Node.Height / 2.0) > Cut)) continue;
            int Shift = (int)Sign * DiagramGridMetrics.CeilToGrid(Needed - Gap);
            double Move(double Value) => (Value - Cut) * Sign >= 0 ? Value + Shift : Value;
            foreach (var Node in Placed)
            {
                if (PinnedEndpoints.Contains(Node.Id)) continue;
                if (Horizontal) Node.X = (int)Move(Node.X); else Node.Y = (int)Move(Node.Y);
            }
            foreach (var Other in Layout.EdgeRoutes)
            {
                if (HintedEdgeIds.Contains(Other.EdgeId)) continue;
                foreach (var Point in Other.Points)
                {
                    if (Horizontal) Point.X = Move(Point.X); else Point.Y = Move(Point.Y);
                }
            }
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames);
            Changed = true;
        }
        return Changed;
    }

    /// <summary>
    /// Shared box for every node in the diagram. Diamonds use this width and twice this height.
    /// A circle never forces the box to become square.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <returns>The box width and height in pixels.</returns>
    public static (int WidthPx, int HeightPx) ResolveStandardBox(DiagramContract Contract)
    {
        if (Contract == null)
        {
            return (DiagramGridMetrics.NODE_WIDTH_PX, DiagramGridMetrics.NODE_HEIGHT_PX);
        }

        int WidthPx = 0;
        int HeightPx = 0;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (NodeContract Node in Contract.Nodes)
        {
            FlowchartNodeDimensions Measured = FlowchartNodeSizeEstimator.Estimate(Node, Options);
            bool Diamond = FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            if (Diamond)
            {
                int DiamondWidth = DiagramGridMetrics.RoundToGrid(Measured.Width);
                int DiamondHeight = DiagramGridMetrics.RoundToGrid(
                    Measured.Height / FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR);
                WidthPx = Math.Max(WidthPx, DiamondWidth);
                HeightPx = Math.Max(HeightPx, DiamondHeight);
                continue;
            }

            int NodeWidth = DiagramGridMetrics.RoundToGrid(Measured.Width);
            int NodeHeight = DiagramGridMetrics.RoundToGrid(Measured.Height);
            WidthPx = Math.Max(WidthPx, NodeWidth);
            HeightPx = Math.Max(HeightPx, NodeHeight);
        }

        if (WidthPx <= 0)
        {
            WidthPx = DiagramGridMetrics.NODE_WIDTH_PX;
        }

        if (HeightPx <= 0)
        {
            HeightPx = DiagramGridMetrics.NODE_HEIGHT_PX;
        }

        if (!UsesCompactLabelSizing(Contract))
        {
            WidthPx = Math.Max(WidthPx, DiagramGridMetrics.NODE_WIDTH_PX);
            HeightPx = Math.Max(HeightPx, DiagramGridMetrics.NODE_HEIGHT_PX);
        }

        // A diamond has twice the box height. Even grid-unit box heights keep
        // both centre-to-centre spans on the same grid with whole-unit gaps.
        HeightPx = 2 * DiagramGridMetrics.CeilToGrid(HeightPx / 2.0);
        return (WidthPx, HeightPx);
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildPlacedBounds(
        List<GridNode> Placed,
        List<GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height,
                Geometry = Node.GeometryProvider?.Create(Node.Width, Node.Height)
            };
        }

        foreach (GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }

    private static void WriteBack(FlowchartLayoutResult Copy, List<GridNode> Placed, List<GridFrame> Frames)
    {
        Copy.NodeBoundsMutable.Clear();
        foreach (GridNode Node in Placed)
        {
            Copy.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height,
                Geometry = Node.GeometryProvider?.Create(Node.Width, Node.Height)
            });
        }

        Copy.ClusterBoundsMutable.Clear();
        double MaxX = 0.0;
        double MaxY = 0.0;
        foreach (GridFrame Frame in Frames)
        {
            Copy.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            });
            MaxX = Math.Max(MaxX, Frame.Right);
            MaxY = Math.Max(MaxY, Frame.Top);
        }

        foreach (GridNode Node in Placed)
        {
            MaxX = Math.Max(MaxX, Node.X + (Node.Width / 2.0));
            MaxY = Math.Max(MaxY, Node.Y + (Node.Height / 2.0));
        }
    }

    private static List<GridNode> NodesInRank(List<GridNode> Placed, int Rank)
    {
        List<GridNode> RankNodes = new List<GridNode>();
        foreach (GridNode Node in Placed)
        {
            if (Node.Rank == Rank)
            {
                RankNodes.Add(Node);
            }
        }

        return RankNodes;
    }

    private static int MaxHalf(List<GridNode> Nodes, bool Horizontal, bool Below)
    {
        int Half = 0;
        foreach (GridNode Node in Nodes)
        {
            Half = Math.Max(Half, MainHalf(Node, Horizontal, Leading: !Below));
        }

        return Half;
    }

    private static int MaxCrossHalf(List<GridNode> Nodes, bool Horizontal, bool Before)
    {
        int Half = 0;
        foreach (GridNode Node in Nodes)
        {
            Half = Math.Max(Half, CrossHalf(Node, Horizontal, Leading: Before));
        }

        return Half;
    }

    private static int MainHalf(GridNode Node, bool Horizontal, bool Leading)
    {
        int Size = Horizontal ? Node.Width : Node.Height;
        return Size / 2;
    }

    private static int CrossHalf(GridNode Node, bool Horizontal, bool Leading)
    {
        int Size = Horizontal ? Node.Height : Node.Width;
        return Size / 2;
    }

    private static double Main(GridNode Node, bool Horizontal, bool Old)
    {
        if (Horizontal)
        {
            return Old ? Node.OldX : Node.X;
        }

        return Old ? Node.OldY : Node.Y;
    }

    private static double Cross(GridNode Node, bool Horizontal, bool Old)
    {
        if (Horizontal)
        {
            return Old ? Node.OldY : Node.Y;
        }

        return Old ? Node.OldX : Node.X;
    }

    private static void SetMain(GridNode Node, int Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Node.X = Value;
        }
        else
        {
            Node.Y = Value;
        }
    }

    private static void SetCross(GridNode Node, int Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Node.Y = Value;
        }
        else
        {
            Node.X = Value;
        }
    }

    private static string? GroupOf(DiagramContract Contract, string NodeId)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                return Node.ParentGroupId;
            }
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (string.Equals(ChildId, NodeId, StringComparison.Ordinal))
                {
                    return Group.Id;
                }
            }
        }

        return null;
    }

    internal static bool IsInsideGroup(DiagramContract Contract, string NodeId, string GroupId)
    {
        string? Current = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(NodeId, Contract);
        int Guard = 0;
        while (!string.IsNullOrEmpty(Current) && Guard < 32)
        {
            if (string.Equals(Current, GroupId, StringComparison.Ordinal))
            {
                return true;
            }

            Current = ParentGroup(Contract, Current);
            Guard++;
        }

        return false;
    }

    private static string? ParentGroup(DiagramContract Contract, string GroupId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, GroupId, StringComparison.Ordinal))
            {
                return Group.ParentGroupId;
            }
        }

        return null;
    }

    private static int DepthOf(DiagramContract Contract, string GroupId)
    {
        int Depth = 0;
        string? Parent = ParentGroup(Contract, GroupId);
        while (!string.IsNullOrEmpty(Parent))
        {
            Depth++;
            Parent = ParentGroup(Contract, Parent);
        }

        return Depth;
    }

    private static Dictionary<string, GridNode> IndexPlaced(List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = new Dictionary<string, GridNode>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            ById[Node.Id] = Node;
        }

        return ById;
    }

    private static Dictionary<string, NodeContract> IndexNodes(DiagramContract Contract)
    {
        Dictionary<string, NodeContract> Nodes = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            Nodes[Node.Id] = Node;
        }

        return Nodes;
    }

    private static FlowchartLayoutResult CopyResult(FlowchartLayoutResult Source)
    {
        FlowchartLayoutResult Copy = new FlowchartLayoutResult
        {
            GraphWidth = Source.GraphWidth,
            GraphHeight = Source.GraphHeight,
            RenderContext = Source.RenderContext
        };
        foreach (FlowchartLayoutNodeBounds Bounds in Source.NodeBounds)
        {
            Copy.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height,
                Geometry = Bounds.Geometry
            });
        }

        foreach (FlowchartLayoutNodeBounds Bounds in Source.ClusterBounds)
        {
            Copy.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            });
        }

        foreach (FlowchartLayoutEdgeRoute Route in Source.EdgeRoutes)
        {
            FlowchartLayoutEdgeRoute Clone = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Route.EdgeId,
                SourceId = Route.SourceId,
                TargetId = Route.TargetId,
                ExitSide = Route.ExitSide,
                EntrySide = Route.EntrySide,
                ExitNormalX = Route.ExitNormalX,
                ExitNormalY = Route.ExitNormalY,
                EntryNormalX = Route.EntryNormalX,
                EntryNormalY = Route.EntryNormalY
            };
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Clone.Points.Add(new FlowchartLayoutPoint
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            Copy.EdgeRoutesMutable.Add(Clone);
        }

        return Copy;
    }

    internal sealed class GridNode
    {
        /// <summary>Rebuilds renderer-owned local geometry after a node dimension change.</summary>
        public INodeGeometryProvider? GeometryProvider;

        public string Id = string.Empty;

        public string? Shape;

        public string? GroupId;

        public double OldX;

        public double OldY;

        private double _x;
        private double _y;
        private int _width;
        private int _height;
        /// <summary>The authoritative region view when this node participates in logical-cell repair.</summary>
        internal FlowchartCellNode? CellNode { get; private set; }
        /// <summary>Per-layout coverage; nested reflows retain their own counters.</summary>
        internal FlowchartCellWriteCoverage? CellWriteCoverage;
        /// <summary>Owning diagnostic sink, independent of unrelated ambient render contexts.</summary>
        internal FlowchartRenderContext? PixelWriteContext;
        public bool PlacementHintLocked;
        /// <summary>Validates actual logical hint relationships on a complete proposed ownership map; null retains a manual absolute lock.</summary>
        internal Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool>? CellHintValidator;
        public double X { get => CellNode?.X ?? _x; set { if (AllowPixelWrite(nameof(X)) && !PlacementHintLocked) _x = value; } }
        public double Y { get => CellNode?.Y ?? _y; set { if (AllowPixelWrite(nameof(Y)) && !PlacementHintLocked) _y = value; } }
        /// <summary>Retains the legacy coordinate adapter only for a node without logical-cell authority.</summary>
        /// <param name="Value">Legacy transformed vertical coordinate.</param>
        public void SetYAfterCoordinateTransform(double Value) { if (AllowPixelWrite(nameof(SetYAfterCoordinateTransform))) _y = Value; }
        /// <summary>Retains legacy frame placement only until a node is bound to explicit cell ownership.</summary>
        /// <param name="X">Legacy horizontal coordinate.</param>
        /// <param name="Y">Legacy vertical coordinate.</param>
        public void SetPositionFromFrameFit(double X, double Y) { if (AllowPixelWrite(nameof(SetPositionFromFrameFit))) { _x = X; _y = Y; } }

        public int Width { get => CellNode == null ? _width : checked((int)CellNode.Width); set { if (AllowPixelWrite(nameof(Width))) _width = value; } }

        public int Height { get => CellNode == null ? _height : checked((int)CellNode.Height); set { if (AllowPixelWrite(nameof(Height))) _height = value; } }

        /// <summary>Binds an explicitly allocated region; existing pixel fields are not consulted or converted.</summary>
        /// <param name="Grid">Grid already owning this node's whole-cell reservation.</param>
        /// <param name="Context">Optional explicit owner for persistent repair diagnostics.</param>
        internal void BindCells(FlowchartLogicalGrid Grid, FlowchartRenderContext? Context = null)
        {
            if (CellNode != null) throw new InvalidOperationException("A node is already bound to its cell grid.");
            CellNode = new(Grid, Id, IsDiamond);
            PixelWriteContext = Context ?? PixelWriteContext ?? FlowchartRenderContext.Current;
            CellWriteCoverage ??= new() { Nodes = 1 };
            CellWriteCoverage.BoundNodes++;
        }

        /// <summary>Rejects an unconverted repair without changing cell authority or legacy backing fields.</summary>
        /// <param name="Member">Attempted drawing coordinate, extent or adapter method.</param>
        /// <returns>Whether this unbound node still permits legacy pixel writes.</returns>
        /// <exception cref="InvalidOperationException">The node's region is already authoritative.</exception>
        private bool AllowPixelWrite(string Member) => FlowchartCellPixelWriteGuard.AllowWrite(this, Member);

        public int Rank;

        public int Column;

        public bool IsDiamond;
    }

    private sealed class ColumnBand
    {
        public readonly List<GridNode> Nodes = new List<GridNode>();

        public double Anchor;

        public int Index;
    }

    internal sealed class GridFrame
    {
        public string Id = string.Empty;

        public int Left;

        public int Top;

        public int Right;

        public int Bottom;

        public int TitleStrip;

        public int Padding;

        public double CenterX()
        {
            return (Left + Right) / 2.0;
        }

        public double CenterY()
        {
            return (Top + Bottom) / 2.0;
        }

        public double CenterMain(bool Horizontal)
        {
            if (Horizontal)
            {
                return CenterX();
            }

            return (Top + Bottom) / 2.0;
        }
    }

    private struct BoxRef
    {
        public string Id;

        public double CenterX;

        public double CenterY;

        public double Width;

        public double Height;

        public double Left;

        public double Right;

        public double Top;

        public double Bottom;

        public bool IsFrame;

        public static BoxRef FromNode(GridNode Node)
        {
            BoxRef Box = new BoxRef
            {
                Id = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height,
                IsFrame = false
            };
            Box.Left = Node.X - (Node.Width / 2.0);
            Box.Right = Node.X + (Node.Width / 2.0);
            Box.Bottom = Node.Y - (Node.Height / 2.0);
            Box.Top = Node.Y + (Node.Height / 2.0);
            return Box;
        }

        public static BoxRef FromFrame(GridFrame Frame)
        {
            return new BoxRef
            {
                Id = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom,
                Left = Frame.Left,
                Right = Frame.Right,
                Top = Frame.Top,
                Bottom = Frame.Bottom,
                IsFrame = true
            };
        }
    }

    /// <summary>
    /// A8: the clear span between ranks is uniform; label boosts apply only to their own pair.
    /// </summary>
    private static List<RankPairGapPlan> BuildRankPairGapPlans(
        List<GridNode> Placed,
        int RankCount,
        bool Horizontal,
        int MainPitch,
        FlowchartLayoutResult Copy,
        DiagramContract Contract)
    {
        List<RankPairGapPlan> Plans = [];
        for (int Rank = 0; Rank + 1 < RankCount; Rank++)
        {
            List<GridNode> RankNodes = NodesInRank(Placed, Rank);
            List<GridNode> NextNodes = NodesInRank(Placed, Rank + 1);
            int HalfBelow = MaxHalf(RankNodes, Horizontal, Below: true);
            int HalfAbove = MaxHalf(NextNodes, Horizontal, Below: false);
            int Gap = Math.Max(2 * DiagramGridMetrics.GRID_UNIT_PX,
                DiagramGridMetrics.CeilToGrid(FlowchartRenderOptions.FromContract(Contract).RankSpacingPx));
            foreach (EdgeContract Edge in Contract.Edges)
            {
                GridNode? Source = RankNodes.Find(Node => Node.Id == Edge.SourceId || Node.Id == Edge.TargetId);
                GridNode? Target = NextNodes.Find(Node => Node.Id == Edge.TargetId || Node.Id == Edge.SourceId);
                if (Source != null && Target != null && Source.Column != Target.Column
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    // Two rounded turns need a source run, two radii and an
                    // arrow-length target run. Grow the gap in whole units.
                    Gap = Math.Max(Gap, DiagramGridMetrics.CeilToGrid(
                        (3 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) + 10));
                }
            }
            int Crossings = CountRankCrossings(Copy, RankNodes, NextNodes);
            if (Crossings > 0)
            {
                Gap = Math.Max(Gap, DiagramGridMetrics.CeilToGrid(DiagramGridMetrics.GapWidthForTracks(Crossings)));
            }
            double MermaidDelta = MermaidMain(NextNodes, Horizontal) - MermaidMain(RankNodes, Horizontal);
            bool HasExplicitLength = Contract.Edges.Any(Edge => Edge.MinLength > 1
                && RankNodes.Any(Node => Node.Id == Edge.SourceId || Node.Id == Edge.TargetId)
                && NextNodes.Any(Node => Node.Id == Edge.TargetId || Node.Id == Edge.SourceId));
            if (HasExplicitLength && MermaidDelta > MainPitch)
            {
                int MermaidSpan = DiagramGridMetrics.CeilToGrid(MermaidDelta);
                int MermaidGap = MermaidSpan - HalfBelow - HalfAbove;
                Gap = Math.Max(Gap, MermaidGap);
            }

            int LabelGapBoost = ComputeLabelledEdgeRankGapBoost(Contract, Rank, RankNodes, NextNodes, Horizontal);
            int Span = HalfBelow + HalfAbove + Gap;
            if (Span < MainPitch)
            {
                Gap = Math.Max(Gap, MainPitch - HalfBelow - HalfAbove);
                Span = MainPitch;
            }

            Plans.Add(new RankPairGapPlan
            {
                HalfBelow = HalfBelow,
                HalfAbove = HalfAbove,
                StructuralClearGap = Span - HalfBelow - HalfAbove,
                LabelClearGapBoost = Math.Max(0, LabelGapBoost - Gap)
            });
        }

        return Plans;
    }

    private static int ComputeLabelledEdgeRankGapBoost(
        DiagramContract Contract,
        int Rank,
        IReadOnlyList<GridNode> RankNodes,
        IReadOnlyList<GridNode> NextNodes,
        bool Horizontal)
    {
        if (RankNodes.Count == 0 || NextNodes.Count == 0)
        {
            return 0;
        }

        HashSet<string> RankIds = new HashSet<string>(RankNodes.Select(Node => Node.Id), StringComparer.Ordinal);
        HashSet<string> NextIds = new HashSet<string>(NextNodes.Select(Node => Node.Id), StringComparer.Ordinal);
        int MaxBoost = 0;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!(RankIds.Contains(Edge.SourceId) && NextIds.Contains(Edge.TargetId))
                && !(RankIds.Contains(Edge.TargetId) && NextIds.Contains(Edge.SourceId)))
            {
                continue;
            }

            FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label,
                Options,
                Options.EdgeLabelFontSizePx);
            int Required = DiagramGridMetrics.CeilToGrid(
                FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    LabelDimensions,
                    Options.EdgeLabelFontSizePx,
                    Horizontal) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            MaxBoost = Math.Max(MaxBoost, Required);
        }

        return MaxBoost;
    }

    private sealed class RankPairGapPlan
    {
        public int HalfBelow;

        public int HalfAbove;

        /// <summary>
        /// Clear gap this pair needs before any diagram-wide uniform floor is applied.
        /// </summary>
        public int StructuralClearGap;

        /// <summary>
        /// Extra clear gap for labels on edges between these ranks only.
        /// </summary>
        public int LabelClearGapBoost;
    }

    private sealed class PortEnd
    {
        public FlowchartLayoutEdgeRoute Route = null!;

        public bool IsSource;

        public bool KeepsCentre;

        public bool IsHinted;

        public string Side = string.Empty;

        public BoxRef Box;

        public BoxRef Other;

        public double OutX;

        public double OutY;
    }
}
