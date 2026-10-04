using System.Collections.Generic;

namespace MermaidRecognition.RuleValidation.Models;

/// <summary>
/// Deserialised <c>ruleset.json</c> document.
/// </summary>
public sealed class RulesetDocument
{
    private string _SchemaVersion = string.Empty;

    private string _Name = string.Empty;

    private List<RuleDefinition> _Rules = [];

    public string SchemaVersion
    {
        get => _SchemaVersion; set => _SchemaVersion = value;
    }

    public string Name
    {
        get => _Name; set => _Name = value;
    }

    public string? DefaultToleranceProfileId { get; set; }

    public List<string> RuleGroups { get; set; } = [];

    public List<RuleDefinition> Rules
    {
        get => _Rules; set => _Rules = value ?? [];
    }
}

/// <summary>
/// One evaluator binding from <c>evaluator-map.json</c>.
/// </summary>
public sealed class EvaluatorBinding
{
    private string _RuleId = string.Empty;

    private string _EvaluatorId = string.Empty;

    private string _EvaluatorType = string.Empty;

    private string _InputModel = string.Empty;

    private string _AppliesTo = string.Empty;

    private List<string> _RequiredFields = [];

    public string RuleId
    {
        get => _RuleId; set => _RuleId = value;
    }

    public string EvaluatorId
    {
        get => _EvaluatorId; set => _EvaluatorId = value;
    }

    public string EvaluatorType
    {
        get => _EvaluatorType; set => _EvaluatorType = value;
    }

    public string InputModel
    {
        get => _InputModel; set => _InputModel = value;
    }

    public string AppliesTo
    {
        get => _AppliesTo; set => _AppliesTo = value;
    }

    public List<string> RequiredFields
    {
        get => _RequiredFields; set => _RequiredFields = value ?? [];
    }

    public string? ToleranceProfileId { get; set; }

    public string? FailureCode { get; set; }
}

/// <summary>
/// Deserialised <c>evaluator-map.json</c> document.
/// </summary>
public sealed class EvaluatorMapDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<EvaluatorBinding> EvaluatorBindings { get; set; } = [];
}

/// <summary>
/// Deserialised <c>expected-contract.json</c> document.
/// </summary>
public sealed class ExpectedContractDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, object> ActualResultsContract { get; set; } = [];

    public Dictionary<string, object> ValidationReportContract { get; set; } = [];

    public List<Dictionary<string, object>> ToleranceProfiles { get; set; } = [];

    public Dictionary<string, object> DiagnosticRequirements { get; set; } = [];
}
