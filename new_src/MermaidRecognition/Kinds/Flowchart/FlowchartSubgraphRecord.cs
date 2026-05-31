namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parse-time subgraph / cluster.
/// </summary>
public sealed class FlowchartSubgraphRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value;
        }
    }

    private string? _Title;

    public string? Title
    {
        get
        {
            return _Title;
        }
        set
        {
            _Title = value;
        }
    }

    private string? _ParentSubgraphId;

    public string? ParentSubgraphId
    {
        get
        {
            return _ParentSubgraphId;
        }
        set
        {
            _ParentSubgraphId = value;
        }
    }

    private FlowchartDirection? _DeclaredDirection;

    public FlowchartDirection? DeclaredDirection
    {
        get
        {
            return _DeclaredDirection;
        }
        set
        {
            _DeclaredDirection = value;
        }
    }

    private readonly List<string> _NodeIds = new List<string>();

    public List<string> NodeIdsMutable
    {
        get
        {
            return _NodeIds;
        }
    }

    private readonly List<string> _ChildSubgraphIds = new List<string>();

    public List<string> ChildSubgraphIdsMutable
    {
        get
        {
            return _ChildSubgraphIds;
        }
    }

    private readonly List<string> _Classes = new List<string>();

    public List<string> ClassesMutable
    {
        get
        {
            return _Classes;
        }
    }
}
