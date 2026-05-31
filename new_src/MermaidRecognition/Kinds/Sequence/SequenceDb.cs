namespace MermaidRecognition.Sequence;

/// <summary>
/// Mermaid-style sequence diagram database (parse-time mutations only).
/// </summary>
public sealed class SequenceDb
{
    private readonly Dictionary<string, SequenceNodeRecord> _ParticipantsById =
        new Dictionary<string, SequenceNodeRecord>(StringComparer.Ordinal);

    private readonly List<SequenceEdgeRecord> _Messages = new List<SequenceEdgeRecord>();

    private readonly Dictionary<string, SequenceGroupRecord> _GroupsById =
        new Dictionary<string, SequenceGroupRecord>(StringComparer.Ordinal);

    private readonly Stack<string> _EndStack = new Stack<string>();

    private string? _ActiveGroupId;

    private string? _MultiBranchFrameRootId;

    private int _NextBlockIndex;

    private int _NextCreationIndex;

    private int _NextNoteIndex;

    private readonly List<SequenceNoteRecord> _Notes = new List<SequenceNoteRecord>();

    private string? _AccessibilityTitle;

    private string? _AccessibilityDescription;

    public IReadOnlyDictionary<string, SequenceNodeRecord> ParticipantsById
    {
        get
        {
            return _ParticipantsById;
        }
    }

    public IReadOnlyList<SequenceEdgeRecord> Messages
    {
        get
        {
            return _Messages;
        }
    }

    public IReadOnlyDictionary<string, SequenceGroupRecord> GroupsById
    {
        get
        {
            return _GroupsById;
        }
    }

    public IReadOnlyList<SequenceNoteRecord> Notes
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

    /// <summary>
    /// Returns an existing participant or creates one with the supplied label and draw type.
    /// </summary>
    public SequenceNodeRecord GetOrCreateParticipant(string Id, string? Label, string DrawType)
    {
        if (_ParticipantsById.TryGetValue(Id, out SequenceNodeRecord? Existing))
        {
            if (Label is not null && Existing.Label is null)
            {
                Existing.Label = Label;
            }

            if (!string.Equals(Existing.DrawType, "participant", StringComparison.Ordinal)
                && string.Equals(DrawType, "actor", StringComparison.Ordinal))
            {
                Existing.DrawType = DrawType;
            }

            AssignParticipantToActiveBox(Id, Existing);
            return Existing;
        }

        SequenceNodeRecord Created = new SequenceNodeRecord();
        Created.Id = Id;
        Created.Label = Label ?? Id;
        Created.DrawType = DrawType;
        _ParticipantsById[Id] = Created;
        AssignParticipantToActiveBox(Id, Created);
        return Created;
    }

    /// <summary>
    /// Ensures implicit participants exist for message endpoints.
    /// </summary>
    public void EnsureImplicitParticipant(string Id)
    {
        GetOrCreateParticipant(Id, null, "participant");
    }

    /// <summary>
    /// Records a directed message between participants.
    /// </summary>
    public void AddMessage(
        string SourceId,
        string TargetId,
        string? Label,
        string ArrowToken,
        string? ActivateTargetId,
        string? DeactivateSourceId)
    {
        EnsureImplicitParticipant(SourceId);
        EnsureImplicitParticipant(TargetId);

        SequenceEdgeRecord Message = new SequenceEdgeRecord();
        Message.CreationIndex = _NextCreationIndex;
        _NextCreationIndex = _NextCreationIndex + 1;
        Message.SourceId = SourceId;
        Message.TargetId = TargetId;
        Message.Label = Label;
        Message.ArrowToken = ArrowToken;
        Message.ActivateTargetId = ActivateTargetId;
        Message.DeactivateSourceId = DeactivateSourceId;
        Message.ParentGroupId = _ActiveGroupId;
        _Messages.Add(Message);

        if (ActivateTargetId is not null)
        {
            SequenceNodeRecord Target = GetOrCreateParticipant(ActivateTargetId, null, "participant");
            Target.IsActive = true;
        }

        if (DeactivateSourceId is not null)
        {
            SequenceNodeRecord Source = GetOrCreateParticipant(DeactivateSourceId, null, "participant");
            Source.IsActive = false;
        }
    }

    /// <summary>
    /// Marks a participant as active from an explicit activate statement.
    /// </summary>
    public void ActivateParticipant(string Id)
    {
        SequenceNodeRecord Participant = GetOrCreateParticipant(Id, null, "participant");
        Participant.IsActive = true;
    }

    /// <summary>
    /// Marks a participant as inactive from an explicit deactivate statement.
    /// </summary>
    public void DeactivateParticipant(string Id)
    {
        SequenceNodeRecord Participant = GetOrCreateParticipant(Id, null, "participant");
        Participant.IsActive = false;
    }

