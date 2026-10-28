using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Detects node-body overlap in layout-space bounds.
/// </summary>
public static class FlowchartLayoutOverlapHelper
{
    private const double OVERLAP_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Returns true when two axis-aligned bounds overlap beyond tolerance.
    /// </summary>
    public static bool NodeBoundsOverlap(BoundsActualResult Left, BoundsActualResult Right)
    {
        double LeftRight = Left.X + Left.Width - OVERLAP_TOLERANCE_PX;
        double LeftLeft = Left.X + OVERLAP_TOLERANCE_PX;
        double LeftBottom = Left.Y + Left.Height - OVERLAP_TOLERANCE_PX;
        double LeftTop = Left.Y + OVERLAP_TOLERANCE_PX;

        double RightRight = Right.X + Right.Width - OVERLAP_TOLERANCE_PX;
        double RightLeft = Right.X + OVERLAP_TOLERANCE_PX;
        double RightBottom = Right.Y + Right.Height - OVERLAP_TOLERANCE_PX;
        double RightTop = Right.Y + OVERLAP_TOLERANCE_PX;

        bool SeparatedHorizontally = LeftRight <= RightLeft || RightRight <= LeftLeft;
        bool SeparatedVertically = LeftBottom <= RightTop || RightBottom <= LeftTop;
        return !SeparatedHorizontally && !SeparatedVertically;
    }

    /// <summary>
    /// Records overlap neighbour facts and updates diagram layout facts.
    /// </summary>
    public static void ApplyNodeBodyOverlapFacts(DiagramActualResult Diagram)
    {
        List<NodeActualResult> RenderedNodes = [.. Diagram.Nodes.Where(Node => Node.IsRendered && Node.Bounds.Width > 0 && Node.Bounds.Height > 0)];

        bool AnyOverlap = false;
        for (int LeftIndex = 0; LeftIndex < RenderedNodes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < RenderedNodes.Count; RightIndex++)
            {
                NodeActualResult LeftNode = RenderedNodes[LeftIndex];
                NodeActualResult RightNode = RenderedNodes[RightIndex];
                if (!NodeBoundsOverlap(LeftNode.Bounds, RightNode.Bounds))
                {
                    continue;
                }

                AnyOverlap = true;
                AppendOverlapNeighbourFact(LeftNode, RightNode);
                AppendOverlapNeighbourFact(RightNode, LeftNode);
            }
        }

        Diagram.Layout["NodeBodiesDoNotOverlap"] = !AnyOverlap;
    }

    private static void AppendOverlapNeighbourFact(NodeActualResult Node, NodeActualResult OtherNode)
    {
        Dictionary<string, object> NeighbourFact = new Dictionary<string, object>
        {
            ["Kind"] = "NodeBodyOverlap",
            ["OtherNodeId"] = OtherNode.NodeId,
            ["OtherNodeLabel"] = OtherNode.Label
        };
        Node.NeighbourFacts.Add(NeighbourFact);
        Node.Facts["Layout.NodeBodiesDoNotOverlap"] = false;
    }
}
