using MDConverter;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Renders the verified flowchart margin improvement without creating or modifying a DOCX; other kinds have separate native render tests.</summary>
[TestFixture, NonParallelizable]
public sealed class OvernightRouteRenderTests : ProductionCatalogueTestFixture
{
    /// <summary>Uses the native painter and DOCX SVG rasterizer for the reference, and the full DOCX PNG entry point for the current result.</summary>
    /// <param name="Id">Unmodified catalogue source whose margin repair was independently accepted.</param>
    [TestCase("F74")]
    [Explicit("Writes PNG review artifacts only when explicitly requested.")]
    public void CaptureBeforeAndAfterMarginRepair(string Id)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        string Root = FindRoot();
        string DirectoryPath = Path.Combine(Root, "out", "markup-review", "overnight-routes");
        Directory.CreateDirectory(DirectoryPath);
        DiagramContract Contract = MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true, DisableFinishedBendRepairsForTests = true }, out FlowchartLayoutResult? Before), Is.True);
        string Svg = FlowchartLegendRenderer.Apply(Contract, FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract, Before));
        byte[]? BeforePng = MermaidDiagramExport.TryRasterizeSvg(Svg);
        Assert.That(BeforePng, Is.Not.Null.And.Not.Empty);
        File.WriteAllText(Path.Combine(DirectoryPath, Id + "-before.svg"), Svg);
        File.WriteAllBytes(Path.Combine(DirectoryPath, Id + "-before.png"), BeforePng!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? After), Is.True);
        string Expected = ProductionBendRenderPathTests.GeometryHash(After);
        string? Published = null;
        Action<DiagramContract, FlowchartLayoutResult>? Previous = DiagramGridLayout.PublishedLayoutObserver;
        DiagramGridLayout.PublishedLayoutObserver = (Source, Layout) =>
        {
            Previous?.Invoke(Source, Layout);
            Published = ProductionBendRenderPathTests.GeometryHash(Layout);
        };
        try
        {
            byte[]? AfterPng = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            Assert.That(AfterPng, Is.Not.Null.And.Not.Empty);
            Assert.That(Published, Is.EqualTo(Expected));
            File.WriteAllBytes(Path.Combine(DirectoryPath, Id + "-after.png"), AfterPng!);
        }
        finally { DiagramGridLayout.PublishedLayoutObserver = Previous; }
        TestContext.Out.WriteLine($"OVERNIGHT_RENDER|{Id}|before={ProductionBendRenderPathTests.GeometryHash(Before)}|after={Expected}|directory={DirectoryPath}");
    }

    /// <summary>Finds the repository owning the review artifacts and original catalogues.</summary>
    /// <returns>The existing repository root.</returns>
    private static string FindRoot()
    {
        for (DirectoryInfo? Current = new(AppContext.BaseDirectory); Current != null; Current = Current.Parent)
        {
            if (File.Exists(Path.Combine(Current.FullName, "enterprise.md")))
            {
                return Current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Cannot locate the source catalogues.");
    }
}
