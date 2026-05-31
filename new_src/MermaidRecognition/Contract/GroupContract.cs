namespace MermaidRecognition.Contract;

/// <summary>
/// Recognised subgraph / cluster (spec §4.5).
/// </summary>
public sealed class GroupContract
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

    private MermaidLabelType _LabelType = MermaidLabelType.Text;

    public MermaidLabelType LabelType
    {
        get
        {
            return _LabelType;
        }
        set
        {
            _LabelType = value;
        }
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get
        {
            return _ParentGroupId;
        }
        set
        {
            _ParentGroupId = value;
        }
    }

    private readonly List<string> _ChildNodeIds = new List<string>();

    public IReadOnlyList<string> ChildNodeIds
    {
        get
        {
            return _ChildNodeIds;
        }
    }

    public List<string> ChildNodeIdsMutable
    {
        get
        {
            return _ChildNodeIds;
        }
    }

    private readonly List<string> _ChildGroupIds = new List<string>();

    public IReadOnlyList<string> ChildGroupIds
    {
        get
        {
            return _ChildGroupIds;
        }
    }

    public List<string> ChildGroupIdsMutable
    {
        get
        {
            return _ChildGroupIds;
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
}
