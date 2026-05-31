namespace MermaidRecognition.Contract;

/// <summary>
/// Root recognition output for one Mermaid fenced block (spec §4).
/// </summary>
public sealed class DiagramContract
{
    private string? _SourceHash;

    public string? SourceHash
    {
        get
        {
            return _SourceHash;
        }
        set
        {
            _SourceHash = value;
        }
    }

    private string _Kind = string.Empty;

    public string Kind
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

    private string _Header = string.Empty;

    public string Header
    {
        get
        {
            return _Header;
        }
        set
        {
            _Header = value;
        }
    }

    private string? _Direction;

    public string? Direction
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

    private readonly DiagramConfigContract _Configuration = new DiagramConfigContract();

    public DiagramConfigContract Configuration
    {
        get
        {
            return _Configuration;
        }
    }

    private readonly List<NodeContract> _Nodes = new List<NodeContract>();

    public IReadOnlyList<NodeContract> Nodes
    {
        get
        {
            return _Nodes;
        }
    }

    public List<NodeContract> NodesMutable
    {
        get
        {
            return _Nodes;
        }
    }

    private readonly List<EdgeContract> _Edges = new List<EdgeContract>();

    public IReadOnlyList<EdgeContract> Edges
    {
        get
        {
            return _Edges;
        }
    }

    public List<EdgeContract> EdgesMutable
    {
        get
        {
            return _Edges;
        }
    }

    private readonly List<GroupContract> _Groups = new List<GroupContract>();

    public IReadOnlyList<GroupContract> Groups
    {
        get
        {
            return _Groups;
        }
    }

    public List<GroupContract> GroupsMutable
    {
        get
        {
            return _Groups;
        }
    }

    private readonly List<MermaidDiagnostic> _Diagnostics = new List<MermaidDiagnostic>();

    public IReadOnlyList<MermaidDiagnostic> Diagnostics
    {
        get
        {
            return _Diagnostics;
        }
    }

    public List<MermaidDiagnostic> DiagnosticsMutable
    {
        get
        {
            return _Diagnostics;
        }
    }

    private readonly List<LinkStyleDirective> _LinkStyles = new List<LinkStyleDirective>();

    public IReadOnlyList<LinkStyleDirective> LinkStyles
    {
        get
        {
            return _LinkStyles;
        }
    }

    public List<LinkStyleDirective> LinkStylesMutable
    {
        get
        {
            return _LinkStyles;
        }
    }

    private readonly List<InteractionDirective> _Interactions = new List<InteractionDirective>();

    public IReadOnlyList<InteractionDirective> Interactions
    {
        get
        {
            return _Interactions;
        }
    }

    public List<InteractionDirective> InteractionsMutable
    {
        get
        {
            return _Interactions;
        }
    }

    private readonly List<ClassDefContract> _ClassDefs = new List<ClassDefContract>();

    public IReadOnlyList<ClassDefContract> ClassDefs
    {
        get
        {
            return _ClassDefs;
        }
    }

    public List<ClassDefContract> ClassDefsMutable
    {
        get
        {
            return _ClassDefs;
        }
    }

    private readonly DiagramAccessibilityContract _Accessibility = new DiagramAccessibilityContract();

    public DiagramAccessibilityContract Accessibility
    {
        get
        {
            return _Accessibility;
        }
    }

    private readonly List<SourceStatementContract> _SourceStatements = new List<SourceStatementContract>();

    /// <summary>
    /// Classified source lines for catalogue gates (§7.4.3); populated for non-graph and L1 kinds.
    /// </summary>
    public IReadOnlyList<SourceStatementContract> SourceStatements
    {
        get
        {
            return _SourceStatements;
        }
    }

    public List<SourceStatementContract> SourceStatementsMutable
    {
        get
        {
            return _SourceStatements;
        }
    }

    private MermaidDiagramRemarkStatus _RemarkStatus = MermaidDiagramRemarkStatus.Active;

    /// <summary>
    /// §9.1 catalogue remark status: <see cref="MermaidDiagramRemarkStatus.CatalogueRemarked"/> blocks are excluded from render gates.
    /// </summary>
    public MermaidDiagramRemarkStatus RemarkStatus
    {
        get
        {
            return _RemarkStatus;
        }
        set
        {
            _RemarkStatus = value;
        }
    }
}
