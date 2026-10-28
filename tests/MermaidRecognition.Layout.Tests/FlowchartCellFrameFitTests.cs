using System.Reflection;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises actual frame-fit and eviction entry points with explicit cells and independent ownership checks.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FlowchartCellFrameFitTests
{
    /// <summary>Owns the independent per-test layout budget without changing production budget accounting.</summary>
    private IDisposable? ContextScope;

    /// <summary>Gives each fixture the same isolated context that a production diagram receives.</summary>
    [SetUp]
    public void StartContext() => ContextScope = FlowchartRenderContext.Activate(new());

    /// <summary>Restores the caller's ambient context even after a forced candidate failure.</summary>
    [TearDown]
    public void EndContext() => ContextScope?.Dispose();

    /// <summary>A late frame-pass rejection restores the entry state even after a complete member assignment had been accepted internally.</summary>
    [Test]
    public void LateFrameFailureRollsBackPreviouslyAcceptedMoves()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string FrameGeometry = FramesOf(Fixture.Frames);
        DiagramGridLayout.GroupFrameRefitObserver = _ => throw new InvalidOperationException("late-frame-rejection");
        try
        {
            Assert.That(() => DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "late-failure"),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("late-frame-rejection"));
        }
        finally { DiagramGridLayout.GroupFrameRefitObserver = null; }
        Assert.That(FlowchartRenderContext.Current.MoveBeforeDetourBudget.AcceptedMoves, Is.GreaterThan(0));
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(FramesOf(Fixture.Frames), Is.EqualTo(FrameGeometry));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>The staged engine's explicit context wins over an exhausted unrelated ambient render.</summary>
    [Test]
    public void FrameFitUsesTheExplicitDiagramBudget()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false);
        FlowchartRenderContext.Current.MoveBeforeDetourBudget.CandidateMoves = FlowchartLayoutRules.RepairCandidateMoves;
        FlowchartRenderContext DiagramContext = new FlowchartRenderContext();
        DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "explicit-context", DiagramContext);
        Assert.That(DiagramContext.MoveBeforeDetourBudget.CandidateMoves, Is.GreaterThan(0));
        Assert.That(FlowchartRenderContext.Current.MoveBeforeDetourBudget.CandidateMoves,
            Is.EqualTo(FlowchartLayoutRules.RepairCandidateMoves));
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Fixture.Contract, Fixture.Frames), Is.Zero);
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>Mixed placement and routing views fail before changing ownership or the caller's frame cache.</summary>
    [Test]
    public void MixedFrameCoordinateViewsAreRejectedWithoutMutation()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string FrameGeometry = FramesOf(Fixture.Frames);
        Fixture.Nodes[0].CellNode!.UseRoutingCoordinates(600);
        Assert.That(() => DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "mixed"),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(FramesOf(Fixture.Frames), Is.EqualTo(FrameGeometry));
    }

    /// <summary>Exhausted named budgets cannot partially translate a frame or leave a trial cache behind.</summary>
    [Test]
    public void ExhaustedFrameBudgetPreservesTheOriginalGeometry()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string FrameGeometry = FramesOf(Fixture.Frames);
        FlowchartRenderContext.Current.MoveBeforeDetourBudget.CandidateMoves = FlowchartLayoutRules.RepairCandidateMoves;
        Assert.That(() => DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "budget"),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(FramesOf(Fixture.Frames), Is.EqualTo(FrameGeometry));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>An empty group's frame is derived metadata and never fabricates a node or extra occupied cells.</summary>
    [Test]
    public void EmptyFrameDoesNotInventCellOccupancy()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false, EmptyGroup: true);
        DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "empty-frame");
        Assert.That(Fixture.Frames.Any(Frame => Frame.Id == "Empty"), Is.True);
        Assert.That(Fixture.Grid.Capture().Regions.Keys, Is.EquivalentTo(["A", "B"]));
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Fixture.Contract, Fixture.Frames), Is.Zero);
        AssertOccupancy(Fixture.Grid);
    }
    /// <summary>Nested and sibling frames settle without copying a pixel trial layout into member positions.</summary>
    /// <param name="Direction">Declared main-flow direction, which does not redefine north.</param>
    /// <param name="Routing">Whether frame fitting runs after the existing routing-axis boundary.</param>
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", false)]
    [TestCase("RL", false)]
    [TestCase("TB", true)]
    [TestCase("BT", true)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void FrameFitMovesCompleteRegionsInEveryDirection(string Direction, bool Routing)
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create(Direction, Routing, Nested: true);
        IReadOnlyDictionary<string, FlowchartCellRegion> Spans = Fixture.Grid.Capture().Regions;
        DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, Direction is "LR" or "RL", "cell-fixture");
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Fixture.Contract, Fixture.Frames), Is.Zero);
        foreach (DiagramGridLayout.GridNode Node in Fixture.Nodes)
        {
            FlowchartCellRegion Region = Fixture.Grid.RegionOf(Node.Id);
            Assert.That(Region.RowSpan, Is.EqualTo(Spans[Node.Id].RowSpan));
            Assert.That(Region.ColumnSpan, Is.EqualTo(Spans[Node.Id].ColumnSpan));
        }
        DiagramGridLayout.GridFrame Parent = Fixture.Frames.Single(Frame => Frame.Id == "Outer");
        DiagramGridLayout.GridFrame Child = Fixture.Frames.Single(Frame => Frame.Id == "Left");
        Assert.That(Child.Left, Is.GreaterThanOrEqualTo(Parent.Left));
        Assert.That(Child.Right, Is.LessThanOrEqualTo(Parent.Right));
        Assert.That(Math.Min(Child.Top, Child.Bottom), Is.GreaterThanOrEqualTo(Math.Min(Parent.Top, Parent.Bottom)));
        Assert.That(Math.Max(Child.Top, Child.Bottom), Is.LessThanOrEqualTo(Math.Max(Parent.Top, Parent.Bottom)));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>A direct eviction moves the foreign node's declared region while preserving the frame members.</summary>
    [Test]
    public void DirectEvictionChangesOnlyTheForeignRegion()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false, Foreign: true);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        DiagramGridLayout.GridNode Foreign = Fixture.Nodes.Single(Node => Node.Id == "B");
        DiagramGridLayout.GridFrame Frame = Fixture.Frames.Single();
        Assert.That(DiagramGridLayout.TryEvictNodeFromFrame(Fixture.Contract, Fixture.Nodes, Foreign, Frame), Is.True);
        Assert.That(Fixture.Grid.RegionOf("A"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(Fixture.Grid.RegionOf("B"), Is.Not.EqualTo(Before.Regions["B"]));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>A locked outsider stays fixed while the entire colliding frame moves through whole cells.</summary>
    [Test]
    public void LockedOutsiderMovesTheFrameMembersInstead()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false, Foreign: true);
        DiagramGridLayout.GridNode Foreign = Fixture.Nodes.Single(Node => Node.Id == "B");
        Foreign.PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        Assert.That(DiagramGridLayout.MoveFrameAwayFromLockedNode(Fixture.Contract, Fixture.Nodes, Foreign,
            Fixture.Frames.Single(), false, DiagramGridMetrics.GRID_UNIT_PX), Is.True);
        Assert.That(Fixture.Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        Assert.That(Fixture.Grid.RegionOf("A"), Is.Not.EqualTo(Before.Regions["A"]));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>A failed frame candidate restores exact declared regions, gaps and derived frame geometry.</summary>
    [Test]
    public void ForcedFrameCandidateFailureRollsBackExactly()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false);
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        string FrameGeometry = FramesOf(Fixture.Frames);
        using IDisposable Scope = FlowchartRenderContext.Activate(new());
        DiagramGridLayout.GroupFrameFitPassObserver = Message =>
        {
            if (Message.StartsWith("cell frame candidate", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("forced-frame-rejection");
            }
        };
        try
        {
            Assert.That(() => DiagramGridLayout.FitFrames(Fixture.Contract, Fixture.Nodes, Fixture.Frames, false, "forced"),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("forced-frame-rejection"));
        }
        finally { DiagramGridLayout.GroupFrameFitPassObserver = null; }
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Fixture.Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
        Assert.That(Fixture.Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));
        Assert.That(FramesOf(Fixture.Frames), Is.EqualTo(FrameGeometry));
        AssertOccupancy(Fixture.Grid);
    }

    /// <summary>A frame fix cannot move a peer while leaving an external placement-hint dependent behind.</summary>
    [Test]
    public void ExternalHintDependentPreventsADirectFrameMove()
    {
        (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Fixture = Create("TB", false, Foreign: true);
        Fixture.Contract.Nodes.Single(Node => Node.Id == "B").PlacementHint = new()
            { Kind = PlacementHintKind.RightOf, PeerId = "A", IsActive = true };
        DiagramGridLayout.GridNode Foreign = Fixture.Nodes.Single(Node => Node.Id == "B");
        Foreign.PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Fixture.Grid.Capture();
        Assert.That(DiagramGridLayout.MoveFrameAwayFromLockedNode(Fixture.Contract, Fixture.Nodes, Foreign,
            Fixture.Frames.Single(), false, DiagramGridMetrics.GRID_UNIT_PX), Is.False);
        Assert.That(Fixture.Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Creates declared adjacent cells whose padded frames conflict, without deriving occupancy from their bounds.</summary>
    /// <param name="Direction">Main-flow direction recorded in the source.</param>
    /// <param name="Routing">Whether to project the node views through the existing axis boundary.</param>
    /// <param name="Nested">Whether the left frame has a parent.</param>
    /// <param name="Foreign">Whether B is a foreign node instead of a sibling group's member.</param>
    /// <param name="EmptyGroup">Whether an empty group adds a frame without node occupancy.</param>
    /// <returns>The source, bound nodes, authoritative grid, and independently fitted initial frame cache.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        FlowchartLogicalGrid Grid, List<DiagramGridLayout.GridFrame> Frames) Create(
        string Direction, bool Routing, bool Nested = false, bool Foreign = false, bool EmptyGroup = false)
    {
        string Left = "subgraph Left\nA[Member]\nend";
        if (Nested)
        {
            Left = "subgraph Outer\n" + Left + "\nend";
        }

        string Right = Foreign ? "B[Foreign]" : "subgraph Right\nB[Member]\nend";
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart " + Direction + "\n" + Left + "\n" + Right
            + (EmptyGroup ? "\nsubgraph Empty\nend" : string.Empty));
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(0, 1, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select(Node => new DiagramGridLayout.GridNode { Id = Node.Id, GroupId = Node.ParentGroupId })];
        foreach (DiagramGridLayout.GridNode? Node in Nodes)
        {
            Node.BindCells(Grid);
            if (Routing)
            {
                Node.CellNode!.UseRoutingCoordinates(600);
            }
        }
        List<DiagramGridLayout.GridFrame> Frames = [];
        typeof(DiagramGridLayout).GetMethod(Routing ? "BuildFramesYUp" : "BuildFrames", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Contract, Nodes, Frames]);
        return (Contract, Nodes, Grid, Frames);
    }

    /// <summary>Serialises the complete frame cache without relying on inaccessible fields being implicitly serialised.</summary>
    /// <param name="Frames">Derived frame objects.</param>
    /// <returns>Exact bounds, title-strip and padding values for comparison.</returns>
    private static string FramesOf(List<DiagramGridLayout.GridFrame> Frames) => JsonSerializer.Serialize(
        Frames.Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom, Frame.TitleStrip, Frame.Padding }));

    /// <summary>Independently checks that every declared reservation remains one exclusive integer rectangle.</summary>
    /// <param name="Grid">Authoritative state after a real repair call.</param>
    private static void AssertOccupancy(FlowchartLogicalGrid Grid)
    {
        HashSet<(int Row, int Column)> Cells = [];
        foreach (FlowchartCellRegion Region in Grid.Capture().Regions.Values)
        {
            int Count = 0;
            for (int Row = Region.StartRow; Row < Region.EndRow; Row++)
            {
                for (int Column = Region.StartColumn; Column < Region.EndColumn; Column++)
            {
                Assert.That(Cells.Add((Row, Column)), Is.True);
                Count++;
            }
            }

            Assert.That(Count, Is.EqualTo(Region.RowSpan * Region.ColumnSpan));
        }
    }
}
