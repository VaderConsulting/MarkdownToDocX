namespace MermaidRecognition.State;

/// <summary>
/// Parse-time composite state group record.
/// </summary>
public sealed class StateGroupRecord
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

    public List<string> ChildNodeIdsMutable
    {
        get
        {
            return _ChildNodeIds;
        }
    }
}
