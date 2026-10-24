using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks complete joint topology proposals, atomic application and unchanged hints before routed scoring.</summary>
[TestFixture]
public sealed class FlowchartCellJointRouteCandidateTests
{
    /// <summary>A nominated middle endpoint offers a complete chain assignment in either flow orientation.</summary>
    /// <param name="Horizontal">Whether flow advances through columns.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void MiddleEndpointOffersWholeChainStraightening(bool Horizontal)
    {
        var Fixture = Create([("A", 0, 4), ("B", 2, 8), ("C", 4, 0)], [("A", "B"), ("B", "C")], Horizontal);
        var Before = Fixture.Grid.Capture();
        var Options = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, Horizontal,
            new HashSet<string> { "B" }).Where(Option => Option.Key.StartsWith("chain|", StringComparison.Ordinal)).ToArray();
        Assert.That(Options, Is.Not.Empty);
        Assert.That(Options.Any(Option => Option.Assignment.After.Regions.Values.Select(Region =>
            Horizontal ? Region.StartRow : Region.StartColumn).Distinct().Count() == 1), Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        foreach (var Option in Options)
        {
            Assert.That(Option.Assignment.TryApply(), Is.True);
            Option.Assignment.Rollback();
            Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        }
    }

    /// <summary>Every branch tail moves with the root in a fan translation and restores exactly as one assignment.</summary>
    [Test]
    public void FanTranslationIncludesRootAndEveryTail()
    {
        var Fixture = Create([("I", -2, 0), ("R", 0, 0), ("A", 2, 0), ("B", 2, 3), ("A2", 4, 0), ("B2", 4, 3)],
            [("I", "R"), ("R", "A"), ("R", "B"), ("A", "A2"), ("B", "B2")], false);
        var Before = Fixture.Grid.Capture();
        var Option = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "R" }).First(Option => Option.Key.StartsWith("fan|", StringComparison.Ordinal));
        Assert.That(Option.Assignment.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "R", "A", "B", "A2", "B2" }));
        var Deltas = Option.Assignment.After.Regions.Where(Pair => Pair.Key != "I").Select(Pair => (Pair.Value.StartRow - Before.Regions[Pair.Key].StartRow,
            Pair.Value.StartColumn - Before.Regions[Pair.Key].StartColumn)).Distinct().ToArray();
        Assert.That(Deltas, Has.Length.EqualTo(1));
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(Option.Assignment.TryApply(), Is.True);
        Option.Assignment.Rollback();
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>The existing straight prefix joins a nominated bent tail in one rigid whole-chain move.</summary>
    [Test]
    public void WholeChainTranslationIncludesTheUnnominatedPrefix()
    {
        var Fixture = Create([("P", 0, 0), ("A", 2, 0), ("Z", 2, 4), ("B", 4, 2), ("C", 6, 0)],
            [("P", "A"), ("P", "Z"), ("A", "B"), ("B", "C")], false);
        var Before = Fixture.Grid.Capture();
        var Option = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "B" }).First(Option => Option.Key.StartsWith("chain-move|", StringComparison.Ordinal));
        Assert.That(Option.Assignment.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(Option.Assignment.After.Regions["P"], Is.EqualTo(Before.Regions["P"]));
        Assert.That(Option.Assignment.TryApply(), Is.True);
        Option.Assignment.Rollback();
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A combined fan-in/fan-out can move all four neighbours to free sides in one atomic assignment.</summary>
    [Test]
    public void HubSidesArePlannedTogetherBeforeAnyNeighbourMoves()
    {
        var Fixture = Create([("H", 2, 2), ("A", 1, 0), ("C", 3, 0), ("D", 1, 4), ("E", 3, 4)],
            [("A", "H"), ("C", "H"), ("H", "D"), ("H", "E")], false);
        var Before = Fixture.Grid.Capture();
        var Option = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "H" }).First(Option => Option.Key.StartsWith("fan-sides|", StringComparison.Ordinal));
        Assert.That(Option.Assignment.Nodes.Select(Node => Node.Id), Is.EquivalentTo(new[] { "A", "C", "D", "E" }));
        Assert.That(Option.Assignment.After.Regions["H"], Is.EqualTo(Before.Regions["H"]));
        Assert.That(Option.Assignment.After.Regions.Values.Select(Region => (Region.StartRow, Region.StartColumn)),
            Is.Unique);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(Option.Assignment.TryApply(), Is.True);
        Option.Assignment.Rollback();
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A transitive hinted follower moves with its owner or the entire proposed hub assignment is rejected.</summary>
    [Test]
    public void HubSideAssignmentClosesOverHintDependents()
    {
        var Fixture = Create([("H", 2, 2), ("A", 1, 0), ("C", 3, 0), ("D", 1, 4), ("E", 3, 4), ("F", 1, 1)],
            [("A", "H"), ("C", "H"), ("H", "D"), ("H", "E")], false);
        Fixture.Contract.Nodes.Single(Node => Node.Id == "F").PlacementHint = new()
            { Kind = PlacementHintKind.RightOf, PeerId = "A", IsActive = true };
        foreach (var Node in Fixture.Nodes) Node.CellHintValidator = Regions => FlowchartPlacementHints.CellHintsMatch(Fixture.Contract, Fixture.Nodes, Regions);
        var Before = Fixture.Grid.Capture();
        var Options = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "H" }).Where(Option => Option.Key.StartsWith("fan-sides|", StringComparison.Ordinal)).ToArray();
        Assert.That(Options, Is.Not.Empty);
        foreach (var Option in Options)
        {
            var A = Option.Assignment.After.Regions["A"]; var F = Option.Assignment.After.Regions["F"];
            Assert.That((F.StartRow - A.StartRow, F.StartColumn - A.StartColumn), Is.EqualTo((0, 1)));
        }
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A complete directional chain follows its original terminal hints in logical cells without changing the hints.</summary>
    /// <param name="Direction">Original leave direction, where north decreases the logical row.</param>
    [TestCase(LayoutHintDirection.North)]
    [TestCase(LayoutHintDirection.South)]
    [TestCase(LayoutHintDirection.East)]
    [TestCase(LayoutHintDirection.West)]
    public void WholeHintedChainUsesTheDeclaredDirection(LayoutHintDirection Direction)
    {
        var Fixture = Create([("A", 0, 0), ("B", 0, 2), ("C", 0, 4)], [("A", "B"), ("B", "C")], false);
        foreach (var Edge in Fixture.Contract.Edges) Edge.RoutingHint = new() { Leave = Direction, IsActive = true };
        var Before = Fixture.Grid.Capture();
        var Option = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "B" }).First(Option => Option.Key.StartsWith("hint-chain|", StringComparison.Ordinal));
        var A = Option.Assignment.After.Regions["A"]; var B = Option.Assignment.After.Regions["B"]; var C = Option.Assignment.After.Regions["C"];
        Assert.That(Direction switch
        {
            LayoutHintDirection.North => A.StartRow > B.StartRow && B.StartRow > C.StartRow && A.StartColumn == C.StartColumn,
            LayoutHintDirection.South => A.StartRow < B.StartRow && B.StartRow < C.StartRow && A.StartColumn == C.StartColumn,
            LayoutHintDirection.East => A.StartColumn < B.StartColumn && B.StartColumn < C.StartColumn && A.StartRow == C.StartRow,
            _ => A.StartColumn > B.StartColumn && B.StartColumn > C.StartColumn && A.StartRow == C.StartRow
        }, Is.True);
        Assert.That(Fixture.Contract.Edges.All(Edge => Edge.RoutingHint!.IsActive && Edge.RoutingHint.Leave == Direction), Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A straight-chain proposal never discards a contradictory explicit entry direction.</summary>
    [Test]
    public void ConflictingTerminalHintsAreNotRewrittenForAChain()
    {
        var Fixture = Create([("A", 0, 0), ("B", 0, 2), ("C", 0, 4)], [("A", "B"), ("B", "C")], false);
        foreach (var Edge in Fixture.Contract.Edges) Edge.RoutingHint = new()
            { Leave = LayoutHintDirection.North, Enter = LayoutHintDirection.North, IsActive = true };
        var Before = Fixture.Grid.Capture();
        Assert.That(DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "B" }).Where(Option => Option.Key.StartsWith("hint-chain|", StringComparison.Ordinal)), Is.Empty);
        Assert.That(Fixture.Contract.Edges.All(Edge => Edge.RoutingHint!.IsActive
            && Edge.RoutingHint.Enter == LayoutHintDirection.North), Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A proposed complete chain cannot share a cell with an unrelated owner.</summary>
    [Test]
    public void OccupiedJointTargetIsRejectedBeforeApplication()
    {
        var Fixture = Create([("A", 0, 0), ("B", 2, 2), ("C", 4, 4), ("X", 2, 0)], [("A", "B"), ("B", "C")], false);
        var Before = Fixture.Grid.Capture();
        var Options = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "B" }).Where(Option => Option.Key.StartsWith("chain|", StringComparison.Ordinal)).ToArray();
        Assert.That(Options, Is.Not.Empty);
        Assert.That(Options.Any(Option => Option.Assignment.After.Regions["B"].StartColumn == 0), Is.False);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A locked anchor may remain fixed while neighbours move, but no proposal may move that anchor.</summary>
    [Test]
    public void JointProposalsKeepHintLockedRegionsExact()
    {
        var Fixture = Create([("A", 0, 0), ("B", 2, 2), ("C", 4, 4)], [("A", "B"), ("B", "C")], false);
        Fixture.Nodes[0].PlacementHintLocked = true;
        var Before = Fixture.Grid.Capture();
        var Options = DiagramGridLayout.CellJointRouteAssignments(Fixture.Contract, Fixture.Nodes, false,
            new HashSet<string> { "B" }).ToArray();
        Assert.That(Options, Is.Not.Empty);
        Assert.That(Options.All(Option => Option.Assignment.After.Regions["A"] == Before.Regions["A"]), Is.True);
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Builds a cell-only fixture without deriving occupancy from pixels or changing production switches.</summary>
    /// <param name="Declarations">Node identities and whole-cell flow/cross starts.</param>
    /// <param name="Links">Logical visible links.</param>
    /// <param name="Horizontal">Whether to transpose rows and columns.</param>
    /// <returns>Original topology, authoritative nodes and shared grid.</returns>
    private static (DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid Grid) Create(
        (string Id, int Row, int Column)[] Declarations, (string Source, string Target)[] Links, bool Horizontal)
    {
        var Contract = new DiagramContract(); var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var Item in Declarations)
        {
            Contract.NodesMutable.Add(new() { Id = Item.Id });
            Assert.That(Grid.TryPlace(Item.Id, Horizontal ? new(Item.Column, Item.Row, 1, 1) : new(Item.Row, Item.Column, 1, 1)), Is.True);
            var Node = new DiagramGridLayout.GridNode { Id = Item.Id, Rank = Item.Row }; Node.BindCells(Grid); Nodes.Add(Node);
        }
        foreach (var Link in Links) Contract.EdgesMutable.Add(new() { SourceId = Link.Source, TargetId = Link.Target });
        return (Contract, Nodes, Grid);
    }
}
