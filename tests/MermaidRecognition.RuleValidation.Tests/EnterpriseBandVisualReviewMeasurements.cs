using System.Diagnostics;
using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Collects read-only crossing-location evidence for the requested Enterprise visual review.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseBandVisualReviewMeasurements
{
    /// <summary>Replays both routing searches on A167's captured final bounds to identify the missing candidate without another layout run.</summary>
    [Test, Explicit("Set BAND_VISUAL_REVIEW_OUTPUT to the completed after capture.")]
    public void ProbeCapturedA167HintSearch()
    {
        using JsonDocument Capture = File.ReadLines(Environment.GetEnvironmentVariable("BAND_VISUAL_REVIEW_OUTPUT")!)
            .Select(Line => JsonDocument.Parse(Line)).Single(Document => Document.RootElement.GetProperty("Id").GetString() == "A167");
        JsonElement Root = Capture.RootElement;
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Root.GetProperty("NodeBounds").Deserialize<FlowchartLayoutNodeBounds[]>()!
            .Concat(Root.GetProperty("ClusterBounds").Deserialize<FlowchartLayoutNodeBounds[]>()!).ToDictionary(Box => Box.NodeId);
        FlowchartLayoutEdgeRoute Route = Root.GetProperty("EdgeRoutes").Deserialize<FlowchartLayoutEdgeRoute[]>(new JsonSerializerOptions
            { PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate })!
            .Single(Route => Route.SourceId == "vendorOp" && Route.TargetId == "devCustodian");
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A167");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        Nodes.TryGetValue(Route.SourceId, out NodeContract? Source); Nodes.TryGetValue(Route.TargetId, out NodeContract? Target);
        FlowchartLayoutPoint Start = Route.Points[0]; FlowchartLayoutPoint End = Route.Points[^1];
        using IDisposable Active = FlowchartRenderContext.Activate(new() { UseCellRegions = true });
        List<(double X, double Y)> Template = DiagramGridRouteBuilder.BuildRoute(Start.X, Start.Y, Bounds[Route.SourceId], Source?.Shape, Route.ExitSide,
            End.X, End.Y, Bounds[Route.TargetId], Target?.Shape, Route.EntrySide, Bounds, Route.SourceId, Route.TargetId,
            Contract, MermaidRecognition.Kinds.Flowchart.FlowchartDirection.TopToBottom, false, RequireLegal: true);
        List<(double X, double Y)> Fallback = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Route, true);
        TestContext.Out.WriteLine("A167_HINT_SEARCH|" + JsonSerializer.Serialize(new { TemplateCount = Template.Count,
            Fallback = Fallback.Select(Point => new { Point.X, Point.Y }),
            Bands = FlowchartFrameTitleBandHelper.Build(Contract, Bounds),
            Legal = FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Fallback) }));
    }

    /// <summary>Measures the explicitly requested catalogue views without changing routing limits, checks or candidates.</summary>
    [Test, Explicit("Set BAND_VISUAL_REVIEW_OUTPUT to a local JSONL evidence path.")]
    public void CaptureRequestedViews()
    {
        string Output = Environment.GetEnvironmentVariable("BAND_VISUAL_REVIEW_OUTPUT")
            ?? throw new InvalidOperationException("Set BAND_VISUAL_REVIEW_OUTPUT.");
        HashSet<string> Requested = (Environment.GetEnvironmentVariable("BAND_VISUAL_REVIEW_IDS")
            ?? "A113,A116,A135,A26,A100,A133,A115,A67,A68,A46,A167,A98,A47,A72,A37")
            .Split(',').ToHashSet(StringComparer.Ordinal);
        File.WriteAllText(Output, "");
        foreach (RouteRuleCatalogueAuditTests.Scenario? Scenario in RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Where(Case => Requested.Contains(Case.Id)))
        {
            DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
            Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True, Scenario.Id);
            FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
            var Locations = EnterpriseCrossingLocations.Measure(Contract, Layout);
            int Crossings = 0;
            for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
            {
                for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
                {
                    Crossings += FlowchartBranchRouteOrder.Intersections(
                    Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
                }
            }

            File.AppendAllText(Output, JsonSerializer.Serialize(new { Scenario.Id, Seconds = Clock.Elapsed.TotalSeconds,
                Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Locations,
                Crossings, Aspect = Score.Width / Score.Height, Score.EmptyShare, Score.OptimizationScore,
                PortBendsRemoved = Layout.RenderContext.BendReassignmentRemoved, Findings = FlowchartRouteRuleChecker.Check(Contract, Layout),
                Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes,
                Layout.RenderContext.LayoutStageViolations }) + Environment.NewLine);
            TestContext.Out.WriteLine("BAND_VISUAL_REVIEW|" + Scenario.Id + "|" + Clock.Elapsed.TotalSeconds);
        }
    }
}
