namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Parsed enterprise diagram model.
/// </summary>
public sealed class EnterpriseDocument
{
    /// <summary>Gets flattened Mermaid configuration; unsupported keys are ignored by rendering.</summary>
    public Dictionary<string, string> Configuration { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Accessible title provided by the source.</summary>
    public string? AccessibilityTitle { get; set; }

    /// <summary>Accessible description provided by the source.</summary>
    public string? AccessibilityDescription { get; set; }

    private string _Version = EnterpriseParser.DEFAULT_VERSION;

    private EnterpriseDiagramDirection _Direction = EnterpriseDiagramDirection.Tb;

    private string _Title = string.Empty;

    private readonly Dictionary<string, EnterpriseElement> _Elements = new Dictionary<string, EnterpriseElement>(StringComparer.Ordinal);

    private readonly Dictionary<string, EnterpriseGroup> _Groups = new Dictionary<string, EnterpriseGroup>(StringComparer.Ordinal);

    private readonly List<EnterpriseRelationship> _Relationships = new List<EnterpriseRelationship>();

    /// <summary>Gets or sets the rule-table version (<c>3.1</c> or <c>3.2</c>).</summary>
    public string Version
    {
        get
        {
            return _Version;
        }
        set
        {
            _Version = value ?? EnterpriseParser.DEFAULT_VERSION;
        }
    }

    /// <summary>Gets or sets the layout direction.</summary>
    public EnterpriseDiagramDirection Direction
    {
        get
        {
            return _Direction;
        }
        set
        {
            _Direction = value;
        }
    }

    /// <summary>Gets or sets the optional diagram title.</summary>
    public string Title
    {
        get
        {
            return _Title;
        }
        set
        {
            _Title = value ?? string.Empty;
        }
    }

    /// <summary>Gets the declared elements keyed by id.</summary>
    public Dictionary<string, EnterpriseElement> Elements
    {
        get
        {
            return _Elements;
        }
    }

    /// <summary>Gets the declared groups keyed by id.</summary>
    public Dictionary<string, EnterpriseGroup> Groups
    {
        get
        {
            return _Groups;
        }
    }

    /// <summary>Gets the declared relationships in source order.</summary>
    public List<EnterpriseRelationship> Relationships
    {
        get
        {
            return _Relationships;
        }
    }

    /// <summary>
    /// Returns true when <paramref name="Id"/> is a declared element or group.
    /// </summary>
    /// <param name="Id">Identifier to look up.</param>
    /// <returns>True when the id exists.</returns>
    public bool HasContainer(string Id)
    {
        if (string.IsNullOrEmpty(Id))
        {
            return false;
        }

        return _Elements.ContainsKey(Id) || _Groups.ContainsKey(Id);
    }
}
