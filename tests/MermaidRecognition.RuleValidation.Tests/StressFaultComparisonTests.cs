using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MDConverter;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Captures exact stress fault identities against two engines using identical production inputs and checkers.</summary>
[TestFixture, NonParallelizable]
public sealed class StressFaultComparisonTests
{
    /// <summary>Measures existing track repairs on private A110 copies, retaining the original production model exactly.</summary>
    [Test, Explicit("Set STRESS_FAULT_REVIEW_OUTPUT; bounded existing repairs only, no production activation.")]
    public void ProbeA110ExistingTrackRepairs()
    {
        string Output = Environment.GetEnvironmentVariable("STRESS_FAULT_REVIEW_OUTPUT")!;
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A110");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        string Before = CellProductionParityTests.Snapshot(Contract, Layout);
        List<object> Trials = [];
        foreach (string Kind in new[] { "terminal", "internal", "separate" })
        {
            FlowchartLayoutResult Candidate = FlowchartLayoutAlternatives.Copy(Layout);
            List<DiagramGridLayout.GridNode> Nodes = [.. Layout.CellNodes!];
            if (Kind == "terminal")
            {
                FlowchartTerminalTrackRepair.Apply(Contract, Candidate, Nodes,
                Candidate.NodeBounds.Concat(Candidate.ClusterBounds).ToDictionary(Box => Box.NodeId));
            }
            else if (Kind == "internal")
            {
                FlowchartDistinctTrackRepair.Apply(Contract, Candidate, Nodes);
            }
            else
            {
                DiagramGridLayout.SeparateTracks(Candidate, Nodes, Contract);
            }

            Trials.Add(new { Kind, Bends = Candidate.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
                Score = FlowchartLayoutScore.Measure(Contract, Candidate, false),
                Findings = FlowchartRouteRuleChecker.Check(Contract, Candidate),
                OffSlots = RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Candidate) });
        }
        File.WriteAllText(Path.Combine(Output, "A110-existing-track-repairs.json"), JsonSerializer.Serialize(Trials));
        Assert.That(CellProductionParityTests.Snapshot(Contract, Layout), Is.EqualTo(Before));
    }

    /// <summary>Lists the nine failed stress cases plus A107; F59 is already one of the nine.</summary>
    /// <returns>Deduplicated cases carrying the canonical stress watchdog metadata.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (string Id in new[] { "F59", "F152", "F153", "A104", "A107", "A110", "A111", "A159", "A160", "A163" })
        {
            yield return CatalogueScenarioPolicy.Apply(new TestCaseData(Id), Id);
        }
    }

    /// <summary>Enumerates every stress case with an available completed baseline; A142 remains its recorded 180-second timeout.</summary>
    /// <returns>Canonical stress identities and their unchanged watchdog metadata.</returns>
    public static IEnumerable<TestCaseData> CompletedStressCases()
    {
        foreach (string Id in CatalogueScenarioPolicy.StressScenarios.Where(Id => Id != "A142").Order())
        {
            yield return CatalogueScenarioPolicy.Apply(new TestCaseData(Id), Id);
        }
    }

    /// <summary>Captures the complete non-timeout stress set for source-change geometry comparisons.</summary>
    /// <param name="Id">Canonical stress identity.</param>
    [TestCaseSource(nameof(CompletedStressCases)), Explicit("Measurement only; A142 keeps its recorded stress timeout.")]
    public void CaptureCompletedStress(string Id) => CaptureProductionFaults(Id);

    /// <summary>Lists every potentially affected Enterprise view, excluding only the already recorded A142 stress timeout.</summary>
    /// <returns>Multi-layer inputs with canonical normal or stress watchdog metadata.</returns>
    public static IEnumerable<TestCaseData> MultiBandCases()
    {
        foreach (RouteRuleCatalogueAuditTests.Scenario? Scenario in RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Where(Scenario => Scenario.Enterprise && Scenario.Id != "A142"))
        {
            DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
            if (Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
                .Select(Node => EnterpriseElementCatalog.Elements[Enum.Parse<EnterpriseElementType>(
                    ((MermaidRecognition.Contract.RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)].Layer).Distinct().Count() > 1)
            {
                yield return CatalogueScenarioPolicy.Apply(new TestCaseData(Scenario.Id), Scenario.Id);
            }
        }
    }

    /// <summary>Measures all diagrams which can enter initial band capacity sizing through the production path.</summary>
    /// <param name="Id">Multi-layer Enterprise catalogue identity.</param>
    [TestCaseSource(nameof(MultiBandCases)), Explicit("Measurement only; set STRESS_FAULT_REVIEW_OUTPUT.")]
    public void CaptureMultiBand(string Id) => CaptureProductionFaults(Id);

    /// <summary>Lists the two requested production renders under their unchanged stress deadlines.</summary>
    /// <returns>A107 and A110 render cases.</returns>
    public static IEnumerable<TestCaseData> CorridorRenderCases()
    {
        foreach (string Id in new[] { "A107", "A110" })
        {
            yield return CatalogueScenarioPolicy.Apply(new TestCaseData(Id), Id);
        }
    }

    /// <summary>Renders the current converter PNG path and verifies its exact geometry against the captured production layout.</summary>
    /// <param name="Id">Requested catalogue view.</param>
    [TestCaseSource(nameof(CorridorRenderCases)), Explicit("Set STRESS_FAULT_REVIEW_OUTPUT; writes PNGs only, never DOCX.")]
    public void RenderProductionCorridorView(string Id)
    {
        string Output = Environment.GetEnvironmentVariable("STRESS_FAULT_REVIEW_OUTPUT")!;
        using JsonDocument Capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, Id + ".json")));
        string Expected = Capture.RootElement.GetProperty("GeometryHash").GetString()!;
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool PreviousCells);
        Action<DiagramContract, FlowchartLayoutResult>? PreviousObserver = DiagramGridLayout.PublishedLayoutObserver;
        string? Actual = null;
        try
        {
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
            DiagramGridLayout.PublishedLayoutObserver = (_, Layout) => Actual = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(GeometryJson(Layout))));
            byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            Assert.That(Actual, Is.EqualTo(Expected));
            string Destination = Path.Combine(Output, Id + "-after.png");
            File.WriteAllBytes(Destination, Png!);
            TestContext.Out.WriteLine("CORRIDOR_RENDER|" + Id + "|" + Path.GetFullPath(Destination));
        }
        finally
        {
            DiagramGridLayout.PublishedLayoutObserver = PreviousObserver;
            AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, PreviousCells);
        }
    }

    /// <summary>Measures all rule and off-slot identities without changing or suppressing any finding.</summary>
    /// <param name="Id">Stress catalogue identity selected for comparison.</param>
    [TestCaseSource(nameof(Cases)), Explicit("Set STRESS_FAULT_REVIEW_OUTPUT; measurement only, not a passing gate assertion.")]
    public void CaptureProductionFaults(string Id)
    {
        string Output = Environment.GetEnvironmentVariable("STRESS_FAULT_REVIEW_OUTPUT")
            ?? throw new InvalidOperationException("Set STRESS_FAULT_REVIEW_OUTPUT.");
        Directory.CreateDirectory(Output);
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognition.Core.MermaidRecognizer.Parse(Scenario.Source);
        Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        Action<int, FlowchartLayoutResult, FlowchartLayoutScore.ComparisonCost, bool>? PreviousObserver = DiagramGridLayout.BandWrapCandidateObserver;
        Action<string, FlowchartLayoutResult>? PreviousStageObserver = DiagramGridLayout.LayoutStageObserver;
        Action<string>? PreviousPortObserver = DiagramGridLayout.CorridorPortOrderObserver;
        FlowchartLayoutResult Layout;
        try
        {
            if (Environment.GetEnvironmentVariable("STRESS_FAULT_PORT_TRACE") == "1")
            {
                DiagramGridLayout.CorridorPortOrderObserver = Record => File.AppendAllText(Path.Combine(Output, Id + "-ports.txt"), Record + "\n");
            }

            if (Environment.GetEnvironmentVariable("STRESS_FAULT_STAGE_TRACE") == "1")
            {
                DiagramGridLayout.LayoutStageObserver = (Stage, Candidate) =>
                {
                    if (!Candidate.RenderContext.BandSpaceDiagnostics.Any(Item => Item.StartsWith("INITIAL_CORRIDOR_CAPACITY|", StringComparison.Ordinal)))
                    {
                        return;
                    }

                    File.AppendAllText(Path.Combine(Output, Id + "-stages.jsonl"), JsonSerializer.Serialize(new {
                        Stage, Findings = FlowchartRouteRuleChecker.Check(Contract, Candidate),
                        Geometry = JsonSerializer.Deserialize<JsonElement>(GeometryJson(Candidate)) }) + "\n");
                };
            }

            if (Environment.GetEnvironmentVariable("STRESS_FAULT_WRAP_TRACE") == "1")
            {
                DiagramGridLayout.BandWrapCandidateObserver = (Width, Candidate, Cost, Legal) =>
                    File.AppendAllText(Path.Combine(Output, Id + "-wraps.jsonl"), JsonSerializer.Serialize(new {
                        Width, Legal, Cost.Bends, Score = (double)Cost,
                        Findings = FlowchartRouteRuleChecker.Check(Contract, Candidate),
                        OffSlots = RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Candidate) }) + "\n");
            }

            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out Layout), Is.True);
        }
        finally
        {
            DiagramGridLayout.BandWrapCandidateObserver = PreviousObserver;
            DiagramGridLayout.LayoutStageObserver = PreviousStageObserver;
            DiagramGridLayout.CorridorPortOrderObserver = PreviousPortObserver;
        }
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout, false);
        string Geometry = GeometryJson(Layout);
        File.WriteAllText(Path.Combine(Output, Id + ".json"), JsonSerializer.Serialize(new { Id,
            Seconds = Clock.Elapsed.TotalSeconds, UseCellRegions = Layout.RenderContext.UseCellRegions,
            Bends = Metrics.Bends, Crossings = BandRowDriftMeasurements.PhysicalCrossings(Layout), Aspect = Metrics.Width / Metrics.Height,
            Metrics.Width, Metrics.Height, Metrics.HintsHonoured, Metrics.HintsTotal,
            GeometryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Geometry))), Findings,
            Regions = Layout.CellNodes?.ToDictionary(Node => Node.Id, Node => new { Node.CellNode!.Region.StartRow,
                Node.CellNode.Region.StartColumn, Node.CellNode.Region.RowSpan, Node.CellNode.Region.ColumnSpan }),
            ColumnGaps = Layout.CellNodes?.FirstOrDefault()?.CellNode?.Grid.Capture().ColumnGaps,
            RowGaps = Layout.CellNodes?.FirstOrDefault()?.CellNode?.Grid.Capture().RowGaps,
            Layout.RenderContext.BandSpaceDiagnostics,
            OffSlots = RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout).Order().ToArray(),
            Layout.RenderContext.LayoutStageViolations, Geometry = JsonSerializer.Deserialize<JsonElement>(Geometry) }));
    }

    /// <summary>Serializes exactly the drawn node, frame and route geometry in the same order for layout and PNG comparisons.</summary>
    /// <param name="Layout">Completed production layout.</param>
    /// <returns>Stable geometry-only JSON, excluding diagnostics and timings.</returns>
    private static string GeometryJson(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new {
            Nodes = Layout.NodeBounds.OrderBy(Box => Box.NodeId).Select(Box => new { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }),
            Frames = Layout.ClusterBounds.OrderBy(Box => Box.NodeId).Select(Box => new { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }),
            Routes = Layout.EdgeRoutes.OrderBy(Route => Route.EdgeId).Select(Route => new { Route.EdgeId, Route.SourceId, Route.TargetId,
                Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY,
                Points = Route.Points.Select(Point => new { Point.X, Point.Y }) }) });
}
