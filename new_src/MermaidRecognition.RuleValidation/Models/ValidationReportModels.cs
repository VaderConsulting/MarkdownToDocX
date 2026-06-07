using System.Collections.Generic;

namespace MermaidRecognition.RuleValidation.Models;

/// <summary>
/// Root <c>validation-report.json</c> document.
/// </summary>
public sealed class ValidationReportDocument
{
    public string SchemaVersion { get; set; } = "2.0.0";

    public string RunId { get; set; } = string.Empty;

    public string OverallResult { get; set; } = "Pass";

    public ValidationReportSummary Summary { get; set; } = new ValidationReportSummary();

    public List<DiagramValidationResult> DiagramResults { get; set; } = new List<DiagramValidationResult>();

    public List<ValidationFailureRecord> Failures { get; set; } = new List<ValidationFailureRecord>();
}

/// <summary>
/// Aggregate counts for one validation run.
/// </summary>
public sealed class ValidationReportSummary
{
    public int DiagramCount { get; set; }

    public int RuleCount { get; set; }

    public int PassCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public int NotApplicableCount { get; set; }

    public int SkippedDiagramCount { get; set; }
}

/// <summary>
/// Per-diagram validation summary.
/// </summary>
public sealed class DiagramValidationResult
{
    public string DiagramId { get; set; } = string.Empty;

    public string Result { get; set; } = "Pass";

    public bool ParserSucceeded { get; set; }

    public bool LayoutSucceeded { get; set; }

    public bool IsFullyRemarked { get; set; }

    public int PassCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public int NotApplicableCount { get; set; }
}

/// <summary>
/// One actionable validation failure.
/// </summary>
public sealed class ValidationFailureRecord
{
    public string RuleId { get; set; } = string.Empty;

    public string EvaluatorId { get; set; } = string.Empty;

    public string Severity { get; set; } = "Error";

    public string DiagramId { get; set; } = string.Empty;

    public string ElementType { get; set; } = string.Empty;

    public string ElementId { get; set; } = string.Empty;

    public string? ScenarioTitle { get; set; }

    public string? HumanElementDescription { get; set; }

    public string? SourceLabel { get; set; }

    public string? TargetLabel { get; set; }

    public string? EdgeLabel { get; set; }

    public string? NodeLabel { get; set; }

    public string? GroupLabel { get; set; }

    public string Message { get; set; } = string.Empty;

    public Dictionary<string, object> Expected { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> Actual { get; set; } = new Dictionary<string, object>();

    public string SuggestedCorrection { get; set; } = string.Empty;

    public Dictionary<string, object>? SourceSpan { get; set; }
}

/// <summary>
/// Result from one rule evaluator invocation.
/// </summary>
public sealed class RuleEvaluationResult
{
    public string Status { get; set; } = "Pass";

    public List<ValidationFailureRecord> Failures { get; set; } = new List<ValidationFailureRecord>();
}

/// <summary>
/// Evaluation scope for one rule invocation.
/// </summary>
public sealed class EvaluationContext
{
    public DiagramActualResult Diagram { get; set; } = new DiagramActualResult();

    public NodeActualResult? Node { get; set; }

    public EdgeActualResult? Edge { get; set; }

    public GroupActualResult? Group { get; set; }

    public string ElementType { get; set; } = "Diagram";

    public string ElementId { get; set; } = string.Empty;

    public Dictionary<string, object> FieldValues { get; set; } = new Dictionary<string, object>();
}
