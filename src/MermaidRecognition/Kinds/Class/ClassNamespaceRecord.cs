namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parse-time class diagram namespace / group.
/// </summary>
public sealed class ClassNamespaceRecord
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

    private string? _ParentNamespaceId;

    public string? ParentNamespaceId
    {
        get => _ParentNamespaceId; set => _ParentNamespaceId = value;
    }

    private readonly List<string> _ChildNodeIds = [];

    public List<string> ChildNodeIdsMutable => _ChildNodeIds;

    private readonly List<string> _ChildNamespaceIds = [];

    public List<string> ChildNamespaceIdsMutable => _ChildNamespaceIds;
}
