namespace MermaidRecognition.Class;

/// <summary>
/// Parse-time class diagram namespace / group.
/// </summary>
public sealed class ClassNamespaceRecord
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

    private string? _ParentNamespaceId;

    public string? ParentNamespaceId
    {
        get
        {
            return _ParentNamespaceId;
        }
        set
        {
            _ParentNamespaceId = value;
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

    private readonly List<string> _ChildNamespaceIds = new List<string>();

    public List<string> ChildNamespaceIdsMutable
    {
        get
        {
            return _ChildNamespaceIds;
        }
    }
}
