namespace MermaidRecognition.Contract;

/// <summary>
/// Recognised subgraph / cluster (spec §4.5).
/// </summary>
public sealed class GroupContract
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

    private MermaidLabelType _LabelType = MermaidLabelType.Text;

    public MermaidLabelType LabelType
    {
        get => _LabelType; set => _LabelType = value;
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get => _ParentGroupId; set => _ParentGroupId = value;
    }

    private readonly List<string> _ChildNodeIds = [];

    public IReadOnlyList<string> ChildNodeIds => _ChildNodeIds;

    public List<string> ChildNodeIdsMutable => _ChildNodeIds;

    private readonly List<string> _ChildGroupIds = [];

    public IReadOnlyList<string> ChildGroupIds => _ChildGroupIds;

    public List<string> ChildGroupIdsMutable => _ChildGroupIds;

    private string? _Direction;

    public string? Direction
    {
        get => _Direction; set => _Direction = value;
    }
}
