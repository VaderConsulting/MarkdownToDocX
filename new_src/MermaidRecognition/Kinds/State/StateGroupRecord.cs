namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parse-time composite state group record.
/// </summary>
public sealed class StateGroupRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get => _ParentGroupId; set => _ParentGroupId = value;
    }

    private readonly List<string> _ChildNodeIds = [];

    public List<string> ChildNodeIdsMutable => _ChildNodeIds;
}
