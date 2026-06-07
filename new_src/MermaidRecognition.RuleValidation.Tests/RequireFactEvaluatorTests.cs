using NUnit.Framework;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Evaluators;
using MermaidRecognition.RuleValidation.Loading;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class RequireFactEvaluatorTests
{
    [Test]
    public void RequireFactEvaluator_FindsDiagramRecognitionFact()
    {
        string SpecDirectory = Path.Combine(AppContext.BaseDirectory, "Rule Specification");
        LoadedSpecBundle Bundle = SpecDocumentLoader.LoadFromDirectory(SpecDirectory);
        RuleDefinition Rule = Bundle.Ruleset.Rules.First(Candidate => Candidate.RuleId == "Header.GraphAlias");
        EvaluatorBinding Binding = Bundle.EvaluatorMap.EvaluatorBindings.First(
            Candidate => Candidate.RuleId == "Header.GraphAlias");

        DiagramActualResult Diagram = new DiagramActualResult
        {
            DiagramId = "F01",
            Recognition = new Dictionary<string, object>
            {
                ["HeaderToken"] = "graph",
                ["LegacyCatalogueRuleSatisfied"] = true
            }
        };

        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            ElementType = "Diagram",
            ElementId = "F01"
        };

        RequireFactEvaluator Evaluator = new RequireFactEvaluator();
        RuleEvaluationResult Result = Evaluator.Evaluate(
            Rule,
            Binding,
            new ActualResultsDocument(),
            Context);

        Assert.That(Result.Status, Is.Not.EqualTo("Fail"));
        Assert.That(Result.Failures, Is.Empty);
    }
}
