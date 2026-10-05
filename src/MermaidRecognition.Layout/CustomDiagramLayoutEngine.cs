using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Wraps the current native Sugiyama placement and custom shared router.
/// </summary>
public sealed class CustomDiagramLayoutEngine : IDiagramLayoutEngine
{
    /// <inheritdoc />
    public bool TryLayout(DiagramContract Contract, DiagramLayoutProfile Profile, out DiagramLayoutSolution Solution)
    {
        Solution = new DiagramLayoutSolution
        {
            EngineKind = DiagramLayoutEngineKind.Custom
        };

        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return false;
        }

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        if (string.IsNullOrWhiteSpace(Svg))
        {
            return false;
        }

        FlowchartRenderedSvgDocument Document = FlowchartRenderedSvgDocument.Parse(Svg, Contract);
        HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupIds.Add(Group.Id);
        }

        foreach (FlowchartRenderedSvgBox Box in Document.Boxes)
        {
            if (string.IsNullOrEmpty(Box.NodeId))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = new FlowchartLayoutNodeBounds
            {
                NodeId = Box.NodeId,
                CenterX = (Box.Left + Box.Right) / 2.0,
                CenterY = (Box.Top + Box.Bottom) / 2.0,
                Width = Math.Max(1.0, Box.Right - Box.Left),
                Height = Math.Max(1.0, Box.Bottom - Box.Top)
            };

            if (GroupIds.Contains(Box.NodeId))
            {
                Solution.GroupBounds.Add(Bounds);
            }
            else
            {
                Solution.NodeBounds.Add(Bounds);
            }
        }

        foreach (FlowchartRenderedSvgStroke Stroke in Document.Strokes)
        {
            string Label = string.Empty;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.SourceId, Stroke.SourceId, StringComparison.Ordinal)
                    && string.Equals(Edge.TargetId, Stroke.TargetId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(Edge.Label))
                {
                    Label = Edge.Label.Trim();
                    break;
                }
            }

            Solution.Edges.Add(new DiagramLayoutRoutedEdge(
                Stroke.SourceId,
                Stroke.TargetId,
                Label,
                new List<(double X, double Y)>(Stroke.Samples)));
        }

        Solution.Width = 1.0;
        Solution.Height = 1.0;
        return Solution.NodeBounds.Count > 0;
    }
}
