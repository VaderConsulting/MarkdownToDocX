using System.Linq;
using MermaidRecognition.RuleValidation.Models;
using MermaidRecognition.RuleValidation.Pipeline;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// Regression tests for F58 full-pipeline rendered-path validation.
/// </summary>
[TestFixture]
public sealed class FlowF58RuleValidationTests
{
    private string _SpecDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _SpecDirectory = Path.Combine(AppContext.BaseDirectory, "Rule Specification");
    }

    [Test]
    public void FlowMarkdown_F58_PassesRenderedPathAndRoutingValidation()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-f58-" + Guid.NewGuid().ToString("N"));
        string TempMarkdown = """
            ### F58 — Full pipeline (shapes + decisions + labels + cylinder)

            ```mermaid
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
            ```
            """;
        string TempFile = Path.Combine(OutputDirectory, "f58.md");
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(TempFile, TempMarkdown);

        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single(Diagram => Diagram.DiagramId == "F58");
        EdgeActualResult ValidateReject = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-Validate-Reject-2");
        EdgeActualResult ArchiveDone = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-DoArchive-Done-10");

        Assert.That(ValidateReject.Facts["Ports.PerpendicularAttach"], Is.EqualTo(true));
        Assert.That(ValidateReject.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true));
        Assert.That(ArchiveDone.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true));
        Assert.That(ArchiveDone.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));

        DiagramValidationResult? DiagramResult = Result.ValidationReport.DiagramResults
            .FirstOrDefault(Candidate => Candidate.DiagramId == "F58");
        Assert.That(DiagramResult, Is.Not.Null);
        Assert.That(DiagramResult!.Result, Is.EqualTo("Pass"));
        Assert.That(DiagramResult.ErrorCount, Is.EqualTo(0));
    }
}
