namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete repair plans and rollback against independently captured ownership, gaps and drawing geometry.</summary>
[TestFixture]
public sealed class FlowchartAtomicRegionRepairTests
{
    /// <summary>A conflicting group target cannot publish either its nonconflicting members or its proposed gap growth.</summary>
    [Test]
    public void RejectedGroupMoveLeavesEveryRegionAndGapUnchanged()
    {
        var (Grid, Nodes) = Create();
        Grid.GrowGap(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        Grid.GrowGap(false, -1, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        var Before = Grid.Capture();
        var Pixels = PixelsOf(Nodes);
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>
        {
            ["A"] = new(0, 2, 1, 1), ["B"] = new(0, 3, 1, 1)
        }, [new(true, 1, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX)]);
        Assert.That(Plan, Is.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
        Assert.That(Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));
        Assert.That(PixelsOf(Nodes), Is.EqualTo(Pixels));
    }

    /// <summary>The only legal transition is the complete swap: each individual assignment conflicts with the other source region.</summary>
    [Test]
    public void SwapPublishesBothNodesInOneState()
    {
        var (Grid, Nodes) = Create();
        var Before = Grid.Capture();
        var A = Grid.RegionOf("A"); var B = Grid.RegionOf("B");
        Assert.That(FlowchartCellRepair.TryAssign([Nodes[0]], new Dictionary<string, FlowchartCellRegion> { ["A"] = B }), Is.False);
        Assert.That(FlowchartCellRepair.TryAssign([Nodes[1]], new Dictionary<string, FlowchartCellRegion> { ["B"] = A }), Is.False);
        var Plan = FlowchartCellRepair.PlanAssignment([Nodes[0], Nodes[1]],
            new Dictionary<string, FlowchartCellRegion> { ["A"] = B, ["B"] = A });
        Assert.That(Plan, Is.Not.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before), "Planning cannot move either member.");
        Assert.That(Plan!.TryApply(), Is.True);
        Assert.That(Grid.Capture(), Is.SameAs(Plan.After));
        Assert.That(Grid.RegionOf("A"), Is.SameAs(B));
        Assert.That(Grid.RegionOf("B"), Is.SameAs(A));
        AssertExclusiveRectangles(Grid);
    }

    /// <summary>Rollback replaces the entire old state after one candidate changed node regions and both gap directions.</summary>
    [Test]
    public void RollbackAfterGapGrowthRestoresRegionsGapsAndPixelsTogether()
    {
        var (Grid, Nodes) = Create();
        Grid.GrowGap(true, -1, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        var Before = Grid.Capture(); var Pixels = PixelsOf(Nodes);
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>
            { ["A"] = new(1, 0, 1, 1), ["B"] = new(1, 1, 1, 1) },
            [new(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX),
             new(false, 0, 2 * DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX)]);
        Assert.That(Plan, Is.Not.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Plan!.TryApply(), Is.True);
        Assert.That(Grid.Capture().ColumnGaps, Is.Not.EquivalentTo(Before.ColumnGaps));
        Assert.That(Grid.Capture().RowGaps, Is.Not.EquivalentTo(Before.RowGaps));
        Plan.Rollback();
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(PixelsOf(Nodes), Is.EqualTo(Pixels));
        AssertExclusiveRectangles(Grid);
    }

    /// <summary>Mutating a caller's target dictionary after validation cannot change the prepared assignment.</summary>
    [Test]
    public void PreparedTargetsAreFrozenBeforeAnyApplication()
    {
        var (Grid, Nodes) = Create();
        var Targets = new Dictionary<string, FlowchartCellRegion> { ["A"] = new(1, 0, 1, 1) };
        var Plan = FlowchartCellRepair.PlanAssignment([Nodes[0]], Targets)!;
        Targets["A"] = Grid.RegionOf("B");
        Targets["B"] = Grid.RegionOf("C");
        Assert.That(Plan.TryApply(), Is.True);
        Assert.That(Grid.RegionOf("A"), Is.EqualTo(new FlowchartCellRegion(1, 0, 1, 1)));
        Assert.That(Grid.RegionOf("B"), Is.EqualTo(new FlowchartCellRegion(0, 1, 1, 1)));
        AssertExclusiveRectangles(Grid);
    }

    /// <summary>A plan based on older ownership or gaps cannot overwrite a later accepted repair.</summary>
    [Test]
    public void StalePlanCannotOverwriteAnotherAcceptedGapChange()
    {
        var (Grid, Nodes) = Create();
        var Plan = FlowchartCellRepair.PlanMove([Nodes[0], Nodes[1]], 1, 0)!;
        Grid.GrowGap(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        var Newer = Grid.Capture();
        Assert.That(Plan.TryApply(), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Newer));
    }

