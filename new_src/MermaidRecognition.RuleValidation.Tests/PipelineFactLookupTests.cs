using NUnit.Framework;
using MermaidRecognition.RuleValidation.Models;
using MermaidRecognition.RuleValidation.Pipeline;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class PipelineFactLookupTests
{
    [Test]
    public void Pipeline_F01_DoesNotFailHeaderGraphAliasMissingFact()
    {
        string SpecDirectory = Path.Combine(AppContext.BaseDirectory, "Rule Specification");
        string TempDirectory = Path.Combine(Path.GetTempPath(), "f01-pipeline-" + Guid.NewGuid().ToString("N"));
        string Markdown = "### F01 — graph alias\n\n```mermaid\ngraph TD\nA[Alpha] --> B[Beta] --> C[Gamma]\n```\n";
        string MarkdownPath = Path.Combine(TempDirectory, "f01.md");
        Directory.CreateDirectory(TempDirectory);
        File.WriteAllText(MarkdownPath, Markdown);

        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(MarkdownPath, TempDirectory);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        Assert.That(Diagram.Recognition["LegacyCatalogueRuleSatisfied"], Is.EqualTo(true));

        ValidationFailureRecord? HeaderFailure = Result.ValidationReport.Failures.FirstOrDefault(
            Failure => Failure.RuleId == "Header.GraphAlias");
        Assert.That(HeaderFailure, Is.Null, HeaderFailure?.Message);
    }
}
