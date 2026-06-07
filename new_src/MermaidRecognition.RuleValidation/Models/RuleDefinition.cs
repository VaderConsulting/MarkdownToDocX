using System.Collections.Generic;
using System.Text.Json;

namespace MermaidRecognition.RuleValidation.Models;

/// <summary>
/// One universal rule from <c>ruleset.json</c>.
/// </summary>
public sealed class RuleDefinition
{
    private string _RuleId = string.Empty;

    private string _RuleGroup = string.Empty;

    private string _AppliesTo = string.Empty;

    private string _Severity = string.Empty;

    private string _Description = string.Empty;

    private JsonElement? _When;

    private Dictionary<string, JsonElement> _Require = new Dictionary<string, JsonElement>();

    private List<string> _FailWhen = new List<string>();

    private string _DiagnosticTemplate = string.Empty;

    private string _SuggestedCorrectionTemplate = string.Empty;

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

    public string RuleGroup
    {
        get
        {
            return _RuleGroup;
        }
        set
        {
            _RuleGroup = value;
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

    public string Severity
    {
        get
        {
            return _Severity;
        }
        set
        {
            _Severity = value;
        }
    }

    public string Description
    {
        get
        {
            return _Description;
        }
        set
        {
            _Description = value;
        }
    }

    public JsonElement? When
    {
        get
        {
            return _When;
        }
        set
        {
            _When = value;
        }
    }

    public Dictionary<string, JsonElement> Require
    {
        get
        {
            return _Require;
        }
        set
        {
            _Require = value ?? new Dictionary<string, JsonElement>();
        }
    }

    public List<string> FailWhen
    {
        get
        {
            return _FailWhen;
        }
        set
        {
            _FailWhen = value ?? new List<string>();
        }
    }

    public string DiagnosticTemplate
    {
        get
        {
            return _DiagnosticTemplate;
        }
        set
        {
            _DiagnosticTemplate = value;
        }
    }

    public string SuggestedCorrectionTemplate
    {
        get
        {
            return _SuggestedCorrectionTemplate;
        }
        set
        {
            _SuggestedCorrectionTemplate = value;
        }
    }
}
