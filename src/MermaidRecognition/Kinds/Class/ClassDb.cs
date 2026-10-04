using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Mermaid-style class diagram database (parse-time mutations only).
/// </summary>
public sealed class ClassDb
{
    private readonly Dictionary<string, ClassNodeRecord> _ClassesById =
        new Dictionary<string, ClassNodeRecord>(StringComparer.Ordinal);

    private readonly List<ClassEdgeRecord> _Edges = [];

    private readonly Dictionary<string, ClassDefContract> _ClassDefsById =
        new Dictionary<string, ClassDefContract>(StringComparer.Ordinal);

    private readonly Dictionary<string, ClassNamespaceRecord> _NamespacesById =
        new Dictionary<string, ClassNamespaceRecord>(StringComparer.Ordinal);

    private readonly Stack<ClassNamespaceRecord> _NamespaceStack = new Stack<ClassNamespaceRecord>();

    private readonly Dictionary<string, string> _ParentNamespaceByNodeId =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private string? _AccessibilityTitle;

    private string? _AccessibilityDescription;

    private readonly List<InteractionDirective> _Interactions = [];

    private readonly List<MermaidDiagnostic> _Diagnostics = [];

    private int _StandaloneNoteCounter;

    public IReadOnlyDictionary<string, ClassNodeRecord> ClassesById => _ClassesById;

    public IReadOnlyList<ClassEdgeRecord> Edges => _Edges;

    public IReadOnlyDictionary<string, ClassDefContract> ClassDefsById => _ClassDefsById;

    public IReadOnlyDictionary<string, ClassNamespaceRecord> NamespacesById => _NamespacesById;

    public string? AccessibilityTitle
    {
        get => _AccessibilityTitle; set => _AccessibilityTitle = value;
    }

    public string? AccessibilityDescription
    {
        get => _AccessibilityDescription; set => _AccessibilityDescription = value;
    }

    public List<InteractionDirective> InteractionsMutable => _Interactions;

    public List<MermaidDiagnostic> DiagnosticsMutable => _Diagnostics;

    /// <summary>
    /// Gets or creates a class node by identifier.
    /// </summary>
    public ClassNodeRecord GetOrCreateClass(string ClassId)
    {
        if (_ClassesById.TryGetValue(ClassId, out ClassNodeRecord? Existing))
        {
            AddNodeToCurrentNamespaceOnce(ClassId, Existing);
            return Existing;
        }

        ClassNodeRecord Created = new ClassNodeRecord
        {
            Id = ClassId,
            Label = ClassId
        };
        _ClassesById[ClassId] = Created;
        AddNodeToCurrentNamespaceOnce(ClassId, Created);
        return Created;
    }

    /// <summary>
    /// Opens a namespace group and pushes it onto the containment stack.
    /// </summary>
    public void PushNamespace(string NamespaceId, string? Title)
    {
        if (!_NamespacesById.TryGetValue(NamespaceId, out ClassNamespaceRecord? Namespace))
        {
            Namespace = new ClassNamespaceRecord
            {
                Id = NamespaceId
            };
            _NamespacesById[NamespaceId] = Namespace;
        }

        if (Title is not null)
        {
            Namespace.Title = Title;
        }

        if (_NamespaceStack.Count > 0)
        {
            ClassNamespaceRecord Parent = _NamespaceStack.Peek();
            Namespace.ParentNamespaceId = Parent.Id;
            if (!Parent.ChildNamespaceIdsMutable.Contains(NamespaceId, StringComparer.Ordinal))
            {
                Parent.ChildNamespaceIdsMutable.Add(NamespaceId);
            }
        }

        _NamespaceStack.Push(Namespace);
    }

    /// <summary>
    /// Closes the innermost open namespace group.
    /// </summary>
    public void PopNamespace()
    {
        if (_NamespaceStack.Count > 0)
        {
            _NamespaceStack.Pop();
        }
    }

    void AddNodeToCurrentNamespaceOnce(string ClassId, ClassNodeRecord Node)
    {
        if (_NamespaceStack.Count == 0)
        {
            return;
        }

        if (_ParentNamespaceByNodeId.ContainsKey(ClassId))
        {
            return;
        }

        ClassNamespaceRecord Current = _NamespaceStack.Peek();
        if (!Current.ChildNodeIdsMutable.Contains(ClassId, StringComparer.Ordinal))
        {
            Current.ChildNodeIdsMutable.Add(ClassId);
        }

        Node.ParentNamespaceId = Current.Id;
        _ParentNamespaceByNodeId[ClassId] = Current.Id;
    }

