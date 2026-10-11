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
    public void CatalogueDiagram_ReportsCompactLayoutMetrics(string Catalogue, int Number)
    {
        string Source = ReadScenario(Catalogue, Number);
        DiagramContract Contract = Catalogue == "A"
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document)
            : MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        TestContext.Out.WriteLine(
            $"{Catalogue}{Number}: width={Metrics.Width:0} height={Metrics.Height:0} "
            + $"ratio={Metrics.Width / Metrics.Height:0.000} overlaps={Metrics.Overlaps} "
            + $"crossings={Metrics.Crossings} forcedMin={Metrics.ForcedMinimum} empty={Metrics.EmptyShare:P1} "
            + $"score={(Metrics.Score.HasValue ? Metrics.Score.Value.ToString("0.0") : "rejected")}");
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
        Assert.That(FlowchartLayoutScore.Measure(Contract, Layout).Score, Is.Not.Null);
    }

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
