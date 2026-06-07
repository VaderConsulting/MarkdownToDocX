using System.Collections.Generic;

namespace MermaidRecognition.RuleValidation.Models;

/// <summary>
/// Deserialised <c>ruleset.json</c> document.
/// </summary>
public sealed class RulesetDocument
{
    private string _SchemaVersion = string.Empty;

    private string _Name = string.Empty;

    private List<RuleDefinition> _Rules = new List<RuleDefinition>();

    public string SchemaVersion
    {
        get
        {
            return _SchemaVersion;
        }
        set
        {
            _SchemaVersion = value;
        }
    }

    public string Name
    {
        get
        {
            return _Name;
        }
        set
        {
            _Name = value;
        }
    }

    public string? DefaultToleranceProfileId { get; set; }

    public List<string> RuleGroups { get; set; } = new List<string>();

    public List<RuleDefinition> Rules
    {
        get
        {
            return _Rules;
        }
        set
        {
            _Rules = value ?? new List<RuleDefinition>();
        }
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

    private List<string> _RequiredFields = new List<string>();

    public string RuleId
    {
        get
        {
            return _RuleId;
        }
        set
        {
            _RuleId = value;
        }
    }

    public string EvaluatorId
    {
        get
        {
            return _EvaluatorId;
        }
        set
        {
            _EvaluatorId = value;
        }
    }

    public string EvaluatorType
    {
        get
        {
            return _EvaluatorType;
        }
        set
        {
            _EvaluatorType = value;
        }
    }

    public string InputModel
    {
        get
        {
            return _InputModel;
        }
        set
        {
            _InputModel = value;
        }
    }

    public string AppliesTo
    {
        get
        {
            return _AppliesTo;
        }
        set
        {
            _AppliesTo = value;
        }
    }

    public List<string> RequiredFields
    {
        get
        {
            return _RequiredFields;
        }
        set
        {
            _RequiredFields = value ?? new List<string>();
        }
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

    public List<EvaluatorBinding> EvaluatorBindings { get; set; } = new List<EvaluatorBinding>();
}

/// <summary>
/// Deserialised <c>expected-contract.json</c> document.
/// </summary>
public sealed class ExpectedContractDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, object> ActualResultsContract { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> ValidationReportContract { get; set; } = new Dictionary<string, object>();

    public List<Dictionary<string, object>> ToleranceProfiles { get; set; } = new List<Dictionary<string, object>>();

    public Dictionary<string, object> DiagnosticRequirements { get; set; } = new Dictionary<string, object>();
}
