using System.Reflection;
using System.Text.Json;
using MermaidRecognition.Core;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Runs the actual move-before-detour search with declared cells and forced candidate rollback.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FlowchartCellMoveSearchTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>Incremental line costs agree with the reference scorer after whole-cell geometry changes while an independent route remains exact.</summary>
    /// <param name="Grow">Whether to change the node span rather than its starting column.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void MovingGeometryScoreMatchesReferenceWithoutChangingOtherLines(bool Grow)
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(true, "flowchart TB\nA --> B\nX --> Y",
            new Dictionary<string, FlowchartCellRegion> { ["A"] = new(0, 0, 1, 1), ["B"] = new(2, 1, 1, 1),
                ["X"] = new(0, 5, 1, 1), ["Y"] = new(2, 5, 1, 1) });
        FlowchartLayoutScore.CellMovementContext Context = new FlowchartLayoutScore.CellMovementContext(Fixture.Contract, Fixture.Layout);
        FlowchartLayoutEdgeRoute Changed = Fixture.Layout.EdgeRoutes.Single(Route => Route.SourceId == "A");
        FlowchartLayoutEdgeRoute Untouched = Fixture.Layout.EdgeRoutes.Single(Route => Route.SourceId == "X");
        (double X, double Y)[] Points = [.. Untouched.Points.Select(Point => (Point.X, Point.Y))];
        FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Fixture.Nodes,
            new Dictionary<string, FlowchartCellRegion> { ["B"] = Grow ? new(2, 1, 2, 1) : new(2, 0, 1, 1) });
        Assert.That(Plan!.TryApply(), Is.True);
        Type StateType = Fixture.State.GetType();
        List<DiagramGridLayout.GridNode> Placed = (List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(Fixture.State)!;
        List<DiagramGridLayout.GridFrame> Frames = (List<DiagramGridLayout.GridFrame>)StateType.GetProperty("Frames")!.GetValue(Fixture.State)!;
        typeof(DiagramGridLayout).GetMethod("WriteBack", StaticPrivate)!.Invoke(null, [Fixture.Layout, Placed, Frames]);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Fixture.Layout.NodeBounds.ToDictionary(Box => Box.NodeId);
        Dictionary<string, NodeContract> Definitions = Fixture.Contract.Nodes.ToDictionary(Node => Node.Id);
        typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
            .GetMethod("AllocateFinalPortSlots", StaticPrivate)!.Invoke(null,
                [Fixture.Contract, Fixture.Layout, Bounds, Definitions, new HashSet<string> { "A", "B" }]);
        DiagramGridRouteBuilder.RebuildAllRoutes(Fixture.Layout, Placed, Definitions, Fixture.Contract, Frames,
            FlowchartDirection.TopToBottom, new HashSet<FlowchartLayoutEdgeRoute> { Changed });
        FlowchartLayoutScore.Result Actual = Context.Measure(new HashSet<FlowchartLayoutEdgeRoute> { Changed });
        FlowchartLayoutScore.Result Expected = FlowchartLayoutScore.Measure(Fixture.Contract, Fixture.Layout, false);
        Assert.Multiple(() =>
        {
            Assert.That(Actual.OptimizationScore, Is.EqualTo(Expected.OptimizationScore));
            Assert.That((Actual.Bends, Actual.Crossings, Actual.Disconnections, Actual.Overlaps),
                Is.EqualTo((Expected.Bends, Expected.Crossings, Expected.Disconnections, Expected.Overlaps)));
            Assert.That(Untouched.Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Points));
        });
    }

    /// <summary>A rejected trial cannot mutate a painted guide retained by the saved layout, including its kind.</summary>
    [Test]
    public void RejectedCandidateRestoresMutableGuideContents()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(true);
        FlowchartLayoutEdgeRoute Route = Fixture.Layout.EdgeRoutes[0];
        FlowchartResolvedGuide Guide = new FlowchartResolvedGuide(Fixture.Contract.Edges[0],
            [.. Route.Points.Select(Point => (Point.X, Point.Y))], FlowchartEdgeRouteKind.Grid);
        Fixture.Layout.ResolvedEdgeGuides = [Guide];
        (double X, double Y)[] Points = [.. Guide.Points];
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        DiagramGridLayout.RouteRepairCandidateObserver = (_, _, _, _) =>
        {
            Guide.Points.Clear(); Guide.Points.Add((1, 2)); Guide.RouteKind = FlowchartEdgeRouteKind.Unknown;
            throw new InvalidOperationException("reject-mutated-guide");
        };
        try { Assert.Throws<TargetInvocationException>(() => Search(Fixture.State)); }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.Multiple(() =>
        {
            Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
            Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
            Assert.That(Guide.Points, Is.EqualTo(Points));
            Assert.That(Guide.RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Grid));
            Assert.That(Fixture.Layout.ResolvedEdgeGuides!.Single(), Is.SameAs(Guide));
        });
    }

    /// <summary>A rejected completed route option restores the renderer cache together with its authoritative regions and routes.</summary>
    [Test]
    public void RejectedCandidateRestoresGuideCacheAndDiagnostics()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(true);
        FlowchartRenderContext Context = Fixture.Layout.RenderContext;
        Context.CachedRouteContract = Fixture.Contract;
        Context.CachedRouteGeometry = "original-geometry";
        List<FlowchartResolvedGuide> Guides = []; Context.CachedRouteGuides = Guides;
        Context.LayoutStageTrace.Add("original-trace");
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture(); string Geometry = GeometryOf(Fixture.Layout);
        bool CacheChanged = false;
        DiagramGridLayout.RouteRepairCandidateObserver = (_, _, _, _) =>
        {
            CacheChanged = Context.CachedRouteGeometry != "original-geometry";
            Context.LayoutStageTrace.Add("discarded-trace");
            throw new InvalidOperationException("reject-cached-candidate");
        };
        try { Assert.Throws<TargetInvocationException>(() => Search(Fixture.State)); }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.Multiple(() =>
        {
            Assert.That(CacheChanged, Is.True, "The real completion path must exercise the cached trial geometry.");
            Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
            Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
            Assert.That(Context.CachedRouteContract, Is.SameAs(Fixture.Contract));
            Assert.That(Context.CachedRouteGeometry, Is.EqualTo("original-geometry"));
            Assert.That(Context.CachedRouteGuides, Is.SameAs(Guides));
            Assert.That(Context.LayoutStageTrace, Is.EqualTo(["original-trace"]));
        });
    }

    /// <summary>Both terminal clearances form one assignment; a locked later obstacle cannot leave the earlier obstacle moved.</summary>
    /// <param name="PinnedLaterObstacle">Whether the enter-side obstacle prevents the complete repair.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void HintedClearancePublishesAllObstacleMovesOrNone(bool PinnedLaterObstacle)
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(false, "flowchart TB\nA[Input] --> B[Output]\nC[First obstacle]\nD[Later obstacle]",
            new Dictionary<string, FlowchartCellRegion>
            {
                ["A"] = new(0, 0, 1, 1), ["B"] = new(4, 0, 1, 1),
                ["C"] = new(-1, 0, 1, 1), ["D"] = new(5, 0, 1, 1)
            }, RoutingCoordinates: false,
            Configuration: FlowchartLogicalGridConfiguration.FromRules() with { RowGap = 0 });
        Fixture.Contract.Edges.Single().RoutingHint = new()
            { Leave = LayoutHintDirection.North, Enter = LayoutHintDirection.South, IsActive = true };
        Fixture.Nodes.Single(Node => Node.Id == "D").PlacementHintLocked = PinnedLaterObstacle;
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        typeof(DiagramGridLayout).GetMethod("ResolveHintedTerminalClearance", StaticPrivate)!.Invoke(null, [Fixture.State]);
        Assert.That(Fixture.Grid.RegionOf("A"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(Fixture.Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        if (PinnedLaterObstacle)
        {
            Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        }
        else
        {
            Assert.That(Fixture.Grid.RegionOf("C"), Is.Not.EqualTo(Before.Regions["C"]));
            Assert.That(Fixture.Grid.RegionOf("D"), Is.Not.EqualTo(Before.Regions["D"]));
            Assert.That(Fixture.Layout.RenderContext.MoveBeforeDetourBudget.AcceptedMoves, Is.EqualTo(1));
        }
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>A real scored gap-growth candidate restores both the grown gaps and every exact route after forced rejection.</summary>
    [Test]
    public void GapGrowthCandidateFailureRestoresTheExactWholeState()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(true, "flowchart TB\nA[Input] --> B[Work]",
            new Dictionary<string, FlowchartCellRegion> { ["A"] = new(0, 0, 1, 1), ["B"] = new(1, 1, 1, 1) });
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        bool ObservedGrowth = false;
        DiagramGridLayout.RouteRepairCandidateObserver = (_, Subject, _, _) =>
        {
            if (!Subject.StartsWith("gap ", StringComparison.Ordinal))
            {
                return;
            }

            ObservedGrowth = Fixture.Grid.Capture().ColumnGaps.Count != 0 || Fixture.Grid.Capture().RowGaps.Count != 0;
            throw new InvalidOperationException("forced-grown-gap-rejection");
        };
        try
        {
            TargetInvocationException? Failure = Assert.Throws<TargetInvocationException>(() => typeof(DiagramGridLayout)
                .GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!
                .GetMethod("RepairCellChannelSpacing", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Fixture.State]));
            Assert.That(Failure!.InnerException!.Message, Is.EqualTo("forced-grown-gap-rejection"));
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(ObservedGrowth, Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
    }

    /// <summary>Calling another repair entry point cannot replenish either named per-diagram search limit.</summary>
    /// <param name="AcceptedLimit">Whether the accepted-move limit rather than the candidate limit is exhausted.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RepeatedRepairCallsShareTheExistingNamedLimits(bool AcceptedLimit)
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(true);
        FlowchartRenderContext.RepairSearchBudget Budget = Fixture.Layout.RenderContext.MoveBeforeDetourBudget;
        if (AcceptedLimit)
        {
            Budget.AcceptedMoves = FlowchartLayoutRules.RepairChainDepth;
        }
        else
        {
            Budget.CandidateMoves = FlowchartLayoutRules.RepairCandidateMoves;
        }

        string Before = GeometryOf(Fixture.Layout);
        Assert.That(Search(Fixture.State), Is.EqualTo((0, 0)));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Before));
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>A blocked hinted terminal ray moves the obstacle by complete cells before the routing-axis projection.</summary>
    [Test]
    public void HintedTerminalClearanceMovesOnlyWholeRegions()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(false, "flowchart TB\nA[Input] --> B[Output]\nC[Obstacle]",
            new Dictionary<string, FlowchartCellRegion>
            {
                ["A"] = new(0, 0, 1, 1), ["B"] = new(2, 0, 1, 1), ["C"] = new(-1, 0, 1, 1)
            }, RoutingCoordinates: false,
            Configuration: FlowchartLogicalGridConfiguration.FromRules() with { RowGap = 0 });
        Fixture.Contract.Edges.Single().RoutingHint = new() { Leave = LayoutHintDirection.North, IsActive = true };
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        typeof(DiagramGridLayout).GetMethod("ResolveHintedTerminalClearance", StaticPrivate)!
            .Invoke(null, [Fixture.State]);
        Assert.That(Fixture.Grid.RegionOf("A"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(Fixture.Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        Assert.That(Fixture.Grid.RegionOf("C").StartColumn, Is.Not.Zero);
        Assert.That(Fixture.Grid.RegionOf("C").RowSpan, Is.EqualTo(1));
        Assert.That(Fixture.Grid.RegionOf("C").ColumnSpan, Is.EqualTo(1));
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>The final materialisation and sealing stages cannot copy cached pixel bounds back into cell-bound nodes.</summary>
    [Test]
    public void FinalCompletionUsesCellGeometryWithoutReverseWritingPixels()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(Offset: false);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [Fixture.State]);
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        AssertNoSharedCells(Fixture.Grid);
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    /// <summary>Overlapping derived frames move their complete member groups through the same atomic cell search.</summary>
    /// <param name="MultipleMembers">Whether each group owns two independent cell reservations.</param>
    [TestCase(false), TestCase(true)]
    public void FrameOverlapRepairMovesWholeMemberRegions(bool MultipleMembers)
    {
        string Script = """
            flowchart TB
              subgraph Left
                A[Left member]
              end
              subgraph Right
                B[Right member]
              end
              A --> C[Output]
              B --> C
            """;
        var Regions = new Dictionary<string, FlowchartCellRegion>
        {
            ["A"] = new(0, 0, 1, 1), ["B"] = new(0, 1, 1, 1), ["C"] = new(2, 0, 1, 1)
        };
        if (MultipleMembers)
        {
            Script = Script.Replace("A[Left member]", "A[Left member]\nA2[Second left member]")
                .Replace("B[Right member]", "B[Right member]\nB2[Second right member]");
            Regions["A2"] = new(1, 0, 1, 1); Regions["B2"] = new(1, 1, 1, 1);
        }
        var Fixture = CreateState(Offset: false, Source: Script, Regions: Regions);
        Assert.That(FramesOverlap(Fixture.Layout), Is.True);
        var Before = Fixture.Grid.Capture();
        var Diagnostics = new List<string>();
        DiagramGridLayout.RouteRepairCandidateObserver = (Kind, Subject, Cost, Legal) =>
            Diagnostics.Add($"{Kind} {Subject}: legal={Legal}, cost={Cost}");
        bool Moved;
        try
        {
            Moved = (bool)typeof(DiagramGridLayout).GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!
                .GetMethod("RepairCellFrameOverlap", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Fixture.State])!;
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(Moved, Is.True, string.Join(Environment.NewLine, Diagnostics));
        Assert.That(FramesOverlap(Fixture.Layout), Is.False);
        Assert.That(Fixture.Grid.RegionOf("C"), Is.EqualTo(Before.Regions["C"]));
        if (MultipleMembers)
            foreach (string[] Members in new[] { new[] { "A", "A2" }, new[] { "B", "B2" } })
                Assert.That(Members.Select(Id => (Fixture.Grid.RegionOf(Id).StartRow - Before.Regions[Id].StartRow,
                    Fixture.Grid.RegionOf(Id).StartColumn - Before.Regions[Id].StartColumn)).Distinct().Count(), Is.EqualTo(1),
                    "every member must translate by the same whole-cell delta");
        AssertNoSharedCells(Fixture.Grid);
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    /// <summary>The engine search can straighten an offset chain using whole-cell candidates only.</summary>
    [Test]
    public void MoveBeforeDetourChangesRegionsAndDerivesItsWinningBounds()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(Offset: true);
        (int Moved, int Examined) Result = Search(Fixture.State);
        Assert.That(Result.Moved, Is.EqualTo(1));
        Assert.That(Result.Examined, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
        foreach (DiagramGridLayout.GridNode Node in Fixture.Nodes)
        {
            FlowchartCellRegion Region = Fixture.Grid.RegionOf(Node.Id);
            (double X, double Y) Origin = Fixture.Grid.Origin(Region); (double Width, double Height) Size = Fixture.Grid.Measure(Region);
            Assert.That(Node.X, Is.EqualTo(Origin.X + (Size.Width / 2)));
            Assert.That(Node.Y, Is.EqualTo(600 - Origin.Y - (Size.Height / 2)));
        }
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>When no candidate improves the layout, regions and rendered routes restore exactly.</summary>
    [Test]
    public void RejectedSearchKeepsTheExactCellAndRouteSnapshot()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(Offset: true);
        foreach (DiagramGridLayout.GridNode Node in Fixture.Nodes)
        {
            Node.PlacementHintLocked = true;
        }

        FlowchartLogicalGrid.Snapshot Cells = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        Assert.That(Search(Fixture.State).Moved, Is.Zero);
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Cells.Regions));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
    }

    /// <summary>An exception while scoring a complete candidate restores cells, frame cache and exact port normals.</summary>
    [Test]
    public void ForcedCandidateFailureRestoresTheWholeOriginalState()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(Offset: true);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        DiagramGridLayout.RouteRepairCandidateObserver = (_, _, _, _) => throw new InvalidOperationException("forced-cell-candidate-rejection");
        try
        {
            TargetInvocationException? Failure = Assert.Throws<TargetInvocationException>(() => Search(Fixture.State));
            Assert.That(Failure!.InnerException!.Message, Is.EqualTo("forced-cell-candidate-rejection"));
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Fixture.Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
        Assert.That(Fixture.Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>Initialises real engine state with fresh single-owner nodes on the fixture's declared logical grid.</summary>
    /// <param name="Offset">Whether the middle node starts in the next logical column.</param>
    /// <param name="Source">Optional group fixture source.</param>
    /// <param name="Regions">Optional explicit node reservations.</param>
    /// <param name="RoutingCoordinates">Whether to project cells into the existing routing axis.</param>
    /// <param name="Configuration">Optional fixed grid parameters for clearance fixtures.</param>
    /// <returns>Actual engine state, nodes, source contract, authoritative grid and derived routes.</returns>
    private static (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract,
        FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) CreateState(bool Offset, string? Source = null,
        Dictionary<string, FlowchartCellRegion>? Regions = null, bool RoutingCoordinates = true,
        FlowchartLogicalGridConfiguration? Configuration = null)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source ?? "flowchart TB\nA[Input] --> B[Work]\nB --> C[Output]");
        Assert.That(FlowchartDagrePlacement.TryPlace(Contract, out FlowchartLayoutResult? Seed), Is.True);
        Type StateType = typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!;
        object State = Activator.CreateInstance(StateType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [Contract, Seed, new FlowchartRenderContext()], null)!;
        typeof(DiagramGridLayout).GetNestedType("GridPreparationStage", BindingFlags.NonPublic)!
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);
        List<DiagramGridLayout.GridNode> Owners = (List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(State)!;
        // Preparation now binds its production seed. This independent unit
        // fixture has its own declared regions/configuration, so create its
        // owners once instead of attempting to rebind those seed owners.
        DiagramGridLayout.GridNode[] Nodes = [.. Owners.Select(Node => new DiagramGridLayout.GridNode
        {
            Id = Node.Id, Rank = Node.Rank, Column = Node.Column,
            GroupId = Node.GroupId, IsDiamond = Node.IsDiamond,
            Shape = Node.Shape, GeometryProvider = Node.GeometryProvider
        })];
        Owners.Clear(); Owners.AddRange(Nodes);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration ?? FlowchartLogicalGridConfiguration.FromRules());
        Regions ??= new Dictionary<string, FlowchartCellRegion>
        {
            ["A"] = new(0, 0, 1, 1), ["B"] = new(1, Offset ? 1 : 0, 1, 1), ["C"] = new(2, 0, 1, 1)
        };
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Regions)
        {
            Assert.That(Grid.TryPlace(Pair.Key, Pair.Value), Is.True);
        }

        foreach (DiagramGridLayout.GridNode? Node in Nodes)
        {
            Node.BindCells(Grid);
            if (RoutingCoordinates)
            {
                Node.CellNode!.UseRoutingCoordinates(600);
            }
        }
        FlowchartLayoutResult Layout = (FlowchartLayoutResult)StateType.GetProperty("Copy")!.GetValue(State)!;
        Layout.CellNodes = Owners;
        Layout.UsesGridRoutes = true;
        var Placed = StateType.GetProperty("Placed")!.GetValue(State)!;
        List<DiagramGridLayout.GridFrame> Frames = (List<DiagramGridLayout.GridFrame>)StateType.GetProperty("Frames")!.GetValue(State)!;
        typeof(DiagramGridLayout).GetMethod("BuildFramesYUp", StaticPrivate)!.Invoke(null, [Contract, Placed, Frames]);
        Dictionary<string, NodeContract> NodeDefinitions = (Dictionary<string, NodeContract>)StateType.GetProperty("Nodes")!.GetValue(State)!;
        typeof(DiagramGridLayout).GetMethod("WriteBack", StaticPrivate)!.Invoke(null, [Layout, Placed, Frames]);
        typeof(DiagramGridLayout).GetMethod("AssignPortSides", StaticPrivate)!.Invoke(null,
            [Contract, Layout, Placed, Frames, NodeDefinitions, FlowchartDirection.TopToBottom, new HashSet<string>()]);
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, (List<DiagramGridLayout.GridNode>)Placed, NodeDefinitions,
            Contract, Frames, FlowchartDirection.TopToBottom);
        // These fixtures invoke the final-model search directly, after allocating
        // ports and rebuilding routes, rather than running its earlier stages.
        StateType.GetProperty("CellFinalRoutingReady")!.SetValue(State, true);
        return (State, Nodes, Contract, Grid, Layout);
    }

    /// <summary>Invokes the production search entry point so its cell dispatch, scoring and rollback are exercised together.</summary>
    /// <param name="State">The engine state created from declared cells.</param>
    /// <returns>Accepted and examined candidate counts.</returns>
    private static (int Moved, int Examined) Search(object State) => ((int, int))typeof(DiagramGridLayout)
        .GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!.GetMethod("MoveBeforeDetour", StaticPrivate)!
        .Invoke(null, [State, FlowchartLayoutRules.RepairCandidateMoves, 1, new HashSet<string>()])!;

    /// <summary>A rejected real gap candidate restores ownership, gaps, ports, routes and nonempty frames together.</summary>
    [Test]
    public void RejectedTerminalGapCandidateRestoresEveryGeometryLayerExactly()
    {
        (object State, DiagramGridLayout.GridNode[] Nodes, DiagramContract Contract, FlowchartLogicalGrid Grid, FlowchartLayoutResult Layout) Fixture = CreateState(false, "flowchart LR\nsubgraph G[Group]\nA[Input] --> B[Work]\nend\nB --> C[Output]",
            new() { ["A"] = new(0, 0, 1, 1), ["B"] = new(0, 1, 1, 1), ["C"] = new(0, 3, 1, 1) });
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        Assert.That(Fixture.Layout.ClusterBounds, Is.Not.Empty);
        FlowchartCellRepair.Assignment? Assignment = FlowchartCellRepair.PlanAssignment(Fixture.Nodes, new Dictionary<string, FlowchartCellRegion>(),
            [new(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX)]);
        Assert.That(Assignment, Is.Not.Null);
        Type Stage = typeof(DiagramGridLayout).GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!;
        Type OptionType = Stage.GetNestedType("CellRepairOption", BindingFlags.NonPublic)!;
        var Option = Activator.CreateInstance(OptionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, ["gap|True|0", "gap 0", Assignment!], null)!;
        Array Options = Array.CreateInstance(OptionType, 1); Options.SetValue(Option, 0);
        bool SawGrowth = false;
        DiagramGridLayout.RouteRepairCandidateObserver = (_, _, _, _) =>
        {
            SawGrowth = Fixture.Grid.Capture().ColumnGaps[0] > Fixture.Grid.Configuration.ColumnGap;
            throw new InvalidOperationException("reject-grown-terminal-gap");
        };
        try
        {
            Assert.Throws<TargetInvocationException>(() => Stage.GetMethod("SearchCellAssignments", StaticPrivate)!
                .Invoke(null, [Fixture.State, Options, FlowchartLayoutRules.RepairCandidateMoves,
                    FlowchartLayoutRules.RepairChainDepth, new HashSet<string>(), null, true]));
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(SawGrowth, Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
    }

    /// <summary>Captures every route point, side and normal together with derived node and frame bounds.</summary>
    /// <param name="Layout">The current cached drawing geometry.</param>
    /// <returns>An exact structural snapshot for the rollback comparison.</returns>
    private static string GeometryOf(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new
    {
        Layout.NodeBounds, Layout.ClusterBounds,
        Routes = Layout.EdgeRoutes.Select(Route => new { Route.EdgeId, Route.ExitSide, Route.EntrySide,
            Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY,
            Points = Route.Points.Select(Point => new[] { Point.X, Point.Y }) })
    });

    /// <summary>Independently enumerates the reserved cells of a small repair fixture.</summary>
    /// <param name="Grid">Authoritative regions after a complete search or rollback.</param>
    private static void AssertNoSharedCells(FlowchartLogicalGrid Grid)
    {
        HashSet<(int Row, int Column)> Occupied = [];
        foreach (FlowchartCellRegion Region in Grid.Capture().Regions.Values)
        {
            for (int Row = Region.StartRow; Row < Region.EndRow; Row++)
            {
                for (int Column = Region.StartColumn; Column < Region.EndColumn; Column++)
                {
                    Assert.That(Occupied.Add((Row, Column)), Is.True);
                }
            }
        }
    }

    /// <summary>Checks the two unrelated derived frame rectangles without using an engine gate implementation.</summary>
    /// <param name="Layout">Fixture with two sibling groups.</param>
    /// <returns>Whether their interiors overlap on both axes.</returns>
    private static bool FramesOverlap(FlowchartLayoutResult Layout)
    {
        FlowchartLayoutNodeBounds A = Layout.ClusterBounds.Single(Box => Box.NodeId == "Left");
        FlowchartLayoutNodeBounds B = Layout.ClusterBounds.Single(Box => Box.NodeId == "Right");
        return Math.Abs(A.CenterX - B.CenterX) * 2 < A.Width + B.Width
            && Math.Abs(A.CenterY - B.CenterY) * 2 < A.Height + B.Height;
    }
}
