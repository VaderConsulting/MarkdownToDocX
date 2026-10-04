namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parse-time sequence diagram control frame or participant box (diagram database record).
/// </summary>
public sealed class SequenceGroupRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
    }

    private string _BlockKeyword = string.Empty;

    public string BlockKeyword
    {
        get => _BlockKeyword; set => _BlockKeyword = value;
    }

    private string? _Title;

    public string? Title
    {
        get => _Title; set => _Title = value;
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
}
