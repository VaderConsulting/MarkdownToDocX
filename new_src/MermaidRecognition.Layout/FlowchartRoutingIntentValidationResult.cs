using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of validating routing intent against <c>ruleset.json</c>.
/// </summary>
public sealed class FlowchartRoutingIntentValidationResult
{
    private List<FlowchartRoutingIntentValidationFinding> _Findings = new List<FlowchartRoutingIntentValidationFinding>();

    /// <summary>
    /// True when no intent-stage validation findings were recorded.
    /// </summary>
    public bool IntentValid { get; set; }

    /// <summary>
    /// Diagram identifier validated.
    /// </summary>
    public string DiagramId { get; set; } = string.Empty;

    /// <summary>
    /// Intent-stage validation findings.
    /// </summary>
    public List<FlowchartRoutingIntentValidationFinding> Findings
    {
        get
        {
            return _Findings;
        }
        set
        {
            _Findings = value ?? new List<FlowchartRoutingIntentValidationFinding>();
        }
    }
}

/// <summary>
/// One routing-intent validation finding.
/// </summary>
public sealed class FlowchartRoutingIntentValidationFinding
{
    private string _EdgeId = string.Empty;

    private string _Message = string.Empty;

    /// <summary>
    /// Edge identifier associated with the finding.
    /// </summary>
    public string EdgeId
    {
        get
        {
            return _EdgeId;
        }
        set
        {
            _EdgeId = value;
        }
    }

    /// <summary>
    /// Human-readable validation message.
    /// </summary>
    public string Message
    {
        get
        {
            return _Message;
        }
        set
        {
            _Message = value;
        }
    }
}