    /// <summary>A node pinned after planning prevents publication of every member and every gap of that candidate.</summary>
    [Test]
    public void PinningAMemberAfterPlanningRejectsTheWholeAssignment()
    {
        var (Grid, Nodes) = Create();
        var Before = Grid.Capture();
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>
            { ["A"] = new(1, 0, 1, 1), ["B"] = new(1, 1, 1, 1) },
            [new(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX)])!;
        Nodes[1].PlacementHintLocked = true;
        Assert.That(Plan.TryApply(), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Neighbour displacement collects the entire collision chain before applying any member.</summary>
    /// <param name="Pinned">Whether the second member prevents the complete displacement.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void DisplacementPlansAllNeighboursOrNone(bool Pinned)
    {
        var (Grid, Nodes) = Create();
        Nodes[1].PlacementHintLocked = Pinned;
        var Before = Grid.Capture();
        var Plan = FlowchartCellRepair.PlanDisplacement(Nodes, [Nodes[0]], 0, 1);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        if (Pinned) Assert.That(Plan, Is.Null);
        else
        {
            Assert.That(Plan, Is.Not.Null);
            Assert.That(Plan!.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "B" }));
            Assert.That(Plan.TryApply(), Is.True);
            Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(1));
            Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(2));
            AssertExclusiveRectangles(Grid);
        }
    }

    /// <summary>A neighbour-of-a-neighbour is included before publication, and a locked tail rejects the entire chain.</summary>
    /// <param name="PinnedTail">Whether the final displaced neighbour is fixed.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RecursiveDisplacementCannotPublishAPartialChain(bool PinnedTail)
    {
        var (Grid, Nodes) = Create();
        Assert.That(Grid.TryPlace("C", new(0, 2, 1, 1)), Is.True);
        Nodes[2].PlacementHintLocked = PinnedTail;
        var Before = Grid.Capture();
        var Plan = FlowchartCellRepair.PlanDisplacement(Nodes, [Nodes[0]], 0, 1);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        if (PinnedTail) Assert.That(Plan, Is.Null);
        else
        {
            Assert.That(Plan, Is.Not.Null);
            Assert.That(Plan!.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "B", "C" }));
            Assert.That(Plan.TryApply(), Is.True);
            Assert.That(Grid.RegionOf("A").StartColumn, Is.EqualTo(1));
            Assert.That(Grid.RegionOf("B").StartColumn, Is.EqualTo(2));
            Assert.That(Grid.RegionOf("C").StartColumn, Is.EqualTo(3));
            AssertExclusiveRectangles(Grid);
        }
    }

    /// <summary>An invalid later gap increase cannot leave an earlier proposed increase or target region published.</summary>
    [Test]
    public void InvalidGapBatchHasNoPartialEffects()
    {
        var (Grid, Nodes) = Create();
        var Before = Grid.Capture();
        Assert.That(() => FlowchartCellRepair.PlanAssignment(Nodes,
            new Dictionary<string, FlowchartCellRegion> { ["A"] = new(1, 0, 1, 1) },
            [new(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX),
             new(false, 0, double.PositiveInfinity, DiagramGridMetrics.GRID_UNIT_PX)]),
            Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Constructs explicit nonoverlapping regions, with space for an atomic neighbour displacement.</summary>
    /// <returns>The shared authoritative grid and bound node views.</returns>
    private static (FlowchartLogicalGrid Grid, DiagramGridLayout.GridNode[] Nodes) Create()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(0, 1, 1, 1)); Grid.TryPlace("C", new(0, 3, 1, 1));
        var Nodes = new[] { "A", "B", "C" }.Select(Id => new DiagramGridLayout.GridNode { Id = Id }).ToArray();
        foreach (var Node in Nodes) Node.BindCells(Grid);
        return (Grid, Nodes);
    }

    /// <summary>Captures derived pixels independently of region snapshots.</summary>
    /// <param name="Nodes">All fixture node views.</param>
    /// <returns>Exact centres and extents to compare after rejected or rolled-back assignments.</returns>
    private static (double X, double Y, int Width, int Height)[] PixelsOf(DiagramGridLayout.GridNode[] Nodes) =>
        Nodes.Select(Node => (Node.X, Node.Y, Node.Width, Node.Height)).ToArray();

    /// <summary>Enumerates the cells of each complete rectangle to detect shared or missing occupancy independently.</summary>
    /// <param name="Grid">Current authoritative grid.</param>
    private static void AssertExclusiveRectangles(FlowchartLogicalGrid Grid)
    {
        var Used = new HashSet<(int Row, int Column)>();
        foreach (var Region in Grid.Capture().Regions.Values)
        {
            int Count = 0;
            for (int Row = Region.StartRow; Row < Region.EndRow; Row++)
            for (int Column = Region.StartColumn; Column < Region.EndColumn; Column++)
            {
                Assert.That(Used.Add((Row, Column)), Is.True);
                Count++;
            }
            Assert.That(Count, Is.EqualTo(Region.RowSpan * Region.ColumnSpan));
        }
    }
}
