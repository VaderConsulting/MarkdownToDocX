using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks logical occupancy independently of visual centres, routing coordinates and derived gaps.</summary>
[TestFixture]
public sealed class FlowchartLogicalGridTests
{
    /// <summary>A group move tests the final occupancy as a unit instead of rejecting its members' intermediate positions.</summary>
    [Test]
    public void GroupTranslationIsAtomicAndRejectedTranslationKeepsEveryRegion()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Assert.That(Grid.TryPlace("first", new(0, 0, 1, 1)), Is.True);
        Assert.That(Grid.TryPlace("second", new(0, 1, 1, 1)), Is.True);
        Assert.That(Grid.TryPlace("fixed", new(0, 3, 1, 1)), Is.True);
        Assert.That(Grid.TryMove(["first", "second"], 0, 1), Is.True);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(Grid.TryMove(["first", "second"], 0, 1), Is.False);
        Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
        Assert.That(Grid.RegionOf("first").StartColumn, Is.EqualTo(1));
        Assert.That(Grid.RegionOf("second").StartColumn, Is.EqualTo(2));
    }

    /// <summary>Swapping two complete cell reservations is legal even when neither single-node move is legal first.</summary>
    [Test]
    public void JointOrderRepairCanSwapWithoutIntermediateSharedCells()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        FlowchartCellRegion First = new FlowchartCellRegion(-1, -2, 2, 2);
        FlowchartCellRegion Second = new FlowchartCellRegion(-1, 0, 2, 2);
        Grid.TryPlace("first", First); Grid.TryPlace("second", Second);
        Assert.That(Grid.TryApply(new Dictionary<string, FlowchartCellRegion>
        {
            ["first"] = Second, ["second"] = First
        }), Is.True);
        Assert.That(Grid.RegionOf("first"), Is.SameAs(Second));
        Assert.That(Grid.RegionOf("second"), Is.SameAs(First));
    }

    /// <summary>Span repair reserves one solid rectangle and refuses growth into any other reservation.</summary>
    [Test]
    public void SpanRepairCannotCreateDisconnectedOrSharedOccupancy()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("hub", new(0, 0, 1, 1)); Grid.TryPlace("fixed", new(1, 1, 1, 1));
        Assert.That(Grid.TryResize("hub", 2, 2), Is.False);
        Assert.That(Grid.RegionOf("hub"), Is.EqualTo(new FlowchartCellRegion(0, 0, 1, 1)));
        Assert.That(Grid.TryResize("hub", 2, 1), Is.True);
        Assert.That(Grid.RegionOf("hub"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid.TryResize("hub", 1, 0));
        Assert.That(Grid.RegionOf("hub"), Is.EqualTo(new FlowchartCellRegion(0, 0, 2, 1)));
    }

    /// <summary>Rejected candidates restore inserted gaps and reservations, giving bit-identical derived pixel geometry.</summary>
    [Test]
    public void RollbackRestoresRegionsAndGapsWithoutReadingPixelBounds()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration);
        Grid.TryPlace("wide", new(-1, -1, 2, 2)); Grid.TryPlace("other", new(2, 2, 1, 1));
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        (double X, double Y) Origin = Grid.Origin(Grid.RegionOf("wide"));
        (double Width, double Height) Size = Grid.Measure(Grid.RegionOf("wide"));
        Grid.GrowGap(true, -1, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        Grid.GrowGap(false, 0, DiagramGridMetrics.GRID_UNIT_PX, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.That(Grid.TryMove(["other"], 1, 1), Is.True);
        Grid.Restore(Before);
        Assert.Multiple(() =>
        {
            Assert.That(Grid.Capture().Regions, Is.EquivalentTo(Before.Regions));
            Assert.That(Grid.Capture().ColumnGaps, Is.EquivalentTo(Before.ColumnGaps));
            Assert.That(Grid.Capture().RowGaps, Is.EquivalentTo(Before.RowGaps));
            Assert.That(Grid.Origin(Grid.RegionOf("wide")), Is.EqualTo(Origin));
            Assert.That(Grid.Measure(Grid.RegionOf("wide")), Is.EqualTo(Size));
        });
    }

    /// <summary>Equal neighbour counts can fit different boundaries; only measured spacing requests growth.</summary>
    [Test]
    public void PortCapacityComesFromAllocatedGeometry()
    {
        double Minimum = DiagramGridMetrics.GRID_UNIT_PX;
        NodeGeometryPort[] Crowded = [new(0, 0, 0, 1), new(Minimum / 2, 0, 0, 1)];
        NodeGeometryPort[] Clear = [new(0, 0, 0, 1), new(Minimum, 0, 0, 1)];
        Assert.Multiple(() =>
        {
            Assert.That(FlowchartPortCapacity.Shortfall(Crowded, Minimum), Is.EqualTo(Minimum / 2));
            Assert.That(FlowchartPortCapacity.Shortfall(Clear, Minimum), Is.Zero);
            Assert.That(FlowchartPortCapacity.Shortfall([Crowded[0]], Minimum), Is.Zero);
            Assert.That(FlowchartPortCapacity.Shortfall([], Minimum), Is.Zero);
        });
    }

    /// <summary>Rule defaults come from renderer dimensions and remain fixed when individual nodes need more room.</summary>
    [Test]
    public void DefaultsStayFixedWhenOneNodeSpansMoreCells()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration);
        FlowchartCellRegion Small = new FlowchartCellRegion(0, 0, 1, 1);
        FlowchartCellRegion Hub = new FlowchartCellRegion(0, 1, 3, 2);
        Assert.That(Grid.TryPlace("small", Small), Is.True);
        Assert.That(Grid.TryPlace("hub", Hub), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Configuration.CellWidth, Is.EqualTo(DiagramGridMetrics.NODE_WIDTH_PX));
            Assert.That(Configuration.CellHeight, Is.EqualTo(FlowchartLayoutRules.BaseCellHeight));
            Assert.That(Grid.Measure(Small), Is.EqualTo((Configuration.CellWidth, Configuration.CellHeight)));
            Assert.That(Grid.Measure(Hub).Width, Is.EqualTo((2 * Configuration.CellWidth) + Configuration.ColumnGap));
            Assert.That(Grid.RegionOf("small"), Is.EqualTo(Small));
        });
    }

    /// <summary>Reserved shape corners still own cells even though the drawn silhouette does not fill them.</summary>
    [Test]
    public void OccupancyUsesWholeRegionsAndRejectedMovesRestoreExactly()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        FlowchartCellRegion Original = new FlowchartCellRegion(-2, -1, 2, 2);
        Assert.That(Grid.TryPlace("diamond", Original), Is.True);
        Assert.That(Grid.TryPlace("neighbour", new(0, -1, 1, 2)), Is.True);
        Assert.That(Grid.TryPlace("other", new(-1, 0, 1, 1)), Is.False);
        Assert.That(Grid.TryPlace("diamond", new(-1, -1, 2, 2)), Is.False);
        Assert.That(Grid.RegionOf("diamond"), Is.SameAs(Original));
    }

    /// <summary>Internal gap growth enlarges spanning nodes but leaves other sizes and base cells unchanged.</summary>
    [Test]
    public void GapGrowthUpdatesDerivedDimensionsOnly()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration);
        FlowchartCellRegion Region = new FlowchartCellRegion(0, 0, 2, 2);
        (double Width, double Height) Before = Grid.Measure(Region);
        Grid.GrowGap(true, 0, 1, DiagramGridMetrics.GRID_UNIT_PX);
        Grid.GrowGap(false, 0, DiagramGridMetrics.GRID_UNIT_PX + 1, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.Multiple(() =>
        {
            Assert.That(Grid.Configuration, Is.SameAs(Configuration));
            Assert.That(Grid.Measure(Region), Is.EqualTo((Before.Width + DiagramGridMetrics.GRID_UNIT_PX,
                Before.Height + (2 * DiagramGridMetrics.GRID_UNIT_PX))));
            Assert.That(Grid.Measure(new(1, 1, 1, 1)), Is.EqualTo((Configuration.CellWidth, Configuration.CellHeight)));
            Assert.That(Grid.Origin(new(1, 1, 1, 1)), Is.EqualTo((Configuration.CellWidth + Configuration.ColumnGap
                + DiagramGridMetrics.GRID_UNIT_PX, Configuration.CellHeight + Configuration.RowGap + (2 * DiagramGridMetrics.GRID_UNIT_PX))));
        });
    }

    /// <summary>Logical negative indices accumulate the same grown gaps in the opposite direction.</summary>
    [Test]
    public void NegativeIndicesKeepOneSharedGridOrigin()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration);
        Grid.GrowGap(true, -1, 1, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.That(Grid.Origin(new(0, -1, 1, 1)).X,
            Is.EqualTo(-Configuration.CellWidth - Configuration.ColumnGap - DiagramGridMetrics.GRID_UNIT_PX));
    }

    /// <summary>Invalid spans cannot become fractional or empty reservations.</summary>
    /// <param name="Rows">Proposed row span.</param>
    /// <param name="Columns">Proposed column span.</param>
    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(-1, 1)]
    [TestCase(1, -1)]
    public void RejectsNonpositiveSpans(int Rows, int Columns) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlowchartCellRegion(0, 0, Rows, Columns));

    /// <summary>Wrapping is measured first and a fitting base cell needs no larger alternatives.</summary>
    [Test]
    public void MeasuresWrappedTextBeforeChoosingTheBaseSpan()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        double Available = Configuration.CellWidth - (4 * Configuration.TextPadding);
        IReadOnlyList<FlowchartNodeSpanCandidate> Candidates = FlowchartNodeSpanCandidates.Measure("one two three four", Configuration,
            Text => Text.Length * Available / "one two three".Length, 24);
        Assert.That(Candidates, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(Candidates[0].RowSpan, Is.EqualTo(1));
            Assert.That(Candidates[0].ColumnSpan, Is.EqualTo(1));
            Assert.That(Candidates[0].DisplayLabel, Is.EqualTo("one two three\nfour"));
        });
    }

    /// <summary>Long text offers taller and wider fits rather than resizing every node or selecting by local area.</summary>
    [Test]
    public void GeneratesIndependentUndominatedWidthAndHeightChoices()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        IReadOnlyList<FlowchartNodeSpanCandidate> Candidates = FlowchartNodeSpanCandidates.Measure(string.Join(" ", Enumerable.Repeat("word", 20)),
            Configuration, Text => Text.Length * 10, 24);
        Assert.That(Candidates.Count, Is.GreaterThan(1));
        foreach (FlowchartNodeSpanCandidate Candidate in Candidates)
        {
            (double Width, double Height) Size = new FlowchartLogicalGrid(Configuration).Measure(new(0, 0, Candidate.RowSpan, Candidate.ColumnSpan));
            Assert.That(Candidate.TextWidth + (4 * Configuration.TextPadding), Is.LessThanOrEqualTo(Size.Width));
            Assert.That(Candidate.TextHeight + (2 * Configuration.TextPadding), Is.LessThanOrEqualTo(Size.Height));
            Assert.That(Candidates.Any(Other => Other != Candidate && Other.RowSpan <= Candidate.RowSpan
                && Other.ColumnSpan <= Candidate.ColumnSpan), Is.False);
        }
    }

    /// <summary>Explicit blank lines survive wrapping and measured mandatory capacity grows only the affected node.</summary>
    [Test]
    public void PreservesExplicitBreaksAndUsesMeasuredPortMinimums()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        FlowchartNodeSpanCandidate Ordinary = FlowchartNodeSpanCandidates.Measure("short", Configuration, Text => Text.Length, 24).Single();
        FlowchartNodeSpanCandidate Hub = FlowchartNodeSpanCandidates.Measure("short\n\nlabel", Configuration, Text => Text.Length, 24,
            MinimumHeight: (2 * Configuration.CellHeight) + Configuration.RowGap + 1).Single();
        Assert.Multiple(() =>
        {
            Assert.That(Hub.DisplayLabel, Is.EqualTo("short\n\nlabel"));
            Assert.That(Hub.RowSpan, Is.EqualTo(3));
            Assert.That(Ordinary.RowSpan, Is.EqualTo(1));
            Assert.That(Ordinary.ColumnSpan, Is.EqualTo(1));
        });
    }

    /// <summary>No maximum span justifies prematurely splitting a long word.</summary>
    [Test]
    public void LongWordsGrowByWholeCellsWithoutBeingSplit()
    {
        FlowchartLogicalGridConfiguration Configuration = FlowchartLogicalGridConfiguration.FromRules();
        string Word = new('W', 100);
        FlowchartNodeSpanCandidate Candidate = FlowchartNodeSpanCandidates.Measure(Word, Configuration, Text => Text.Length * 16, 24).Single();
        Assert.That(Candidate.DisplayLabel, Is.EqualTo(Word));
        Assert.That(Candidate.ColumnSpan, Is.GreaterThan(1));
    }
}
