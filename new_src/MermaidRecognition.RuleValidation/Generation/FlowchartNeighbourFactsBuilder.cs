using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Populates neighbour relationship facts from layout centres and diagram direction.
/// </summary>
public static class FlowchartNeighbourFactsBuilder
{
    private const double NEIGHBOUR_TOLERANCE_PX = 2.0;

    /// <summary>
    /// Records directional neighbour facts and validates consistency for rendered nodes.
    /// </summary>
    public static void Apply(DiagramActualResult Diagram, FlowchartDirection Direction)
    {
        Dictionary<string, NodeActualResult> NodeById = new Dictionary<string, NodeActualResult>(StringComparer.Ordinal);
        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            NodeById[Node.NodeId] = Node;
        }

        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            if (!Node.IsRendered)
            {
                continue;
            }

            Node.Facts["NeighbourFactsConsistent"] = true;
        }

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!Edge.IsVisible)
            {
                continue;
            }

            if (!NodeById.TryGetValue(Edge.SourceNodeId, out NodeActualResult? SourceNode)
                || !NodeById.TryGetValue(Edge.TargetNodeId, out NodeActualResult? TargetNode))
            {
                continue;
            }

            if (!SourceNode.IsRendered || !TargetNode.IsRendered)
            {
                continue;
            }

            string? RelativePosition = ResolveRelativePosition(
                SourceNode.Bounds,
                TargetNode.Bounds,
                Direction);
            if (RelativePosition is null)
            {
                continue;
            }

            Dictionary<string, object> NeighbourFact = new Dictionary<string, object>
            {
                ["Kind"] = "PrimaryFlowNeighbour",
                ["OtherNodeId"] = TargetNode.NodeId,
                ["OtherNodeLabel"] = TargetNode.Label,
                ["RelativePosition"] = RelativePosition
            };
            SourceNode.NeighbourFacts.Add(NeighbourFact);

            if (!NeighbourOrderingMatchesDirection(
                    SourceNode.Bounds,
                    TargetNode.Bounds,
                    Direction,
                    RelativePosition))
            {
                SourceNode.Facts["NeighbourFactsConsistent"] = false;
            }
        }
    }

    private static string? ResolveRelativePosition(
        BoundsActualResult SourceBounds,
        BoundsActualResult TargetBounds,
        FlowchartDirection Direction)
    {
        double DeltaX = TargetBounds.CentreX - SourceBounds.CentreX;
        double DeltaY = TargetBounds.CentreY - SourceBounds.CentreY;

        switch (Direction)
        {
            case FlowchartDirection.TopToBottom:
                if (DeltaY > NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Below";
                }

                if (DeltaY < -NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Above";
                }

                break;
            case FlowchartDirection.BottomToTop:
                if (DeltaY < -NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Below";
                }

                if (DeltaY > NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Above";
                }

                break;
            case FlowchartDirection.LeftToRight:
                if (DeltaX > NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Right";
                }

                if (DeltaX < -NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Left";
                }

                break;
            case FlowchartDirection.RightToLeft:
                if (DeltaX < -NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Left";
                }

                if (DeltaX > NEIGHBOUR_TOLERANCE_PX)
                {
                    return "Right";
                }

                break;
        }

        if (Math.Abs(DeltaX) <= NEIGHBOUR_TOLERANCE_PX && Math.Abs(DeltaY) <= NEIGHBOUR_TOLERANCE_PX)
        {
            return "Aligned";
        }

        return null;
    }

    private static bool NeighbourOrderingMatchesDirection(
        BoundsActualResult SourceBounds,
        BoundsActualResult TargetBounds,
        FlowchartDirection Direction,
        string RelativePosition)
    {
        if (!EdgeActualResultIsPrimaryFlow(SourceBounds, TargetBounds, Direction))
        {
            return true;
        }

        return Direction switch
        {
            FlowchartDirection.TopToBottom => RelativePosition == "Below" || RelativePosition == "Aligned",
            FlowchartDirection.BottomToTop => RelativePosition == "Above" || RelativePosition == "Aligned",
            FlowchartDirection.LeftToRight => RelativePosition == "Right" || RelativePosition == "Aligned",
            FlowchartDirection.RightToLeft => RelativePosition == "Left" || RelativePosition == "Aligned",
            _ => true
        };
    }

    private static bool EdgeActualResultIsPrimaryFlow(
        BoundsActualResult SourceBounds,
        BoundsActualResult TargetBounds,
        FlowchartDirection Direction) => Direction switch
        {
            FlowchartDirection.TopToBottom => TargetBounds.CentreY >= SourceBounds.CentreY - NEIGHBOUR_TOLERANCE_PX,
            FlowchartDirection.BottomToTop => TargetBounds.CentreY <= SourceBounds.CentreY + NEIGHBOUR_TOLERANCE_PX,
            FlowchartDirection.LeftToRight => TargetBounds.CentreX >= SourceBounds.CentreX - NEIGHBOUR_TOLERANCE_PX,
            FlowchartDirection.RightToLeft => TargetBounds.CentreX <= SourceBounds.CentreX + NEIGHBOUR_TOLERANCE_PX,
            _ => true
        };
}