    /// <summary>
    /// Records a note annotation anchored to one or more participants.
    /// </summary>
    public void AddNote(string Placement, string AnchorIds, string Text)
    {
        SequenceNoteRecord Note = new SequenceNoteRecord();
        Note.Id = "note-" + _NextNoteIndex.ToString();
        _NextNoteIndex = _NextNoteIndex + 1;
        Note.Placement = Placement;
        Note.AnchorIds = AnchorIds;
        Note.Text = Text;
        _Notes.Add(Note);
    }

    /// <summary>
    /// Opens a control-block frame or branch group.
    /// </summary>
    public void OpenBlock(SequenceBlockParseResult Block)
    {
        if (Block.IsBranchKeyword)
        {
            OpenBranchBlock(Block);
            return;
        }

        SequenceGroupRecord Group = CreateGroup(Block.Keyword, Block.Title);
        if (_EndStack.Count > 0)
        {
            SequenceGroupRecord Parent = GetGroup(_EndStack.Peek());
            Group.ParentGroupId = Parent.Id;
            if (!Parent.ChildGroupIdsMutable.Contains(Group.Id, StringComparer.Ordinal))
            {
                Parent.ChildGroupIdsMutable.Add(Group.Id);
            }
        }

        _EndStack.Push(Group.Id);
        _ActiveGroupId = Group.Id;

        if (Block.IsMultiBranchFrame)
        {
            _MultiBranchFrameRootId = Group.Id;
        }
    }

    /// <summary>
    /// Opens a participant box group.
    /// </summary>
    public void OpenBox(SequenceBoxParseResult Box)
    {
        SequenceGroupRecord Group = CreateGroup("box", Box.Title);
        if (_EndStack.Count > 0)
        {
            SequenceGroupRecord Parent = GetGroup(_EndStack.Peek());
            Group.ParentGroupId = Parent.Id;
            if (!Parent.ChildGroupIdsMutable.Contains(Group.Id, StringComparer.Ordinal))
            {
                Parent.ChildGroupIdsMutable.Add(Group.Id);
            }
        }

        _EndStack.Push(Group.Id);
        _ActiveGroupId = Group.Id;
    }

    /// <summary>
    /// Closes the innermost open block or box frame.
    /// </summary>
    public void CloseBlock()
    {
        if (_EndStack.Count == 0)
        {
            _ActiveGroupId = null;
            _MultiBranchFrameRootId = null;
            return;
        }

        string ClosedGroupId = _EndStack.Pop();
        if (string.Equals(_MultiBranchFrameRootId, ClosedGroupId, StringComparison.Ordinal))
        {
            _MultiBranchFrameRootId = null;
        }

        if (_EndStack.Count > 0)
        {
            _ActiveGroupId = _EndStack.Peek();
        }
        else
        {
            _ActiveGroupId = null;
        }
    }

    void OpenBranchBlock(SequenceBlockParseResult Block)
    {
        if (_MultiBranchFrameRootId is null)
        {
            SequenceGroupRecord Group = CreateGroup(Block.Keyword, Block.Title);
            _EndStack.Push(Group.Id);
            _ActiveGroupId = Group.Id;
            return;
        }

        SequenceGroupRecord Branch = CreateGroup(Block.Keyword, Block.Title);
        Branch.ParentGroupId = _MultiBranchFrameRootId;
        SequenceGroupRecord FrameRoot = GetGroup(_MultiBranchFrameRootId);
        if (!FrameRoot.ChildGroupIdsMutable.Contains(Branch.Id, StringComparer.Ordinal))
        {
            FrameRoot.ChildGroupIdsMutable.Add(Branch.Id);
        }

        _ActiveGroupId = Branch.Id;
    }

    SequenceGroupRecord CreateGroup(string Keyword, string? Title)
    {
        string GroupId = Keyword + "-" + _NextBlockIndex.ToString();
        _NextBlockIndex = _NextBlockIndex + 1;

        SequenceGroupRecord Group = new SequenceGroupRecord();
        Group.Id = GroupId;
        Group.BlockKeyword = Keyword;
        Group.Title = Title;
        _GroupsById[GroupId] = Group;
        return Group;
    }

    SequenceGroupRecord GetGroup(string GroupId)
    {
        if (_GroupsById.TryGetValue(GroupId, out SequenceGroupRecord? Group))
        {
            return Group;
        }

        throw new InvalidOperationException("Unknown sequence group id: " + GroupId);
    }

    void AssignParticipantToActiveBox(string ParticipantId, SequenceNodeRecord Participant)
    {
        if (_ActiveGroupId is null)
        {
            return;
        }

        SequenceGroupRecord ActiveGroup = GetGroup(_ActiveGroupId);
        if (!string.Equals(ActiveGroup.BlockKeyword, "box", StringComparison.Ordinal))
        {
            return;
        }

        Participant.ParentGroupId = ActiveGroup.Id;
        if (!ActiveGroup.ChildNodeIdsMutable.Contains(ParticipantId, StringComparer.Ordinal))
        {
            ActiveGroup.ChildNodeIdsMutable.Add(ParticipantId);
        }
    }
}
