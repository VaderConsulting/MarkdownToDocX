using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Routing-intent diagnostic layer tests for torture-test Markdown fixtures and ruleset validation.
/// </summary>
[TestFixture]
public sealed class FlowchartRoutingIntentTests
{
    private const string TORTURE_MARKDOWN_FILE = "RoutingIntent/routing-torture-test.md";

    private const string RULESET_FILE = "RoutingIntent/ruleset.json";

    [Test]
    public void RenderRouteClassMap_MatchesFullIntentAnalysis()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start --> Decide{Ready?}
            Decide -->|yes| Left[Left path]
            Decide -->|no| Right[Right path]
            Left --> Join[Join]
            Right --> Join
            Join --> End[Done]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract, Layout, Direction, "test");

        Dictionary<string, string> RouteClasses = FlowchartEdgeRoutingIntentAnalyzer
            .BuildRouteClassNamesByEdgeKey(Contract, Layout, Direction);

        foreach (FlowchartEdgeRoutingIntentRecord Edge in Intent.Edges)
        {
            string Expected = Edge.RouteClass switch
            {
                FlowchartRoutingIntentRouteClass.CentreSpine => "CentreSpine",
                FlowchartRoutingIntentRouteClass.ShortAdjacent => "ShortAdjacent",
                FlowchartRoutingIntentRouteClass.LocalDecisionBranch => "LocalDecisionBranch",
                FlowchartRoutingIntentRouteClass.MultiInputMerge => "FanIn",
                FlowchartRoutingIntentRouteClass.MergeBack => "MergeBack",
                FlowchartRoutingIntentRouteClass.LongExceptionBypass or FlowchartRoutingIntentRouteClass.SkipBypass => "LongBypass",
                FlowchartRoutingIntentRouteClass.ReturnBackward => "CycleOrReturn",
                FlowchartRoutingIntentRouteClass.CrossLane or FlowchartRoutingIntentRouteClass.ObstacleAvoidance => "CrossLane",
                _ => "Unknown"
            };
            Assert.That(RouteClasses[Edge.EdgeId], Is.EqualTo(Expected), Edge.EdgeId);
        }
    }

    private const string F58_FLOWCHART = """
        flowchart TD
        Start([Start]) --> Ingest[Ingest file]
        Ingest --> Validate{Valid markdown?}
        Validate -->|No| Reject[Reject with error]
        Validate -->|Yes| Parse[Parse blocks]
        Parse --> HasMermaid{Mermaid fence?}
        HasMermaid -->|Yes| Render[Render SVG]
        HasMermaid -->|No| Word[Build DOCX]
        Render --> Word
        DoArchive{Archive?}
        Word --> DoArchive
        DoArchive -->|Yes| Persist
        DoArchive -->|No| Done
        Reject --> Done
        Persist[(Optional archive)] --> Done
        """;

    [Test]
    public void Analyze_RegressionFlow001_ProducesRoutingIntentForEveryVisibleEdge()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract,
            Layout,
            Direction,
            "RegressionFlow001");

        Assert.That(Intent.DiagramId, Is.EqualTo("RegressionFlow001"));
        Assert.That(Intent.Direction, Is.EqualTo("TD"));
        Assert.That(Intent.Edges.Count, Is.EqualTo(13));

        foreach (FlowchartEdgeRoutingIntentRecord Edge in Intent.Edges)
        {
            Assert.That(Edge.EdgeId, Does.Match(@"^E\d{3}$"));
            Assert.That(Edge.SourceNodeId, Is.Not.Empty);
            Assert.That(Edge.TargetNodeId, Is.Not.Empty);
            Assert.That(Edge.RouteClass, Is.Not.EqualTo(FlowchartRoutingIntentRouteClass.Unknown));
            Assert.That(Edge.ExpectedBehaviour, Is.Not.Empty);
            Assert.That(Edge.InvalidBehaviours, Is.Not.Empty);
        }
    }

    [Test]
    public void ExtractFlowcharts_TortureFixture_FindsAllDiagrams()
    {
        string Markdown = ReadFixture(TORTURE_MARKDOWN_FILE);
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams =
            MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);

        Assert.That(Diagrams.Count, Is.EqualTo(14));
        Assert.That(Diagrams[0].DiagramId, Is.EqualTo("RegressionFlow001"));
        Assert.That(Diagrams[1].DiagramId, Is.EqualTo("CentreSpineLinear"));
        Assert.That(Diagrams[13].DiagramId, Is.EqualTo("DirectionRL"));
    }

    [Test]
    public void ProcessMarkdownFixture_TortureTest_AllDiagramsParseAndLayout()
    {
        string Markdown = ReadFixture(TORTURE_MARKDOWN_FILE);
        List<FlowchartRoutingIntentPipelineResult> Results =
            FlowchartRoutingIntentPipeline.ProcessMarkdownFixture(Markdown, Ruleset: null);

        Assert.That(Results.Count, Is.EqualTo(14));
        foreach (FlowchartRoutingIntentPipelineResult Result in Results)
        {
            Assert.That(Result.ParserSucceeded, Is.True, Result.DiagramId + " parser");
            Assert.That(Result.LayoutSucceeded, Is.True, Result.DiagramId + " layout");
            Assert.That(Result.Intent, Is.Not.Null, Result.DiagramId + " intent");
            Assert.That(Result.Intent!.Edges.Count, Is.GreaterThan(0), Result.DiagramId + " edge count");
        }
    }

    [Test]
    public void DeserializeRuleset_LoadsDiagramIntentFixtures()
    {
        string RulesetJson = ReadFixture(RULESET_FILE);
        FlowchartRoutingIntentRuleset Ruleset =
            FlowchartRoutingIntentJsonSerializer.DeserializeRuleset(RulesetJson);

        Assert.That(Ruleset.ResolveDiagramExpectations().Count, Is.GreaterThanOrEqualTo(14));
        Assert.That(
            Ruleset.ResolveDiagramExpectations().Any(
                Diagram => string.Equals(Diagram.DiagramId, "RegressionFlow001", StringComparison.Ordinal)),
            Is.True,
            "Combined ruleset should expose RegressionFlow001 via DiagramIntentFixtures.");
    }

    [Test]
    public void ValidateTortureFixture_AgainstRuleset_AllDiagramsPassIntentValidation()
    {
        string Markdown = ReadFixture(TORTURE_MARKDOWN_FILE);
        string RulesetJson = ReadFixture(RULESET_FILE);
        FlowchartRoutingIntentRuleset Ruleset =
            FlowchartRoutingIntentJsonSerializer.DeserializeRuleset(RulesetJson);

        List<FlowchartRoutingIntentPipelineResult> Results =
            FlowchartRoutingIntentPipeline.ProcessMarkdownFixture(Markdown, Ruleset);

        StringBuilder FailureReport = new StringBuilder();
        foreach (FlowchartRoutingIntentPipelineResult Result in Results)
        {
            if (Result.IntentValidation is null || Result.IntentValidation.IntentValid)
            {
                continue;
            }

            FailureReport.AppendLine("Diagram " + Result.DiagramId + ":");
            foreach (FlowchartRoutingIntentValidationFinding Finding in Result.IntentValidation.Findings)
            {
                FailureReport.AppendLine("  " + Finding.EdgeId + ": " + Finding.Message);
            }
        }

        if (FailureReport.Length > 0)
        {
            Assert.Fail(FailureReport.ToString());
        }
    }

    [Test]
    public void ValidateTortureFixture_SeparatesIntentAndRenderFailures()
    {
        string Markdown = ReadFixture(TORTURE_MARKDOWN_FILE);
        string RulesetJson = ReadFixture(RULESET_FILE);
        FlowchartRoutingIntentRuleset Ruleset =
            FlowchartRoutingIntentJsonSerializer.DeserializeRuleset(RulesetJson);

        Assert.That(
            Ruleset.ResolveDiagramExpectations().Any(
                Diagram => string.Equals(Diagram.DiagramId, "RegressionFlow001", StringComparison.Ordinal)),
            Is.True,
            "ruleset.json must expose RegressionFlow001 via Diagrams or DiagramIntentFixtures. "
                + "Rebuild the test project so RoutingIntent/ruleset.json is copied to the output folder.");

        List<FlowchartRoutingIntentPipelineResult> Results =
            FlowchartRoutingIntentPipeline.ProcessMarkdownFixture(Markdown, Ruleset);

        FlowchartRoutingIntentPipelineResult Regression = Results
            .First(Result => string.Equals(Result.DiagramId, "RegressionFlow001", StringComparison.Ordinal));

        Assert.That(Regression.LayoutSucceeded, Is.True, "RegressionFlow001 layout should succeed.");
        Assert.That(Regression.Intent, Is.Not.Null);
        Assert.That(Regression.IntentValidation, Is.Not.Null);

        if (Regression.IntentValidation!.IntentValid != true)
        {
            StringBuilder IntentFailureReport = new StringBuilder();
            IntentFailureReport.AppendLine("RegressionFlow001 intent validation failed:");
            foreach (FlowchartRoutingIntentValidationFinding Finding in Regression.IntentValidation.Findings)
            {
                IntentFailureReport.AppendLine("  " + Finding.EdgeId + ": " + Finding.Message);
            }

            Assert.Fail(IntentFailureReport.ToString());
        }

        Assert.That(Regression.IntentJson, Does.Contain("\"RouteClass\""));
        Assert.That(Regression.RenderDiagnostics, Is.Not.Null);

        bool HasRenderStageFinding = Regression.RenderDiagnostics.Count > 0;
        TestContext.WriteLine(
            "RegressionFlow001 intent-valid="
            + Regression.IntentValidation.IntentValid
            + " render-findings="
            + Regression.RenderDiagnostics.Count);

        if (HasRenderStageFinding)
        {
            TestContext.WriteLine("Render-stage findings remain even when intent validation passes.");
        }
    }

    [Test]
    public void SerializeDiagram_RegressionFlow001_ProducesReadableJson()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract,
            Layout,
            Direction,
            "RegressionFlow001");

        string Json = FlowchartRoutingIntentJsonSerializer.SerializeDiagram(Intent);
        Assert.That(Json, Does.Contain("\"DiagramId\": \"RegressionFlow001\""));
        Assert.That(Json, Does.Contain("\"RouteClass\": \"CentreSpine\""));
        Assert.That(Json, Does.Contain("\"InvalidBehaviours\""));
        Assert.That(Json, Does.Contain("\"ExpectedBehaviour\""));
    }

    [Test]
    public void Debug_PrintAllTortureDiagramRouteClasses_ForRulesetAuthoring()
    {
        string Markdown = ReadFixture(TORTURE_MARKDOWN_FILE);
        List<FlowchartRoutingIntentPipelineResult> Results =
            FlowchartRoutingIntentPipeline.ProcessMarkdownFixture(Markdown, Ruleset: null);

        foreach (FlowchartRoutingIntentPipelineResult Result in Results)
        {
            TestContext.WriteLine("[" + Result.DiagramId + "]");
            foreach (FlowchartEdgeRoutingIntentRecord Edge in Result.Intent!.Edges)
            {
                TestContext.WriteLine(
                    "  "
                    + Edge.SourceNodeId
                    + "->"
                    + Edge.TargetNodeId
                    + " "
                    + Edge.RouteClass);
            }
        }

        Assert.Pass("Route classes written to test output.");
    }

    [Test]
    public void Debug_PrintRegressionFlow001Intent_ForRulesetAuthoring()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract,
            Layout,
            Direction,
            "RegressionFlow001");

        string Json = FlowchartRoutingIntentJsonSerializer.SerializeDiagram(Intent);
        TestContext.WriteLine(Json);
        Assert.Pass("Intent JSON written to test output.");
    }

    private static void AssertRouteClass(
        FlowchartRoutingIntentDiagramResult Intent,
        string SourceNodeId,
        string TargetNodeId,
        FlowchartRoutingIntentRouteClass ExpectedRouteClass)
    {
        FlowchartEdgeRoutingIntentRecord? Edge = Intent.Edges.FirstOrDefault(
            Candidate => string.Equals(Candidate.SourceNodeId, SourceNodeId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetNodeId, TargetNodeId, StringComparison.Ordinal));

        Assert.That(Edge, Is.Not.Null, SourceNodeId + "->" + TargetNodeId + " should exist");
        Assert.That(
            Edge!.RouteClass,
            Is.EqualTo(ExpectedRouteClass),
            SourceNodeId + "->" + TargetNodeId + " route class");
    }

    private static string ReadFixture(string RelativePath)
    {
        string BaseDirectory = AppContext.BaseDirectory;
        string FullPath = Path.Combine(BaseDirectory, RelativePath);
        Assert.That(File.Exists(FullPath), Is.True, "Missing fixture file: " + FullPath);
        return File.ReadAllText(FullPath);
    }
}
