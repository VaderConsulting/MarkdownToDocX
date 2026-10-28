using System.IO;
using NUnit.Framework;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Loading;
using MermaidRecognition.RuleValidation.Models;
using MermaidRecognition.RuleValidation.Pipeline;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// End-to-end rule-validation tests for flow.md catalogue extraction and parsing.
/// </summary>
[TestFixture]
public sealed class FlowMarkdownRuleValidationTests : ProductionCatalogueTestFixture
{
    private string _SpecDirectory = string.Empty;

    private string _FlowMarkdownPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        string BaseDirectory = AppContext.BaseDirectory;
        _SpecDirectory = Path.Combine(BaseDirectory, "Rule Specification");
        _FlowMarkdownPath = Path.Combine(BaseDirectory, "flow.md");
    }

    [Test]
    public void SpecBundle_LoadsAllRuleIdsWithEvaluatorBindings()
    {
        LoadedSpecBundle Bundle = SpecDocumentLoader.LoadFromDirectory(_SpecDirectory);
        HashSet<string> RuleIds = [.. Bundle.Ruleset.Rules.Select(Rule => Rule.RuleId)];
        Assert.That(Bundle.EvaluatorMap.EvaluatorBindings.Count, Is.EqualTo(RuleIds.Count));
        foreach (EvaluatorBinding Binding in Bundle.EvaluatorMap.EvaluatorBindings)
        {
            Assert.That(RuleIds, Does.Contain(Binding.RuleId));
        }
    }

    [Test]
    public void FlowMarkdown_Extractor_ResolvesScenarioIdsFromHeadings()
    {
        string Markdown = File.ReadAllText(_FlowMarkdownPath);
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams =
            MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);

        Assert.That(Diagrams.Count, Is.GreaterThan(80));
        Assert.That(Diagrams.Any(Diagram => Diagram.DiagramId == "F01"), Is.True);
        Assert.That(Diagrams.Any(Diagram => Diagram.DiagramId == "F89"), Is.True);
        Assert.That(Diagrams.Count(Diagram => Diagram.DiagramId == "F88"), Is.EqualTo(1));
        Assert.That(Diagrams.Any(Diagram => Diagram.DiagramId == "F65" && Diagram.IsFullyRemarked), Is.True);
    }

    [Test]
    public void FlowMarkdown_F58_PassesInFullCatalogueRun()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-f58-catalogue-" + Guid.NewGuid().ToString("N"));
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(_FlowMarkdownPath, OutputDirectory);

        DiagramValidationResult? DiagramResult = Result.ValidationReport.DiagramResults
            .FirstOrDefault(Candidate => Candidate.DiagramId == "F58");
        Assert.That(DiagramResult, Is.Not.Null);
        Assert.That(DiagramResult!.Result, Is.EqualTo("Pass"));
        Assert.That(DiagramResult.ErrorCount, Is.EqualTo(0));
    }

    [Test]
    public void FlowMarkdown_Pipeline_GeneratesActualResultsAndValidationReport()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-rule-validation-" + Guid.NewGuid().ToString("N"));
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(_FlowMarkdownPath, OutputDirectory);

        Assert.That(File.Exists(Result.ActualResultsPath), Is.True);
        Assert.That(File.Exists(Result.ValidationReportPath), Is.True);
        Assert.That(Result.ActualResults.Diagrams.Count, Is.GreaterThan(80));
        Assert.That(Result.ActualResults.Source.ActiveDiagramCount, Is.GreaterThan(70));
        Assert.That(Result.ActualResults.Diagrams.Count(Diagram => Diagram.ParserSucceeded), Is.GreaterThan(70));
        Assert.That(Result.ValidationReport.DiagramResults.Count, Is.EqualTo(Result.ActualResults.Diagrams.Count));
    }

    [Test]
    public void FlowMarkdown_ActiveDiagrams_ParseAndLayout()
    {
        string Markdown = File.ReadAllText(_FlowMarkdownPath);
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams =
            MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);

        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> ActiveDiagrams = [.. Diagrams
            .Where(Diagram => !Diagram.IsFullyRemarked)
            .Take(12)];

        int LayoutSuccessCount = 0;
        foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram in ActiveDiagrams)
        {
            MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
            string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-single-" + Diagram.DiagramId);
            string TempMarkdown = "```mermaid\n" + Diagram.Source + "\n```";
            string TempFile = Path.Combine(OutputDirectory, Diagram.DiagramId + ".md");
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(TempFile, "### " + Diagram.DiagramId + " — test\n\n" + TempMarkdown);
            MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);
            DiagramActualResult? DiagramResult = Result.ActualResults.Diagrams.FirstOrDefault();
            if (DiagramResult is not null && DiagramResult.ParserSucceeded && DiagramResult.LayoutSucceeded)
            {
                LayoutSuccessCount++;
            }
        }

        Assert.That(LayoutSuccessCount, Is.EqualTo(ActiveDiagrams.Count));
    }

    [Test]
    public void FlowMarkdown_F35_DoesNotReportUnnecessaryDetourOnPathB()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-f35-" + Guid.NewGuid().ToString("N"));
        string TempMarkdown = """
            ### F35 — Diamond with multiple incoming edges

            ```mermaid
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            ```
            """;
        string TempFile = Path.Combine(OutputDirectory, "f35.md");
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(TempFile, TempMarkdown);

        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);

        List<ValidationFailureRecord> F35Failures = [.. Result.ValidationReport.Failures.Where(Failure => Failure.DiagramId == "F35")];

        Assert.That(
            F35Failures.Any(Failure => Failure.RuleId == "Routing.Path.NoUnnecessaryDetour"),
            Is.False,
            "Path B to Merge? is a straight spine-aligned connector and must not fail NoUnnecessaryDetour.");
    }

    [Test]
    public void FlowMarkdown_F125_DoesNotReportUnnecessaryDetourOnPathB()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-f125-" + Guid.NewGuid().ToString("N"));
        string TempMarkdown = """
            ### F125 — Nodes first: diamond with multiple incoming edges

            ```mermaid
            flowchart TB
            PA[Path A]
            PB[Path B]
            PC[Path C]
            D{Merge?}
            E[Continue]
            F[Stop]
            PA --> D
            PB --> D
            PC --> D
            D -->|yes| E
            D -->|no| F
            ```
            """;
        string TempFile = Path.Combine(OutputDirectory, "f125.md");
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(TempFile, TempMarkdown);

        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);

        List<ValidationFailureRecord> F125Failures = [.. Result.ValidationReport.Failures.Where(Failure => Failure.DiagramId == "F125")];

        Assert.That(
            F125Failures.Any(Failure => Failure.RuleId == "Routing.Path.NoUnnecessaryDetour"),
            Is.False,
            "Path B to Merge? is a straight spine-aligned connector and must not fail NoUnnecessaryDetour.");
    }
}
