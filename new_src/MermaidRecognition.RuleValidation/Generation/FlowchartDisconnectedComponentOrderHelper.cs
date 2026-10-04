using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates disconnected single-node components stack on the flow axis in declaration order (F12, F82).
/// </summary>
public static class FlowchartDisconnectedComponentOrderHelper
{
    private const double CROSS_AXIS_ALIGN_TOLERANCE_PX = 5.0;

    private const double FLOW_AXIS_MIN_SEPARATION_PX = 20.0;

    /// <summary>
    /// Records disconnected component ordering facts on the diagram layout.
    /// </summary>
    public static void ApplyDisconnectedComponentOrderFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        Diagram.Layout["HasDisconnectedSingleNodeComponents"] = false;
        Diagram.Layout["DisconnectedComponentsFollowFlowAxis"] = true;

        if (Contract.Nodes.Count < 2 || BoundsById.Count < 2)
        {
            return;
        }

        List<List<string>> ComponentNodeIds = BuildSingleNodeComponents(Contract, BoundsById);
        if (ComponentNodeIds.Count < 2)
        {
            return;
        }

        Diagram.Layout["HasDisconnectedSingleNodeComponents"] = true;

        List<string> OrderedNodeIds = ComponentNodeIds
            .Select(Group => Group[0])
            .OrderBy(NodeId => GetCreationIndex(Contract, NodeId))
            .ToList();

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.RightToLeft
            || Direction == FlowchartDirection.BottomToTop;

        bool AnyOrderingViolation = false;
        for (int Index = 0; Index < OrderedNodeIds.Count - 1; Index++)
        {
            string EarlierNodeId = OrderedNodeIds[Index];
            string LaterNodeId = OrderedNodeIds[Index + 1];
            if (!BoundsById.TryGetValue(EarlierNodeId, out FlowchartLayoutNodeBounds? EarlierBounds)
                || !BoundsById.TryGetValue(LaterNodeId, out FlowchartLayoutNodeBounds? LaterBounds))
            {
                continue;
            }

            if (HorizontalFlow)
            {
                double Separation = LaterBounds.CenterX - EarlierBounds.CenterX;
                if (ReverseFlow)
                {
                    Separation = EarlierBounds.CenterX - LaterBounds.CenterX;
                }

                if (Separation < FLOW_AXIS_MIN_SEPARATION_PX)
                {
                    AnyOrderingViolation = true;
                }
            }
            else
            {
                double Separation = EarlierBounds.CenterY - LaterBounds.CenterY;
                if (ReverseFlow)
                {
                    Separation = LaterBounds.CenterY - EarlierBounds.CenterY;
                }

                if (Separation < FLOW_AXIS_MIN_SEPARATION_PX)
                {
                    AnyOrderingViolation = true;
                }
            }
        }

        bool AnyCrossAxisViolation = false;
        List<double> CrossAxisCenters = [];
        foreach (string NodeId in OrderedNodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds))
            {
                continue;
            }

            CrossAxisCenters.Add(HorizontalFlow ? Bounds.CenterY : Bounds.CenterX);
        }

        if (CrossAxisCenters.Count >= 2)
        {
            double MinCenter = CrossAxisCenters.Min();
            double MaxCenter = CrossAxisCenters.Max();
            if (MaxCenter - MinCenter > CROSS_AXIS_ALIGN_TOLERANCE_PX)
            {
                AnyCrossAxisViolation = true;
            }
        }

        Diagram.Layout["DisconnectedComponentsFollowFlowAxis"] = !AnyOrderingViolation && !AnyCrossAxisViolation;
    }

    private static List<List<string>> BuildSingleNodeComponents(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        Dictionary<string, string> ComponentRootByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string NodeId in BoundsById.Keys)
        {
            ComponentRootByNodeId[NodeId] = NodeId;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ComponentRootByNodeId.ContainsKey(Edge.SourceId)
                || !ComponentRootByNodeId.ContainsKey(Edge.TargetId))
            {
                continue;
            }

            UnionComponentRoots(ComponentRootByNodeId, Edge.SourceId, Edge.TargetId);
        }

        Dictionary<string, List<string>> NodeIdsByComponentRoot =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> Entry in ComponentRootByNodeId)
        {
            if (!NodeIdsByComponentRoot.TryGetValue(Entry.Value, out List<string>? NodeIds))
            {
                NodeIds = [];
                NodeIdsByComponentRoot[Entry.Value] = NodeIds;
            }

            NodeIds.Add(Entry.Key);
        }

        List<List<string>> Components = [];
        foreach (List<string> NodeIds in NodeIdsByComponentRoot.Values)
        {
            if (NodeIds.Count == 1)
            {
                Components.Add(NodeIds);
            }
            else
            {
                return [];
            }
        }

        return Components;
    }

    private static int GetCreationIndex(DiagramContract Contract, string NodeId)
    {
        for (int Index = 0; Index < Contract.Nodes.Count; Index++)
        {
            if (string.Equals(Contract.Nodes[Index].Id, NodeId, StringComparison.Ordinal))
            {
                return Index;
            }
        }

        return int.MaxValue;
    }

    private static void UnionComponentRoots(
        Dictionary<string, string> ComponentRootByNodeId,
        string LeftNodeId,
        string RightNodeId)
    {
        string LeftRoot = FindComponentRoot(ComponentRootByNodeId, LeftNodeId);
        string RightRoot = FindComponentRoot(ComponentRootByNodeId, RightNodeId);
        if (string.Equals(LeftRoot, RightRoot, StringComparison.Ordinal))
        {
            return;
        }

        ComponentRootByNodeId[RightRoot] = LeftRoot;
    }

    private static string FindComponentRoot(
        Dictionary<string, string> ComponentRootByNodeId,
        string NodeId)
    {
        string Root = ComponentRootByNodeId[NodeId];
        while (!string.Equals(Root, ComponentRootByNodeId[Root], StringComparison.Ordinal))
        {
            Root = ComponentRootByNodeId[Root];
        }

        ComponentRootByNodeId[NodeId] = Root;
        return Root;
    }
}
