namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises repair entry points through cell-bound engine nodes, including forbidden legacy writes.</summary>
[TestFixture]
public sealed class FlowchartCellRepairTests
{
    /// <summary>Keeping a pinned start cell is insufficient: changing its spans also changes its derived centre.</summary>
    [Test]
    public void SpanRepairsCannotMoveAPinnedCentre()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        Nodes[0].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        double X = Nodes[0].X, Y = Nodes[0].Y;
        Assert.That(FlowchartCellRepair.TryResize(Nodes[0], 2, 1), Is.False);
        Assert.That(FlowchartCellRepair.TryAssign(Nodes, new Dictionary<string, FlowchartCellRegion>
            { [Nodes[0].Id] = new(0, 0, 2, 1) }), Is.False);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Nodes[0].X, Is.EqualTo(X));
        Assert.That(Nodes[0].Y, Is.EqualTo(Y));
    }

    /// <summary>A shared gap moves only the regions on its affected side; pinned regions reject the entire candidate.</summary>
    [Test]
    public void GapRepairPreservesOccupancyAndPinnedGeometry()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        double FirstX = Nodes[0].X, SecondX = Nodes[1].X;
        Nodes[1].PlacementHintLocked = true;
        Assert.That(FlowchartCellRepair.TryGrowGap(Nodes, true, 0, 1), Is.False);
        Assert.That(Grid.Capture().ColumnGaps, Is.Empty);
        Nodes[1].PlacementHintLocked = false;
        Nodes[0].PlacementHintLocked = true;
        Assert.That(FlowchartCellRepair.TryGrowGap(Nodes, true, 0, 1), Is.True);
        Assert.That(Nodes[0].X, Is.EqualTo(FirstX));
        Assert.That(Nodes[1].X, Is.EqualTo(SecondX + DiagramGridMetrics.GRID_UNIT_PX));
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Internal gap growth enlarges a rectangular reservation's drawn body but does not enlarge a diamond just to fill space.</summary>
    [Test]
    public void InternalGapGrowthPreservesTheDiamondShapeScale()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("diamond", new(0, 0, 2, 1));
        DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = "diamond", IsDiamond = true };
        Node.BindCells(Grid);
        int BeforeHeight = Node.Height;
        (double Width, double Height) BeforeSize = Grid.Measure(Node.CellNode!.Region);
        Grid.GrowGap(false, 0, 4 * DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.That(Grid.Measure(Node.CellNode.Region).Height, Is.GreaterThan(BeforeSize.Height));
        Assert.That(Node.Height, Is.EqualTo(BeforeHeight));
    }

    /// <summary>Binding ignores provisional drawing coordinates and sizes instead of reverse-engineering occupancy from them.</summary>
    [Test]
    public void NodePixelsAreReadOnlyProjectionsOfTheDeclaredRegion()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("node", new(-2, 3, 2, 2));
        DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = "node", X = -123.5, Y = 765.25, Width = 7, Height = 9 };
        Node.BindCells(Grid);
        FlowchartCellRegion Region = Grid.RegionOf("node");
        (double X, double Y) Origin = Grid.Origin(Region); (double Width, double Height) Extent = Grid.Measure(Region);
        Assert.Multiple(() =>
        {
            Assert.That(Node.X, Is.EqualTo(Origin.X + (Extent.Width / 2)));
            Assert.That(Node.Y, Is.EqualTo(Origin.Y + (Extent.Height / 2)));
            Assert.That(Node.Width, Is.EqualTo(Extent.Width));
            Assert.That(Node.Height, Is.EqualTo(Extent.Height));
            Assert.That(Node.CellNode!.Region, Is.SameAs(Region));
        });
    }

    /// <summary>Any unconverted repair attempting a pixel mutation fails before changing cell state.</summary>
    /// <param name="Operation">Legacy mutation entry point.</param>
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Width")]
    [TestCase("Height")]
    [TestCase("FrameRollback")]
    [TestCase("CoordinateWrite")]
    public void PixelMutationCannotBypassCellAuthority(string Operation)
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (Operation)
            {
                case "X": Nodes[0].X += DiagramGridMetrics.GRID_UNIT_PX; break;
                case "Y": Nodes[0].Y += 0.5; break;
                case "Width": Nodes[0].Width += DiagramGridMetrics.GRID_UNIT_PX; break;
                case "Height": Nodes[0].Height += DiagramGridMetrics.GRID_UNIT_PX; break;
                case "FrameRollback": Nodes[0].SetPositionFromFrameFit(0.25, 0.25); break;
                case "CoordinateWrite": Nodes[0].SetYAfterCoordinateTransform(17.5); break;
            }
        });
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Translation moves a complete linked group atomically and cannot move a pinned member.</summary>
    [Test]
    public void GroupMoveAndPinnedMoveUseTheSameCellTransaction()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        Assert.That(FlowchartCellRepair.TryMove(Nodes, 1, 0), Is.True);
        Assert.That(Grid.RegionOf("first").StartRow, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("second").StartRow, Is.EqualTo(1));
        Nodes[1].PlacementHintLocked = true;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartCellRepair.TryMove(Nodes, -1, 0), Is.False);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Accepted and rejected span candidates remain contiguous cell rectangles and never steal another reservation.</summary>
    [Test]
    public void SpanRepairUsesWholeRegionsWithoutPartialMutation()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        Assert.That(FlowchartCellRepair.TryResize(Nodes[0], 2, 2), Is.False);
        Assert.That(Grid.RegionOf("first"), Is.EqualTo(new FlowchartCellRegion(0, 0, 1, 1)));
        Assert.That(FlowchartCellRepair.TryResize(Nodes[0], 2, 1), Is.True);
        Assert.That(Grid.RegionOf("first"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FlowchartCellRepair.TryResize(Nodes[0], -1, 1));
        Assert.That(Grid.RegionOf("first"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 1)));
    }

    /// <summary>The complete order-search option may swap cells but may not assign the same region twice.</summary>
    [Test]
    public void JointAssignmentRejectsSharedOccupancyAndAllowsWholeRegionSwaps()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        FlowchartCellRegion First = Grid.RegionOf("first"); FlowchartCellRegion Second = Grid.RegionOf("second");
        Assert.That(FlowchartCellRepair.TryAssign(Nodes, new Dictionary<string, FlowchartCellRegion>
        {
            ["first"] = Second, ["second"] = Second
        }), Is.False);
        Assert.That(Grid.RegionOf("first"), Is.SameAs(First));
        Assert.That(Grid.RegionOf("second"), Is.SameAs(Second));
        Assert.That(FlowchartCellRepair.TryAssign(Nodes, new Dictionary<string, FlowchartCellRegion>
        {
            ["first"] = Second, ["second"] = First
        }), Is.True);
        Assert.That(Grid.RegionOf("first"), Is.SameAs(Second));
        Assert.That(Grid.RegionOf("second"), Is.SameAs(First));
    }

    /// <summary>Rollback restores occupancy and every derived dimension, including nodes spanning a grown gap.</summary>
    [Test]
    public void RejectedCandidateRestoresPixelsByRestoringCellsAndGaps()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        (double X, double Y, int Width, int Height)[] Bounds = [.. Nodes.Select(Node => (Node.X, Node.Y, Node.Width, Node.Height))];
        Assert.That(FlowchartCellRepair.TryMove(Nodes, 2, 1), Is.True);
        Grid.GrowGap(true, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.That(FlowchartCellRepair.TryResize(Nodes[0], 3, 1), Is.True);
        FlowchartCellRepair.Restore(Nodes, Before);
        Assert.That(Nodes.Select(Node => (Node.X, Node.Y, Node.Width, Node.Height)), Is.EqualTo(Bounds));
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
    }

    /// <summary>Changing the projection never changes a region or turns north into increasing logical row numbers.</summary>
    [Test]
    public void RoutingCoordinateAdapterDoesNotChangeCellOwnership()
    {
        (FlowchartLogicalGrid? Grid, DiagramGridLayout.GridNode[]? Nodes) = MakePair();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        const double Flip = 600;
        double Original = Nodes[0].Y;
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.CellNode!.UseRoutingCoordinates(Flip);
        }

        Assert.That(Nodes[0].Y, Is.EqualTo(Flip - Original));
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(FlowchartCellRepair.TryMove([Nodes[0]], -1, 0), Is.True);
        Assert.That(Grid.RegionOf("first").StartRow, Is.EqualTo(-1));
        Assert.That(Nodes[0].Y, Is.GreaterThan(Flip - Original));
    }

    /// <summary>A diamond's reserved corners are occupied, while its proportional silhouette remains inside that region.</summary>
    [Test]
    public void DiamondSpanRepairCannotCreateAnUndersizedReservation()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("diamond", new(0, 0, 2, 1));
        DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = "diamond", IsDiamond = true };
        Node.BindCells(Grid);
        Assert.That(FlowchartCellRepair.TryResize(Node, 1, 1), Is.False);
        Assert.That(Grid.RegionOf("diamond"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 1)));
        Assert.That(Node.Height, Is.GreaterThan(0));
        Assert.That(Node.Height, Is.LessThanOrEqualTo(Grid.Measure(Node.CellNode!.Region).Height));
        Assert.That(Grid.TryPlace("corner", new(0, 0, 1, 1)), Is.False);
    }

    /// <summary>Creates two adjacent declared regions before binding the engine nodes.</summary>
    /// <returns>The owning grid and two cell-authoritative engine nodes.</returns>
    private static (FlowchartLogicalGrid Grid, DiagramGridLayout.GridNode[] Nodes) MakePair()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("first", new(0, 0, 1, 1)); Grid.TryPlace("second", new(0, 1, 1, 1));
        DiagramGridLayout.GridNode[] Nodes = [new DiagramGridLayout.GridNode { Id = "first" }, new DiagramGridLayout.GridNode { Id = "second" }];
        foreach (DiagramGridLayout.GridNode? Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        return (Grid, Nodes);
    }
}
