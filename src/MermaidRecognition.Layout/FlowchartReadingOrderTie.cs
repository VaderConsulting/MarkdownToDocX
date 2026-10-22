using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Rule 1.8: prefer left-to-right, then top-to-bottom placement when scores tie.</summary>
internal static class FlowchartReadingOrderTie
{
    internal static double Measure(DiagramContract Contract, IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = NodeBounds
            .GroupBy(Bounds => Bounds.NodeId, System.StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), System.StringComparer.Ordinal);
        double ReadingCost = 0.0;
        int Order = 0;
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            ReadingCost += Order * Bounds.CenterX;
            ReadingCost -= Order * Bounds.CenterY;
            Order++;
        }

        return ReadingCost;
    }
}
