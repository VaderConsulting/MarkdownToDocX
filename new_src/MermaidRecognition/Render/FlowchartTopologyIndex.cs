using MermaidRecognition.Contract;

namespace MermaidRecognition.Render;

/// <summary>
/// Graph topology facts for structural routing (spec §11.8).
/// </summary>
internal sealed class FlowchartTopologyIndex
{
    private readonly Dictionary<string, NodeContract> _NodesById =
        new Dictionary<string, NodeContract>(StringComparer.Ordinal);

    private readonly Dictionary<string, List<EdgeContract>> _OutgoingBySource =
        new Dictionary<string, List<EdgeContract>>(StringComparer.Ordinal);

    private readonly Dictionary<string, List<EdgeContract>> _IncomingByTarget =
        new Dictionary<string, List<EdgeContract>>(StringComparer.Ordinal);

    public static FlowchartTopologyIndex Build(DiagramContract Contract)
    {
        FlowchartTopologyIndex Index = new FlowchartTopologyIndex();

        foreach (NodeContract Node in Contract.Nodes)
        {
            Index._NodesById[Node.Id] = Node;
        }

        List<EdgeContract> OrderedEdges = Contract.Edges
            .OrderBy(Edge => Edge.CreationIndex >= 0 ? Edge.CreationIndex : int.MaxValue)
            .ThenBy(Edge => Edge.SourceId, StringComparer.Ordinal)
            .ThenBy(Edge => Edge.TargetId, StringComparer.Ordinal)
            .ToList();

        foreach (EdgeContract Edge in OrderedEdges)
        {
            Index.AddOutgoing(Edge.SourceId, Edge);
            Index.AddIncoming(Edge.TargetId, Edge);
        }

        return Index;
    }

    public bool TryGetNode(string NodeId, out NodeContract Node)
    {
        return _NodesById.TryGetValue(NodeId, out Node!);
    }

    public IReadOnlyList<EdgeContract> GetOutgoing(string NodeId)
    {
        if (_OutgoingBySource.TryGetValue(NodeId, out List<EdgeContract>? Edges))
        {
            return Edges;
        }

        return Array.Empty<EdgeContract>();
    }

    public IReadOnlyList<EdgeContract> GetIncoming(string NodeId)
    {
        if (_IncomingByTarget.TryGetValue(NodeId, out List<EdgeContract>? Edges))
        {
            return Edges;
        }

        return Array.Empty<EdgeContract>();
    }

    /// <summary>
    /// Detects whether an edge closes a cycle (target already on the DFS stack or visited).
    /// </summary>
    public bool IsCycleBackEdge(EdgeContract CandidateEdge)
    {
        if (string.Equals(CandidateEdge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Dictionary<string, int> VisitStateByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, NodeContract> Entry in _NodesById)
        {
            VisitStateByNodeId[Entry.Key] = 0;
        }

        List<string> RootNodeIds = new List<string>();
        foreach (KeyValuePair<string, NodeContract> Entry in _NodesById)
        {
            if (!_IncomingByTarget.TryGetValue(Entry.Key, out List<EdgeContract>? IncomingEdges)
                || IncomingEdges.Count == 0)
            {
                RootNodeIds.Add(Entry.Key);
            }
        }

        if (RootNodeIds.Count == 0)
        {
            RootNodeIds.AddRange(_NodesById.Keys);
        }

        foreach (string RootNodeId in RootNodeIds)
        {
            if (VisitStateByNodeId[RootNodeId] == 0
                && DepthFirstSearchForBackEdge(RootNodeId, CandidateEdge, VisitStateByNodeId))
            {
                return true;
            }
        }

        return false;
    }

    private bool DepthFirstSearchForBackEdge(
        string NodeId,
        EdgeContract CandidateEdge,
        Dictionary<string, int> VisitStateByNodeId)
    {
        VisitStateByNodeId[NodeId] = 1;

        if (_OutgoingBySource.TryGetValue(NodeId, out List<EdgeContract>? OutgoingEdges))
        {
            foreach (EdgeContract Edge in OutgoingEdges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (EdgesMatch(Edge, CandidateEdge))
                {
                    int TargetState = VisitStateByNodeId.TryGetValue(Edge.TargetId, out int State) ? State : 0;
                    if (TargetState != 0)
                    {
                        return true;
                    }
                }

                int NextState = VisitStateByNodeId.TryGetValue(Edge.TargetId, out int ExistingState) ? ExistingState : 0;
                if (NextState == 0
                    && DepthFirstSearchForBackEdge(Edge.TargetId, CandidateEdge, VisitStateByNodeId))
                {
                    return true;
                }
            }
        }

        VisitStateByNodeId[NodeId] = 2;
        return false;
    }

    private static bool EdgesMatch(EdgeContract Left, EdgeContract Right)
    {
        return Left.SourceId == Right.SourceId
            && Left.TargetId == Right.TargetId
            && Left.CreationIndex == Right.CreationIndex;
    }

    private void AddOutgoing(string NodeId, EdgeContract Edge)
    {
        if (!_OutgoingBySource.TryGetValue(NodeId, out List<EdgeContract>? Edges))
        {
            Edges = new List<EdgeContract>();
            _OutgoingBySource[NodeId] = Edges;
        }

        Edges.Add(Edge);
    }

    private void AddIncoming(string NodeId, EdgeContract Edge)
    {
        if (!_IncomingByTarget.TryGetValue(NodeId, out List<EdgeContract>? Edges))
        {
            Edges = new List<EdgeContract>();
            _IncomingByTarget[NodeId] = Edges;
        }

        Edges.Add(Edge);
    }
}