    /// <summary>
    /// Ensures class nodes exist for the given identifiers.
    /// </summary>
    public void EnsureClassIdsExist(IEnumerable<string> ClassIds)
    {
        foreach (string ClassId in ClassIds)
        {
            GetOrCreateClass(ClassId);
        }
    }

    /// <summary>
    /// Adds a member declaration to the owning class.
    /// </summary>
    public void AddMember(string ClassId, string RawText, MermaidSourceSpan SourceSpan)
    {
        ClassNodeRecord ClassNode = GetOrCreateClass(ClassId);
        ClassNode.DeclarationsMutable.Add(new MermaidDeclaration("member", RawText, SourceSpan));
    }

    /// <summary>
    /// Adds a stereotype annotation declaration to the owning class.
    /// </summary>
    public void AddStereotype(string ClassId, string RawText, MermaidSourceSpan SourceSpan)
    {
        ClassNodeRecord ClassNode = GetOrCreateClass(ClassId);
        ClassNode.DeclarationsMutable.Add(new MermaidDeclaration("stereotype", RawText, SourceSpan));
    }

    /// <summary>
    /// Adds a relation edge between two classes.
    /// </summary>
    public void AddRelation(
        string SourceId,
        string TargetId,
        string RelationToken,
        string? SourceCardinality,
        string? TargetCardinality,
        string? RelationLabel)
    {
        EnsureClassIdsExist(new[] { SourceId, TargetId });
        ClassEdgeRecord Edge = new ClassEdgeRecord
        {
            SourceId = SourceId,
            TargetId = TargetId,
            RelationToken = RelationToken,
            SourceCardinality = SourceCardinality,
            TargetCardinality = TargetCardinality,
            RelationLabel = RelationLabel,
            CreationIndex = _Edges.Count
        };
        _Edges.Add(Edge);
    }

    /// <summary>
    /// Registers or merges a <c>classDef</c> style template.
    /// </summary>
    public void RegisterClassDef(string ClassId, RecognitionStyle Style)
    {
        if (!_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? Existing))
        {
            Existing = new ClassDefContract
            {
                ClassId = ClassId
            };
            _ClassDefsById[ClassId] = Existing;
        }

        RecognitionStyleMerge.Apply(Style, Existing.Style);
    }

    /// <summary>
    /// Applies inline <c>style</c> directives to one or more class nodes.
    /// </summary>
    public void ApplyInlineStyleToTargets(IEnumerable<string> TargetIds, RecognitionStyle Style)
    {
        foreach (string TargetId in TargetIds)
        {
            ClassNodeRecord ClassNode = GetOrCreateClass(TargetId);
            RecognitionStyleMerge.Apply(Style, ClassNode.InlineStyle);
        }
    }

    /// <summary>
    /// Attaches a note declaration to an existing or newly created class node.
    /// </summary>
    public void AddNoteForClass(string ClassId, string Text, MermaidSourceSpan SourceSpan)
    {
        ClassNodeRecord ClassNode = GetOrCreateClass(ClassId);
        ClassNode.DeclarationsMutable.Add(new MermaidDeclaration("note", Text, SourceSpan));
    }

    /// <summary>
    /// Creates a synthetic note node for a standalone diagram note.
    /// </summary>
    public void AddStandaloneNote(string Text, MermaidSourceSpan SourceSpan)
    {
        string NoteId = "note-" + _StandaloneNoteCounter.ToString();
        _StandaloneNoteCounter++;

        ClassNodeRecord NoteNode = new ClassNodeRecord
        {
            Id = NoteId,
            Label = Text
        };
        NoteNode.DeclarationsMutable.Add(new MermaidDeclaration("note", Text, SourceSpan));
        _ClassesById[NoteId] = NoteNode;
        AddNodeToCurrentNamespaceOnce(NoteId, NoteNode);
    }

    /// <summary>
    /// Applies a named classDef to one or more class nodes.
    /// </summary>
    public void ApplyClassToTargets(IEnumerable<string> TargetIds, string ClassId)
    {
        foreach (string TargetId in TargetIds)
        {
            ClassNodeRecord ClassNode = GetOrCreateClass(TargetId);
            if (!ClassNode.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
            {
                ClassNode.ClassesMutable.Add(ClassId);
            }

            if (_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? ClassDef))
            {
                RecognitionStyleMerge.Apply(ClassDef.Style, ClassNode.InlineStyle);
            }
        }
    }
}
