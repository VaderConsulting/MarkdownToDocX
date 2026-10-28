using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks legality-driven frame clearance while retaining logical hints and exact transactional rollback.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FlowchartCellFrameHintRepairTests
{
    /// <summary>A measured external gap can move a hinted node in pixels while leaving its whole-cell relationship exact.</summary>
    [Test]
    public void ExternalGapPreservesHintedCellsAndRestoresExactly()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); double X = Nodes[1].X;
        FlowchartCellRepair.Assignment Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(), [new(true, 0, 60, 30)])!;
        Assert.That(Plan.TryApply(), Is.True);
        Assert.That(Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        Assert.That(Nodes[1].X, Is.EqualTo(X + 60));
        Assert.That(FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Grid.Capture().Regions), Is.True);
        Plan.Rollback(); Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Nodes[1].X, Is.EqualTo(X));
    }

    /// <summary>Ordinary score optimizations cannot use a legality exemption to move an honoured hint.</summary>
    [Test]
    public void OrdinaryMoveRetainsTheHintLock()
    {
        (DiagramContract _, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartCellRepair.PlanMove([Nodes[1]], 0, 1), Is.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A legality candidate cannot break starting-row alignment or cross to the wrong side of its peer.</summary>
    /// <param name="Row">Proposed child starting row.</param>
    /// <param name="Column">Proposed child starting column.</param>
    [TestCase(1, 1)]
    [TestCase(0, -1)]
    public void LegalityMoveStillRejectsBrokenHint(int Row, int Column)
    {
        (DiagramContract _, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture(); FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartCellRepair.PlanAssignment(Nodes,
            new Dictionary<string, FlowchartCellRegion> { ["B"] = new(Row, Column, 1, 1) }, AllowHintedRegionRepair: true), Is.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Minimum cell offsets are enforced even for a frame-legality candidate.</summary>
    [Test]
    public void LegalityMoveCannotShortenTheDeclaredOffset()
    {
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Assert.That(Grid.TryMove(["B"], 0, 2), Is.True);
        Contract.Nodes.Single(Node => Node.Id == "B").PlacementHint!.Offset = 2;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(FlowchartCellRepair.PlanAssignment(Nodes,
            new Dictionary<string, FlowchartCellRegion> { ["B"] = new(0, 2, 1, 1) }, AllowHintedRegionRepair: true), Is.Null);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>The lower-cost external-gap assignment beats a farther whole-cell move when both preserve the hint.</summary>
    [Test]
    public void FrameFitSelectsGapInsteadOfTheFartherHintedCell()
    {
        using IDisposable Scope = FlowchartRenderContext.Activate(new());
        (DiagramContract? Contract, List<DiagramGridLayout.GridNode>? Nodes, FlowchartLogicalGrid? Grid) = Fixture();
        Nodes[0].CellHintValidator = null; // An independently fixed frame member cannot move.
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture(); List<DiagramGridLayout.GridFrame> Frames = [];
        DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames);
        DiagramGridLayout.FitFrames(Contract, Nodes, Frames, false, "hinted-gap-choice");
        Assert.That(Grid.RegionOf("B"), Is.EqualTo(Before.Regions["B"]));
        Assert.That(Grid.Capture().ColumnGaps[0], Is.EqualTo(90));
        Assert.That(Grid.RegionOf("A"), Is.EqualTo(Before.Regions["A"]));
        Assert.That(FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Grid.Capture().Regions), Is.True);
    }

    /// <summary>A nested-frame layout can prefer a whole-cell outsider move plus gaps over a legal gap-only assignment.</summary>
    [Test]
    public void FrameFitCanPreferCellsOverGapOnly()
    {
        using IDisposable Scope = FlowchartRenderContext.Activate(new());
        DiagramContract Contract = new DiagramContract();
        GroupContract Region = new GroupContract { Id = "Region" }; Region.ChildGroupIdsMutable.Add("Zone"); Region.ChildNodeIdsMutable.AddRange(["Gate", "Db"]);
        GroupContract Zone = new GroupContract { Id = "Zone", ParentGroupId = "Region" }; Zone.ChildNodeIdsMutable.AddRange(["web", "App"]);
        Contract.GroupsMutable.AddRange([Region, Zone]);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        List<DiagramGridLayout.GridNode> Nodes = [];
        string[] Ids = ["Client", "Gate", "web", "App", "Db"];
        for (int Row = 0; Row < Ids.Length; Row++)
        {
            string? Group = Row == 0 ? null : Row is 2 or 3 ? "Zone" : "Region";
            Contract.NodesMutable.Add(new() { Id = Ids[Row], ParentGroupId = Group, Shape = "rect" });
            Grid.TryPlace(Ids[Row], new(Row, 0, 1, 1));
            DiagramGridLayout.GridNode Node = new DiagramGridLayout.GridNode { Id = Ids[Row], GroupId = Group }; Node.BindCells(Grid); Nodes.Add(Node);
            if (Row > 0)
            {
                Contract.EdgesMutable.Add(new() { SourceId = Ids[Row - 1], TargetId = Ids[Row] });
            }
        }
        FlowchartCellRepair.Assignment GapOnly = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(),
            [new(false, 0, 120, 30), new(false, 1, 90, 30), new(false, 3, 30, 30)])!;
        List<DiagramGridLayout.GridFrame> GapFrames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, GapOnly.After);
        Dictionary<string, FlowchartLayoutNodeBounds> GapBounds = Nodes.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(GapOnly.After.Regions[Node.Id], Node.GeometryProvider, GapOnly.After));
        Assert.That(DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, GapFrames, GapBounds), Is.Empty);
        double GapScore = PlacementScore(Nodes, GapOnly.After, GapFrames);
        List<DiagramGridLayout.GridFrame> Frames = []; DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames);
        DiagramGridLayout.FitFrames(Contract, Nodes, Frames, false, "move-versus-gap-score");
        Assert.That(Grid.RegionOf("Client").StartRow, Is.Not.EqualTo(0));
        Assert.That(PlacementScore(Nodes, Grid.Capture(), Frames), Is.LessThan(GapScore));
    }

    /// <summary>Measures a complete counterfactual placement including its actual node and frame bounds.</summary>
    /// <param name="Nodes">Complete bound diagram.</param>
    /// <param name="State">Immutable proposed regions and gaps.</param>
    /// <param name="Frames">Frames projected from exactly that state.</param>
    /// <returns>The existing placement score used to compare legal alternatives.</returns>
    private static double PlacementScore(List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid.Snapshot State,
        List<DiagramGridLayout.GridFrame> Frames)
    {
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Layout.NodeBoundsMutable.Add(Node.CellNode!.Bounds(State.Regions[Node.Id], Node.GeometryProvider, State));
        }

        foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            Layout.ClusterBoundsMutable.Add(new() { NodeId = Frame.Id,
            CenterX = (Frame.Left + Frame.Right) / 2.0, CenterY = (Frame.Top + Frame.Bottom) / 2.0,
            Width = Frame.Right - Frame.Left, Height = Math.Abs(Frame.Top - Frame.Bottom) });
        }

        return FlowchartLayoutScore.MeasurePlacement(Layout);
    }

    /// <summary>Creates a hinted external child next to a frame whose required clearance exceeds the base gap.</summary>
    /// <returns>Complete source membership, bound nodes and authoritative grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Fixture()
    {
        DiagramContract Contract = new DiagramContract();
        GroupContract Group = new GroupContract { Id = "G", Title = "G" }; Group.ChildNodeIdsMutable.Add("A");
        Contract.GroupsMutable.Add(Group);
        Contract.NodesMutable.AddRange([new() { Id = "A", ParentGroupId = "G", Shape = "rect" },
            new() { Id = "B", Shape = "rect", PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "A", IsActive = true } }]);
        Contract.EdgesMutable.Add(new() { SourceId = "A", TargetId = "B" });
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(0, 1, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [new() { Id = "A", GroupId = "G" }, new() { Id = "B" }];
        Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> Validate = Regions => FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Regions);
        foreach (DiagramGridLayout.GridNode Node in Nodes) { Node.BindCells(Grid); Node.PlacementHintLocked = true; Node.CellHintValidator = Validate; }
        return (Contract, Nodes, Grid);
    }
}
