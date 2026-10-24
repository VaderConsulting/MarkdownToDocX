using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MDConverter;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Verifies that the bend catalogue and the DOCX rasterizer observe identical production geometry.</summary>
[TestFixture, NonParallelizable]
public sealed class ProductionBendRenderPathTests
{
    /// <summary>Profiles the actual A115 DOCX PNG call independently of an earlier catalogue layout, retaining the normal invocation watchdog.</summary>
    [Test, Explicit("Writes immediate production-render stage evidence for the A115 timeout investigation.")]
    public void ProfileA115ActualDocxRasterStages()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        string Destination = Path.Combine(FindRoot(), "out", "markup-review", "overnight-a115-raster.profile");
        File.WriteAllText(Destination, "START|" + DateTimeOffset.UtcNow + Environment.NewLine);
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool Previous);
        var PreviousTiming = DiagramGridLayout.LayoutStageTimingObserver;
        var PreviousPublished = DiagramGridLayout.PublishedLayoutObserver;
        DiagramGridLayout.LayoutStageTimingObserver = (Stage, Milliseconds) =>
            File.AppendAllText(Destination, Stage + "|" + Milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine);
        DiagramGridLayout.PublishedLayoutObserver = (_, Layout) =>
            File.AppendAllText(Destination, "PUBLISHED|" + GeometryHash(Layout) + Environment.NewLine);
        try
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
            var Clock = System.Diagnostics.Stopwatch.StartNew();
            byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            File.AppendAllText(Destination, "RETURN|" + Clock.Elapsed.TotalSeconds + Environment.NewLine);
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            File.WriteAllBytes(Path.Combine(FindRoot(), "out", "markup-review", "band-views", "A115-after-v2.png"), Png!);
        }
        finally
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, Previous);
            DiagramGridLayout.LayoutStageTimingObserver = PreviousTiming;
            DiagramGridLayout.PublishedLayoutObserver = PreviousPublished;
        }
    }

    /// <summary>Compares full node, frame and route hashes and writes the requested untouched DOCX-resolution PNG.</summary>
    /// <param name="Id">Enterprise view rendered by the same production entry point used by DOCX export.</param>
    [TestCase("A113"), TestCase("A115"), TestCase("A116"), TestCase("A117")]
    public void BendCatalogueGeometryMatchesDocxRasterizer(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Catalogue), Is.True);
        string Expected = GeometryHash(Catalogue);
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool Previous);
        string? Actual = null;
        DiagramGridLayout.PublishedLayoutObserver = (PublishedContract, Layout) =>
        {
            if (PublishedContract.Nodes.Count == Contract.Nodes.Count) Actual = GeometryHash(Layout);
        };
        try
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
            var Timer = System.Diagnostics.Stopwatch.StartNew();
            byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            Assert.That(Actual, Is.EqualTo(Expected), "The checked catalogue must be the exact completed DOCX geometry.");
            string Root = FindRoot();
            string Destination = Path.Combine(Root, "out", "markup-review", "band-views", Id + "-after-v2.png");
            Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
            File.WriteAllBytes(Destination, Png!);
            TestContext.Out.WriteLine($"PRODUCTION_BEND_PATH|{Id}|catalogue={Expected}|docx={Actual}|png={Destination}|seconds={Timer.Elapsed.TotalSeconds:F3}");
        }
        finally
        {
            DiagramGridLayout.PublishedLayoutObserver = null;
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, Previous);
        }
    }

    /// <summary>Hashes drawn model geometry only, excluding diagnostics, timings and object identities.</summary>
    /// <param name="Layout">Completed production model.</param>
    /// <returns>Stable SHA-256 for all node bounds, frames, allocated terminals and routes.</returns>
    internal static string GeometryHash(FlowchartLayoutResult Layout)
    {
        string Signature = JsonSerializer.Serialize(new { Nodes = Layout.NodeBounds.Select(Box => new
            { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }), Frames = Layout.ClusterBounds.Select(Box => new
            { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }), Layout.EdgeRoutes });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Signature)));
    }

    /// <summary>Finds the repository that owns the source catalogues and requested image output.</summary>
    /// <returns>Existing repository root.</returns>
    private static string FindRoot()
    {
        for (DirectoryInfo? Directory = new(AppContext.BaseDirectory); Directory != null; Directory = Directory.Parent)
            if (File.Exists(Path.Combine(Directory.FullName, "enterprise.md"))
                && File.Exists(Path.Combine(Directory.FullName, "src", "MDConverter", "MDConverter.csproj"))) return Directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate the catalogue repository root.");
    }
}
