using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Protects level whole-cell rows and a common row origin when partner medians vary across a wrapped Enterprise band.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseBandRowTests
{
    /// <summary>Uses plateau triggering only for horizontal order; it never introduces another page row, even in a TB view.</summary>
    /// <param name="Direction">Direction applied to other locally layered bands.</param>
    [TestCase("TB"), TestCase("LR")]
    public void PlateauTriggeringOrdersOneUnwrappedRow(string Direction)
    {
        string Source = "enterprise\n direction " + Direction + "\n application-component app\n"
            + string.Join("\n", Enumerable.Range(0, 5).Select(Index => $" plateau p{Index}")) + "\n"
            + string.Join("\n", Enumerable.Range(0, 4).Select(Index => $" p{Index} -triggering-> p{Index + 1}"));
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        var Nodes = Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 }).ToList();
        var Context = new FlowchartRenderContext { UseCellRegions = true, EnterpriseBandWrapWidth = 2 };
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, Direction == "LR", Context);
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        var Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.That(Enumerable.Range(0, 5).Select(Index => Regions["p" + Index].StartRow).Distinct().Count(), Is.EqualTo(1));
        for (int Index = 0; Index < 4; Index++)
            Assert.That(Regions["p" + Index].EndColumn, Is.LessThanOrEqualTo(Regions["p" + (Index + 1)].StartColumn));
    }

    /// <summary>Rejects repair-time diagonal drift but permits a complete row to move atomically within its band.</summary>
    /// <param name="Direction">Declared direction, which never rotates the page bands.</param>
    [TestCase("TB"), TestCase("LR")]
    public void UnhintedRowMovesTogetherOrNotAtAll(string Direction)
    {
        var (Contract, Nodes) = PairBand(Direction, false);
        var Grid = Nodes[0].CellNode!.Grid;
        var Before = Grid.Capture();
        var One = Before.Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
        One["b0"] = FlowchartCellRepair.Translate(One["b0"], -1, 0);
        Assert.That(Grid.TryPrepare(One, [], out _), Is.False);
        Assert.That(Grid.Capture().Regions, Is.EqualTo(Before.Regions));
        var Both = Before.Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
        Both["b0"] = FlowchartCellRepair.Translate(Both["b0"], -1, 0);
        Both["b1"] = FlowchartCellRepair.Translate(Both["b1"], -1, 0);
        Assert.That(Grid.TryPrepare(Both, [], out var After), Is.True);
        Assert.That(Grid.TryCommit(Before, After!), Is.True);
        Assert.That(Nodes.Single(Node => Node.Id == "b0").CellNode!.Region.StartRow,
            Is.EqualTo(Nodes.Single(Node => Node.Id == "b1").CellNode!.Region.StartRow));
    }

    /// <summary>Keeps an active positional hint outside the inferred unhinted row cohort instead of silently locking or deactivating it.</summary>
    /// <param name="Direction">Declared local direction.</param>
    [TestCase("TB"), TestCase("LR")]
    public void ActiveHintIsNotReplacedByAnInferredRowConstraint(string Direction)
    {
        var (Contract, Nodes) = PairBand(Direction, true);
        var Grid = Nodes[0].CellNode!.Grid;
        var Candidate = Grid.Capture().Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
        Candidate["b1"] = FlowchartCellRepair.Translate(Candidate["b1"], -1, 0);
        Assert.That(Grid.CheckRegionConvention(Candidate), Is.True,
            "The existing hint validator, rather than a new inferred row constraint, owns hinted positions.");
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "b1").PlacementHint!.IsActive, Is.True);
    }

    /// <summary>Exempts a hint's unhinted reference peer and transitive dependents together, preserving the existing hint-replay moving unit.</summary>
    [Test]
    public void HintReferenceAndTransitiveDependentKeepTheirExistingMovingUnit()
    {
        const string Source = "enterprise\n business-actor b0\n business-actor b1 right-of b0\n business-actor b2 right-of b1\n"
            + " business-actor other\n application-component app\n b0 -assignment-> app\n b1 -assignment-> app\n b2 -assignment-> app\n other -assignment-> app";
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        var Nodes = Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 }).ToList();
        var Context = new FlowchartRenderContext { UseCellRegions = true };
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, Context);
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        var Grid = Nodes[0].CellNode!.Grid;
        var Candidate = Grid.Capture().Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
        foreach (string Id in new[] { "b0", "b1", "b2" }) Candidate[Id] = FlowchartCellRepair.Translate(Candidate[Id], -1, 0);
        Assert.That(Grid.TryPrepare(Candidate, [], out _), Is.True);
        Assert.That(Contract.Nodes.Where(Node => Node.PlacementHint != null).All(Node => Node.PlacementHint!.IsActive), Is.True);
    }

    /// <summary>Builds two free Business partners above Application on production cell-owned state.</summary>
    /// <param name="Direction">Direction of local relationships.</param>
    /// <param name="Hinted">Whether the second Business element has an active relative hint.</param>
    /// <returns>Declared contract and atomically assigned initial band members.</returns>
    private static (MermaidRecognition.Contract.DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes) PairBand(string Direction, bool Hinted)
    {
        string Source = "enterprise\n direction " + Direction + "\n business-actor b0\n business-actor b1"
            + (Hinted ? " right-of b0" : "") + "\n application-component app\n b0 -assignment-> app\n b1 -assignment-> app";
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        var Nodes = Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 }).ToList();
        var Context = new FlowchartRenderContext { UseCellRegions = true };
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, Direction == "LR", Context);
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        return (Contract, Nodes);
    }

    /// <summary>Wraps n free elements into exactly r level rows without carrying the last row's median column into the next row.</summary>
    /// <param name="Direction">Direction used only for local relationships.</param>
    /// <param name="Count">Number of elements in each of the two partner bands.</param>
    /// <param name="Columns">Explicit whole-cell capacity for this placement fixture.</param>
    /// <param name="ExpectedRows">Required count of distinct page-top coordinates.</param>
    [TestCase("TB", 7, 3, 3), TestCase("LR", 7, 3, 3)]
    [TestCase("TB", 10, 4, 3), TestCase("LR", 10, 4, 3)]
    public void WrappedPartnerBandHasExactlyItsRowsAndResetsTheLeftEdge(string Direction, int Count, int Columns, int ExpectedRows)
    {
        string Source = "enterprise\n direction " + Direction + "\n"
            + string.Join("\n", Enumerable.Range(0, Count).Select(Index =>
                $" business-actor b{Index}\n application-component a{Index}\n b{Index} -assignment-> a{Index}"));
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        var Nodes = Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 }).ToList();
        var Context = new FlowchartRenderContext { UseCellRegions = true, EnterpriseBandWrapWidth = Columns };
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, Direction == "LR", Context);
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        var Grid = Nodes[0].CellNode!.Grid;
        foreach (string Prefix in new[] { "a", "b" })
        {
            var Members = Nodes.Where(Node => Node.Id.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            var Rows = Members.GroupBy(Node => Node.CellNode!.Region.StartRow).OrderBy(Row => Row.Key).ToArray();
            Assert.That(Rows, Has.Length.EqualTo(ExpectedRows), Prefix);
            Assert.That(Members.Select(Node => Grid.Origin(Node.CellNode!.Region).Y).Distinct().Count(), Is.EqualTo(ExpectedRows), Prefix);
            foreach (var Row in Rows)
            {
                Assert.That(Row.Min(Node => Grid.Origin(Node.CellNode!.Region).X), Is.EqualTo(0), Prefix + ": every row starts at the band left edge");
                Assert.That(Row.Select(Node => Grid.Origin(Node.CellNode!.Region).Y).Distinct().Count(), Is.EqualTo(1));
            }
        }
    }
}
