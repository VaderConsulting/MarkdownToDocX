namespace MermaidSharp.Layout;

class LayoutGraph
{
    public Dictionary<string, LayoutNode> Nodes { get; } = [];
    public List<LayoutEdge> Edges { get; } = [];
    public List<LayoutNode>[] Ranks { get; private set; } = [];

    public void AddNode(LayoutNode node) => Nodes[node.Id] = node;

    public void AddEdge(LayoutEdge edge)
    {
        Edges.Add(edge);
        if (Nodes.TryGetValue(edge.SourceId, out var source))
        {
            source.OutEdges.Add(edge);
        }
        if (Nodes.TryGetValue(edge.TargetId, out var target))
        {
            target.InEdges.Add(edge);
        }
    }

    public LayoutNode? GetNode(string id) =>
        Nodes.GetValueOrDefault(id);

    public IEnumerable<LayoutNode> GetSuccessors(string nodeId)
    {
        if (!Nodes.TryGetValue(nodeId, out var node)) yield break;
        foreach (var edge in node.OutEdges)
        {
            if (Nodes.TryGetValue(edge.TargetId, out var target))
            {
                yield return target;
            }
        }
    }

    public IEnumerable<LayoutNode> GetPredecessors(string nodeId)
    {
        if (!Nodes.TryGetValue(nodeId, out var node)) yield break;
        foreach (var edge in node.InEdges)
        {
            if (Nodes.TryGetValue(edge.SourceId, out var source))
            {
                yield return source;
            }
        }
    }

    public void BuildRanks()
    {
        var maxRank = Nodes.Values.Max(_ => _.Rank);
        Ranks = new List<LayoutNode>[maxRank + 1];
        for (var i = 0; i <= maxRank; i++)
        {
            Ranks[i] = [];
        }
        // Deterministic ordering: iterate nodes by their assigned Order.
        // This matches Mermaid/dagre-d3's stable insertion-order behaviour and avoids dictionary iteration variance.
#pragma warning disable IDE0007
        foreach (LayoutNode node in Nodes.Values.OrderBy(_ => _.Order))
        {
            Ranks[node.Rank].Add(node);
        }
#pragma warning restore IDE0007
    }

    public void UpdateOrderInRanks()
    {
        for (var r = 0; r < Ranks.Length; r++)
        {
            var nodesInRank = Ranks[r].OrderBy(_ => _.Order).ToList();
            for (var i = 0; i < nodesInRank.Count; i++)
            {
                nodesInRank[i].Order = i;
            }
            Ranks[r] = nodesInRank;
        }
    }
}

internal class LayoutNode
{
    public required string Id { get; init; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Rank { get; set; } = -1;
    public int Order { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsDummy { get; set; }
    public string? OriginalEdgeSource { get; set; }
    public string? OriginalEdgeTarget { get; set; }

    public List<LayoutEdge> InEdges { get; } = [];
    public List<LayoutEdge> OutEdges { get; } = [];
}

internal class LayoutEdge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }

    /// <summary>
    /// Minimum rank gap between source and target (dagre minlen); at least <c>1</c>.
    /// </summary>
    public int MinRankSpan { get; set; } = 1;

    public int Weight { get; set; } = 1;
    public bool IsReversed { get; set; }

    /// <summary>Wrapped edge-label box width (px), measured before layout (Mermaid <c>edge.width</c>).</summary>
    public double LabelWidthPx { get; set; }

    /// <summary>Wrapped edge-label box height (px), measured before layout (Mermaid <c>edge.height</c>).</summary>
    public double LabelHeightPx { get; set; }

    public List<Position> Points { get; } = [];
}
