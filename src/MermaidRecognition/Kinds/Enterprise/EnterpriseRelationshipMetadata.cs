namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Notation facts for one relationship kind.
/// </summary>
public sealed class EnterpriseRelationshipMetadata
{
    private readonly EnterpriseRelationshipKind _Kind;

    private readonly string _Name;

    private readonly char _Letter;

    private readonly string _LineStyle;

    private readonly string? _StartMarker;

    private readonly string? _EndMarker;

    /// <summary>
    /// Creates relationship metadata.
    /// </summary>
    /// <param name="Kind">Relationship kind.</param>
    /// <param name="Name">Canonical name.</param>
    /// <param name="Letter">Matrix letter.</param>
    /// <param name="LineStyle">solid, dotted, or dashed.</param>
    /// <param name="StartMarker">Source-end marker key.</param>
    /// <param name="EndMarker">Target-end marker key.</param>
    public EnterpriseRelationshipMetadata(
        EnterpriseRelationshipKind Kind,
        string Name,
        char Letter,
        string LineStyle,
        string? StartMarker,
        string? EndMarker)
    {
        _Kind = Kind;
        _Name = Name;
        _Letter = Letter;
        _LineStyle = LineStyle;
        _StartMarker = StartMarker;
        _EndMarker = EndMarker;
    }

    /// <summary>Gets the relationship kind.</summary>
    public EnterpriseRelationshipKind Kind
    {
        get
        {
            return _Kind;
        }
    }

    /// <summary>Gets the canonical name.</summary>
    public string Name
    {
        get
        {
            return _Name;
        }
    }

    /// <summary>Gets the matrix letter.</summary>
    public char Letter
    {
        get
        {
            return _Letter;
        }
    }

    /// <summary>Gets the stroke style.</summary>
    public string LineStyle
    {
        get
        {
            return _LineStyle;
        }
    }

    /// <summary>Gets the source-end marker key.</summary>
    public string? StartMarker
    {
        get
        {
            return _StartMarker;
        }
    }

    /// <summary>Gets the target-end marker key.</summary>
    public string? EndMarker
    {
        get
        {
            return _EndMarker;
        }
    }
}
