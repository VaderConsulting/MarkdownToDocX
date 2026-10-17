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

    /// <summary>Both terminal clearances form one assignment; a locked later obstacle cannot leave the earlier obstacle moved.</summary>
    /// <param name="PinnedLaterObstacle">Whether the enter-side obstacle prevents the complete repair.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void HintedClearancePublishesAllObstacleMovesOrNone(bool PinnedLaterObstacle)
    {
        var Fixture = CreateState(false, "flowchart TB\nA[Input] --> B[Output]\nC[First obstacle]\nD[Later obstacle]",
            new Dictionary<string, FlowchartCellRegion>
            {
                ["A"] = new(0, 0, 1, 1), ["B"] = new(4, 0, 1, 1),
                ["C"] = new(-1, 0, 1, 1), ["D"] = new(5, 0, 1, 1)
            }, RoutingCoordinates: false,
            Configuration: FlowchartLogicalGridConfiguration.FromRules() with { RowGap = 0 });
        Fixture.Contract.Edges.Single().RoutingHint = new()
            { Leave = LayoutHintDirection.North, Enter = LayoutHintDirection.South, IsActive = true };
        Fixture.Nodes.Single(Node => Node.Id == "D").PlacementHintLocked = PinnedLaterObstacle;
        var Before = Fixture.Grid.Capture();
        typeof(DiagramGridLayout).GetMethod("ResolveHintedTerminalClearance", StaticPrivate)!.Invoke(null, [Fixture.State]);
        Assert.That(Fixture.Grid.RegionOf("A"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(Fixture.Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        if (PinnedLaterObstacle) Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
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
        var Fixture = CreateState(true, "flowchart TB\nA[Input] --> B[Work]",
            new Dictionary<string, FlowchartCellRegion> { ["A"] = new(0, 0, 1, 1), ["B"] = new(1, 1, 1, 1) });
        var Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        bool ObservedGrowth = false;
        DiagramGridLayout.RouteRepairCandidateObserver = (_, Subject, _, _) =>
        {
            if (!Subject.StartsWith("gap ", StringComparison.Ordinal)) return;
            ObservedGrowth = Fixture.Grid.Capture().ColumnGaps.Count != 0 || Fixture.Grid.Capture().RowGaps.Count != 0;
            throw new InvalidOperationException("forced-grown-gap-rejection");
        };
        try
        {
            var Failure = Assert.Throws<TargetInvocationException>(() => typeof(DiagramGridLayout)
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
        var Fixture = CreateState(true);
        var Budget = Fixture.Layout.RenderContext.MoveBeforeDetourBudget;
        if (AcceptedLimit) Budget.AcceptedMoves = FlowchartLayoutRules.RepairChainDepth;
        else Budget.CandidateMoves = FlowchartLayoutRules.RepairCandidateMoves;
        string Before = GeometryOf(Fixture.Layout);
        Assert.That(Search(Fixture.State), Is.EqualTo((0, 0)));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Before));
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>A blocked hinted terminal ray moves the obstacle by complete cells before the routing-axis projection.</summary>
    [Test]
    public void HintedTerminalClearanceMovesOnlyWholeRegions()
    {
        var Fixture = CreateState(false, "flowchart TB\nA[Input] --> B[Output]\nC[Obstacle]",
            new Dictionary<string, FlowchartCellRegion>
            {
                ["A"] = new(0, 0, 1, 1), ["B"] = new(2, 0, 1, 1), ["C"] = new(-1, 0, 1, 1)
            }, RoutingCoordinates: false,
            Configuration: FlowchartLogicalGridConfiguration.FromRules() with { RowGap = 0 });
        Fixture.Contract.Edges.Single().RoutingHint = new() { Leave = LayoutHintDirection.North, IsActive = true };
        var Before = Fixture.Grid.Capture();
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
        var Fixture = CreateState(Offset: false);
        var Before = Fixture.Grid.Capture();
        typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [Fixture.State]);
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        AssertNoSharedCells(Fixture.Grid);
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    /// <summary>Overlapping derived frames move their complete member groups through the same atomic cell search.</summary>
    [Test]
    public void FrameOverlapRepairMovesWholeMemberRegions()
    {
        var Fixture = CreateState(Offset: false, Source: """
            flowchart TB
              subgraph Left
                A[Left member]
              end
              subgraph Right
                B[Right member]
              end
              A --> C[Output]
              B --> C
            """, Regions: new Dictionary<string, FlowchartCellRegion>
            {
                ["A"] = new(0, 0, 1, 1), ["B"] = new(0, 1, 1, 1), ["C"] = new(2, 0, 1, 1)
            });
        Assert.That(FramesOverlap(Fixture.Layout), Is.True);
        bool Moved = (bool)typeof(DiagramGridLayout).GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!
            .GetMethod("RepairCellFrameOverlap", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Fixture.State])!;
        Assert.That(Moved, Is.True);
        Assert.That(FramesOverlap(Fixture.Layout), Is.False);
        AssertNoSharedCells(Fixture.Grid);
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    /// <summary>The engine search can straighten an offset chain using whole-cell candidates only.</summary>
    [Test]
    public void MoveBeforeDetourChangesRegionsAndDerivesItsWinningBounds()
    {
        var Fixture = CreateState(Offset: true);
        var Result = Search(Fixture.State);
        Assert.That(Result.Moved, Is.EqualTo(1));
        Assert.That(Result.Examined, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
        foreach (var Node in Fixture.Nodes)
        {
            var Region = Fixture.Grid.RegionOf(Node.Id);
            var Origin = Fixture.Grid.Origin(Region); var Size = Fixture.Grid.Measure(Region);
            Assert.That(Node.X, Is.EqualTo(Origin.X + Size.Width / 2));
            Assert.That(Node.Y, Is.EqualTo(600 - Origin.Y - Size.Height / 2));
        }
        Assert.That(FlowchartRouteRuleChecker.Check(Fixture.Contract, Fixture.Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>When no candidate improves the layout, regions and rendered routes restore exactly.</summary>
    [Test]
    public void RejectedSearchKeepsTheExactCellAndRouteSnapshot()
    {
        var Fixture = CreateState(Offset: true);
        foreach (var Node in Fixture.Nodes) Node.PlacementHintLocked = true;
        var Cells = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        Assert.That(Search(Fixture.State).Moved, Is.Zero);
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Cells.Regions));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
    }

    /// <summary>An exception while scoring a complete candidate restores cells, frame cache and exact port normals.</summary>
    [Test]
    public void ForcedCandidateFailureRestoresTheWholeOriginalState()
    {
        var Fixture = CreateState(Offset: true);
        var Before = Fixture.Grid.Capture();
        string Geometry = GeometryOf(Fixture.Layout);
        DiagramGridLayout.RouteRepairCandidateObserver = (_, _, _, _) => throw new InvalidOperationException("forced-cell-candidate-rejection");
        try
        {
            var Failure = Assert.Throws<TargetInvocationException>(() => Search(Fixture.State));
            Assert.That(Failure!.InnerException!.Message, Is.EqualTo("forced-cell-candidate-rejection"));
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Fixture.Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
        Assert.That(Fixture.Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));
        Assert.That(GeometryOf(Fixture.Layout), Is.EqualTo(Geometry));
        AssertNoSharedCells(Fixture.Grid);
    }

    /// <summary>Initialises real engine state from declared regions, bypassing only the not-yet-converted placement producer.</summary>
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
        var Contract = MermaidRecognizer.Parse(Source ?? "flowchart TB\nA[Input] --> B[Work]\nB --> C[Output]");
        Assert.That(FlowchartDagrePlacement.TryPlace(Contract, out var Seed), Is.True);
        Type StateType = typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!;
        object State = Activator.CreateInstance(StateType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [Contract, Seed, new FlowchartRenderContext()], null)!;
        typeof(DiagramGridLayout).GetNestedType("GridPreparationStage", BindingFlags.NonPublic)!
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);
        var Nodes = ((List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(State)!).ToArray();
        var Grid = new FlowchartLogicalGrid(Configuration ?? FlowchartLogicalGridConfiguration.FromRules());
        Regions ??= new Dictionary<string, FlowchartCellRegion>
        {
            ["A"] = new(0, 0, 1, 1), ["B"] = new(1, Offset ? 1 : 0, 1, 1), ["C"] = new(2, 0, 1, 1)
        };
        foreach (var Pair in Regions) Assert.That(Grid.TryPlace(Pair.Key, Pair.Value), Is.True);
        foreach (var Node in Nodes)
        {
            Node.BindCells(Grid);
            if (RoutingCoordinates) Node.CellNode!.UseRoutingCoordinates(600);
        }
        var Layout = (FlowchartLayoutResult)StateType.GetProperty("Copy")!.GetValue(State)!;
        Layout.UsesGridRoutes = true;
        var Placed = StateType.GetProperty("Placed")!.GetValue(State)!;
        var Frames = (List<DiagramGridLayout.GridFrame>)StateType.GetProperty("Frames")!.GetValue(State)!;
        typeof(DiagramGridLayout).GetMethod("BuildFramesYUp", StaticPrivate)!.Invoke(null, [Contract, Placed, Frames]);
        var NodeDefinitions = (Dictionary<string, NodeContract>)StateType.GetProperty("Nodes")!.GetValue(State)!;
        typeof(DiagramGridLayout).GetMethod("WriteBack", StaticPrivate)!.Invoke(null, [Layout, Placed, Frames]);
        typeof(DiagramGridLayout).GetMethod("AssignPortSides", StaticPrivate)!.Invoke(null,
            [Contract, Layout, Placed, Frames, NodeDefinitions, FlowchartDirection.TopToBottom, new HashSet<string>()]);
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, (List<DiagramGridLayout.GridNode>)Placed, NodeDefinitions,
            Contract, Frames, FlowchartDirection.TopToBottom);
        return (State, Nodes, Contract, Grid, Layout);
    }

    /// <summary>Invokes the production search entry point so its cell dispatch, scoring and rollback are exercised together.</summary>
    /// <param name="State">The engine state created from declared cells.</param>
    /// <returns>Accepted and examined candidate counts.</returns>
    private static (int Moved, int Examined) Search(object State) => ((int, int))typeof(DiagramGridLayout)
        .GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!.GetMethod("MoveBeforeDetour", StaticPrivate)!
        .Invoke(null, [State, FlowchartLayoutRules.RepairCandidateMoves, 1, new HashSet<string>()])!;

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
        var Occupied = new HashSet<(int Row, int Column)>();
        foreach (var Region in Grid.Capture().Regions.Values)
        for (int Row = Region.StartRow; Row < Region.EndRow; Row++)
        for (int Column = Region.StartColumn; Column < Region.EndColumn; Column++)
            Assert.That(Occupied.Add((Row, Column)), Is.True);
    }

    /// <summary>Checks the two unrelated derived frame rectangles without using an engine gate implementation.</summary>
    /// <param name="Layout">Fixture with two sibling groups.</param>
    /// <returns>Whether their interiors overlap on both axes.</returns>
    private static bool FramesOverlap(FlowchartLayoutResult Layout)
    {
        var A = Layout.ClusterBounds.Single(Box => Box.NodeId == "Left");
        var B = Layout.ClusterBounds.Single(Box => Box.NodeId == "Right");
        return Math.Abs(A.CenterX - B.CenterX) * 2 < A.Width + B.Width
            && Math.Abs(A.CenterY - B.CenterY) * 2 < A.Height + B.Height;
    }
}
