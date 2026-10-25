namespace MermaidRecognition.Kinds.Enterprise;

using MermaidRecognition.Contract;

/// <summary>
/// A directed relationship between two declared elements.
/// </summary>
public sealed class EnterpriseRelationship
{
    /// <summary>Gets or sets whether the label uses Mermaid markdown-string formatting.</summary>
    public bool IsMarkdownLabel { get; set; }

    /// <summary>Gets or sets the optional routing hint.</summary>
    public RoutingHintContract? RoutingHint { get; set; }

    private string _SourceId = string.Empty;

    private string _TargetId = string.Empty;

    private EnterpriseRelationshipKind _Kind;

    private string _Label = string.Empty;

    private int _LineNumber;

    private EnterpriseAccessKind? _AccessKind;

    private string? _InfluenceStrength;

    private bool _DirectedAssociation;

    private bool _HiddenByNesting;

    private int _MinLength = 1;

    /// <summary>Gets or sets the source element id.</summary>
    public string SourceId
    {
        get
        {
            return _SourceId;
        }
        set
        {
            _SourceId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the target element id.</summary>
    public string TargetId
    {
        get
        {
            return _TargetId;
        }
        set
        {
            _TargetId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the relationship kind.</summary>
    public EnterpriseRelationshipKind Kind
    {
        get
        {
            return _Kind;
        }
        set
        {
            _Kind = value;
        }
    }

    /// <summary>Gets or sets the optional edge label.</summary>
    public string Label
    {
        get
        {
            return _Label;
        }
        set
        {
            _Label = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the 1-based line number in the diagram block.</summary>
    public int LineNumber
    {
        get
        {
            return _LineNumber;
        }
        set
        {
            _LineNumber = value;
        }
    }

    /// <summary>Gets or sets the access qualifier when <see cref="Kind"/> is access.</summary>
    public EnterpriseAccessKind? AccessKind
    {
        get
        {
            return _AccessKind;
        }
        set
        {
            _AccessKind = value;
        }
    }

    /// <summary>Gets or sets the influence strength qualifier.</summary>
    public string? InfluenceStrength
    {
        get
        {
            return _InfluenceStrength;
        }
        set
        {
            _InfluenceStrength = value;
        }
    }

    /// <summary>Gets or sets whether an association is directed.</summary>
    public bool DirectedAssociation
    {
        get
        {
            return _DirectedAssociation;
        }
        set
        {
            _DirectedAssociation = value;
        }
    }

    /// <summary>
    /// Gets or sets the minimum rank span, from the closing dashes. 1 is a normal link.
    /// </summary>
    public int MinLength
    {
        get
        {
            return _MinLength;
        }
        set
        {
            _MinLength = value < 1 ? 1 : value;
        }
    }

    /// <summary>
    /// Gets or sets whether this relationship is expressed by nesting and should not be drawn as a line.
    /// </summary>
    public bool HiddenByNesting
    {
        get
        {
            return _HiddenByNesting;
        }
        set
        {
            _HiddenByNesting = value;
        }
    }
}
