using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CatalogueScreenshotRegressionTests
{
    [TestCase("F137", """
        flowchart TB
        Poll --> Handle{Handled?}
        Handle -->|Yes| Ack
        Handle -->|No| backoff
        backoff --> Poll
        """)]
    [TestCase("F143", """
        flowchart LR
        Start([Start])
        split{Route?}
        Fast[Fast lane]
        slow
        Bulk[Bulk lane]
        Join[Merge]
        Done([Done])
        Start --> split
        split -->|small| Fast
        split -->|normal| slow
        split -->|large| Bulk
        Fast --> Join
        slow --> Join
        Bulk --> Join
        Join --> Done
        """)]
    public void DecisionAndLoopRoutes_UseOrthogonalVerticesWithoutHooks(string Id, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True,
            Id + " must layout");
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.That(Findings, Is.Empty, Id + ": " + string.Join("; ", Findings.Select(F => F.Description)));
    }

    [Test]
    public void F149_ReadRoute_ExitsApiBottomNotUnderDatabase()
    {
        const string Source = """
            ---
            config:
              layout: open
            ---
            flowchart LR
            subgraph Front["Front end"]
              UI[Web UI]
              Cache[(Cache)]
            end
            subgraph Back["Back end"]
              Api[API]
              Db[(Database)]
            end
            Audit[Audit log]
            UI -->|request| Api
            Api -->|read| Cache
            Api -->|write| Db
            Db -->|change| Audit
            UI -.->|event| Audit
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutEdgeRoute Read = Layout.EdgeRoutes.Single(Route => Route.SourceId == "Api" && Route.TargetId == "Cache");
        Assert.That(Read.ExitSide, Is.AnyOf("bottom", "left"));
        Assert.That(Read.ExitSide, Is.Not.EqualTo("right"));
        FlowchartLayoutNodeBounds Db = Layout.NodeBounds.Single(Box => Box.NodeId == "Db");
        FlowchartLayoutNodeBounds Audit = Layout.NodeBounds.Single(Box => Box.NodeId == "Audit");
        Assert.That(Math.Abs(Db.CenterY - Audit.CenterY), Is.LessThan(40.0), "Database aligns with Audit for a straight change edge");
    }

    [TestCase("F151")]
    [TestCase("F152")]
    [TestCase("F153")]
    public void WrapStress_DiagramFitsWordWidthWithReadableLabels(string Id)
    {
        string Markdown = ExtractScenario(Id);
        DiagramContract Contract = MermaidRecognizer.Parse(Markdown);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True, Id);
        FlowchartDocxEmbedMetrics.EmbedReport Report = FlowchartDocxEmbedMetrics.Measure(Layout);
        TestContext.Out.WriteLine($"{Id}|scale={Report.WidthScale:0.###}|px={Report.WidthPixels:0}x{Report.HeightPixels:0}|readable={Report.LabelReadable}");
        Assert.That(Report.WidthScale, Is.GreaterThan(0.35), Id + " Word scale too aggressive");
        Assert.That(Report.LabelReadable, Is.True, Id + " node labels shrink below readable size at page width");
        Assert.That(Report.WidthPixels, Is.LessThanOrEqualTo(FlowchartLayoutRules.DiagramUsableWidthPx * 1.5),
            Id + " layout span should stay near page usable width");
    }

    private static string ExtractScenario(string Id)
    {
        string FlowPath = Path.Combine(FindRepoRoot(), "flow.md");
        string Markdown = File.ReadAllText(FlowPath);
        string Pattern = $"### {Id}";
        int Start = Markdown.IndexOf(Pattern, StringComparison.Ordinal);
        Assert.That(Start, Is.GreaterThanOrEqualTo(0), "Missing " + Id);
        int Fence = Markdown.IndexOf("```mermaid", Start, StringComparison.Ordinal);
        int End = Markdown.IndexOf("```", Fence + 10, StringComparison.Ordinal);
        return Markdown[(Fence + 10)..End].Trim();
    }

    private static string FindRepoRoot()
    {
        string? Current = AppContext.BaseDirectory;
        while (Current != null)
        {
            if (File.Exists(Path.Combine(Current, "flow.md")))
            {
                return Current;
            }

            Current = Directory.GetParent(Current)?.FullName;
        }

        throw new DirectoryNotFoundException("flow.md not found");
    }
}
