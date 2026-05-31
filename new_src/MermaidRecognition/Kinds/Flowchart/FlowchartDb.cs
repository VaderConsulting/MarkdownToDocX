namespace MermaidRecognition.Flowchart;

/// <summary>
/// Mermaid-style flowchart diagram database (parse-time mutations only). Mirrors <c>flowDb.ts</c> responsibilities.
/// </summary>
public sealed class FlowchartDb
{
    private readonly Dictionary<string, FlowchartNodeRecord> _NodesById =
        new Dictionary<string, FlowchartNodeRecord>(StringComparer.Ordinal);

    private readonly List<FlowchartEdgeRecord> _Edges = new List<FlowchartEdgeRecord>();

    private readonly Dictionary<string, FlowchartSubgraphRecord> _SubgraphById =
        new Dictionary<string, FlowchartSubgraphRecord>(StringComparer.Ordinal);

    private readonly Stack<FlowchartSubgraphRecord> _SubgraphStack = new Stack<FlowchartSubgraphRecord>();

    private readonly Dictionary<string, string> _ParentSubgraphByNodeId =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly List<MermaidDiagnostic> _Diagnostics = new List<MermaidDiagnostic>();

    private readonly List<LinkStyleDirective> _LinkStyles = new List<LinkStyleDirective>();

    private readonly List<InteractionDirective> _Interactions = new List<InteractionDirective>();

    private readonly Dictionary<string, ClassDefContract> _ClassDefsById =
        new Dictionary<string, ClassDefContract>(StringComparer.Ordinal);

    private string? _AccessibilityTitle;

    private string? _AccessibilityDescription;

    private FlowchartDirection _Direction = FlowchartDirection.TopToBottom;

    private string _Header = string.Empty;

    public string Header
    {
        get
        {
            return _Header;
        }
        set
        {
            _Header = value;
        }
    }

    public FlowchartDirection Direction
    {
        get
        {
            return _Direction;
        }
        set
        {
            _Direction = value;
        }
    }

    public IReadOnlyDictionary<string, FlowchartNodeRecord> NodesById
    {
        get
        {
            return _NodesById;
        }
    }

    public IReadOnlyList<FlowchartEdgeRecord> Edges
    {
        get
        {
            return _Edges;
        }
    }

    public List<MermaidDiagnostic> DiagnosticsMutable
    {
        get
        {
            return _Diagnostics;
        }
    }

    public List<LinkStyleDirective> LinkStylesMutable
    {
        get
        {
            return _LinkStyles;
        }
    }

    public List<InteractionDirective> InteractionsMutable
    {
        get
        {
            return _Interactions;
        }
    }

