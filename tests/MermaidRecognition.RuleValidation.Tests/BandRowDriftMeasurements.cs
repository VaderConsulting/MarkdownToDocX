using System.Text.Json;
using System.Globalization;
using System.Xml.Linq;
using MDConverter;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;
using MermaidRecognition.Contract;
using System.Diagnostics;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Records original local ranks, planned rows and final page coordinates for the Enterprise diagonal-drift investigation.</summary>
[TestFixture, NonParallelizable]
public sealed class BandRowDriftMeasurements
{
    /// <summary>Renders each shared-column review view once through the actual DOCX PNG dispatcher and checks its published geometry against the routed capture.</summary>
    /// <param name="Id">Review view whose completed capture is available.</param>
    [TestCase("A113"), TestCase("A115"), TestCase("A116"), TestCase("A117")]
    [Explicit("Set BAND_DRIFT_OUTPUT to the shared-column capture directory; no DOCX is written.")]
    public void RenderSharedColumnView(string Id)
    {
        string Output = Environment.GetEnvironmentVariable("BAND_DRIFT_OUTPUT") ?? throw new InvalidOperationException("Set BAND_DRIFT_OUTPUT.");
        using JsonDocument Reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, Id + ".json")));
        string Expected = Reference.RootElement.GetProperty("Hash").GetString()!;
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool Previous);
        Action<DiagramContract, FlowchartLayoutResult>? PreviousObserver = DiagramGridLayout.PublishedLayoutObserver;
        string? Actual = null;
        DiagramGridLayout.PublishedLayoutObserver = (_, Layout) => Actual = ProductionBendRenderPathTests.GeometryHash(Layout);
        try
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
            Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
            byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            Assert.That(Actual, Is.EqualTo(Expected));
            string Version = Environment.GetEnvironmentVariable("BAND_REVIEW_VERSION") ?? "v2";
            string Destination = Path.GetFullPath(Path.Combine(Output, "..", "..", "band-views", Id + "-after-" + Version + ".png"));
            File.WriteAllBytes(Destination, Png!);
            TestContext.Out.WriteLine($"SHARED_COLUMN_RENDER|{Id}|{Clock.Elapsed.TotalSeconds:F3}|{Actual}|{Destination}");
        }
        finally
        {
            DiagramGridLayout.PublishedLayoutObserver = PreviousObserver;
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, Previous);
        }
    }

    /// <summary>Measures the existing track-separation repair on an independent level-row layout without retaining a route or changing a checker.</summary>
    [Test, Explicit("Diagnostic candidate only; does not enable another production repair pass.")]
    public void MeasureExistingTrackSeparationOnA115()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        string Before = CellProductionParityTests.Snapshot(Contract, Layout);
        FlowchartLayoutResult Candidate = FlowchartLayoutAlternatives.Copy(Layout);
        DiagramGridLayout.SeparateTracks(Candidate, [.. Layout.CellNodes!], Contract);
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> OriginalFindings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> CandidateFindings = FlowchartRouteRuleChecker.Check(Contract, Candidate);
        Dictionary<string, FlowchartLayoutEdgeRoute> OriginalRoutes = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId);
        FlowchartLayoutEdgeRoute[] Changed = [.. Candidate.EdgeRoutes.Where(Route => JsonSerializer.Serialize(Route) != JsonSerializer.Serialize(OriginalRoutes[Route.EdgeId]))];
        TestContext.Out.WriteLine("ROW_TRACK_TRIAL|" + JsonSerializer.Serialize(new
        {
            Changed = Changed.Select(Route => Route.EdgeId),
            BeforeBends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
            AfterBends = Candidate.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
            BeforeCrossings = PhysicalCrossings(Layout), AfterCrossings = PhysicalCrossings(Candidate),
            BeforeShared = OriginalFindings.Count(Finding => Finding.Rule == "5.1"),
            AfterShared = CandidateFindings.Count(Finding => Finding.Rule == "5.1"),
            NewFindings = CandidateFindings.Where(Finding => !OriginalFindings.Any(Old => Old.Rule == Finding.Rule && Old.Subject == Finding.Subject)),
            LengthAdded = Candidate.EdgeRoutes.Sum(Route => Route.Points.Zip(Route.Points.Skip(1)).Sum(double ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair) => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y)))
                - Layout.EdgeRoutes.Sum(Route => Route.Points.Zip(Route.Points.Skip(1)).Sum(double ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair) => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y)))
        }));
        Assert.That(CellProductionParityTests.Snapshot(Contract, Layout), Is.EqualTo(Before));
    }

    /// <summary>Counts physical orthogonal crossings using the same route-pair geometry as the review captures.</summary>
    /// <param name="Layout">Complete independent drawn layout.</param>
    /// <returns>Sum of physical crossings over distinct line pairs.</returns>
    internal static int PhysicalCrossings(FlowchartLayoutResult Layout)
    {
        int Count = 0;
        for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
        {
            for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
            {
                Count += FlowchartBranchRouteOrder.Intersections(
                Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
            }
        }

        return Count;
    }

    /// <summary>Writes the actual native Enterprise SVG and a smaller viewport preview when the full DOCX-resolution PNG exceeds the image viewer's size limit.</summary>
    [Test, Explicit("Set BAND_DRIFT_OUTPUT to the completed capture directory; the full DOCX PNG is left untouched.")]
    public void CaptureA115NativeSvgPreview()
    {
        string Output = Environment.GetEnvironmentVariable("BAND_DRIFT_OUTPUT") ?? throw new InvalidOperationException("Set BAND_DRIFT_OUTPUT.");
        using JsonDocument Reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "A115.json")));
        string Expected = Reference.RootElement.GetProperty("Hash").GetString()!;
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool Previous);
        Action<DiagramContract, FlowchartLayoutResult>? PreviousObserver = DiagramGridLayout.PublishedLayoutObserver;
        string? Actual = null;
        DiagramGridLayout.PublishedLayoutObserver = (_, Layout) => Actual = ProductionBendRenderPathTests.GeometryHash(Layout);
        try
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
            Assert.That(NativeDiagramRenderBridge.TryRenderSvg(Scenario.Source, out string Svg), Is.True);
            Assert.That(Actual, Is.EqualTo(Expected));
            string Destination = Path.GetFullPath(Path.Combine(Output, "..", "..", "band-views"));
            string Version = Environment.GetEnvironmentVariable("BAND_REVIEW_VERSION") ?? "v2";
            File.WriteAllText(Path.Combine(Destination, "A115-after-" + Version + ".svg"), Svg);
            XDocument Document = XDocument.Parse(Svg);
            XElement Root = Document.Root!;
            var ViewBox = Root.Attribute("viewBox")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(Value => double.Parse(Value, CultureInfo.InvariantCulture)).ToArray();
            // This changes only the display viewport. Every original node and
            // route coordinate stays in the native viewBox and the full PNG is
            // retained at precisely the DOCX export resolution.
            Root.SetAttributeValue("width", (ViewBox[2] / 4).ToString(CultureInfo.InvariantCulture));
            Root.SetAttributeValue("height", (ViewBox[3] / 4).ToString(CultureInfo.InvariantCulture));
            byte[]? Png = MermaidDiagramExport.TryRasterizeSvg(Document.ToString());
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            File.WriteAllBytes(Path.Combine(Destination, "A115-after-" + Version + "-preview.png"), Png!);
        }
        finally
        {
            DiagramGridLayout.PublishedLayoutObserver = PreviousObserver;
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, Previous);
        }
    }

    /// <summary>Captures a complete production view and the planning evidence for its selected routed wrap.</summary>
    /// <param name="Id">Review scenario, including the unchanged single-layer control.</param>
    [TestCase("A113"), TestCase("A114"), TestCase("A115"), TestCase("A116"), TestCase("A117")]
    [Explicit("Set BAND_DRIFT_OUTPUT to a diagnostic directory; no baseline is changed.")]
    public void Capture(string Id)
    {
        string DirectoryPath = Environment.GetEnvironmentVariable("BAND_DRIFT_OUTPUT")
            ?? throw new InvalidOperationException("Set BAND_DRIFT_OUTPUT.");
        Directory.CreateDirectory(DirectoryPath);
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        List<EnterpriseBandPlacement.ElementPlacement> Records = [];
        Action<EnterpriseBandPlacement.ElementPlacement>? Previous = EnterpriseBandPlacement.PlacementObserver;
        EnterpriseBandPlacement.PlacementObserver = Records.Add;
        FlowchartLayoutResult Layout;
        Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out Layout!), Is.True);
        }
        finally { EnterpriseBandPlacement.PlacementObserver = Previous; }
        int Wrap = Layout.RenderContext.EnterpriseBandWrapWidth ?? int.MaxValue;
        Dictionary<string, EnterpriseBandPlacement.ElementPlacement> Selected = Records.Where(Record => Record.Wrap == Wrap && Record.Stage == "final-band-plan")
            .GroupBy(Record => Record.Id).ToDictionary(Group => Group.Key, Group => Group.Last());
        FlowchartLogicalGrid Grid = Layout.CellNodes![0].CellNode!.Grid;
        var Final = Layout.CellNodes.Select(Node =>
        {
            FlowchartCellRegion Region = Node.CellNode!.Region;
            (double X, double Y) Origin = Grid.Origin(Region);
            EnterpriseBandPlacement.ElementPlacement? Plan = Selected.GetValueOrDefault(Node.Id);
            var Starts = Layout.CellNodes.Where(Peer => Selected.GetValueOrDefault(Peer.Id)?.Band == Plan?.Band)
                .Select(Peer => Peer.CellNode!.Region.StartRow).Distinct().Order().ToArray();
            return new { Node.Id, Plan?.Band, Plan?.Rank, Plan?.Order, Row = Array.IndexOf(Starts, Region.StartRow),
                Region.StartRow, Region.StartColumn, Region.RowSpan, Region.ColumnSpan, X = Origin.X, Y = Origin.Y,
                PlannedRow = Plan?.Row, PlannedColumn = Plan?.Region.StartColumn };
        }).ToArray();
        FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
        int Crossings = PhysicalCrossings(Layout);
        File.WriteAllText(Path.Combine(DirectoryPath, Id + ".json"), JsonSerializer.Serialize(new
        {
            Id, Seconds = Clock.Elapsed.TotalSeconds, Wrap, Hash = ProductionBendRenderPathTests.GeometryHash(Layout),
            Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Crossings,
            Score.Width, Score.Height, Aspect = Score.Width / Score.Height, Score.EmptyShare, Score.OptimizationScore, Final, Planning = Records,
            Findings = FlowchartRouteRuleChecker.Check(Contract, Layout), Layout.RenderContext.LayoutStageViolations,
            Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes
        }));
        TestContext.Out.WriteLine($"BAND_DRIFT|{Id}|{Clock.Elapsed.TotalSeconds:F3}|bends={Score.Bends}|crossings={Crossings}");
    }
}
