namespace MermaidRecognition.Sequence;

/// <summary>
/// Parse-time sequence diagram control frame or participant box (diagram database record).
/// </summary>
public sealed class SequenceGroupRecord
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

    private string _BlockKeyword = string.Empty;

    public string BlockKeyword
    {
        get
        {
            return _BlockKeyword;
        }
        set
        {
            _BlockKeyword = value;
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
}
