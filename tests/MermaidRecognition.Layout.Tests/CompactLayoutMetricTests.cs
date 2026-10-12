using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CompactLayoutMetricTests
{
    [TestCase("F", 151)]
    [TestCase("F", 152)]
    [TestCase("F", 153)]
    [TestCase("A", 64)]
    [TestCase("A", 104)]
    [TestCase("A", 109)]
    [TestCase("A", 110)]
    [TestCase("A", 114)]
    [TestCase("A", 117)]
    [TestCase("A", 142)]
    [TestCase("A", 168)]
    public void CatalogueDiagram_ReportsCompactLayoutMetrics(string Catalogue, int Number)
    {
        string Source = ReadScenario(Catalogue, Number);
        DiagramContract Contract = Catalogue == "A"
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document)
            : MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        if (Number >= 154)
            Assert.That(Metrics.Score, Is.Not.Null,
                $"A{Number} must remain a scoreable hinted layout: "
                    + string.Join("; ", Metrics.Violations.Select(Failure => Failure.Description)));
        TestContext.Out.WriteLine(
            $"{Catalogue}{Number}: width={Metrics.Width:0} height={Metrics.Height:0} "
            + $"ratio={Metrics.Width / Metrics.Height:0.000} overlaps={Metrics.Overlaps} "
            + $"crossings={Metrics.Crossings} forcedMin={Metrics.ForcedMinimum} empty={Metrics.EmptyShare:P1} "
            + $"score={(Metrics.Score.HasValue ? Metrics.Score.Value.ToString("0.0") : "rejected")}");
    }

    private static readonly int[] PlacementHintReportScenarios =
        [150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164,
            165, 166, 167, 168, 169];

    [TestCaseSource(nameof(PlacementHintReportScenarios))]
    public void PlacementHintScenario_ReportsBriefTableRow(int Number)
    {
        string Source = ReadScenario("A", Number);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        Assert.That(Metrics.Disconnections, Is.Zero,
            $"A{Number} has {Metrics.Disconnections} line endpoints detached from their source or target.");
        TestContext.Out.WriteLine(
            $"| A{Number} | {Metrics.Width:0} x {Metrics.Height:0} | {Metrics.Width / Metrics.Height:0.000} "
            + $"| {Metrics.Overlaps} | {Metrics.Crossings} | {Metrics.Bends} "
            + $"| {Metrics.HintsHonoured}/{Metrics.HintsTotal} | {Metrics.EmptyShare:P1} "
            + $"| {(Metrics.Score.HasValue ? Metrics.Score.Value.ToString("0.0") : "rejected")} |");
    }

    [Test]
    public void A64_IsolatedGroupMembers_PackAcrossRowsAndColumns()
    {
        string Source = ReadScenario("A", 64);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        List<FlowchartLayoutNodeBounds> Members = Layout.NodeBounds.Where(Box => Box.NodeId.StartsWith("a", StringComparison.Ordinal)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(Members.Select(Box => Box.CenterX).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(Members.Select(Box => Box.CenterY).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(FlowchartLayoutScore.Measure(Contract, Layout).Score, Is.Not.Null);
        });
    }

    [Test]
    public void A117_PortSideRepair_RemovesLineRuleRejection()
    {
        string Source = ReadScenario("A", 117);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        Assert.That(Metrics.Score, Is.Not.Null,
            string.Join("; ", Metrics.Violations.Select(Failure => Failure.Kind + ": " + Failure.Description)));
    }

    [Test]
    public void A72_DerivedSplitMerge_UsesStraightAndElbowRoutes()
    {
        FlowchartLayoutResult Layout = LayoutScenario(72, out DiagramContract Contract);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);

        Assert.Multiple(() =>
        {
            Assert.That(Metrics.Disconnections, Is.Zero);
            Assert.That(Metrics.Bends, Is.LessThanOrEqualTo(6));
            Assert.That(Route(Layout, "checkout", "invoicing").Points, Has.Count.EqualTo(2));
            Assert.That(Route(Layout, "pick", "pack").Points, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void A100_OffsetFans_UseAtMostOneCornerPerRelationship()
    {
        FlowchartLayoutResult Layout = LayoutScenario(100, out DiagramContract Contract);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);

        Assert.Multiple(() =>
        {
            Assert.That(Metrics.Disconnections, Is.Zero);
            Assert.That(Metrics.Bends, Is.LessThanOrEqualTo(5));
            Assert.That(Layout.EdgeRoutes, Has.All.Matches<FlowchartLayoutEdgeRoute>(Route => Route.Points.Count <= 3));
        });
    }

    [Test]
    public void A112_JunctionMesh_HasNoDetachedEndsOrLoopingFans()
    {
        FlowchartLayoutResult Layout = LayoutScenario(112, out DiagramContract Contract);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);

        Assert.Multiple(() =>
        {
            Assert.That(Metrics.Disconnections, Is.Zero);
            Assert.That(Metrics.Bends, Is.LessThanOrEqualTo(16));
            Assert.That(Route(Layout, "split2", "pay").Points, Has.Count.EqualTo(2));
        });
    }

    [TestCase(150, "orderSuite", "webShop")]
    [TestCase(151, "placeOrder", "orderSvc")]
    public void PicturedMainSpines_AreStraightAndCentred(int Number, string Source, string Target)
    {
        FlowchartLayoutResult Layout = LayoutScenario(Number, out DiagramContract Contract);
        FlowchartLayoutEdgeRoute Route = CompactLayoutMetricTests.Route(Layout, Source, Target);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        Assert.Multiple(() =>
        {
            Assert.That(Route.Points, Has.Count.EqualTo(2));
            Assert.That(Metrics.Disconnections, Is.Zero);
        });
    }

    [Test]
    public void A54_LabelledReturns_AttachPerpendicularlyAndRemainScoreable()
    {
        FlowchartLayoutResult Layout = LayoutScenario(54, out DiagramContract Contract);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        Assert.Multiple(() =>
        {
            Assert.That(Metrics.Disconnections, Is.Zero);
            Assert.That(Metrics.Score, Is.Not.Null,
                string.Join("; ", Metrics.Violations.Select(Failure => Failure.Description)));
        });
    }

    [TestCase(167, "australia", "trusted")]
    [TestCase(169, "dplhAdmin", "daw")]
    public void HintedGroupEndpoints_HaveFramesAndScoreableRoutes(int Number, string Source, string GroupId)
    {
        FlowchartLayoutResult Layout = LayoutScenario(Number, out DiagramContract Contract);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        FlowchartLayoutNodeBounds Frame = Layout.ClusterBounds.Single(Box => Box.NodeId == GroupId);
        FlowchartLayoutPoint End = Route(Layout, Source, GroupId).Points[^1];
        double Left = Frame.CenterX - Frame.Width / 2.0;
        double Right = Frame.CenterX + Frame.Width / 2.0;
        double Bottom = Frame.CenterY - Frame.Height / 2.0;
        double Top = Frame.CenterY + Frame.Height / 2.0;
        bool OnOutline = (Math.Abs(End.X - Left) < 1.0 || Math.Abs(End.X - Right) < 1.0)
                && End.Y >= Bottom - 1.0 && End.Y <= Top + 1.0
            || (Math.Abs(End.Y - Bottom) < 1.0 || Math.Abs(End.Y - Top) < 1.0)
                && End.X >= Left - 1.0 && End.X <= Right + 1.0;

        Assert.Multiple(() =>
        {
            Assert.That(Metrics.Score, Is.Not.Null,
                string.Join("; ", Metrics.Violations.Select(Failure => Failure.Description)));
            Assert.That(Metrics.Overlaps, Is.Zero);
            Assert.That(Metrics.Disconnections, Is.Zero);
            Assert.That(OnOutline, Is.True, $"{Source} must end on the {GroupId} frame outline.");
        });
    }

    private static FlowchartLayoutResult LayoutScenario(int Number, out DiagramContract Contract)
    {
        Contract = EnterpriseFlowchartContractMapper.Build(
            EnterpriseValidator.Analyze(ReadScenario("A", Number)).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        return Layout;
    }

    private static FlowchartLayoutEdgeRoute Route(FlowchartLayoutResult Layout, string SourceId, string TargetId) =>
        Layout.EdgeRoutes.Single(Route => Route.SourceId == SourceId && Route.TargetId == TargetId);

    [Test]
    public void A110_ForcedMinimum_ComesFromItsCompleteAdjacentRankBlocks()
    {
        string Source = ReadScenario("A", 110);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Assert.That(FlowchartLayoutScore.Measure(Contract, Layout).ForcedMinimum, Is.EqualTo(243));
    }

    private static string ReadScenario(string Catalogue, int Number)
    {
        string Markdown = File.ReadAllText(FindRepositoryFile(Catalogue == "F" ? "flow.md" : "enterprise.md"));
        Match Section = Regex.Match(Markdown, $@"(?ms)^### {Catalogue}{Number}\b(?<body>.*?)(?=^### {Catalogue}\d+\b|\z)");
        Assert.That(Section.Success, Is.True);
        Match Fence = Regex.Match(Section.Groups["body"].Value, @"(?ms)\x60\x60\x60mermaid\s*\r?\n(?<source>.*?)\r?\n\x60\x60\x60");
        Assert.That(Fence.Success, Is.True);
        return Fence.Groups["source"].Value;
    }

    private static string FindRepositoryFile(string FileName)
    {
        DirectoryInfo? Cursor = new(AppContext.BaseDirectory);
        while (Cursor != null)
        {
            string Candidate = Path.Combine(Cursor.FullName, FileName);
            if (File.Exists(Candidate)) return Candidate;
            Cursor = Cursor.Parent;
        }
        throw new FileNotFoundException(FileName);
    }
}
