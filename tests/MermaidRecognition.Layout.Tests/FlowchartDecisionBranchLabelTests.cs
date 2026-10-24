using System.Globalization;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartDecisionBranchLabelTests
{
    private const string YesNoTdSource = """
        flowchart TD
        A["Incoming messages"] --> B{"Matches filter?"}
        B -- Yes --> C["Forward"]
        B -- No --> D["Discard / divert"]
        """;

    [TestCase("Y", true)]
    [TestCase("y", true)]
    [TestCase(" Yes ", true)]
    [TestCase("YES", true)]
    [TestCase("yes", true)]
    [TestCase("N", false)]
    [TestCase("maybe", false)]
    public void IsYesBranchLabel_MatchesRule311(string Label, bool Expected)
    {
        Assert.That(FlowchartDecisionBranchLabelHelper.IsYesBranchLabel(Label), Is.EqualTo(Expected));
    }

    [TestCase("N", true)]
    [TestCase("n", true)]
    [TestCase(" No ", true)]
    [TestCase("NO", true)]
    [TestCase("no", true)]
    [TestCase("Y", false)]
    [TestCase("maybe", false)]
    public void IsNoBranchLabel_MatchesRule311(string Label, bool Expected)
    {
        Assert.That(FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Label), Is.EqualTo(Expected));
    }

    [Test]
    public void TryLayout_TdDecision_YAndNLabels_MatchYesNoPortPlacement()
    {
        (double YesX, double YesY, double NoX, double NoY) Baseline = ResolveBranchSourcePorts(YesNoTdSource);
        (double AltYesX, double AltYesY, double AltNoX, double AltNoY) = ResolveBranchSourcePorts("""
            flowchart TD
            A["Incoming messages"] --> B{"Matches filter?"}
            B -- Y --> C["Forward"]
            B -- N --> D["Discard / divert"]
            """);

        Assert.That(AltYesX, Is.EqualTo(Baseline.YesX).Within(0.5));
        Assert.That(AltYesY, Is.EqualTo(Baseline.YesY).Within(0.5));
        Assert.That(AltNoX, Is.EqualTo(Baseline.NoX).Within(0.5));
        Assert.That(AltNoY, Is.EqualTo(Baseline.NoY).Within(0.5));
    }

    [Test]
    public void TryLayout_TdDecision_NoBranchLeavesEastVertexNotSlopedSe()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(YesNoTdSource);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds Hub = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");
        double RightVertexX = Hub.CenterX + (Hub.Width / 2.0);
        double RightVertexY = Hub.CenterY;
        double LowerRightSlopeX = Hub.CenterX + (Hub.Width / 4.0);
        double LowerRightSlopeY = Hub.CenterY - (Hub.Height / 4.0);

        Assert.That(NoRoute.Points[0].X, Is.EqualTo(RightVertexX).Within(1.0));
        Assert.That(NoRoute.Points[0].Y, Is.EqualTo(RightVertexY).Within(1.0));
        Assert.That(Math.Abs(NoRoute.Points[0].X - LowerRightSlopeX), Is.GreaterThan(4.0));
        Assert.That(Math.Abs(NoRoute.Points[0].Y - LowerRightSlopeY), Is.GreaterThan(4.0));
    }

    [Test]
    public void TryLayout_TdDecision_UpperCaseYesNoLabels_MatchCanonicalYesNoPortPlacement()
    {
        (double YesX, double YesY, double NoX, double NoY) Baseline = ResolveBranchSourcePorts(YesNoTdSource);
        (double AltYesX, double AltYesY, double AltNoX, double AltNoY) = ResolveBranchSourcePorts("""
            flowchart TD
            A["Incoming messages"] --> B{"Matches filter?"}
            B -- YES --> C["Forward"]
            B -- NO --> D["Discard / divert"]
            """);

        Assert.That(AltYesX, Is.EqualTo(Baseline.YesX).Within(0.5));
        Assert.That(AltYesY, Is.EqualTo(Baseline.YesY).Within(0.5));
        Assert.That(AltNoX, Is.EqualTo(Baseline.NoX).Within(0.5));
        Assert.That(AltNoY, Is.EqualTo(Baseline.NoY).Within(0.5));
    }

    private static (double YesX, double YesY, double NoX, double NoY) ResolveBranchSourcePorts(string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");
        return (YesRoute.Points[0].X, YesRoute.Points[0].Y, NoRoute.Points[0].X, NoRoute.Points[0].Y);
    }

    [Test]
    [Explicit("Writes NO_EXIT_OUT csv for catalogue F* No branch attach points.")]
    public void Capture_FlowCatalogue_NoBranchAttachPoints()
    {
        string? outputPath = Environment.GetEnvironmentVariable("NO_EXIT_OUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Assert.Ignore("NO_EXIT_OUT not set");
        }

        string flowPath = Path.Combine(FindRepoRoot(), "flow.md");
        string text = File.ReadAllText(flowPath);
        List<string> lines = [];
        foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram diagram in MermaidMarkdownFenceExtractor.ExtractFlowcharts(text))
        {
            string id = diagram.DiagramId;
            if (!id.StartsWith("F", StringComparison.Ordinal))
            {
                continue;
            }

            string source = diagram.Source;
            DiagramContract contract = MermaidRecognizer.Parse(source);
            if (!contract.Edges.Any(Edge => FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Edge.Label)))
            {
                continue;
            }
            if (!FlowchartSugiyamaLayoutEngine.TryLayout(contract, out FlowchartLayoutResult layout))
            {
                continue;
            }

            foreach (EdgeContract edge in contract.Edges.Where(Edge => FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Edge.Label)))
            {
                FlowchartLayoutEdgeRoute route = FindRoute(layout, edge.SourceId, edge.TargetId);
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{id},{edge.SourceId}->{edge.TargetId},{route.Points[0].X:R},{route.Points[0].Y:R}"));
            }
        }

        File.WriteAllLines(outputPath, lines);
        Assert.Pass($"Wrote {lines.Count} rows to {outputPath}");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "flow.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find flow.md");
    }

    private static FlowchartLayoutEdgeRoute FindRoute(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.Equals(Route.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Route.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Route;
            }
        }

        throw new AssertionException($"Missing route {SourceId}->{TargetId}");
    }
}
