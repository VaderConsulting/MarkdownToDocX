using System.Collections.Generic;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Archi;

/// <summary>
/// Neutral enterprise model used for Archi and coArchi file exchange.
/// </summary>
internal sealed class ArchiExchangeModel
{
    private string _ModelName = "Enterprise model";

    private string _ModelArchiId = string.Empty;

    private string _Version = EnterpriseParser.DEFAULT_VERSION;

    private readonly List<ArchiExchangeElement> _Elements = new List<ArchiExchangeElement>();

    private readonly List<ArchiExchangeRelationship> _Relationships = new List<ArchiExchangeRelationship>();

    private readonly List<string> _Warnings = new List<string>();

    /// <summary>Gets or sets the Archi model display name.</summary>
    public string ModelName
    {
        get
        {
            return _ModelName;
        }
        set
        {
            _ModelName = string.IsNullOrWhiteSpace(value) ? "Enterprise model" : value.Trim();
        }
    }

    /// <summary>Gets or sets the Archi model object id.</summary>
    public string ModelArchiId
    {
        get
        {
            return _ModelArchiId;
        }
        set
        {
            _ModelArchiId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the enterprise rule-table version.</summary>
    public string Version
    {
        get
        {
            return _Version;
        }
        set
        {
            _Version = string.IsNullOrWhiteSpace(value) ? EnterpriseParser.DEFAULT_VERSION : value.Trim();
        }
    }

    /// <summary>Gets the mapped elements.</summary>
    public List<ArchiExchangeElement> Elements
    {
        get
        {
            return _Elements;
        }
    }

    /// <summary>Gets the mapped relationships.</summary>
    public List<ArchiExchangeRelationship> Relationships
    {
        get
        {
            return _Relationships;
        }
    }

    /// <summary>Gets non-fatal mapping notes.</summary>
    public List<string> Warnings
    {
        get
        {
            return _Warnings;
        }
    }
}

/// <summary>
/// One element in an Archi exchange model.
/// </summary>
internal sealed class ArchiExchangeElement
{
    private string _ArchiId = string.Empty;

    private string _MermaidId = string.Empty;

    private string _Label = string.Empty;

    private string? _ParentMermaidId;

    /// <summary>Gets or sets the stable Archi object id.</summary>
    public string ArchiId
    {
        get
        {
            return _ArchiId;
        }
        set
        {
            _ArchiId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the Mermaid enterprise identifier.</summary>
    public string MermaidId
    {
        get
        {
            return _MermaidId;
        }
        set
        {
            _MermaidId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the enterprise element type.</summary>
    public EnterpriseElementType ElementType { get; set; }

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

    /// <summary>Gets or sets the optional parent Mermaid id.</summary>
    public string? ParentMermaidId
    {
        get
        {
            return _ParentMermaidId;
        }
        set
        {
            _ParentMermaidId = value;
        }
    }

    /// <summary>Gets or sets junction polarity when the type is a junction.</summary>
    public EnterpriseJunctionKind? JunctionKind { get; set; }
}

/// <summary>
/// One relationship in an Archi exchange model.
/// </summary>
internal sealed class ArchiExchangeRelationship
{
    private string _ArchiId = string.Empty;

    private string _SourceMermaidId = string.Empty;

    private string _TargetMermaidId = string.Empty;

    private string _Label = string.Empty;

    /// <summary>Gets or sets the stable Archi object id.</summary>
    public string ArchiId
    {
        get
        {
            return _ArchiId;
        }
        set
        {
            _ArchiId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the source Mermaid id.</summary>
    public string SourceMermaidId
    {
        get
        {
            return _SourceMermaidId;
        }
        set
        {
            _SourceMermaidId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the target Mermaid id.</summary>
    public string TargetMermaidId
    {
        get
        {
            return _TargetMermaidId;
        }
        set
        {
            _TargetMermaidId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the relationship kind.</summary>
    public EnterpriseRelationshipKind Kind { get; set; }

    /// <summary>Gets or sets the optional label.</summary>
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

    /// <summary>Gets or sets access direction when kind is access.</summary>
    public EnterpriseAccessKind? AccessKind { get; set; }

    /// <summary>Gets or sets whether an association is directed.</summary>
    public bool DirectedAssociation { get; set; }
}
