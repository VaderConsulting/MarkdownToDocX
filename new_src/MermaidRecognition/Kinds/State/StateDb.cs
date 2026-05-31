using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.State;

/// <summary>
/// Mermaid-style state diagram database (parse-time mutations only).
/// </summary>
public sealed class StateDb
{
    private readonly Dictionary<string, StateNodeRecord> _StatesById =
        new Dictionary<string, StateNodeRecord>(StringComparer.Ordinal);

    private readonly List<StateEdgeRecord> _Edges = new List<StateEdgeRecord>();

    private readonly Dictionary<string, StateGroupRecord> _GroupsById =
        new Dictionary<string, StateGroupRecord>(StringComparer.Ordinal);

    private readonly Stack<StateGroupRecord> _CompositeStack = new Stack<StateGroupRecord>();

    private int _StartEndCount;

    private int _NextNoteIndex;

    private readonly List<StateNoteRecord> _Notes = new List<StateNoteRecord>();

    private readonly Dictionary<string, ClassDefContract> _ClassDefsById =
        new Dictionary<string, ClassDefContract>(StringComparer.Ordinal);

    private string? _AccessibilityTitle;

    private string? _AccessibilityDescription;

    private readonly List<InteractionDirective> _Interactions = new List<InteractionDirective>();

    private readonly List<MermaidDiagnostic> _Diagnostics = new List<MermaidDiagnostic>();

    public IReadOnlyDictionary<string, StateNodeRecord> StatesById
    {
        get
        {
            return _StatesById;
        }
    }

    public IReadOnlyList<StateEdgeRecord> Edges
    {
        get
        {
            return _Edges;
        }
    }

    public IReadOnlyDictionary<string, StateGroupRecord> GroupsById
    {
        get
        {
            return _GroupsById;
        }
    }

    public IReadOnlyDictionary<string, ClassDefContract> ClassDefsById
    {
        get
        {
            return _ClassDefsById;
        }
    }

    public IReadOnlyList<StateNoteRecord> Notes
    {
        get
        {
            return _Notes;
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

    public List<InteractionDirective> InteractionsMutable
    {
        get
        {
            return _Interactions;
        }
    }

    public List<MermaidDiagnostic> DiagnosticsMutable
    {
        get
        {
            return _Diagnostics;
        }
    }

    /// <summary>
    /// Gets or creates a state node by identifier.
    /// </summary>
    public StateNodeRecord GetOrCreateState(string StateId)
    {
        if (_StatesById.TryGetValue(StateId, out StateNodeRecord? Existing))
        {
            AddNodeToCurrentCompositeOnce(StateId, Existing);
            return Existing;
        }

        StateNodeRecord Created = new StateNodeRecord();
        Created.Id = StateId;
        Created.Label = StateId;
        _StatesById[StateId] = Created;
        AddNodeToCurrentCompositeOnce(StateId, Created);
        return Created;
    }

    /// <summary>
    /// Sets the display label for an existing or new state.
    /// </summary>
    public void SetStateLabel(string StateId, string Label)
    {
        StateNodeRecord State = GetOrCreateState(StateId);
        State.Label = Label;
    }

    /// <summary>
    /// Sets the pseudo-state type (start, end, fork, join, choice).
    /// </summary>
    public void SetStateType(string StateId, string StateType)
    {
        StateNodeRecord State = GetOrCreateState(StateId);
        State.StateType = StateType;
    }

    /// <summary>
    /// Records a note annotation anchored to one or more states.
    /// </summary>
    public void AddNote(string Placement, string AnchorIds, string Text)
    {
        StateNoteRecord Note = new StateNoteRecord();
        Note.Id = "note-" + _NextNoteIndex.ToString();
        _NextNoteIndex = _NextNoteIndex + 1;
        Note.Placement = Placement;
        Note.AnchorIds = AnchorIds;
        Note.Text = Text;
        _Notes.Add(Note);
    }

    /// <summary>
    /// Opens a composite state group and pushes it onto the containment stack.
    /// </summary>
    public void PushComposite(string GroupId)
    {
        if (!_GroupsById.TryGetValue(GroupId, out StateGroupRecord? Group))
        {
            Group = new StateGroupRecord();
            Group.Id = GroupId;
            _GroupsById[GroupId] = Group;
        }

        if (_CompositeStack.Count > 0)
        {
            StateGroupRecord Parent = _CompositeStack.Peek();
            Group.ParentGroupId = Parent.Id;
        }

        GetOrCreateState(GroupId);
        _CompositeStack.Push(Group);
    }

    /// <summary>
    /// Closes the innermost open composite state group.
    /// </summary>
    public void PopComposite()
    {
        if (_CompositeStack.Count > 0)
        {
            _CompositeStack.Pop();
        }
    }

    /// <summary>
    /// Adds a transition edge, resolving <c>[*]</c> endpoints per Mermaid rules.
    /// </summary>
    public void AddTransition(string SourceRaw, string TargetRaw, string? Label)
    {
        string SourceId = ResolveEndpointId(SourceRaw, true);
        string TargetId = ResolveEndpointId(TargetRaw, false);
        GetOrCreateState(SourceId);
        GetOrCreateState(TargetId);

        StateEdgeRecord Edge = new StateEdgeRecord();
        Edge.SourceId = SourceId;
        Edge.TargetId = TargetId;
        Edge.Label = Label;
        Edge.CreationIndex = _Edges.Count;
        _Edges.Add(Edge);
    }

    string ResolveEndpointId(string RawId, bool IsSource)
    {
        if (!string.Equals(RawId, "[*]", StringComparison.Ordinal))
        {
            return RawId;
        }

        if (_CompositeStack.Count > 0)
        {
            string ParentId = _CompositeStack.Peek().Id;
            string ResolvedId = IsSource ? ParentId + "_start" : ParentId + "_end";
            SetStateType(ResolvedId, IsSource ? "start" : "end");
            return ResolvedId;
        }

        _StartEndCount++;
        string RootId = (IsSource ? "start" : "end") + _StartEndCount.ToString();
        SetStateType(RootId, IsSource ? "start" : "end");
        return RootId;
    }

    void AddNodeToCurrentCompositeOnce(string StateId, StateNodeRecord Node)
    {
        if (_CompositeStack.Count == 0)
        {
            return;
        }

        StateGroupRecord Current = _CompositeStack.Peek();
        if (string.Equals(StateId, Current.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (!Current.ChildNodeIdsMutable.Contains(StateId, StringComparer.Ordinal))
        {
            Current.ChildNodeIdsMutable.Add(StateId);
        }

        Node.ParentGroupId = Current.Id;
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
    /// Applies inline <c>style</c> directives to one or more state nodes.
    /// </summary>
    public void ApplyInlineStyleToTargets(IEnumerable<string> TargetIds, RecognitionStyle Style)
    {
        foreach (string TargetId in TargetIds)
        {
            StateNodeRecord State = GetOrCreateState(TargetId);
            RecognitionStyleMerge.Apply(Style, State.InlineStyle);
        }
    }

    /// <summary>
    /// Applies a named classDef to one or more state nodes.
    /// </summary>
    public void ApplyClassToTargets(IEnumerable<string> TargetIds, string ClassId)
    {
        foreach (string TargetId in TargetIds)
        {
            StateNodeRecord State = GetOrCreateState(TargetId);
            if (!State.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
            {
                State.ClassesMutable.Add(ClassId);
            }

            if (_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? ClassDef))
            {
                RecognitionStyleMerge.Apply(ClassDef.Style, State.InlineStyle);
            }
        }
    }
}
