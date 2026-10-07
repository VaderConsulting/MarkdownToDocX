using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Widens neighbouring column or rank gaps when group frames or outside nodes need clearance (rulebook 7.3).
/// </summary>
internal static class DiagramGridFrameGapHelper
{
    /// <summary>
    /// Grows whole column or rank gaps by whole grid steps so frames never overlap each other or non-member nodes.
    /// </summary>
    internal static void WidenNeighbouringFrameGaps(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Placed,
        FlowchartLayoutResult Copy,
        bool Horizontal)
    {
        if (Contract.Groups.Count == 0)
        {
            return;
        }

        List<DiagramGridLayout.GridFrame> Frames = [];
        DiagramGridLayout.BuildPreviewFrames(Contract, Placed, Frames);
        if (Frames.Count == 0)
        {
            return;
        }

        Dictionary<string, string?> GroupByNode = BuildGroupByNode(Contract);
        SortedDictionary<int, List<DiagramGridLayout.GridNode>> ByColumn = BuildByColumn(Placed, Horizontal);
        List<int> ColumnKeys = [.. ByColumn.Keys];
        for (int Index = 1; Index < ColumnKeys.Count; Index++)
        {
            int LeftKey = ColumnKeys[Index - 1];
            int RightKey = ColumnKeys[Index];
            if (!NeedsGapWidening(Contract, ByColumn[LeftKey], ByColumn[RightKey], GroupByNode, Frames, Copy, Horizontal))
            {
                continue;
            }

            int ExtraSteps = CountCrossingEdgesBetweenColumns(Copy, ByColumn[LeftKey], ByColumn[RightKey]) > 0 ? 2 : 1;
            int Push = ExtraSteps * DiagramGridMetrics.GRID_UNIT_PX;
            double BoundaryCross = CrossOfColumn(ByColumn[RightKey], Horizontal);
            ShiftCrossFrom(Placed, BoundaryCross - 0.5, Push, Horizontal);
            DiagramGridLayout.BuildPreviewFrames(Contract, Placed, Frames);
        }
    }

    private static bool NeedsGapWidening(
        DiagramContract Contract,
        List<DiagramGridLayout.GridNode> LeftColumn,
        List<DiagramGridLayout.GridNode> RightColumn,
        Dictionary<string, string?> GroupByNode,
        List<DiagramGridLayout.GridFrame> Frames,
        FlowchartLayoutResult Copy,
        bool Horizontal)
    {
        foreach (DiagramGridLayout.GridFrame LeftFrame in Frames)
        {
            foreach (DiagramGridLayout.GridFrame RightFrame in Frames)
            {
                if (string.Equals(LeftFrame.Id, RightFrame.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (FramesOverlap(LeftFrame, RightFrame))
                {
                    return true;
                }
            }
        }

        foreach (DiagramGridLayout.GridNode Node in LeftColumn)
        {
            string? NodeGroup = GroupByNode.TryGetValue(Node.Id, out string? GroupId) ? GroupId : null;
            foreach (DiagramGridLayout.GridFrame Frame in Frames)
            {
                if (string.Equals(Frame.Id, NodeGroup, StringComparison.Ordinal))
                {
                    continue;
                }

                if (NodeOverlapsFrame(Node, Frame))
                {
                    return true;
                }
            }
        }

        foreach (DiagramGridLayout.GridNode Node in RightColumn)
        {
            string? NodeGroup = GroupByNode.TryGetValue(Node.Id, out string? GroupId) ? GroupId : null;
            foreach (DiagramGridLayout.GridFrame Frame in Frames)
            {
                if (string.Equals(Frame.Id, NodeGroup, StringComparison.Ordinal))
                {
                    continue;
                }

                if (NodeOverlapsFrame(Node, Frame))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool FramesOverlap(DiagramGridLayout.GridFrame Left, DiagramGridLayout.GridFrame Right)
    {
        return Left.Right > Right.Left
            && Right.Right > Left.Left
            && Left.Top > Right.Bottom
            && Right.Top > Left.Bottom;
    }

    private static bool NodeOverlapsFrame(DiagramGridLayout.GridNode Node, DiagramGridLayout.GridFrame Frame)
    {
        double Left = Node.X - (Node.Width / 2.0);
        double Right = Node.X + (Node.Width / 2.0);
        double Bottom = Node.Y - (Node.Height / 2.0);
        double Top = Node.Y + (Node.Height / 2.0);
        return Right > Frame.Left && Left < Frame.Right && Top > Frame.Bottom && Bottom < Frame.Top;
    }

    private static int CountCrossingEdgesBetweenColumns(
        FlowchartLayoutResult Copy,
        List<DiagramGridLayout.GridNode> LeftColumn,
        List<DiagramGridLayout.GridNode> RightColumn)
    {
        HashSet<string> LeftIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> RightIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in LeftColumn)
        {
            LeftIds.Add(Node.Id);
        }

        foreach (DiagramGridLayout.GridNode Node in RightColumn)
        {
            RightIds.Add(Node.Id);
        }

        int Count = 0;
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            bool LeftToRight = LeftIds.Contains(Route.SourceId) && RightIds.Contains(Route.TargetId);
            bool RightToLeft = RightIds.Contains(Route.SourceId) && LeftIds.Contains(Route.TargetId);
            if (LeftToRight || RightToLeft)
            {
                Count++;
            }
        }

        return Count;
    }

    private static Dictionary<string, string?> BuildGroupByNode(DiagramContract Contract)
    {
        Dictionary<string, string?> GroupByNode = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            GroupByNode[Node.Id] = Node.ParentGroupId;
        }

        return GroupByNode;
    }

    private static SortedDictionary<int, List<DiagramGridLayout.GridNode>> BuildByColumn(
        List<DiagramGridLayout.GridNode> Placed,
        bool Horizontal)
    {
        SortedDictionary<int, List<DiagramGridLayout.GridNode>> ByColumn = new SortedDictionary<int, List<DiagramGridLayout.GridNode>>();
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            if (!ByColumn.TryGetValue(Node.Column, out List<DiagramGridLayout.GridNode>? Members))
            {
                Members = [];
                ByColumn[Node.Column] = Members;
            }

            Members.Add(Node);
        }

        return ByColumn;
    }

    private static double CrossOfColumn(List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        double Sum = 0.0;
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Sum += Horizontal ? Node.Y : Node.X;
        }

        return Sum / Nodes.Count;
    }

    private static void ShiftCrossFrom(List<DiagramGridLayout.GridNode> Placed, double FromCross, int Push, bool Horizontal)
    {
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            double Cross = Horizontal ? Node.Y : Node.X;
            if (Cross >= FromCross)
            {
                if (Horizontal)
                {
                    Node.Y += Push;
                }
                else
                {
                    Node.X += Push;
                }
            }
        }
    }
}
