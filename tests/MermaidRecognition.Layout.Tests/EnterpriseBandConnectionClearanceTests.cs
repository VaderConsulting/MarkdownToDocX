using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks initial band column selection without moving rows, dropping hints or publishing partial occupancy.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseBandConnectionClearanceTests
{
    /// <summary>A blocked column is retained and diagnosed when its only blocker is controlled by an honoured placement hint.</summary>
    [Test]
    public void HintedBlockerKeepsItsRegionAndRecordsTheConflict()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\n business-process first\n business-process middle below first\n business-process last"
            + "\n application-component app\n first -triggering-> middle\n middle -triggering-> last"
            + "\n first -triggering-> last\n last -serving-> app").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true };
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.Multiple(() =>
        {
            Assert.That(Contract.Nodes.Single(Node => Node.Id == "middle").PlacementHint!.IsActive, Is.True);
            Assert.That(Regions["middle"].StartColumn, Is.EqualTo(Regions["first"].StartColumn));
            Assert.That(Context.BandSpaceDiagnostics, Has.Some.Contains("blocker=middle|hintControlled=True"));
        });
    }

    /// <summary>Clearing a same-band shortcut must not undo a singleton's completed cross-band partner alignment.</summary>
    [Test]
    public void PartnerAlignedBlockerKeepsItsColumn()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\n business-process first\n business-process middle\n business-process last"
            + "\n application-component app\n first -triggering-> middle\n middle -triggering-> last"
            + "\n first -triggering-> last\n middle -serving-> app").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true };
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, Context), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.Multiple(() =>
        {
            Assert.That(Regions["middle"].StartColumn, Is.EqualTo(Regions["app"].StartColumn));
            Assert.That(Context.BandSpaceDiagnostics, Has.Some.Contains("blocker=middle"));
        });
    }

    /// <summary>A free blocker moves to another column before ports or routes are constructed.</summary>
    /// <param name="Direction">The local vertical ranking direction.</param>
    [TestCase("TB"), TestCase("BT")]
    public void DirectConnectionKeepsItsColumnClear(string Direction)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\n direction " + Direction + "\n business-process first\n business-process middle\n business-process last"
            + "\n application-component app\n first -triggering-> middle\n middle -triggering-> last"
            + "\n first -triggering-> last\n last -serving-> app").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.Multiple(() =>
        {
            Assert.That(Regions["first"].StartColumn, Is.EqualTo(Regions["last"].StartColumn));
            Assert.That(Regions["middle"].StartColumn, Is.Not.EqualTo(Regions["first"].StartColumn));
            Assert.That(new[] { "first", "middle", "last" }.Max(Id => Regions[Id].EndRow), Is.LessThanOrEqualTo(Regions["app"].StartRow));
            Assert.That(Regions.Values.SelectMany((Region, Index) => Regions.Values.Skip(Index + 1)
                .Select(Other => Region.Overlaps(Other))), Has.None.True);
        });
    }
}
