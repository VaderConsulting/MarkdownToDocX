using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Gives connected root containers in one grid column a shared cross-axis extent.</summary>
internal static class FlowchartSiblingFrameAlignment
{
    internal static void Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, bool Horizontal)
    {
        bool Contains(string Group, string Id) => Group == Id || FlowchartCompoundEndpointHelper.Contains(Contract, Group, Id);
        var Roots = Frames.Where(Frame => !Contract.Groups.Any(Group => Group.Id != Frame.Id && Contains(Group.Id, Frame.Id))).ToList();
        int Low(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Top : Frame.Left;
        int High(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Bottom : Frame.Right;
        int MainLow(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Left : Frame.Top;
        int MainHigh(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Right : Frame.Bottom;
        foreach (var Column in Roots.GroupBy(Low))
        {
            var Members = Column.ToList();
            if (Members.Count < 2) continue;
            int Extent = Members.Max(High);
            foreach (var Frame in Members.Where(Frame => High(Frame) < Extent))
            {
                if (!Members.Any(Peer => Peer != Frame && Contract.Edges.Any(Edge =>
                    !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && (Contains(Frame.Id, Edge.SourceId) && Contains(Peer.Id, Edge.TargetId)
                        || Contains(Peer.Id, Edge.SourceId) && Contains(Frame.Id, Edge.TargetId))))) continue;
                bool Blocked = Roots.Any(Other => Other != Frame
                    && MainLow(Other) < MainHigh(Frame) + DiagramGridMetrics.GRID_UNIT_PX
                    && MainHigh(Other) > MainLow(Frame) - DiagramGridMetrics.GRID_UNIT_PX
                    && Low(Other) < Extent + DiagramGridMetrics.GRID_UNIT_PX && High(Other) > Low(Frame) - DiagramGridMetrics.GRID_UNIT_PX);
                Blocked |= Nodes.Any(Node => !Contains(Frame.Id, Node.Id)
                    && (Horizontal ? Node.X - Node.Width / 2.0 : Node.Y - Node.Height / 2.0) < MainHigh(Frame) + DiagramGridMetrics.GRID_UNIT_PX
                    && (Horizontal ? Node.X + Node.Width / 2.0 : Node.Y + Node.Height / 2.0) > MainLow(Frame) - DiagramGridMetrics.GRID_UNIT_PX
                    && (Horizontal ? Node.Y - Node.Height / 2.0 : Node.X - Node.Width / 2.0) < Extent + DiagramGridMetrics.GRID_UNIT_PX
                    && (Horizontal ? Node.Y + Node.Height / 2.0 : Node.X + Node.Width / 2.0) > Low(Frame) - DiagramGridMetrics.GRID_UNIT_PX);
                if (Blocked) continue;
                int Shift = (int)Math.Floor((Extent - High(Frame)) / (2.0 * DiagramGridMetrics.GRID_UNIT_PX)) * DiagramGridMetrics.GRID_UNIT_PX;
                foreach (var Node in Nodes.Where(Node => Contains(Frame.Id, Node.Id)))
                    if (Horizontal) Node.Y += Shift; else Node.X += Shift;
                foreach (var Child in Frames.Where(Child => Child != Frame && Contains(Frame.Id, Child.Id)))
                    if (Horizontal) { Child.Top += Shift; Child.Bottom += Shift; }
                    else { Child.Left += Shift; Child.Right += Shift; }
                if (Horizontal) Frame.Bottom = Extent; else Frame.Right = Extent;
            }
        }
    }
}
