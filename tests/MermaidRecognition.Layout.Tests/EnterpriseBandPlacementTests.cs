using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Layout.Dagre;
using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks semantic bands and hint conventions using the production cell-owned layout path.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseBandPlacementTests
{
    /// <summary>Unlinked application elements wrap independently of the declared direction and retain whole, level cell rows.</summary>
    /// <param name="Direction">The direction used only by locally linked elements.</param>
    [TestCase("TB"), TestCase("BT"), TestCase("LR"), TestCase("RL")]
    public void FreeElementsUsePartnerPlacementInsteadOfLocalRanks(string Direction)
    {
        string Source = "enterprise\n direction " + Direction + "\n business-actor owner\n"
            + string.Join("\n", Enumerable.Range(0, 6).Select(Index => " application-component a" + Index + "\n owner -assignment-> a" + Index));
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, Direction is "LR" or "RL", new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes, new() { UseCellRegions = true, EnterpriseBandWrapWidth = 4 }), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        FlowchartCellRegion[] Apps = [.. Regions.Where(Pair => Pair.Key.StartsWith("a", StringComparison.Ordinal)).Select(Pair => Pair.Value)];
        Assert.That(Apps.Select(Region => Region.StartRow).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Apps.Max(Region => Region.EndColumn), Is.LessThanOrEqualTo(4));
        Assert.That(Apps.All(Region => Region.StartRow >= Regions["owner"].EndRow), Is.True);
    }

    /// <summary>A single-element band follows its adjacent partner's starting columns instead of the left margin.</summary>
    [Test]
    public void SingleElementBandOverlapsItsPartnersColumns()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze("enterprise\n direction LR\n business-process first\n business-process middle\n business-process last\n application-component app\n first -triggering-> middle\n middle -triggering-> last\n last -serving-> app").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, true, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.That(Regions["last"].StartColumn, Is.GreaterThan(0));
        Assert.That(Regions["app"].StartColumn, Is.EqualTo(Regions["last"].StartColumn));
    }

    /// <summary>Plateaus, deliveries and work packages keep three level rows with exact realisation columns and a middle hub.</summary>
    [Test]
    public void ImplementationTimelineUsesThreeLevelRowsAndAlignedDeliveries()
    {
        string Source = "enterprise\n direction LR\n application-component app\n plateau before\n plateau after\n before -triggering-> after\n before -aggregation-> app\n work-package programme\n"
            + string.Join("\n", Enumerable.Range(0, 3).Select(Index => " work-package w" + Index + "\n deliverable d" + Index
                + "\n w" + Index + " -realization-> d" + Index + "\n programme -composition-> w" + Index))
            + "\n w0 -triggering-> w1\n w1 -triggering-> w2";
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, true, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.That(Regions["before"].StartRow, Is.EqualTo(Regions["after"].StartRow));
        for (int Index = 0; Index < 3; Index++)
        {
            Assert.That(Regions["d" + Index].StartRow, Is.EqualTo(Regions["d0"].StartRow));
            Assert.That(Regions["w" + Index].StartRow, Is.EqualTo(Regions["w0"].StartRow));
            Assert.That(Regions["d" + Index].StartColumn, Is.EqualTo(Regions["w" + Index].StartColumn));
        }
        Assert.That(Regions["before"].EndRow, Is.LessThanOrEqualTo(Regions["d0"].StartRow));
        Assert.That(Regions["d0"].EndRow, Is.LessThanOrEqualTo(Regions["w0"].StartRow));
        Assert.That(Regions["programme"].StartRow, Is.EqualTo(Regions["w0"].StartRow));
        Assert.That(Regions["programme"].EndColumn, Is.EqualTo(Regions["w1"].StartColumn));
    }

    /// <summary>Within-band and skipping-rank relationships enter the same original adjacent-layer crossing counter.</summary>
    /// <param name="LastRow">Adjacent or non-adjacent destination rank.</param>
    [TestCase(1), TestCase(3)]
    public void CompleteGraphCountsAndReducesCrossingsWithinOneBand(int LastRow)
    {
        (IReadOnlyList<string> Order, int Before, int After) Result = DagreLayoutEngine.OrderCompleteBandBlocks(["a", "b", "c", "d"],
            [new("a", "a", 0, 0), new("b", "b", 0, 0), new("c", "c", LastRow, 0), new("d", "d", LastRow, 0)],
            [("a", "d"), ("b", "c")]);
        Assert.That(Result.Before, Is.GreaterThan(0));
        Assert.That(Result.After, Is.Zero);
    }

    /// <summary>A sparse pair of bands starts at the ordinary rank gap, with no artificial empty cell row.</summary>
    [Test]
    public void SparseBandsUseTheNormalRankGapWithoutCapacityGrowth()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze("enterprise\n business-actor actor\n application-component app\n actor -association-> app").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        double Gap = Grid.Origin(Before.Regions["app"]).Y - Grid.Origin(Before.Regions["actor"]).Y - Grid.Measure(Before.Regions["actor"]).Height;
        Assert.That(Gap, Is.EqualTo(Grid.Configuration.RowGap));
        Assert.That(EnterpriseBandPlacement.ReserveRoutingSpace(Contract, Nodes), Is.False);
        Assert.That(Grid.Capture().RowGaps, Is.EqualTo(Before.RowGaps));
    }

    /// <summary>Compares fully routed legal wraps, includes the unwrapped layout, and preserves discarded model ownership exactly.</summary>
    [Test]
    public void WrapSelectionPublishesACandidateNoWorseThanItsRoutedTrials()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze("enterprise\n business-actor a\n business-actor b\n business-actor c\n application-component app\n a -association-> app\n b -association-> app\n c -association-> app").Document!);
        List<(int Width, FlowchartLayoutResult Layout, FlowchartLayoutScore.ComparisonCost Cost, bool Legal, string Snapshot)> Samples = [];
        DiagramGridLayout.BandWrapCandidateObserver = (Width, Layout, Cost, Legal) => Samples.Add((Width, Layout, Cost, Legal, Geometry(Layout)));
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out FlowchartLayoutResult? Layout), Is.True);
            Assert.That(Samples.Any(Sample => Sample.Width == int.MaxValue), Is.True);
            Assert.That(Samples.Count, Is.GreaterThan(1).And.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            (int Width, FlowchartLayoutResult Layout, FlowchartLayoutScore.ComparisonCost Cost, bool Legal, string Snapshot)[] Legal = [.. Samples.Where(Sample => Sample.Legal)];
            Assert.That(Legal, Is.Not.Empty);
            FlowchartLayoutScore.ComparisonCost Minimum = Legal.Select(Sample => Sample.Cost).Min();
            // Confirming the winner reserves its initial corridor capacity and can improve the trial's score.
            Assert.That(FlowchartLayoutScore.Measure(Contract, Layout, false).Cost.CompareTo(Minimum), Is.LessThanOrEqualTo(0));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout), Is.Empty);
            Assert.That(Layout.RenderContext, Is.SameAs(Context));
            foreach ((int Width, FlowchartLayoutResult Layout, FlowchartLayoutScore.ComparisonCost Cost, bool Legal, string Snapshot) Sample in Samples)
            {
                Assert.That(Sample.Layout.EdgeRoutes.All(Route => Route.Points.Count >= 2), Is.True);
                Assert.That(Geometry(Sample.Layout), Is.EqualTo(Sample.Snapshot));
            }
        }
        finally { DiagramGridLayout.BandWrapCandidateObserver = null; }
    }

    /// <summary>Captures authoritative regions, gaps, frames, routes and exact terminal metadata without mutable diagnostics.</summary>
    /// <param name="Layout">An independently owned completed initial-placement candidate.</param>
    /// <returns>Exact comparison signature for discarded candidate ownership.</returns>
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new {
        Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes,
        Regions = Layout.CellNodes![0].CellNode!.Grid.Capture().Regions.Select(Pair => new { Pair.Key,
            Pair.Value.StartRow, Pair.Value.StartColumn, Pair.Value.RowSpan, Pair.Value.ColumnSpan }),
        Layout.CellNodes[0].CellNode!.Grid.Capture().RowGaps, Layout.CellNodes[0].CellNode!.Grid.Capture().ColumnGaps });

    /// <summary>A hint cannot move a Motivation element into the Application band; the ignored hint is reported under 2.21(g).</summary>
    [Test]
    public void HintMovingMotivationOutOfTheTopBandIsIgnoredAndReported()
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze("enterprise\n goal aim right-of app\n application-component app\n app -realization-> aim").Document!;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        PlacementHintContract Hint = Contract.Nodes.Single(Node => Node.Id == "aim").PlacementHint!;
        Assert.That(Hint.IsActive, Is.False);
        Assert.That(Hint.UnhonouredReason, Does.Contain("2.23").And.Contain("2.21(g)"));
        Dictionary<string, FlowchartCellRegion> Regions = Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region);
        Assert.That(Regions["aim"].EndRow, Is.LessThanOrEqualTo(Regions["app"].StartRow));
    }

    /// <summary>LR triggering is ranked within its own band, with an unwrapped plateau row and Motivation in the top band.</summary>
    [Test]
    public void LeftToRightTimelineKeepsBandMembershipAndMotivationAtTheTop()
    {
        string Source = "enterprise\n direction LR\n business-role sponsor\n application-component app\n goal aim\n"
            + " plateau baseline\n plateau transition\n plateau target\n gap first\n gap second\n"
            + " baseline -triggering-> transition\n transition -triggering-> target\n"
            + " first -association-> baseline\n first -association-> transition\n second -association-> transition\n second -association-> target\n"
            + string.Join("\n", Enumerable.Range(0, 8).Select(Index => " work-package w" + Index)) + "\n"
            + string.Join("\n", Enumerable.Range(0, 7).Select(Index => " w" + Index + " -triggering-> w" + (Index + 1)))
            + "\n sponsor -assignment-> w0\n w7 -realization-> app\n target -realization-> aim";
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, true, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Nodes[0].CellNode!.Grid.Capture().Regions;
        Assert.That(Regions["sponsor"].EndRow, Is.LessThanOrEqualTo(Regions["app"].StartRow));
        Assert.That(Regions["app"].EndRow, Is.LessThanOrEqualTo(Enumerable.Range(0, 8).Min(Index => Regions["w" + Index].StartRow)));
        Assert.That(Regions["baseline"].StartRow, Is.EqualTo(Regions["target"].StartRow));
        Assert.That(Regions["baseline"].EndColumn, Is.LessThanOrEqualTo(Regions["first"].StartColumn));
        Assert.That(Regions["first"].EndColumn, Is.LessThanOrEqualTo(Regions["transition"].StartColumn));
        Assert.That(Regions["transition"].EndColumn, Is.LessThanOrEqualTo(Regions["second"].StartColumn));
        Assert.That(Regions["second"].EndColumn, Is.LessThanOrEqualTo(Regions["target"].StartColumn));
        for (int Index = 0; Index < 7; Index++)
        {
            FlowchartCellRegion A = Regions["w" + Index]; FlowchartCellRegion B = Regions["w" + (Index + 1)];
            Assert.That(B.StartRow > A.StartRow || (B.StartRow == A.StartRow && B.StartColumn >= A.EndColumn), Is.True);
        }
        Assert.That(Regions["aim"].EndRow, Is.LessThanOrEqualTo(Regions.Where(Pair => Pair.Key != "aim").Min(Pair => Pair.Value.StartRow)));
    }

    /// <summary>Cross-band tracks receive the measured missing space before any route search, without moving reservations.</summary>
    [Test]
    public void CrowdedBandGapReservesDeclaredTracksAndRepeatedReservationIsIdempotent()
    {
        string Source = "enterprise\n application-component app\n"
            + string.Join("\n", Enumerable.Range(0, 9).Select(Index => " business-actor actor" + Index + "\n actor" + Index + " -association-> app"));
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.HasErrors, Is.False);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Analysis.Document!);
        Assert.That(Contract.Edges.Count, Is.EqualTo(9));
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartCellRegion A = Nodes.Where(Node => Node.Id.StartsWith("actor", StringComparison.Ordinal)).Select(Node => Node.CellNode!.Region).MaxBy(Region => Region.EndRow)!;
        FlowchartCellRegion B = Nodes.Single(Node => Node.Id == "app").CellNode!.Region;
        double Available() => Grid.Origin(B).Y - Grid.Origin(A).Y - Grid.Measure(A).Height;
        double Required = (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) + (8 * FlowchartLayoutRules.TrackSpacing);
        bool NeedsGrowth = Available() < Required;
        Assert.That(EnterpriseBandPlacement.ReserveRoutingSpace(Contract, Nodes), Is.EqualTo(NeedsGrowth));
        Assert.That(Grid.Capture().Regions, Is.EqualTo(Before.Regions));
        Assert.That(Available(), Is.GreaterThanOrEqualTo(Required));
        FlowchartLogicalGrid.Snapshot Reserved = Grid.Capture();
        Assert.That(EnterpriseBandPlacement.ReserveRoutingSpace(Contract, Nodes), Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Reserved));
    }

    /// <summary>A skipped middle band receives an interior free column while endpoint bands keep their cells.</summary>
    [Test]
    public void SkippingBandReservesAnInteriorCorridorWithoutMovingEndpoints()
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze("enterprise\n business-actor actor\n application-component app\n node host\n actor -association-> host").Document!);
        List<DiagramGridLayout.GridNode> Nodes = [.. Contract.Nodes.Select((Node, Index) => new DiagramGridLayout.GridNode { Id = Node.Id, Rank = Index, Column = 0 })];
        FlowchartCellInitialPlacement.Bind(Nodes, 150, 90, false, new() { UseCellRegions = true });
        Assert.That(EnterpriseBandPlacement.Apply(Contract, Nodes), Is.True);
        FlowchartLogicalGrid.Snapshot Before = Nodes[0].CellNode!.Grid.Capture();
        Assert.That(EnterpriseBandPlacement.ReserveSkippingBandCorridors(Contract, Nodes), Is.True);
        FlowchartLogicalGrid.Snapshot After = Nodes[0].CellNode!.Grid.Capture();
        Assert.That(After.Regions["actor"], Is.EqualTo(Before.Regions["actor"]));
        Assert.That(After.Regions["host"], Is.EqualTo(Before.Regions["host"]));
        Assert.That(After.Regions["app"].StartColumn, Is.GreaterThan(After.Regions["actor"].StartColumn));
        Assert.That(After.Regions["app"].StartRow, Is.EqualTo(Before.Regions["app"].StartRow));
    }

    /// <summary>Unrelated multi-band groupings remain disjoint rectangles with independent aspect sequences.</summary>
    [Test]
    public void UnrelatedFramesKeepLocalAspectOrderAcrossBusinessAndApplication()
    {
        const string Source = """
            enterprise
              group one "First group"
              group two "Second group"
              business-object bo1 "Business object" in one
              business-process bp1 "Business process" in one
              business-actor ba1 "Business actor" in one
              data-object do1 "Data" in one
              application-service as1 "Service" in one
              application-component ac1 "Component" in one
              business-object bo2 "Business object" in two
              business-process bp2 "Business process" in two
              business-actor ba2 "Business actor" in two
              data-object do2 "Data" in two
              application-service as2 "Service" in two
              application-component ac2 "Component" in two
              bp1 -access-> bo1
              ba1 -assignment-> bp1
              ac1 -realization-> as1
              bp2 -access-> bo2
              ba2 -assignment-> bp2
              ac2 -realization-> as2
            """;
        FlowchartLayoutResult Layout = LayoutSource(Source);
        FlowchartLayoutNodeBounds[] Frames = [.. Layout.ClusterBounds.Where(Frame => Frame.NodeId is "one" or "two")];
        Assert.That(Frames, Has.Length.EqualTo(2), "Each group is published as exactly one rectangle.");
        Assert.That(Math.Abs(Frames[0].CenterX - Frames[1].CenterX), Is.GreaterThanOrEqualTo((Frames[0].Width + Frames[1].Width) / 2));
        Dictionary<string, FlowchartCellRegion> Cells = Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region);
        foreach (string Suffix in new[] { "1", "2" })
        {
            Assert.That(Cells["bo" + Suffix].EndColumn, Is.LessThanOrEqualTo(Cells["bp" + Suffix].StartColumn));
            Assert.That(Cells["bp" + Suffix].EndColumn, Is.LessThanOrEqualTo(Cells["ba" + Suffix].StartColumn));
            Assert.That(Cells["do" + Suffix].EndColumn, Is.LessThanOrEqualTo(Cells["as" + Suffix].StartColumn));
            Assert.That(Cells["as" + Suffix].EndColumn, Is.LessThanOrEqualTo(Cells["ac" + Suffix].StartColumn));
        }
        Assert.That(new[] { "bo1", "bp1", "ba1", "bo2", "bp2", "ba2" }.Max(Id => Cells[Id].EndRow),
            Is.LessThanOrEqualTo(new[] { "do1", "as1", "ac1", "do2", "as2", "ac2" }.Min(Id => Cells[Id].StartRow)));
    }

    /// <summary>A relative hint cannot align nodes from different layer bands onto the same row.</summary>
    [Test]
    public void HintMovingAnElementIntoAnotherBandIsIgnoredAndReported()
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze("enterprise\n business-actor actor\n application-component app right-of actor\n actor -association-> app").Document!;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out _), Is.True);
        PlacementHintContract Hint = Contract.Nodes.Single(Node => Node.Id == "app").PlacementHint!;
        Assert.That(Hint.IsActive, Is.False);
        Assert.That(Hint.UnhonouredReason, Does.Contain("2.23").And.Contain("2.21(g)"));
    }

    /// <summary>A group-level hint cannot invert the fixed Business and Application band order.</summary>
    [Test]
    public void HintReorderingBandsIsIgnoredAndReported()
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze("enterprise\n group business below application\n group application\n business-actor actor in business\n application-component app in application\n actor -association-> app").Document!;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out _), Is.True);
        PlacementHintContract Hint = Contract.Groups.Single(Group => Group.Id == "business").PlacementHint!;
        Assert.That(Hint.IsActive, Is.False);
        Assert.That(Hint.UnhonouredReason, Does.Contain("2.23").And.Contain("2.21(g)"));
    }

    /// <summary>Builds and routes a validated source under production cell ownership, retaining the authoritative regions for assertions.</summary>
    /// <param name="Source">Enterprise source including the header.</param>
    /// <returns>Finished native layout.</returns>
    private static FlowchartLayoutResult LayoutSource(string Source)
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document!;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        return Layout;
    }
}
