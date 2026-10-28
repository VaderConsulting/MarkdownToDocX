namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the initial logical region producer independently of the production pixel placement path.</summary>
[TestFixture]
public sealed class FlowchartCellInitialPlacementTests
{
    /// <summary>Initial reassignment can exchange occupied cells atomically and derives both new centres from the accepted regions.</summary>
    [Test]
    public void ExistingNodePlacementPublishesACompleteLogicalSwap()
    {
        FlowchartLogicalGrid Grid = FlowchartCellInitialPlacement.Create(FlowchartLogicalGridConfiguration.FromRules(),
            [new("A", 0, 0, 1, 1), new("B", 0, 1, 1, 1)], false);
        DiagramGridLayout.GridNode A = new DiagramGridLayout.GridNode { Id = "A", Rank = 0, Column = 1 };
        DiagramGridLayout.GridNode B = new DiagramGridLayout.GridNode { Id = "B", Rank = 0, Column = 0 };
        A.BindCells(Grid); B.BindCells(Grid);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartCellInitialPlacement.TryAssign([A, B], false), Is.True);
        Assert.That(Grid.RegionOf("A"), Is.EqualTo(Before.Regions["B"]));
        Assert.That(Grid.RegionOf("B"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(A.X, Is.GreaterThan(B.X));
    }

    /// <summary>A locked reservation is fixed while the complete automatic plan finds exclusive cells around it.</summary>
    [Test]
    public void ExistingNodePlacementPreservesLockedHintCells()
    {
        FlowchartLogicalGrid Grid = FlowchartCellInitialPlacement.Create(FlowchartLogicalGridConfiguration.FromRules(),
            [new("A", 0, 0, 1, 1), new("B", 0, 1, 1, 1)], false);
        DiagramGridLayout.GridNode A = new DiagramGridLayout.GridNode { Id = "A", Rank = 0, Column = 1 };
        DiagramGridLayout.GridNode B = new DiagramGridLayout.GridNode { Id = "B", Rank = 0, Column = 0 };
        A.BindCells(Grid); B.BindCells(Grid); B.PlacementHintLocked = true;
        FlowchartCellRegion Fixed = Grid.RegionOf("B");
        Assert.That(FlowchartCellInitialPlacement.TryAssign([A, B], false), Is.True);
        Assert.That(Grid.RegionOf("B"), Is.SameAs(Fixed));
        Assert.That(Grid.RegionOf("A").Overlaps(Fixed), Is.False);
    }
    /// <summary>Physical rank order determines direction; region production does not introduce another axis flip or pixel offset.</summary>
    /// <param name="Direction">Declared diagram direction.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void RankedInputsProduceExclusiveRegionsInEveryDirection(string Direction)
    {
        bool Horizontal = Direction is "LR" or "RL";
        bool Reverse = Direction is "BT" or "RL";
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartCellPlacementSeed[] Inputs =
        [
            new FlowchartCellPlacementSeed("start", Reverse ? 1 : 0, 0, 1, 1),
            new FlowchartCellPlacementSeed("decision", Reverse ? 0 : 1, 0, 2, 1)
        ];
        FlowchartLogicalGrid Grid = FlowchartCellInitialPlacement.Create(Configuration, Inputs, Horizontal);
        (double X, double Y) Start = Grid.Origin(Grid.RegionOf("start"));
        (double X, double Y) Decision = Grid.Origin(Grid.RegionOf("decision"));
        double Delta = Horizontal ? Decision.X - Start.X : Decision.Y - Start.Y;
        Assert.That(Math.Sign(Delta), Is.EqualTo(Reverse ? -1 : 1));
        Assert.That(Grid.RegionOf("start").Overlaps(Grid.RegionOf("decision")), Is.False);
        Assert.That(Grid.RegionOf("decision").RowSpan, Is.EqualTo(2));
        Assert.That(Grid.Configuration, Is.SameAs(Configuration));
    }

    /// <summary>Repeated rank/column proposals resolve deterministically using whole cells, independent of input enumeration order.</summary>
    [Test]
    public void DuplicateProposalsProduceTheSameCompletePlanInEitherInputOrder()
    {
        FlowchartCellPlacementSeed[] Inputs = [new FlowchartCellPlacementSeed("A", 0, 0, 1, 1), new FlowchartCellPlacementSeed("B", 0, 0, 1, 1)];
        FlowchartLogicalGridConfiguration Config = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartLogicalGrid First = FlowchartCellInitialPlacement.Create(Config, Inputs, false);
        FlowchartLogicalGrid Second = FlowchartCellInitialPlacement.Create(Config, Inputs.Reverse().ToArray(), false);
        Assert.That(First.Capture().Regions, Is.EquivalentTo(Second.Capture().Regions));
        Assert.That(First.RegionOf("A"), Is.EqualTo(new FlowchartCellRegion(0, 0, 1, 1)));
        Assert.That(First.RegionOf("B"), Is.EqualTo(new FlowchartCellRegion(0, 1, 1, 1)));
    }

    /// <summary>No incomplete grid is returned for a disconnected or nonpositive span proposal.</summary>
    /// <param name="Rows">Proposed row span.</param>
    /// <param name="Columns">Proposed column span.</param>
    [TestCase(0, 1)]
    [TestCase(1, -1)]
    public void InvalidSpansCannotCreateAnInitialGrid(int Rows, int Columns) =>
        Assert.That(() => FlowchartCellInitialPlacement.Create(FlowchartLogicalGridConfiguration.FromRules(),
            [new("A", 0, 0, Rows, Columns)], false), Throws.TypeOf<ArgumentOutOfRangeException>());
}
