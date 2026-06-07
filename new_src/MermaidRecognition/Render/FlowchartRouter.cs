using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Render;

/// <summary>
/// Structural flowchart edge router (Stage 11.B–C pilot). Does not branch on edge label text.
/// </summary>
public static class FlowchartRouter
{
    private const string INVISIBLE_STROKE = "invisible";

    /// <summary>
    /// Builds a structural route plan for every edge in the contract.
    /// </summary>
    public static bool TryRouteAll(DiagramContract Contract, out FlowchartRouteResult Result)
    {
        Result = new FlowchartRouteResult();

        if (Contract.Edges.Count == 0)
        {
            return true;
        }

        FlowchartTopologyIndex Topology = FlowchartTopologyIndex.Build(Contract);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            FlowchartEdgeRoute Route = ClassifyEdge(Edge, Topology, Contract);
            Result.RoutesMutable.Add(Route);
        }

        return true;
    }

    /// <summary>
    /// Classifies one edge using topology only — never edge label wording.
    /// </summary>
    public static FlowchartEdgeRoute ClassifyEdge(EdgeContract Edge, DiagramContract Contract)
    {
        FlowchartTopologyIndex Topology = FlowchartTopologyIndex.Build(Contract);
        return ClassifyEdge(Edge, Topology, Contract);
    }

    /// <summary>
    /// Classifies one edge using topology only — never edge label wording.
    /// </summary>
    internal static FlowchartEdgeRoute ClassifyEdge(
        EdgeContract Edge,
        FlowchartTopologyIndex Topology,
        DiagramContract Contract)
    {
        FlowchartEdgeRoute Route = new FlowchartEdgeRoute
        {
            EdgeKey = BuildEdgeKey(Edge),
            SourceId = Edge.SourceId,
            TargetId = Edge.TargetId
        };

        if (string.Equals(Edge.Stroke, INVISIBLE_STROKE, StringComparison.OrdinalIgnoreCase))
        {
            Route.Kind = FlowchartRouteKind.InvisibleLink;
            return Route;
        }

        IReadOnlyList<EdgeContract> Outgoing = Topology.GetOutgoing(Edge.SourceId);
        IReadOnlyList<EdgeContract> Incoming = Topology.GetIncoming(Edge.TargetId);

        Route.SourceBranchIndex = IndexOfEdge(Edge, Outgoing);
        Route.TargetJoinIndex = IndexOfEdge(Edge, Incoming);

        bool SourceIsDecision = Topology.TryGetNode(Edge.SourceId, out NodeContract? SourceNode)
            && IsDecisionShape(SourceNode);

        bool TargetIsDecision = Topology.TryGetNode(Edge.TargetId, out NodeContract? TargetNode)
            && IsDecisionShape(TargetNode);

        if (SourceIsDecision && Outgoing.Count >= 2)
        {
            Route.Kind = FlowchartRouteKind.DecisionFanOut;
            return Route;
        }

        if (TargetIsDecision && Incoming.Count >= 2)
        {
            Route.Kind = FlowchartRouteKind.DecisionFanIn;
            return Route;
        }

        if (IsBackEdgeLoop(Edge, Topology, Contract))
        {
            Route.Kind = FlowchartRouteKind.BackEdgeLoop;
            return Route;
        }

        if (CrossesGroupBoundary(Edge, Contract))
        {
            Route.Kind = FlowchartRouteKind.CrossGroupBoundary;
            return Route;
        }

        Route.Kind = FlowchartRouteKind.SimpleLink;
        return Route;
    }

    private static string BuildEdgeKey(EdgeContract Edge)
    {
        if (!string.IsNullOrEmpty(Edge.Id))
        {
            return Edge.Id;
        }

        return Edge.SourceId + "->" + Edge.TargetId + "#" + Edge.CreationIndex;
    }

    private static int IndexOfEdge(EdgeContract Edge, IReadOnlyList<EdgeContract> Siblings)
    {
        for (int Index = 0; Index < Siblings.Count; Index++)
        {
            EdgeContract Candidate = Siblings[Index];
            if (Candidate == Edge
                || (Candidate.SourceId == Edge.SourceId
                    && Candidate.TargetId == Edge.TargetId
                    && Candidate.CreationIndex == Edge.CreationIndex))
            {
                return Index;
            }
        }

        return 0;
    }

    private static bool IsDecisionShape(NodeContract Node)
    {
        if (string.IsNullOrEmpty(Node.Shape))
        {
            return false;
        }

        string Normalised = Node.Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackEdgeLoop(
        EdgeContract Edge,
        FlowchartTopologyIndex Topology,
        DiagramContract Contract)
    {
        if (!Topology.TryGetNode(Edge.SourceId, out NodeContract? Source)
            || !Topology.TryGetNode(Edge.TargetId, out NodeContract? Target))
        {
            return false;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        if (Direction == FlowchartDirection.BottomToTop)
        {
            return Edge.TargetId.Equals(Source.Id, StringComparison.Ordinal)
                && Edge.SourceId.Equals(Target.Id, StringComparison.Ordinal);
        }

        if (Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft)
        {
            if (IsDecisionShape(Source) || IsDecisionShape(Target))
            {
                return false;
            }

            return Topology.IsCycleBackEdge(Edge);
        }

        return false;
    }

    private static bool CrossesGroupBoundary(EdgeContract Edge, DiagramContract Contract)
    {
        string? SourceGroup = FindGroupForNode(Edge.SourceId, Contract);
        string? TargetGroup = FindGroupForNode(Edge.TargetId, Contract);

        if (SourceGroup == null && TargetGroup == null)
        {
            return false;
        }

        return !string.Equals(SourceGroup, TargetGroup, StringComparison.Ordinal);
    }

    private static string? FindGroupForNode(string NodeId, DiagramContract Contract)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (Group.ChildNodeIds.Contains(NodeId, StringComparer.Ordinal))
            {
                return Group.Id;
            }
        }

        return null;
    }
}
