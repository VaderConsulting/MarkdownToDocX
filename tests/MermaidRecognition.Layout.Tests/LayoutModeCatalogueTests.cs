using System.Text.RegularExpressions;
using MermaidRecognition.Config;
using MermaidRecognition.Core;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class LayoutModeCatalogueTests
{
    [TestCase("F", 144, DiagramLayoutMode.Grid)]
    [TestCase("F", 145, DiagramLayoutMode.Open)]
    [TestCase("F", 146, DiagramLayoutMode.Grid)]
    [TestCase("F", 147, DiagramLayoutMode.Open)]
    [TestCase("F", 148, DiagramLayoutMode.Grid)]
    [TestCase("F", 149, DiagramLayoutMode.Open)]
    [TestCase("F", 150, DiagramLayoutMode.Grid)]
    [TestCase("A", 143, DiagramLayoutMode.Grid)]
    [TestCase("A", 144, DiagramLayoutMode.Open)]
    [TestCase("A", 145, DiagramLayoutMode.Grid)]
    [TestCase("A", 146, DiagramLayoutMode.Open)]
    [TestCase("A", 147, DiagramLayoutMode.Grid)]
    [TestCase("A", 148, DiagramLayoutMode.Open)]
    [TestCase("A", 149, DiagramLayoutMode.Grid)]
    public void CatalogueScenario_RendersWithItsDeclaredPlacementMode(
        string Catalogue, int Number, DiagramLayoutMode ExpectedMode)
    {
        string Source = ReadScenario(Catalogue, Number);
        if (Catalogue == "F")
        {
            var Contract = MermaidRecognizer.Parse(Source);
            Assert.That(Contract.LayoutMode, Is.EqualTo(ExpectedMode));
            Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out string Svg), Is.True);
            Assert.That(Svg, Does.StartWith("<svg"));
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
            AssertOrthogonal(Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y))));
        }
        else
        {
            EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
            Assert.That(Analysis.Errors, Is.Empty);
            Assert.That(Analysis.Document.LayoutMode, Is.EqualTo(ExpectedMode));
            Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out string Svg), Is.True);
            Assert.That(Svg, Does.StartWith("<svg"));
            var Layout = EnterpriseLayoutEngine.Layout(Analysis.Document);
            AssertOrthogonal(EnterpriseFlowchartEdgeRouter.Route(Analysis.Document, Layout.Nodes)
                .Select(Route => Route.SvgPoints));
        }
    }

    [TestCase("F", 144, 146)]
    [TestCase("A", 143, 145)]
    public void ExplicitGrid_MatchesTheDefaultCatalogueScenario(string Catalogue, int Explicit, int Default)
    {
        Assert.That(Render(Catalogue, Explicit), Is.EqualTo(Render(Catalogue, Default)));
    }

    [TestCase("F", 150, 146)]
    [TestCase("A", 149, 145)]
    public void UnknownMode_WarnsOnceAndFallsBackToTheDefaultGrid(
        string Catalogue, int Unknown, int Default)
    {
        string Source = ReadScenario(Catalogue, Unknown);
        if (Catalogue == "F")
        {
            var Contract = MermaidRecognizer.Parse(Source);
            Assert.That(Contract.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(1));
        }
        else
        {
            var Analysis = EnterpriseValidator.Analyze(Source);
            Assert.That(Analysis.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(1));
        }
        Assert.That(Render(Catalogue, Unknown), Is.EqualTo(Render(Catalogue, Default)));
    }

    private static string Render(string Catalogue, int Number)
    {
        string Source = ReadScenario(Catalogue, Number);
        if (Catalogue == "A") return EnterpriseRenderBridge.RenderEnterpriseSvg(Source);
        Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out string Svg), Is.True);
        return Svg;
    }

    private static void AssertOrthogonal(IEnumerable<IEnumerable<(double X, double Y)>> Routes)
    {
        foreach (var Route in Routes)
        {
            var Points = Route.ToList();
            foreach (var Segment in Points.Zip(Points.Skip(1)))
                Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                    || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True);
        }
    }

    private static string ReadScenario(string Catalogue, int Number)
    {
        string Markdown = File.ReadAllText(FindRepositoryFile(Catalogue == "F" ? "flow.md" : "enterprise.md"));
        Match Section = Regex.Match(Markdown,
            $@"(?ms)^### {Catalogue}{Number}\b(?<body>.*?)(?=^### {Catalogue}\d+\b|\z)");
        Assert.That(Section.Success, Is.True, $"{Catalogue}{Number} heading is missing.");
        Match Fence = Regex.Match(Section.Groups["body"].Value,
            @"(?ms)```mermaid\s*\r?\n(?<source>.*?)\r?\n```");
        Assert.That(Fence.Success, Is.True, $"{Catalogue}{Number} Mermaid fence is missing.");
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