    public IReadOnlyDictionary<string, FlowchartSubgraphRecord> SubgraphsById
    {
        get
        {
            return _SubgraphById;
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

    public FlowchartNodeRecord GetOrCreateNode(string NodeId)
    {
        if (_NodesById.TryGetValue(NodeId, out FlowchartNodeRecord? Existing))
        {
            return Existing;
        }

        FlowchartNodeRecord Created = new FlowchartNodeRecord();
        Created.Id = NodeId;
        Created.Label = NodeId;
        _NodesById[NodeId] = Created;
        return Created;
    }

    public void EnsureNodeIdsExist(IEnumerable<string> NodeIds)
    {
        foreach (string NodeId in NodeIds)
        {
            GetOrCreateNode(NodeId);
        }
    }

    public void AddLink(
        FlowchartNodeSet Starts,
        FlowchartNodeSet Ends,
        FlowchartLinkToken Token,
        int StatementLineIndex,
        int LinkOrdinalInStatement,
        string LinkSegmentText)
    {
        List<string> StartItems = Starts.ItemsMutable;
        List<string> EndItems = Ends.ItemsMutable;
        if (StartItems.Count == 0 || EndItems.Count == 0)
        {
            return;
        }

        EnsureNodeIdsExist(StartItems);
        EnsureNodeIdsExist(EndItems);
        foreach (string NodeId in StartItems.Concat(EndItems))
        {
            AddNodeToCurrentSubgraphOnce(NodeId);
        }

        string? UserId = Token.UserDefinedEdgeId;
        HashSet<string> UsedUserIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FlowchartEdgeRecord Existing in _Edges)
        {
            if (Existing.IsUserDefinedId && Existing.Id is not null)
            {
                UsedUserIds.Add(Existing.Id);
            }
        }

        for (int StartIndex = 0; StartIndex < StartItems.Count; StartIndex++)
        {
            string StartId = StartItems[StartIndex];
            for (int EndIndex = 0; EndIndex < EndItems.Count; EndIndex++)
            {
                string EndId = EndItems[EndIndex];
                bool IsLastStart = StartIndex == StartItems.Count - 1;
                bool IsFirstEnd = EndIndex == 0;
                string? EdgeId = null;
                bool IsUserDefined = false;
                if (UserId is not null && IsLastStart && IsFirstEnd && !UsedUserIds.Contains(UserId))
                {
                    EdgeId = UserId;
                    IsUserDefined = true;
                    UsedUserIds.Add(UserId);
                }

                FlowchartEdgeRecord Edge = new FlowchartEdgeRecord();
                Edge.SourceId = StartId;
                Edge.TargetId = EndId;
                Edge.CreationIndex = _Edges.Count;
                Edge.MinLength = Token.MinLength;
                Edge.Stroke = Token.Stroke;
                Edge.MarkerStart = Token.MarkerStart;
                Edge.MarkerEnd = Token.MarkerEnd;
                Edge.Label = string.IsNullOrEmpty(Token.Label)
                    ? Token.Label
                    : FlowchartLabelNormalizer.Normalize(Token.Label);
                Edge.Id = EdgeId ?? GenerateEdgeId(StartId, EndId, Edge.CreationIndex);
                Edge.IsUserDefinedId = IsUserDefined;
                Edge.ExpansionTrace = new EdgeExpansionTrace(
                    new MermaidSourceSpan(0, 0, StatementLineIndex, 0, StatementLineIndex, 0),
                    LinkOrdinalInStatement,
                    StartIndex,
                    EndIndex,
                    new List<string>(StartItems),
                    new List<string>(EndItems),
                    LinkSegmentText);
                _Edges.Add(Edge);
            }
        }
    }

    public void PushSubgraph(string SubgraphId, string? Title)
    {
        FlowchartSubgraphRecord Subgraph = new FlowchartSubgraphRecord();
        Subgraph.Id = SubgraphId;
        Subgraph.Title = Title;
        if (_SubgraphStack.Count > 0)
        {
            FlowchartSubgraphRecord Parent = _SubgraphStack.Peek();
            Subgraph.ParentSubgraphId = Parent.Id;
            Parent.ChildSubgraphIdsMutable.Add(SubgraphId);
        }

        _SubgraphById[SubgraphId] = Subgraph;
        _SubgraphStack.Push(Subgraph);
    }

    public void PopSubgraph()
    {
        if (_SubgraphStack.Count > 0)
        {
            _SubgraphStack.Pop();
        }
    }

    /// <summary>
    /// Sets layout direction on the innermost open subgraph.
    /// </summary>
    public void SetCurrentSubgraphDirection(FlowchartDirection Direction)
    {
        if (_SubgraphStack.Count == 0)
        {
            return;
        }

        _SubgraphStack.Peek().DeclaredDirection = Direction;
    }

    public void AddNodeToCurrentSubgraphOnce(string NodeId)
    {
        if (_SubgraphStack.Count == 0)
        {
            return;
        }

        if (_ParentSubgraphByNodeId.ContainsKey(NodeId))
        {
            return;
        }

        FlowchartSubgraphRecord Current = _SubgraphStack.Peek();
        Current.NodeIdsMutable.Add(NodeId);
        _ParentSubgraphByNodeId[NodeId] = Current.Id;
        FlowchartNodeRecord Node = GetOrCreateNode(NodeId);
        Node.ParentSubgraphId = Current.Id;
    }

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

    public void ApplyInlineStyleToTargets(IEnumerable<string> TargetIds, RecognitionStyle Style)
    {
        foreach (string TargetId in TargetIds)
        {
            if (_NodesById.TryGetValue(TargetId, out FlowchartNodeRecord? Node))
            {
                RecognitionStyleMerge.Apply(Style, Node.InlineStyle);
                continue;
            }

            if (_SubgraphById.TryGetValue(TargetId, out FlowchartSubgraphRecord? Subgraph))
            {
                foreach (string NodeId in Subgraph.NodeIdsMutable)
                {
                    RecognitionStyleMerge.Apply(Style, GetOrCreateNode(NodeId).InlineStyle);
                }

                continue;
            }

            FlowchartEdgeRecord? Edge = FindEdgeById(TargetId);
            if (Edge is not null)
            {
                if (Style.Stroke is not null)
                {
                    Edge.Stroke = Style.Stroke;
                }

                continue;
            }

            RecognitionStyleMerge.Apply(Style, GetOrCreateNode(TargetId).InlineStyle);
        }
    }

    public void ApplyClassToTargets(IEnumerable<string> TargetIds, string ClassId)
    {
        foreach (string TargetId in TargetIds)
        {
            if (_NodesById.TryGetValue(TargetId, out FlowchartNodeRecord? Node))
            {
                if (!Node.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
                {
                    Node.ClassesMutable.Add(ClassId);
                }

                if (_ClassDefsById.TryGetValue(ClassId, out ClassDefContract? ClassDef))
                {
                    RecognitionStyleMerge.Apply(ClassDef.Style, Node.InlineStyle);
                }

                continue;
            }

            if (_SubgraphById.TryGetValue(TargetId, out FlowchartSubgraphRecord? Subgraph))
            {
                if (!Subgraph.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
                {
                    Subgraph.ClassesMutable.Add(ClassId);
                }

                foreach (string NodeId in Subgraph.NodeIdsMutable)
                {
                    ApplyClassToTargets(new[] { NodeId }, ClassId);
                }

                continue;
            }

            FlowchartEdgeRecord? Edge = FindEdgeById(TargetId);
            if (Edge is not null && !Edge.ClassesMutable.Contains(ClassId, StringComparer.Ordinal))
            {
                Edge.ClassesMutable.Add(ClassId);
            }
        }
    }

    public FlowchartEdgeRecord? FindEdgeById(string EdgeId)
    {
        foreach (FlowchartEdgeRecord Edge in _Edges)
        {
            if (string.Equals(Edge.Id, EdgeId, StringComparison.Ordinal))
            {
                return Edge;
            }
        }

        return null;
    }

    static string GenerateEdgeId(string SourceId, string TargetId, int CreationIndex)
    {
        return "L-" + SourceId + "-" + TargetId + "-" + CreationIndex.ToString();
    }
}
