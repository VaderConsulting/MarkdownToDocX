using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Aligns grouped members with external cross-boundary targets in open layout mode (F149).</summary>
internal static class FlowchartOpenCrossTargetAlignment
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Placed, bool Horizontal)
    {
        if (Placed.Any(Node => Node.CellNode != null))
        {
            return;
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        List<EdgeContract> Visible = Contract.Edges
            .Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (EdgeContract Edge in Visible)
        {
            if (!ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Source)
                || !ById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? Target))
            {
                continue;
            }

            if (Source.GroupId == null || Target.GroupId != null || Source.GroupId == Target.GroupId)
            {
                continue;
            }

            if (Visible.Count(Other => Other.SourceId == Edge.SourceId) != 1)
            {
                continue;
            }

            double DesiredCross = Horizontal ? Target.Y : Target.X;
            if (Horizontal)
            {
                Source.Y = (int)Math.Round(DesiredCross);
            }
            else
            {
                Source.X = (int)Math.Round(DesiredCross);
            }
        }
    }
}
