using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Shared edge selection and shape rules used by fan-in, fan-out and decision
/// port allocation. Route construction remains with the specialised allocator.
/// </summary>
internal static class FlowchartSharedPortAllocator
{
    internal static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    internal static int CountRoutableEdges(DiagramContract Contract, string NodeId, bool Outgoing) =>
        GetEdges(Contract, NodeId, Outgoing, ExcludeBottomFaceCorridors: true).Count;

    internal static List<EdgeContract> GetRoutableEdgesFromSource(DiagramContract Contract, string SourceId) =>
        GetEdges(Contract, SourceId, Outgoing: true, ExcludeBottomFaceCorridors: true);

    internal static List<EdgeContract> GetRoutableEdgesToTarget(DiagramContract Contract, string TargetId) =>
        GetEdges(Contract, TargetId, Outgoing: false, ExcludeBottomFaceCorridors: true);

    internal static int CountVisibleEdges(DiagramContract Contract, string NodeId, bool Outgoing) =>
        GetEdges(Contract, NodeId, Outgoing, ExcludeBottomFaceCorridors: false).Count;

    internal static List<EdgeContract> GetVisibleEdgesFromSource(DiagramContract Contract, string SourceId) =>
        GetEdges(Contract, SourceId, Outgoing: true, ExcludeBottomFaceCorridors: false);

    internal static List<EdgeContract> GetVisibleEdgesToTarget(DiagramContract Contract, string TargetId) =>
        GetEdges(Contract, TargetId, Outgoing: false, ExcludeBottomFaceCorridors: false);

    private static List<EdgeContract> GetEdges(
        DiagramContract Contract,
        string NodeId,
        bool Outgoing,
        bool ExcludeBottomFaceCorridors)
    {
        List<EdgeContract> Edges = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || (ExcludeBottomFaceCorridors && FlowchartEdgeGuidePointBuilder.UsesBottomFaceCorridor(Edge, Contract)))
            {
                continue;
            }

            string EndpointId = Outgoing ? Edge.SourceId : Edge.TargetId;
            if (string.Equals(EndpointId, NodeId, StringComparison.Ordinal))
            {
                Edges.Add(Edge);
            }
        }

        return Edges;
    }
}
