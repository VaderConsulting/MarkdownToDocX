using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Er;

/// <summary>
/// Mermaid-style ER diagram database (parse-time mutations only).
/// </summary>
public sealed class ErDb
{
    private readonly Dictionary<string, ErNodeRecord> _EntitiesById =
        new Dictionary<string, ErNodeRecord>(StringComparer.Ordinal);

    private readonly List<ErEdgeRecord> _Edges = new List<ErEdgeRecord>();

    private int _EdgeCreationIndex;

    private readonly Dictionary<string, ClassDefContract> _ClassDefsById =
        new Dictionary<string, ClassDefContract>(StringComparer.Ordinal);

    private string? _AccessibilityTitle;

    private string? _AccessibilityDescription;

    public IReadOnlyDictionary<string, ErNodeRecord> EntitiesById
    {
        get
        {
            return _EntitiesById;
        }
    }

    public IReadOnlyList<ErEdgeRecord> Edges
    {
        get
        {
            return _Edges;
        }
    }

    public IReadOnlyDictionary<string, ClassDefContract> ClassDefsById
    {
        get
        {
            return _ClassDefsById;
        }
    }

    public string? AccessibilityTitle
    {
        get
        {
            return _AccessibilityTitle;
        }
        set
        {
            _AccessibilityTitle = value;
        }
    }

    public string? AccessibilityDescription
    {
        get
        {
            return _AccessibilityDescription;
        }
        set
        {
            _AccessibilityDescription = value;
        }
    }

    /// <summary>
    /// Gets or creates an entity node by identifier.
    /// </summary>
    public ErNodeRecord GetOrCreateEntity(string EntityId)
    {
        if (_EntitiesById.TryGetValue(EntityId, out ErNodeRecord? Existing))
        {
            return Existing;
        }

        ErNodeRecord Created = new ErNodeRecord();
        Created.Id = EntityId;
        Created.Label = EntityId;
        _EntitiesById[EntityId] = Created;
        return Created;
    }

    /// <summary>
    /// Records a relationship edge between two entities.
    /// </summary>
    public void AddRelationship(
        string SourceId,
        string TargetId,
        string LeftMarker,
        string Link,
        string RightMarker,
        string? RoleLabel)
    {
        GetOrCreateEntity(SourceId);
        GetOrCreateEntity(TargetId);

        ErEdgeRecord Edge = new ErEdgeRecord();
        Edge.SourceId = SourceId;
        Edge.TargetId = TargetId;
        Edge.LeftMarker = LeftMarker;
        Edge.Link = Link;
        Edge.RightMarker = RightMarker;
        Edge.RoleLabel = RoleLabel;
        Edge.CreationIndex = _EdgeCreationIndex;
        _EdgeCreationIndex++;
        _Edges.Add(Edge);
    }

    /// <summary>
    /// Registers or merges a <c>classDef</c> style template.
    /// </summary>
    public void RegisterClassDef(string ClassId, RecognitionStyle Style)
    {
        if (!_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? Existing))
        {
            Existing = new ClassDefContract();
            Existing.ClassId = ClassId;
            _ClassDefsById[ClassId] = Existing;
        }

        RecognitionStyleMerge.Apply(Style, Existing.Style);
    }

    /// <summary>
    /// Applies inline <c>style</c> directives to one or more entity nodes.
    /// </summary>
    public void ApplyInlineStyleToTargets(IEnumerable<string> TargetIds, RecognitionStyle Style)
    {
        foreach (string TargetId in TargetIds)
        {
            ErNodeRecord Entity = GetOrCreateEntity(TargetId);
            RecognitionStyleMerge.Apply(Style, Entity.InlineStyle);
        }
    }

    /// <summary>
    /// Applies a named classDef to one or more entity nodes.
    /// </summary>
    public void ApplyClassToTargets(IEnumerable<string> TargetIds, string ClassId)
    {
        foreach (string TargetId in TargetIds)
        {
            ErNodeRecord Entity = GetOrCreateEntity(TargetId);
            if (!Entity.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
            {
                Entity.ClassesMutable.Add(ClassId);
            }

            if (_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? ClassDef))
            {
                RecognitionStyleMerge.Apply(ClassDef.Style, Entity.InlineStyle);
            }
        }
    }
}
