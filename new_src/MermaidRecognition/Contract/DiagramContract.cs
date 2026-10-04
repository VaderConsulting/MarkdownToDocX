using MermaidRecognition.Core;

namespace MermaidRecognition.Contract;

/// <summary>
/// Root recognition output for one Mermaid fenced block (spec §4).
/// </summary>
public sealed class DiagramContract
{
    private string? _SourceHash;

    public string? SourceHash
    {
        get => _SourceHash; set => _SourceHash = value;
    }

    private string _Kind = string.Empty;

    public string Kind
    {
        get => _Kind; set => _Kind = value;
    }

    private string _Header = string.Empty;

    public string Header
    {
        get => _Header; set => _Header = value;
    }

    private string? _Direction;

    public string? Direction
    {
        get => _Direction; set => _Direction = value;
    }

    private readonly DiagramConfigContract _Configuration = new DiagramConfigContract();

    public DiagramConfigContract Configuration => _Configuration;

    private readonly List<NodeContract> _Nodes = [];

    public IReadOnlyList<NodeContract> Nodes => _Nodes;

    public List<NodeContract> NodesMutable => _Nodes;

    private readonly List<EdgeContract> _Edges = [];

    public IReadOnlyList<EdgeContract> Edges => _Edges;

    public List<EdgeContract> EdgesMutable => _Edges;

    private readonly List<GroupContract> _Groups = [];

    public IReadOnlyList<GroupContract> Groups => _Groups;

    public List<GroupContract> GroupsMutable => _Groups;

    private readonly List<MermaidDiagnostic> _Diagnostics = [];

    public IReadOnlyList<MermaidDiagnostic> Diagnostics => _Diagnostics;

    public List<MermaidDiagnostic> DiagnosticsMutable => _Diagnostics;

    private readonly List<LinkStyleDirective> _LinkStyles = [];

    public IReadOnlyList<LinkStyleDirective> LinkStyles => _LinkStyles;

    public List<LinkStyleDirective> LinkStylesMutable => _LinkStyles;

    private readonly List<InteractionDirective> _Interactions = [];

    public IReadOnlyList<InteractionDirective> Interactions => _Interactions;

    public List<InteractionDirective> InteractionsMutable => _Interactions;

    private readonly List<ClassDefContract> _ClassDefs = [];

    public IReadOnlyList<ClassDefContract> ClassDefs => _ClassDefs;

    public List<ClassDefContract> ClassDefsMutable => _ClassDefs;

    private readonly DiagramAccessibilityContract _Accessibility = new DiagramAccessibilityContract();

    public DiagramAccessibilityContract Accessibility => _Accessibility;

    private readonly List<SourceStatementContract> _SourceStatements = [];

    /// <summary>
    /// Classified source lines for catalogue gates (§7.4.3); populated for non-graph and L1 kinds.
    /// </summary>
    public IReadOnlyList<SourceStatementContract> SourceStatements => _SourceStatements;

    public List<SourceStatementContract> SourceStatementsMutable => _SourceStatements;

    private MermaidDiagramRemarkStatus _RemarkStatus = MermaidDiagramRemarkStatus.Active;

    /// <summary>
    /// §9.1 catalogue remark status: <see cref="MermaidDiagramRemarkStatus.CatalogueRemarked"/> blocks are excluded from render gates.
    /// </summary>
    public MermaidDiagramRemarkStatus RemarkStatus
    {
        get => _RemarkStatus; set => _RemarkStatus = value;
    }
}
