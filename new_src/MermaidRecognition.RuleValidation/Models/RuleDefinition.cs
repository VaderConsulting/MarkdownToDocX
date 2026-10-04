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

    private Dictionary<string, JsonElement> _Require = [];

    private List<string> _FailWhen = [];

    private string _DiagnosticTemplate = string.Empty;

    private string _SuggestedCorrectionTemplate = string.Empty;

    public string RuleId
    {
        get => _RuleId; set => _RuleId = value;
    }

    public string RuleGroup
    {
        get => _RuleGroup; set => _RuleGroup = value;
    }

    public string AppliesTo
    {
        get => _AppliesTo; set => _AppliesTo = value;
    }

    public string Severity
    {
        get => _Severity; set => _Severity = value;
    }

    public string Description
    {
        get => _Description; set => _Description = value;
    }

    public JsonElement? When
    {
        get => _When; set => _When = value;
    }

    public Dictionary<string, JsonElement> Require
    {
        get => _Require; set => _Require = value ?? [];
    }

    public List<string> FailWhen
    {
        get => _FailWhen; set => _FailWhen = value ?? [];
    }

    public string DiagnosticTemplate
    {
        get => _DiagnosticTemplate; set => _DiagnosticTemplate = value;
    }

    public string SuggestedCorrectionTemplate
    {
        get => _SuggestedCorrectionTemplate; set => _SuggestedCorrectionTemplate = value;
    }
}
