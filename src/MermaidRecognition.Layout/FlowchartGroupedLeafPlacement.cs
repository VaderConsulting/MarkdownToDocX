using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Aligns a local leaf beside a grouped endpoint of a boundary-crossing path.</summary>
internal static class FlowchartGroupedLeafPlacement
{
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, bool Horizontal)
    {
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            return FlowchartCellLeafPlacement.Apply(Contract, Nodes, Frames, Horizontal, External: false);
        }

        bool Changed = false;
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (DiagramGridLayout.GridNode? Leaf in Nodes.Where(Node => Node.GroupId != null && !Node.IsDiamond))
        {
            List<EdgeContract> Incident = [.. Edges.Where(Edge => Edge.SourceId == Leaf.Id || Edge.TargetId == Leaf.Id)];
            if (Incident.Count != 1)
            {
                continue;
            }

            EdgeContract Edge = Incident[0];
            if (!ById.TryGetValue(Edge.SourceId == Leaf.Id ? Edge.TargetId : Edge.SourceId, out DiagramGridLayout.GridNode? Parent)
                || Parent.GroupId != Leaf.GroupId || Parent.IsDiamond)
            {
                continue;
            }
            // Keep an unbranched path intact. Only its additional local input/output moves aside.
            if (!Edges.Any(Other => Other != Edge
                && (Edge.SourceId == Leaf.Id ? Other.TargetId == Parent.Id : Other.SourceId == Parent.Id)
                && ById.ContainsKey(Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId)
                && !FlowchartCompoundEndpointHelper.Contains(Contract, Leaf.GroupId!,
                    Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId)))
            {
                continue;
            }

            FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, Options, Options.EdgeLabelFontSizePx);
            int Gap = DiagramGridMetrics.CeilToGrid(Math.Max(DiagramGridMetrics.GRID_UNIT_PX,
                string.IsNullOrWhiteSpace(Edge.Label) ? 0 : (Horizontal ? Size.Height : Size.Width) + 12));
            double BestCost = double.PositiveInfinity, Best = Cross(Leaf);
            foreach (int Sign in new[] { -1, 1 })
            {
                double Candidate = Cross(Parent) + (Sign * (Half(Parent) + Gap + Half(Leaf)));
                double X = Horizontal ? Parent.X : Candidate, Y = Horizontal ? Candidate : Parent.Y;
                if (Nodes.Any(Other => Other != Leaf && Math.Abs(Other.X - X) < ((Other.Width + Leaf.Width) / 2.0) + 30
                    && Math.Abs(Other.Y - Y) < ((Other.Height + Leaf.Height) / 2.0) + 30))
                {
                    continue;
                }

                if (Frames.Any(Frame => !FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id, Leaf.Id)
                    && X + (Leaf.Width / 2.0) + 30 > Frame.Left && X - (Leaf.Width / 2.0) - 30 < Frame.Right
                    && Y + (Leaf.Height / 2.0) + 30 > Math.Min(Frame.Top, Frame.Bottom)
                    && Y - (Leaf.Height / 2.0) - 30 < Math.Max(Frame.Top, Frame.Bottom)))
                {
                    continue;
                }

                double Start = Cross(Parent) + (Sign * Half(Parent)), End = Candidate - (Sign * Half(Leaf));
                if (Nodes.Any(Other => Other != Leaf && Other != Parent
                    && Math.Abs(Main(Other) - Main(Parent)) < ((Horizontal ? Other.Width : Other.Height) / 2.0) + 6
                    && Cross(Other) + Half(Other) + 6 > Math.Min(Start, End)
                    && Cross(Other) - Half(Other) - 6 < Math.Max(Start, End)))
                {
                    continue;
                }

                int Crossings = Edges.Count(Other => Other.SourceId != Parent.Id && Other.TargetId != Parent.Id
                    && Other.SourceId != Leaf.Id && Other.TargetId != Leaf.Id
                    && ById.TryGetValue(Other.SourceId, out DiagramGridLayout.GridNode? A) && ById.TryGetValue(Other.TargetId, out DiagramGridLayout.GridNode? B)
                    && Cross(A) > Math.Min(Start, End) && Cross(A) < Math.Max(Start, End)
                    && (Main(A) - Main(Parent)) * (Main(B) - Main(Parent)) < 0);
                // The move must also keep the containing frame clear of unrelated bodies.
                DiagramGridLayout.GridFrame? Frame = Frames.FirstOrDefault(Frame => Frame.Id == Leaf.GroupId);
                if (Frame != null && Nodes.Any(Other => !FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id, Other.Id)
                    && Other.X + (Other.Width / 2.0) + 30 > Math.Min(Frame.Left, X - (Leaf.Width / 2.0) - 30)
                    && Other.X - (Other.Width / 2.0) - 30 < Math.Max(Frame.Right, X + (Leaf.Width / 2.0) + 30)
                    && Other.Y + (Other.Height / 2.0) + 30 > Math.Min(Math.Min(Frame.Top, Frame.Bottom), Y - (Leaf.Height / 2.0) - 30)
                    && Other.Y - (Other.Height / 2.0) - 30 < Math.Max(Math.Max(Frame.Top, Frame.Bottom), Y + (Leaf.Height / 2.0) + 30)))
                {
                    continue;
                }

                double Cost = (Crossings * 1000) + Math.Abs(Candidate - Cross(Leaf));
                if (Cost < BestCost) { BestCost = Cost; Best = Candidate; }
            }
            if (double.IsPositiveInfinity(BestCost))
            {
                continue;
            }

            if (Horizontal) { Leaf.X = Parent.X; Leaf.Y = (int)Math.Round(Best); }
            else { Leaf.Y = Parent.Y; Leaf.X = (int)Math.Round(Best); }
            Leaf.Rank = Parent.Rank;
            Changed = true;
        }
        return Changed;
    }
}
