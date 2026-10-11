using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Places a singly connected outside node beside its grouped neighbour.</summary>
internal static class FlowchartExternalLeafPlacement
{
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, bool Horizontal)
    {
        bool Changed = false;
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Edges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        var Options = FlowchartRenderOptions.FromContract(Contract);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double Half(DiagramGridLayout.GridNode Node) => (Horizontal ? Node.Height : Node.Width) / 2.0;
        foreach (var Leaf in Nodes.Where(Node => Node.GroupId == null && !Node.IsDiamond))
        {
            var Incident = Edges.Where(Edge => Edge.SourceId == Leaf.Id || Edge.TargetId == Leaf.Id).ToList();
            if (Incident.Count != 1) continue;
            var Edge = Incident[0];
            if (!ById.TryGetValue(Edge.SourceId == Leaf.Id ? Edge.TargetId : Edge.SourceId, out var Parent)
                || Parent.GroupId == null || Parent.IsDiamond) continue;
            var Frame = Frames.Where(Frame => FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id, Parent.Id))
                .OrderByDescending(Frame => Math.Abs((double)(Frame.Right - Frame.Left) * (Frame.Bottom - Frame.Top))).FirstOrDefault();
            if (Frame == null || !Edges.Any(Other => (Edge.SourceId == Leaf.Id
                    ? Other.TargetId == Parent.Id : Other.SourceId == Parent.Id)
                && (Other.SourceId != Edge.SourceId || Other.TargetId != Edge.TargetId)
                && FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id,
                    Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId))) continue;
            var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, Options, Options.EdgeLabelFontSizePx);
            int Gap = DiagramGridMetrics.CeilToGrid(Math.Max(DiagramGridMetrics.GRID_UNIT_PX,
                string.IsNullOrWhiteSpace(Edge.Label) ? 0 : (Horizontal ? Size.Height : Size.Width) + 12));
            double BestCost = double.PositiveInfinity, Best = Cross(Leaf);
            foreach (int Sign in new[] { -1, 1 })
            {
                double Border = Horizontal ? Sign < 0 ? Math.Min(Frame.Top, Frame.Bottom) : Math.Max(Frame.Top, Frame.Bottom)
                    : Sign < 0 ? Frame.Left : Frame.Right;
                double Candidate = Border + Sign * (Gap + Half(Leaf));
                double X = Horizontal ? Parent.X : Candidate, Y = Horizontal ? Candidate : Parent.Y;
                if (Nodes.Any(Other => Other != Leaf && Math.Abs(Other.X - X) < (Other.Width + Leaf.Width) / 2.0 + 30
                    && Math.Abs(Other.Y - Y) < (Other.Height + Leaf.Height) / 2.0 + 30)) continue;
                if (Frames.Any(Other => X + Leaf.Width / 2.0 + 30 > Other.Left && X - Leaf.Width / 2.0 - 30 < Other.Right
                    && Y + Leaf.Height / 2.0 + 30 > Math.Min(Other.Top, Other.Bottom)
                    && Y - Leaf.Height / 2.0 - 30 < Math.Max(Other.Top, Other.Bottom))) continue;
                double Start = Cross(Parent) + Sign * Half(Parent), End = Candidate - Sign * Half(Leaf);
                if (Nodes.Any(Other => Other != Leaf && Other != Parent
                    && Math.Abs(Main(Other) - Main(Parent)) < (Horizontal ? Other.Width : Other.Height) / 2.0 + 6
                    && Cross(Other) + Half(Other) + 6 > Math.Min(Start, End)
                    && Cross(Other) - Half(Other) - 6 < Math.Max(Start, End))) continue;
                int Crossings = Edges.Count(Other => Other.SourceId != Parent.Id && Other.TargetId != Parent.Id
                    && Other.SourceId != Leaf.Id && Other.TargetId != Leaf.Id
                    && ById.TryGetValue(Other.SourceId, out var A) && ById.TryGetValue(Other.TargetId, out var B)
                    && Cross(A) > Math.Min(Start, End) && Cross(A) < Math.Max(Start, End)
                    && (Main(A) - Main(Parent)) * (Main(B) - Main(Parent)) < 0);
                double Cost = Crossings * 1000 + Math.Abs(End - Start) + Math.Abs(Candidate - Cross(Leaf)) / 100;
                if (Cost < BestCost) { BestCost = Cost; Best = Candidate; }
            }
            if (double.IsPositiveInfinity(BestCost)) continue;
            if (Horizontal) { Leaf.X = Parent.X; Leaf.Y = (int)Math.Round(Best); }
            else { Leaf.Y = Parent.Y; Leaf.X = (int)Math.Round(Best); }
            Leaf.Rank = Parent.Rank;
            Changed = true;
        }
        return Changed;
    }
}
