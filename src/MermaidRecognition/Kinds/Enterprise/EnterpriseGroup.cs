namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// A visual grouping container (specification Grouping notation).
/// </summary>
public sealed class EnterpriseGroup
{
    /// <summary>Gets or sets the optional local layout direction.</summary>
    public EnterpriseDiagramDirection? Direction { get; set; }

    private string _Id = string.Empty;

    private string _Label = string.Empty;

    private string? _ParentId;

    private int _LineNumber;

    /// <summary>Gets or sets the group identifier.</summary>
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

    /// <summary>Gets or sets the group tab label.</summary>
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

    /// <summary>Gets or sets the optional parent group id.</summary>
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
