using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Evaluators;

/// <summary>
/// Executable evaluator logic for one or more rules.
/// </summary>
public interface IRuleEvaluator
{
    /// <summary>
    /// Stable evaluator identifier from <c>evaluator-map.json</c>.
    /// </summary>
    string EvaluatorId { get; }

    /// <summary>
    /// Evaluates one rule against the current actual-results context.
    /// </summary>
    RuleEvaluationResult Evaluate(
        RuleDefinition RuleDefinition,
        EvaluatorBinding EvaluatorBinding,
        ActualResultsDocument ActualResults,
        EvaluationContext EvaluationContext);
}
