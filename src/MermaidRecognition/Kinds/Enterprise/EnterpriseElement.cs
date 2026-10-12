namespace MermaidRecognition.Kinds.Enterprise;

using MermaidRecognition.Contract;

/// <summary>
/// A declared Application or Technology element.
/// </summary>
public sealed class EnterpriseElement
{
    /// <summary>Gets or sets whether the label uses Mermaid markdown-string formatting.</summary>
    public bool IsMarkdownLabel { get; set; }

    /// <summary>Gets or sets the optional placement hint.</summary>
    public PlacementHintContract? PlacementHint { get; set; }

    private string _Id = string.Empty;

    private EnterpriseElementType _ElementType;

    private string _Label = string.Empty;

    private string? _ParentId;

    private int _LineNumber;

    private EnterpriseJunctionKind? _JunctionKind;

    /// <summary>Gets or sets the element identifier.</summary>
    public string Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the element type.</summary>
    public EnterpriseElementType ElementType
    {
        get
        {
            return _ElementType;
        }
        set
        {
            _ElementType = value;
        }
    }

    /// <summary>Gets or sets the display label.</summary>
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

    /// <summary>Gets or sets the optional parent group or element id.</summary>
    public string? ParentId
    {
        get
        {
            return _ParentId;
        }
        set
        {
            _ParentId = value;
        }
    }

    /// <summary>Gets or sets and/or when the type is a junction.</summary>
    public EnterpriseJunctionKind? JunctionKind
    {
        get
        {
            return _JunctionKind;
        }
        set
        {
            _JunctionKind = value;
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
}
