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
