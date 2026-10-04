namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parse-time subgraph / cluster.
/// </summary>
public sealed class FlowchartSubgraphRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
    }

    private string? _Title;

    public string? Title
    {
        get => _Title; set => _Title = value;
    }

    private string? _ParentSubgraphId;

    public string? ParentSubgraphId
    {
        get => _ParentSubgraphId; set => _ParentSubgraphId = value;
    }

    private FlowchartDirection? _DeclaredDirection;

    public FlowchartDirection? DeclaredDirection
    {
        get => _DeclaredDirection; set => _DeclaredDirection = value;
    }

    private readonly List<string> _NodeIds = [];

    public List<string> NodeIdsMutable => _NodeIds;

    private readonly List<string> _ChildSubgraphIds = [];

    public List<string> ChildSubgraphIdsMutable => _ChildSubgraphIds;

    private readonly List<string> _Classes = [];

    public List<string> ClassesMutable => _Classes;
}
