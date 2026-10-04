using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Orchestrates routing-intent extraction from Markdown torture-test fixtures.
/// </summary>
public static class FlowchartRoutingIntentPipeline
{
    /// <summary>
    /// Extracts diagrams from Markdown, analyses routing intent, and optionally validates against a ruleset.
    /// </summary>
    public static List<FlowchartRoutingIntentPipelineResult> ProcessMarkdownFixture(
        string Markdown,
        FlowchartRoutingIntentRuleset? Ruleset)
    {
        List<FlowchartRoutingIntentPipelineResult> Results = [];
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams =
            MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);

        foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram in Diagrams)
        {
            FlowchartRoutingIntentPipelineResult PipelineResult = ProcessDiagram(Diagram, Ruleset);
            Results.Add(PipelineResult);
        }

        return Results;
    }

    /// <summary>
    /// Analyses routing intent for one extracted diagram.
    /// </summary>
    public static FlowchartRoutingIntentPipelineResult ProcessDiagram(
        MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram,
        FlowchartRoutingIntentRuleset? Ruleset)
    {
        FlowchartRoutingIntentPipelineResult PipelineResult = new FlowchartRoutingIntentPipelineResult
        {
            DiagramId = Diagram.DiagramId
        };

        DiagramContract Contract = MermaidRecognizer.Parse(Diagram.Source);
        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            PipelineResult.ParserSucceeded = true;
            PipelineResult.LayoutSucceeded = false;
            return PipelineResult;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract,
            Layout,
            Direction,
            Diagram.DiagramId);

        PipelineResult.ParserSucceeded = true;
        PipelineResult.LayoutSucceeded = true;
        PipelineResult.Intent = Intent;
        PipelineResult.IntentJson = FlowchartRoutingIntentJsonSerializer.SerializeDiagram(Intent);

        if (Ruleset is not null)
        {
            FlowchartRoutingIntentValidationResult Validation =
                FlowchartRoutingIntentValidator.ValidateAgainstRuleset(Intent, Ruleset);
            PipelineResult.IntentValidation = Validation;
            PipelineResult.IntentValidationSucceeded = Validation.IntentValid;
        }

        List<FlowchartEdgeRouteDiagnosticFinding> RenderFindings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);
        PipelineResult.RenderDiagnostics = RenderFindings;

        return PipelineResult;
    }
}

/// <summary>
/// Combined routing-intent pipeline result for one diagram.
/// </summary>
public sealed class FlowchartRoutingIntentPipelineResult
{
    private string _DiagramId = string.Empty;

    private string _IntentJson = string.Empty;

    private FlowchartRoutingIntentDiagramResult? _Intent;

    private FlowchartRoutingIntentValidationResult? _IntentValidation;

    private List<FlowchartEdgeRouteDiagnosticFinding> _RenderDiagnostics = [];

    /// <summary>
    /// Diagram identifier from the torture-test fixture.
    /// </summary>
    public string DiagramId
    {
        get => _DiagramId; set => _DiagramId = value;
    }

    /// <summary>
    /// True when Mermaid recognition succeeded.
    /// </summary>
    public bool ParserSucceeded { get; set; }

    /// <summary>
    /// True when Sugiyama layout succeeded.
    /// </summary>
    public bool LayoutSucceeded { get; set; }

    /// <summary>
    /// Routing-intent diagram result.
    /// </summary>
    public FlowchartRoutingIntentDiagramResult? Intent
    {
        get => _Intent; set => _Intent = value;
    }

    /// <summary>
    /// Serialised routing-intent JSON.
    /// </summary>
    public string IntentJson
    {
        get => _IntentJson; set => _IntentJson = value;
    }

    /// <summary>
    /// Ruleset validation result, when a ruleset was supplied.
    /// </summary>
    public FlowchartRoutingIntentValidationResult? IntentValidation
    {
        get => _IntentValidation; set => _IntentValidation = value;
    }

    /// <summary>
    /// True when ruleset validation succeeded.
    /// </summary>
    public bool? IntentValidationSucceeded { get; set; }

    /// <summary>
    /// Render-stage structural diagnostic findings.
    /// </summary>
    public List<FlowchartEdgeRouteDiagnosticFinding> RenderDiagnostics
    {
        get => _RenderDiagnostics; set => _RenderDiagnostics = value ?? [];
    }
}
