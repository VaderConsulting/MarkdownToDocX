namespace MermaidSharp.Layout;

static class Acyclic
{
    public static void Run(LayoutGraph graph)
    {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();
        var edgesToReverse = new List<LayoutEdge>();

        foreach (var node in graph.Nodes.Values)
        {
            if (!visited.Contains(node.Id))
            {
                Dfs(graph, node.Id, visited, stack, edgesToReverse);
            }
        }

        // Reverse back edges to break cycles (swap endpoints and adjacency so ranker/routing see a DAG).
        foreach (var edge in edgesToReverse)
        {
            var source = graph.GetNode(edge.SourceId);
            var target = graph.GetNode(edge.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            source.OutEdges.Remove(edge);
            target.InEdges.Remove(edge);

            var OldSourceId = edge.SourceId;
            edge.SourceId = edge.TargetId;
            edge.TargetId = OldSourceId;
            edge.IsReversed = true;

            source = graph.GetNode(edge.SourceId);
            target = graph.GetNode(edge.TargetId);
            if (source is not null && target is not null)
            {
                source.OutEdges.Add(edge);
                target.InEdges.Add(edge);
            }
        }
    }

    static void Dfs(LayoutGraph graph, string nodeId, HashSet<string> visited,
        HashSet<string> stack, List<LayoutEdge> edgesToReverse)
    {
        visited.Add(nodeId);
        stack.Add(nodeId);

        var node = graph.GetNode(nodeId);
        if (node is null)
        {
            return;
        }

        foreach (var edge in node.OutEdges.ToList())
        {
            if (stack.Contains(edge.TargetId))
            {
                // Back edge found - this creates a cycle
                edgesToReverse.Add(edge);
            }
            else if (!visited.Contains(edge.TargetId))
            {
                Dfs(graph, edge.TargetId, visited, stack, edgesToReverse);
            }
        }

        stack.Remove(nodeId);
    }

    public static void Undo(LayoutGraph graph)
    {
        foreach (var edge in graph.Edges.Where(_ => _.IsReversed))
        {
            var source = graph.GetNode(edge.SourceId);
            var target = graph.GetNode(edge.TargetId);
            if (source is not null && target is not null)
            {
                source.OutEdges.Remove(edge);
                target.InEdges.Remove(edge);
            }

            var OldSourceIdUndo = edge.SourceId;
            edge.SourceId = edge.TargetId;
            edge.TargetId = OldSourceIdUndo;
            edge.IsReversed = false;

            source = graph.GetNode(edge.SourceId);
            target = graph.GetNode(edge.TargetId);
            if (source is not null && target is not null)
            {
                source.OutEdges.Add(edge);
                target.InEdges.Add(edge);
            }
        }
    }
}
