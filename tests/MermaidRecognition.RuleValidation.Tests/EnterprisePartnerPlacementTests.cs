using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks partner proximity on completed production layouts rather than stopping at the private band planner.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterprisePartnerPlacementTests
{
    /// <summary>Protects the two shared-track pairs exposed by level-row placement on the finished A115 production path.</summary>
    [Test]
    public void A115LevelRowsDoNotRestoreTheDriftSharedTracks()
    {
        var Contract = ReadContract("A115");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        AssertHardGeometry(Contract, Layout);
        foreach (var Pair in new[] {
            ("replication->sydneyData:8", "mqSvc->orderMgr:22"),
            ("perthMq->mqSvc:17", "drSvc->shop:23") })
        {
            var First = Layout.EdgeRoutes.Single(Route => Route.EdgeId == Pair.Item1);
            var Second = Layout.EdgeRoutes.Single(Route => Route.EdgeId == Pair.Item2);
            Assert.That(FlowchartBranchRouteOrder.Intersections(
                First.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                Second.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Overlap, Is.Zero,
                $"{Pair.Item1} x {Pair.Item2}");
        }
    }

    /// <summary>Measures nested-frame clearance separately from ungrouped partner placement on the completed A115 production layout.</summary>
    [Test, Explicit("Set ENTERPRISE_PARTNER_TRACE to a diagnostic directory; every proposed region remains unpublished.")]
    public void MeasureA115MixedBandClearance()
    {
        string Output = Environment.GetEnvironmentVariable("ENTERPRISE_PARTNER_TRACE") ?? throw new InvalidOperationException("Set ENTERPRISE_PARTNER_TRACE.");
        Directory.CreateDirectory(Output);
        var Contract = ReadContract("A115");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Nodes = Layout.CellNodes!.ToList();
        var Grid = Nodes[0].CellNode!.Grid;
        var Before = Grid.Capture();
        string ExactBefore = CellProductionParityTests.Snapshot(Contract, Layout);
        var Measurements = new List<object>();
        foreach (string Id in new[] { "shopImg1", "shopImg2", "shopImg3", "sydneyOrders", "wan", "lbSvc", "containerSvc", "dbSvc", "mqSvc", "drSvc" })
        {
            var Region = Before.Regions[Id];
            var Trials = new List<object>();
            int Left = Before.Regions.Values.Min(Item => Item.StartColumn), Right = Before.Regions.Values.Max(Item => Item.EndColumn);
            foreach (int Column in Enumerable.Range(Left, Right - Left + 1))
            {
                var Changes = new Dictionary<string, FlowchartCellRegion> { [Id] = new(Region.StartRow, Column, Region.RowSpan, Region.ColumnSpan) };
                if (!Grid.TryPrepare(Changes, [], out var Candidate)) continue;
                var Bounds = Nodes.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(Candidate!.Regions[Node.Id], Node.GeometryProvider, Candidate));
                var Frames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Candidate!);
                var Faults = DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, Frames, Bounds);
                Trials.Add(new { Column, Faults });
            }
            Measurements.Add(new { Id, Region.StartColumn, Region.ColumnSpan,
                Group = Contract.Nodes.Single(Node => Node.Id == Id).ParentGroupId, Trials });
        }
        File.WriteAllText(Path.Combine(Output, "A115-clearance.json"), JsonSerializer.Serialize(new
        {
            Measurements, Frames = Layout.ClusterBounds, Regions = Nodes.Select(Node => new { Node.Id, Node.GroupId,
                Node.CellNode!.Region.StartRow, Node.CellNode.Region.StartColumn, Node.CellNode.Region.ColumnSpan }),
            Grid.Configuration.ColumnGap, Grid.Configuration.RowGap, Before.ColumnGaps, Before.RowGaps
        }));
        Assert.That(CellProductionParityTests.Snapshot(Contract, Layout), Is.EqualTo(ExactBefore));
    }

    /// <summary>Protects single-element bands after wrap selection, routing and every repair have completed.</summary>
    /// <param name="Id">Realistic view containing a singleton band and a later timeline or route repair.</param>
    [TestCase("A116"), TestCase("A117")]
    public void SingletonBandsStayWithinOnePartnerColumnAfterProductionRepairs(string Id)
    {
        var Contract = ReadContract(Id);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        AssertHardGeometry(Contract, Layout);
        var Regions = Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region);
        var Layers = LayersOf(Contract);
        var Singles = Layers.GroupBy(Pair => Pair.Value).Where(Band => Band.Count() == 1).Select(Band => Band.Single().Key).ToArray();
        Assert.That(Singles, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (string Node in Singles)
            {
                var Partners = PartnerColumns(Contract, Layers, Regions, Node);
                if (Partners.Length == 0) continue;
                double Median = (Partners[(Partners.Length - 1) / 2] + Partners[Partners.Length / 2]) / 2;
                Assert.That(Math.Abs(Regions[Node].StartColumn - Median), Is.LessThanOrEqualTo(1),
                    $"{Id}/{Node}: column {Regions[Node].StartColumn}, partners' median {Median}");
            }
        });
    }

    /// <summary>Finds stranded members in mixed grouped/ungrouped bands unless actual cross-band partners justify the separation.</summary>
    /// <param name="Id">Realistic mixed-band view.</param>
    [TestCase("A115")]
    public void MixedBandsKeepNeighboursOrAnActualPartnerPull(string Id)
    {
        var Contract = ReadContract(Id);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        AssertHardGeometry(Contract, Layout);
        var Regions = Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region);
        var Layers = LayersOf(Contract);
        var Groups = Contract.Nodes.ToDictionary(Node => Node.Id, Node => Node.ParentGroupId);
        var Mixed = Layers.GroupBy(Pair => Pair.Value).Where(Band => Band.Any(Pair => Groups[Pair.Key] == null)
            && Band.Any(Pair => Groups[Pair.Key] != null)).ToArray();
        Assert.That(Mixed, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var Band in Mixed)
            foreach (var Member in Band)
            {
                var Region = Regions[Member.Key];
                int Gap = Band.Where(Peer => Peer.Key != Member.Key).Min(Peer =>
                    Math.Max(0, Math.Max(Regions[Peer.Key].StartColumn - Region.EndColumn, Region.StartColumn - Regions[Peer.Key].EndColumn)));
                var Partners = PartnerColumns(Contract, Layers, Regions, Member.Key);
                double? Median = Partners.Length == 0 ? null : (Partners[(Partners.Length - 1) / 2] + Partners[Partners.Length / 2]) / 2;
                Assert.That(Gap <= 1 || Median is double Pull && Math.Abs(Region.StartColumn - Pull) <= 1, Is.True,
                    $"{Id}/{Member.Key}: nearest cell-edge gap {Gap}, column {Region.StartColumn}, partner median {Median}");
            }
        });
    }

    /// <summary>Records singleton coordinates after each production stage without changing the layout or candidate order.</summary>
    /// <param name="Id">View whose singleton movement is being diagnosed.</param>
    [TestCase("A116"), TestCase("A117"), Explicit("Set ENTERPRISE_PARTNER_TRACE to a diagnostic directory.")]
    public void TraceSingletonStages(string Id)
    {
        string DirectoryPath = Environment.GetEnvironmentVariable("ENTERPRISE_PARTNER_TRACE") ?? throw new InvalidOperationException("Set ENTERPRISE_PARTNER_TRACE.");
        Directory.CreateDirectory(DirectoryPath);
        string Path = System.IO.Path.Combine(DirectoryPath, Id + "-stages.jsonl");
        File.WriteAllText(Path, "");
        var Contract = ReadContract(Id);
        var Layers = LayersOf(Contract);
        var Singles = Layers.GroupBy(Pair => Pair.Value).Where(Band => Band.Count() == 1).Select(Band => Band.Single().Key).ToArray();
        var Previous = DiagramGridLayout.LayoutStageObserver;
        DiagramGridLayout.LayoutStageObserver = (Stage, Layout) =>
        {
            if (Layout.CellNodes is not { Count: > 0 }) return;
            var Regions = Layout.CellNodes.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region);
            foreach (string Node in Singles)
                File.AppendAllText(Path, JsonSerializer.Serialize(new { Id, Stage, Wrap = Layout.RenderContext.EnterpriseBandWrapWidth,
                    Node, Column = Regions[Node].StartColumn, Row = Regions[Node].StartRow,
                    Partners = PartnerColumns(Contract, Layers, Regions, Node) }) + Environment.NewLine);
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
            File.AppendAllText(Path, JsonSerializer.Serialize(new { Published = ProductionBendRenderPathTests.GeometryHash(Layout),
                Wrap = Layout.RenderContext.EnterpriseBandWrapWidth }) + Environment.NewLine);
        }
        finally { DiagramGridLayout.LayoutStageObserver = Previous; }
    }

    /// <summary>Loads an actual Enterprise catalogue contract used by the production renderer.</summary>
    /// <param name="Id">Catalogue identity.</param>
    /// <returns>Typed contract with unchanged hints and memberships.</returns>
    private static DiagramContract ReadContract(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == Id);
        return EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
    }

    /// <summary>Verifies that production proximity was achieved with legal complete routes, authoritative cells and non-overlapping frames.</summary>
    /// <param name="Contract">Original declared nodes, groups and links.</param>
    /// <param name="Layout">Completed production result.</param>
    private static void AssertHardGeometry(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Layout.EdgeRoutes.Count, Is.EqualTo(Contract.Edges.Count));
            Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count >= 2), Is.True);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(CellGridAudit.Faults(Contract, Layout, Layout.CellNodes!), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
        });
    }

    /// <summary>Maps typed elements to their semantic bands without inferring graph ranks.</summary>
    /// <param name="Contract">Typed production contract.</param>
    /// <returns>Declared layer membership.</returns>
    private static Dictionary<string, EnterpriseLayer> LayersOf(DiagramContract Contract) => Contract.Nodes
        .Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
        .ToDictionary(Node => Node.Id, Node => EnterpriseElementCatalog.Elements[Enum.Parse<EnterpriseElementType>(
            ((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)].Layer);

    /// <summary>Measures actual partners in other bands, resolving group endpoints to their declared members.</summary>
    /// <param name="Contract">Declared relationships and memberships.</param>
    /// <param name="Layers">Semantic bands.</param>
    /// <param name="Regions">Completed authoritative cell regions.</param>
    /// <param name="Id">Element whose pull is measured.</param>
    /// <returns>Sorted starting columns; local relationships never contribute a cross-band pull.</returns>
    private static double[] PartnerColumns(DiagramContract Contract, IReadOnlyDictionary<string, EnterpriseLayer> Layers,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions, string Id) => Contract.Edges
        .Where(Edge => Edge.SourceId == Id || Edge.TargetId == Id).Select(Edge => Edge.SourceId == Id ? Edge.TargetId : Edge.SourceId)
        .Distinct(StringComparer.Ordinal).SelectMany(Partner => Regions.ContainsKey(Partner) ? new[] { Partner }
            : Regions.Keys.Where(Member => DiagramGridLayout.IsInsideGroup(Contract, Member, Partner)))
        .Where(Partner => Layers.TryGetValue(Partner, out var Layer) && Layer != Layers[Id])
        .Select(Partner => (double)Regions[Partner].StartColumn).Order().ToArray();
}
